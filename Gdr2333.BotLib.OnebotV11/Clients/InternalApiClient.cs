﻿/*
   Copyright 2025 All contributors of Gdr2333.BotLib

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using Gdr2333.BotLib.OnebotV11.Utils;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Gdr2333.BotLib.OnebotV11.Clients;

internal class InternalApiClient(WebSocket apiWebSocket, CancellationToken cancellationToken)
{
    private readonly CancellationToken _cancellationToken = cancellationToken;

    private readonly ConcurrentDictionary<Guid, Action<OnebotV11ApiResult>> _apiCallResults = new();

    private readonly Channel<OnebotV11ApiRequest> _apiRequests = Channel.CreateUnbounded<OnebotV11ApiRequest>();

    private readonly WebSocket _apiWebSocket = apiWebSocket;

    private readonly JsonSerializerOptions _opt = StaticData.GetOptions();

    /// <summary>
    /// 当事件接收器出现异常时触发的事件
    /// </summary>
    public event EventHandler<Exception>? OnExceptionOccurrence;

    public void Start()
    {
        ApiCallLoop();
        ApiReceiveLoop();
    }

    private async void ApiCallLoop()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                var request = await _apiRequests.Reader.ReadAsync(_cancellationToken);
                var requestBin = JsonSerializer.SerializeToUtf8Bytes(request, request.GetType(), _opt);
                await _apiWebSocket.SendAsync(requestBin, WebSocketMessageType.Text, true, _cancellationToken);
            }
            catch (Exception e)
            {
                OnExceptionOccurrence?.Invoke(null, e);
            }
        }
    }

    private async void ApiReceiveLoop()
    {
        var buffer = new byte[10240];
        Memory<byte> bufferMem = new(buffer);
        do
        {
            try
            {
                var res = await _apiWebSocket.ReceiveAsync(buffer, _cancellationToken);
                var result = JsonSerializer.Deserialize<OnebotV11ApiResult>(buffer.AsSpan(0, res.Count), _opt)
                    ?? throw new InvalidDataException($"无法解析的API调用结果！返回原文：{Convert.ToBase64String(buffer[..res.Count])}");
                if (_apiCallResults.TryRemove(result.Guid, out var action))
                    action(result);
            }
            catch (Exception e)
            {
                OnExceptionOccurrence?.Invoke(null, e);
            }
        } while (!_cancellationToken.IsCancellationRequested);
    }

    public Task CallApiAsync(string apiName, CancellationToken? cancellationToken = null)
    {
        var realCancellationToken = cancellationToken ?? _cancellationToken;
        var taskSource = new TaskCompletionSource();
        var callGuid = Guid.NewGuid();
        bool returned = false;
        realCancellationToken.Register(() =>
        {
            if (!returned)
            {
                taskSource.SetCanceled();
                _apiCallResults.TryRemove(callGuid, out _);
            }
        });
        _apiCallResults.TryAdd(callGuid, (res) =>
        {
            returned = true;
            switch (res.Retcode)
            {
                case 0:
                case 1:
                    taskSource.SetResult();
                    break;
                default:
                    taskSource.SetException(new OnebotV11ClientException($"返回了错误结果！调用ID={res.Guid}，错误码={res.Retcode}，错误={res.ErrorMessage}，错误描述={res.ErrorMessageEx}"));
                    break;
            }
        });
        _apiRequests.Writer.WriteAsync(new() { Action = apiName, Guid = callGuid }, realCancellationToken).AsTask().Wait();
        return taskSource.Task;
    }

    public Task CallApiAsync<TRequest>(string apiName, TRequest requestData, CancellationToken? cancellationToken = null)
    {
        var realCancellationToken = cancellationToken ?? _cancellationToken;
        var taskSource = new TaskCompletionSource();
        var callGuid = Guid.NewGuid();
        bool returned = false;
        realCancellationToken.Register(() =>
        {
            if (!returned)
            {
                taskSource.SetCanceled();
                _apiCallResults.TryRemove(callGuid, out _);
            }
        });
        _apiCallResults.TryAdd(callGuid, (res) =>
        {
            returned = true;
            switch (res.Retcode)
            {
                case 0:
                case 1:
                    taskSource.SetResult();
                    break;
                default:
                    taskSource.SetException(new OnebotV11ClientException($"返回了错误结果！调用ID={res.Guid}，错误码={res.Retcode}，错误={res.ErrorMessage}，错误描述={res.ErrorMessageEx}"));
                    break;
            }
        });
        _apiRequests.Writer.WriteAsync(new OnebotV11ApiRequest<TRequest>() { Action = apiName, Params = requestData, Guid = callGuid }, realCancellationToken).AsTask().Wait();
        return taskSource.Task;
    }

    public Task<TResult> InvokeApiAsync<TResult>(string apiName, CancellationToken? cancellationToken = null)
    {
        var realCancellationToken = cancellationToken ?? _cancellationToken;
        var taskSource = new TaskCompletionSource<TResult>();
        var callGuid = Guid.NewGuid();
        bool returned = false;
        realCancellationToken.Register(() =>
        {
            if (!returned)
            {
                taskSource.SetCanceled();
                _apiCallResults.TryRemove(callGuid, out _);
            }
        });
        _apiCallResults.TryAdd(callGuid, (res) =>
        {
            returned = true;
            switch (res.Retcode)
            {
                case 0:
                    if (res.Data is not null)
                    {
                        var result = res.Data.Value.Deserialize<TResult>();
                        if (result is not null)
                        {
                            taskSource.SetResult(result);
                            break;
                        }
                    }
                    goto case 1;
                case 1:
                    taskSource.SetException(new OnebotV11ClientException($"服务端认为任务完成成功，但没有返回结果。调用ID={res.Guid}，错误码={res.Retcode}，错误={res.ErrorMessage}，错误描述={res.ErrorMessageEx}"));
                    break;
                default:
                    taskSource.SetException(new OnebotV11ClientException($"返回了错误结果！调用ID={res.Guid}，错误码={res.Retcode}，错误={res.ErrorMessage}，错误描述={res.ErrorMessageEx}"));
                    break;
            }
        });
        _apiRequests.Writer.WriteAsync(new() { Action = apiName, Guid = callGuid }, realCancellationToken).AsTask().Wait();
        return taskSource.Task;
    }

    /// <summary>
    /// 调用API
    /// </summary>
    /// <typeparam name="TRequest">请求内容类型</typeparam>
    /// <typeparam name="TResult">响应内容类型</typeparam>
    /// <param name="apiName">API名称</param>
    /// <param name="requestData">请求数据</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>响应数据</returns>
    public Task<TResult> InvokeApiAsync<TRequest, TResult>(string apiName, TRequest requestData, CancellationToken? cancellationToken = null)
    {
        var realCancellationToken = cancellationToken ?? _cancellationToken;
        var taskSource = new TaskCompletionSource<TResult>();
        var callGuid = Guid.NewGuid();
        bool returned = false;
        realCancellationToken.Register(() =>
        {
            if (!returned)
            {
                taskSource.SetCanceled();
                _apiCallResults.TryRemove(callGuid, out _);
            }
        });
        _apiCallResults.TryAdd(callGuid, (res) =>
        {
            returned = true;
            switch (res.Retcode)
            {
                case 0:
                    if (res.Data is not null)
                    {
                        var result = res.Data.Value.Deserialize<TResult>();
                        if (result is not null)
                        {
                            taskSource.SetResult(result);
                            break;
                        }
                    }
                    goto case 1;
                case 1:
                    taskSource.SetException(new OnebotV11ClientException($"服务端认为任务完成成功，但没有返回结果。调用ID={res.Guid}，错误码={res.Retcode}，错误={res.ErrorMessage}，错误描述={res.ErrorMessageEx}"));
                    break;
                default:
                    taskSource.SetException(new OnebotV11ClientException($"返回了错误结果！调用ID={res.Guid}，错误码={res.Retcode}，错误={res.ErrorMessage}，错误描述={res.ErrorMessageEx}"));
                    break;
            }
        });
        _apiRequests.Writer.WriteAsync(new OnebotV11ApiRequest<TRequest>() { Action = apiName, Params = requestData, Guid = callGuid }, realCancellationToken).AsTask().Wait();
        return taskSource.Task;
    }
}

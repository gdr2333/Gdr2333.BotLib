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
using System.Text.Json.Serialization;

namespace Gdr2333.BotLib.OnebotV11.Messages;

/// <summary>
/// 音乐分享消息段基类
/// </summary>
[method: JsonConstructor]
public abstract class MusicSharePartBase() : MessagePartBase("music")
{
    /// <summary>
    /// 音乐分享类型
    /// </summary>
    [JsonIgnore]
    public string MusicShareType { get; internal set; } = string.Empty;
}

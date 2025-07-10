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
using Gdr2333.BotLib.OnebotV11.Messages.Parts.Base;
using Gdr2333.BotLib.OnebotV11.Messages.Parts.Payload;
using System.Text.Json.Serialization;

namespace Gdr2333.BotLib.OnebotV11.Messages.Parts;

public class ForwardNodePart : MessagePartBase
{
    [JsonIgnore]
    public long Id { get; set; }

    [JsonInclude, JsonRequired, JsonPropertyName("data")]
    private Int64IdPayload? _data;

    [JsonConstructor]
    private ForwardNodePart() : base("node")
    {
    }

    public ForwardNodePart(long messageId) : base("node")
    {
        Id = messageId;
    }

    public override void OnDeserialized()
    {
        Id = _data!.Id;
        _data = null;
    }

    public override void OnSerializing()
    {
        _data = new()
        {
            Id = Id,
        };
    }

    public override string ToString() =>
        "转发消息";
}

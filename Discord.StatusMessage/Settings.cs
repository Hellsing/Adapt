using Adapt.Lib;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Discord.StatusMessage
{
    public class GlobalSettings : IGlobalSettings
    {
        [JsonProperty("StatusMessage", Required = Required.Always)]
        public string StatusMessage { get; set; } = string.Empty;

        [JsonProperty("StreamUrl", Required = Required.AllowNull)]
        public string StreamUrl { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("StatusType")]
        public ActivityType StatusType { get; set; } = ActivityType.Playing;
    }

    public class ServerSettings : IServerSettings { }
}
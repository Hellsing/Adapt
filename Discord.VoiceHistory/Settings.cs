using Adapt.Lib;

namespace Discord.VoiceHistory
{
    public class GlobalSettings : IGlobalSettings { }

    public class ServerSettings : IServerSettings
    {
        public ulong TargetChannelId { get; set; } = 860990808782405672;
    }
}
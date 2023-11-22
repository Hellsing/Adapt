using Adapt.Lib;

namespace Adapt.InfiniteVoice
{
    public class GlobalSettings : IGlobalSettings { }

    public class ServerSettings : IServerSettings
    {
        public Dictionary<ulong, List<TemporaryChannel>> TemporaryChannels = new();
    }
}
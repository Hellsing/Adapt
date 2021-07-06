using System.Collections.Generic;
using Adapt.Lib;

namespace Discord.InfinityVoice
{
    public class GlobalSettings : IGlobalSettings
    {
        public string NumberPrefix { get; set; } = "#";
    }

    public class ServerSettings : IServerSettings
    {
        public HashSet<ulong> ManagedChannels { get; set; } = new();
        public HashSet<ulong> ManagedCategories { get; set; } = new();
    }
}
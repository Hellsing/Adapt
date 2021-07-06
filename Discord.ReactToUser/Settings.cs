using System.Collections.Generic;
using Adapt.Lib;

namespace Discord.ReactToUser
{
    public class ServerSettings : IServerSettings
    {
        /// <summary>
        ///     A collection containing all users to react with the corresponding emotes to.
        /// </summary>
        public Dictionary<ulong, List<string>> UserEmotes { get; set; } = new()
        {
            { 250694293533097984, new List<string> { "\U0001F916", "MrDestructoid" } }
        };
    }

    public class GlobalSettings : IGlobalSettings { }
}
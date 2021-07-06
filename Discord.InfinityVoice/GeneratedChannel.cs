using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Adapt.Lib;
using Discord.WebSocket;
using Newtonsoft.Json;
using Serilog;

namespace Discord.InfinityVoice
{
    public class GeneratedChannel
    {
        public const string FileName = nameof(InfinityVoice) + "_" + nameof(GeneratedChannel) + ".json";
        public static readonly string FilePath = Path.Combine(CoreSettings.DataDirectoryPath, FileName);

        public static Dictionary<ulong, List<GeneratedChannel>> Channels { get; } = new();

        static GeneratedChannel()
        {
            // Load previous generated channels
            if (File.Exists(FilePath))
            {
                try
                {
                    Channels = JsonConvert.DeserializeObject<Dictionary<ulong, List<GeneratedChannel>>>(File.ReadAllText(FilePath, Encoding.Unicode));
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, "Failed to read file!");
                }
            }

            // Save file
            SaveToFile();
        }

        public static void SaveToFile()
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Channels, Formatting.Indented), Encoding.Unicode);
            }
            catch (Exception e)
            {
                Log.Logger.Here().Error(e, "Failed to save file!");
            }
        }

        public ulong ServerId { get; set; }
        public string ParentChannelName { get; set; }
        public ulong ParentId { get; set; }
        public ulong Id { get; set; }
        public int Index { get; set; }

        [JsonIgnore]
        public string ChannelName => $"{ParentChannelName} #{Index}";
        [JsonIgnore]
        public SocketGuild Server => InfinityVoice.Instance.Manager.Client.GetGuild(ServerId);
        [JsonIgnore]
        public SocketVoiceChannel VoiceChannel => Server?.GetVoiceChannel(Id);
    }
}
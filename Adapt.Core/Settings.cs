using System;
using System.Collections.Generic;
using System.IO;
using Adapt.Lib;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Adapt.Core
{
    public class Settings : ISettings
    {
        private const string SettingsFileName = nameof(Settings) + ".json";
        private static readonly string SettingsFilePath = Path.Combine(CoreSettings.DataDirectoryPath, SettingsFileName);

        private static readonly string ServerSettingsMainFolder = Path.Combine(CoreSettings.DataDirectoryPath, "Servers");

        public static Settings Instance { get; set; }

        /// <summary>
        ///     Discord related settings
        /// </summary>
        [JsonProperty(Order = 0)]
        public DiscordSettings Discord { get; set; } = new();

        /// <summary>
        ///     Key: Discord component dll file name
        ///     Value: Settings instance for that component
        /// </summary>
        [JsonProperty(Order = 1)]
        public Dictionary<string, object> DiscordComponents = new();

        [JsonIgnore]
        public static Dictionary<IDiscordComponent, Tuple<object, Type, string>> ServerSettings = new();

        static Settings()
        {
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    // Load existing settings
                    Instance = FromJson(File.ReadAllText(SettingsFilePath));
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, $"Failed to load {SettingsFileName}!");

                    try
                    {
                        File.Copy(SettingsFilePath, SettingsFilePath + "_corrupted");
                    }
                    catch (Exception e1)
                    {
                        Log.Logger.Here().Error(e1, $"Failed to make backup of the old {SettingsFileName} file!");
                    }
                }
            }

            if (!Directory.Exists(ServerSettingsMainFolder))
            {
                try
                {
                    Directory.CreateDirectory(ServerSettingsMainFolder);
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, "Failed to create folder: " + ServerSettingsMainFolder);
                }
            }

            // Create new instance if it's null
            Instance ??= new Settings();
        }

        private Settings() { }

        public T RegisterSettings<T>(string componentFileName, T settings) where T : IGlobalSettings
        {
            if (DiscordComponents.ContainsKey(componentFileName))
            {
                try
                {
                    // Get the base object
                    var obj = (JObject) DiscordComponents[componentFileName];

                    // Apply the converted object reference
                    DiscordComponents[componentFileName] = obj.ToObject(typeof(T));

                    Log.Logger.Here().Information($"Loaded settings entry for '{componentFileName}'!");

                    // Return the object reference
                    return (T) DiscordComponents[componentFileName];
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, $"Failed to load settings entry for '{componentFileName}'!");
                }
            }

            Log.Logger.Here().Information($"Creating new settings entry for '{componentFileName}'!");

            // Apply settings
            DiscordComponents[componentFileName] = settings;

            if (settings == null)
            {
                Log.Logger.Here().Warning($"Settings entry is null for \"{componentFileName}\"!");
            }

            // Save settings
            Save();

            return settings;
        }

        public void RegisterServerSettings<T>(string componentFileName, IDictionary<ulong, T> settings, IDiscordComponent component) where T : IServerSettings
        {
            if (ServerSettings.ContainsKey(component))
            {
                return;
            }

            // Register component settings file internally
            ServerSettings[component] = new Tuple<object, Type, string>(settings, typeof(T), componentFileName);
        }

        public void RegisterServerForSettings(IGuild guild)
        {
            var serverSettingsPath = Path.Combine(ServerSettingsMainFolder, guild.Id.ToString());

            if (!Directory.Exists(serverSettingsPath))
            {
                try
                {
                    Directory.CreateDirectory(serverSettingsPath);
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, $"Failed to create settings folder for server {guild.Name} \"{guild.Id}\"!");
                    return;
                }
            }

            foreach (var serverSetting in ServerSettings.Values)
            {
                dynamic settings = serverSetting.Item1;

                var filePath = Path.Combine(serverSettingsPath, serverSetting.Item3[..^4] + ".json");
                if (File.Exists(filePath))
                {
                    try
                    {
                        dynamic deserialized = JsonConvert.DeserializeObject(File.ReadAllText(filePath), serverSetting.Item2);
                        settings[guild.Id] = deserialized;

                        Log.Logger.Here().Debug($"Loaded server settings for component \"{serverSetting.Item3}\" for server \"{guild.Id}\"!");
                    }
                    catch (Exception e)
                    {
                        Log.Logger.Here().Error(e, $"Failed to load server settings for module {serverSetting.Item3}, server {guild.Name} \"{guild.Id}\"!");
                    }
                }

                if (settings.ContainsKey(guild.Id) && settings[guild.Id] != null)
                {
                    continue;
                }

                try
                {
                    dynamic instance = Activator.CreateInstance(serverSetting.Item2);
                    settings[guild.Id] = instance;

                    Log.Logger.Here().Debug($"Created blank settings file for component \"{serverSetting.Item3}\" for server \"{guild.Id}\"!");
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, $"Failed to create an instance of {serverSetting.Item2.FullName}!");
                    settings[guild.Id] = null;
                }
            }

            Save();
        }

        public void Save()
        {
            try
            {
                // Save global settings to file
                File.WriteAllText(SettingsFilePath, ToJson());

                // Save server settings files
                foreach (var serverSetting in ServerSettings.Values)
                {
                    dynamic settings = serverSetting.Item1;
                    foreach (var entry in settings)
                    {
                        string serverSettingsPath = Path.Combine(ServerSettingsMainFolder, entry.Key.ToString());

                        var filePath = Path.Combine(serverSettingsPath, serverSetting.Item3[..^4] + ".json");

                        File.WriteAllText(filePath, JsonConvert.SerializeObject(entry.Value, Formatting.Indented));
                    }
                }
            }
            catch (Exception e)
            {
                Log.Logger.Here().Error(e, $"Failed to save {SettingsFileName}!");
            }
        }

        public string ToJson(Formatting formatting = Formatting.Indented)
        {
            return JsonConvert.SerializeObject(this, formatting);
        }

        public static Settings FromJson(string jsonString)
        {
            return JsonConvert.DeserializeObject<Settings>(jsonString);
        }

        public class DiscordSettings
        {
            public string BotToken { get; set; } = string.Empty;
        }
    }
}
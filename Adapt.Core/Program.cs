using System;
using System.IO;
using System.Threading.Tasks;
using Adapt.Lib;
using Serilog;

namespace Adapt.Core
{
    public static class Program
    {
        public static DiscordManager DiscordManager { get; private set; }

        public static void Main(string[] args)
        {
            // Initialize logger
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                        .MinimumLevel.Debug()
#else
                        .MinimumLevel.Warning()
#endif
                        .Enrich.FromLogContext()
                        .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] [{FilePath}.{MemberName}] {Message}{NewLine}{Exception}")
                        .CreateLogger();

            Log.Logger.Here().Information("Loading program.");

            // Create data directory
            Directory.CreateDirectory(CoreSettings.DataDirectoryPath);

            // Initialize remaining components asynchronously
            InitializeAsync().GetAwaiter().GetResult();
        }

        private static async Task InitializeAsync()
        {
            // Save settings
            Settings.Instance.Save();

            Log.Logger.Here().Information("Creating Discord connection...");

            try
            {
                // Ready up Discord Connection
                DiscordManager = new DiscordManager();
                await DiscordManager.InitializeDiscordConnection();
            }
            catch (Exception e)
            {
                Log.Logger.Here().Error("Failed to initialize Discord connection!", e);
            }

            // Block the program until it is closed
            await Task.Delay(-1);
        }
    }
}
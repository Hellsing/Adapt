using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Adapt.Lib;
using Discord;
using Discord.WebSocket;
using Serilog;

namespace Adapt.Core
{
    public class DiscordManager : IDiscordManager
    {
        public Dictionary<string, IDiscordComponent> Components { get; } = new();

        public DiscordSocketClient Client { get; private set; }
        public IReadOnlyCollection<SocketGuild> Guilds => Client?.Guilds;

        private bool ServersInitialized { get; set; }
        private bool CommandsCreated { get; set; }

        public async Task InitializeDiscordConnection()
        {
            // Validate bot token
            if (string.IsNullOrWhiteSpace(Settings.Instance.Discord.BotToken))
            {
                Log.Logger.Here().Warning("Please set a valid Discord bot token!");
                Log.Logger.Here().Warning("Discord connection aborted!");
                return;
            }

            // Create Discord client instance
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
                GatewayIntents = GatewayIntents.All
            });

            #region Event Handler Registration

            // Initialize the slash command listener class
            SlashCommandListener.Initialize(this);

            // Listen to required events
            Client.Log += ClientOnLog;
            Client.Ready += ClientOnReady;

            Client.MessageReceived += ClientOnMessageReceived;
            Client.MessageDeleted += ClientOnMessageDeleted;

            Client.UserJoined += ClientOnUserJoined;
            Client.UserLeft += ClientOnUserLeft;

            Client.ChannelCreated += ClientOnChannelCreated;
            Client.ChannelDestroyed += ClientOnChannelDestroyed;
            Client.ChannelUpdated += ClientOnChannelUpdated;

            Client.UserVoiceStateUpdated += ClientOnUserVoiceStateUpdated;

            Client.ReactionAdded += ClientOnReactionAdded;
            Client.ReactionRemoved += ClientOnReactionRemoved;
            Client.ReactionsCleared += ClientOnReactionsCleared;

            Client.UserUpdated += ClientOnUserUpdated;
            Client.GuildMemberUpdated += ClientOnGuildMemberUpdated;

            Client.JoinedGuild += ClientOnJoinedGuild;
            Client.LeftGuild += ClientOnLeftGuild;

            Client.InteractionCreated += ClientOnInteractionCreated;

            #endregion

            // Initialize components
            Log.Logger.Here().Information("Loading Discord components...");

            #region Assembly Loading

            // Add an assembly resolver
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args)
            {
                var assemblyPath = Path.Combine(CoreSettings.ComponentAssemblyFolder, "lib", new AssemblyName(args.Name).Name + ".dll");
                return !File.Exists(assemblyPath) ? null : Assembly.LoadFrom(assemblyPath);
            };

            try
            {
                // Create directories
                Directory.CreateDirectory(CoreSettings.ComponentAssemblyFolder);
                Directory.CreateDirectory(Path.Combine(CoreSettings.ComponentAssemblyFolder, "lib"));

                // Get a list of assemblies inside the folder
                var fileList = Directory.GetFiles(CoreSettings.ComponentAssemblyFolder, "*.dll");
                Log.Logger.Here().Debug($"Found a total of {fileList.Length} files!");

                var type = typeof(IDiscordComponent);

                foreach (var file in fileList)
                {
                    Log.Logger.Here().Debug(file);

                    var fileName = Path.GetFileName(file);

                    try
                    {
                        // Load assembly
                        var assembly = Assembly.LoadFrom(Path.GetFullPath(file));

                        if (assembly != null)
                        {
                            var rawTypes = assembly.GetTypes();
                            if (rawTypes != null)
                            {
                                var types = rawTypes.Where(o => type.IsAssignableFrom(o)).ToList();
                                if (types.Count > 0)
                                {
                                    Log.Logger.Here().Debug($"Found a total of {types.Count} valid types!");

                                    foreach (var component in types)
                                    {
                                        try
                                        {
                                            var instance = (IDiscordComponent)Activator.CreateInstance(component);
                                            Components[fileName] = instance;

                                            var methodInfo = component.GetMethod(nameof(IDiscordComponent.Initialize));
                                            if (methodInfo != null)
                                            {
                                                methodInfo.Invoke(instance, new object[] { this });
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            Log.Logger.Here().Error(e, $"Failed to apply reflection on '{component.FullName}' in '{file}'!");

                                            Components[fileName] = null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Logger.Here().Error(e, $"Failed to load assembly '{file}'!");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Logger.Here().Error(e, "Failed to load Discord component!");
            }

            #endregion

            // Inform about the loaded components
            Log.Logger.Here().Information("Loaded the following Discord components:");
            foreach (var component in Components.Values)
            {
                Log.Logger.Here().Information(" - " + component.ComponentName);
                Log.Logger.Here().Information("       " + component.ComponentDescription);
            }

            try
            {
                // Login and start the Discord client
                await Client.LoginAsync(TokenType.Bot, Settings.Instance.Discord.BotToken);
                await Client.StartAsync();
            }
            catch (Exception e)
            {
                Log.Logger.Here().Error(e, "Failed to start Discord client!");
                throw;
            }
        }

        public T RegisterSettings<T>(IDiscordComponent component, T settings) where T : IGlobalSettings
        {
            foreach (var (key, value) in Components)
            {
                if (component == value)
                {
                    // Register the settings and return
                    return Settings.Instance.RegisterSettings(key, settings);
                }
            }

            return settings;
        }

        public void RegisterSettings<T>(IDiscordComponent component, IDictionary<ulong, T> settings) where T : IServerSettings
        {
            foreach (var (key, value) in Components)
            {
                if (component == value)
                {
                    // Apply the settings
                    Settings.Instance.RegisterServerSettings(key, settings, component);
                    return;
                }
            }
        }

        public void SaveSettings()
        {
            // Save the settings
            Settings.Instance.Save();
        }

        private Task InvokeComponentMethod(Func<IDiscordComponent, Task> invokeFunc)
        {
            Task.Run(async () =>
            {
                foreach (var component in Components.Values)
                {
                    try
                    {
                        await invokeFunc.Invoke(component);
                    }
                    catch (Exception e)
                    {
                        Log.Logger.Here().Error(e, $"Failed to invoke component method for `{component.ComponentName}`!");
                    }
                }
            });

            return Task.CompletedTask;
        }

        #region Event Handlers

        private async Task ClientOnReady()
        {
            if (!ServersInitialized)
            {
                ServersInitialized = true;

                foreach (var guild in Guilds)
                {
                    Settings.Instance.RegisterServerForSettings(guild);
                }
            }

            if (!CommandsCreated)
            {
                CommandsCreated = true;

                // Let the components create their commands
                await InvokeComponentMethod(component => component.CreateCommands(Client.Rest));
            }

            await InvokeComponentMethod(component => component.OnReady());
        }

        private async Task ClientOnLog(LogMessage logMessage)
        {
            var message = $"[{logMessage.Source}] {logMessage.Message}";

            if (logMessage.Exception != null)
            {
                Log.Logger.Here().Error(logMessage.Exception, message);
            }

            switch (logMessage.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Log.Logger.Here().Error(message);
                    break;
                case LogSeverity.Warning:
                    Log.Logger.Here().Warning(message);
                    break;
                case LogSeverity.Info:
                    Log.Logger.Here().Information(message);
                    break;
                case LogSeverity.Verbose:
                    Log.Logger.Here().Verbose(message);
                    break;
                case LogSeverity.Debug:
                    Log.Logger.Here().Debug(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(logMessage.Severity.ToString());
            }

            await InvokeComponentMethod(component => component.OnLog(logMessage));
        }

        private async Task ClientOnMessageReceived(SocketMessage message)
        {
            await InvokeComponentMethod(component => component.OnMessageReceived(message));
        }

        private async Task ClientOnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            await InvokeComponentMethod(component => component.OnMessageDeleted(message, channel));
        }

        private async Task ClientOnUserJoined(SocketGuildUser user)
        {
            await InvokeComponentMethod(component => component.OnUserJoined(user));
        }

        private async Task ClientOnUserLeft(SocketGuild guild, SocketUser user)
        {
            await InvokeComponentMethod(component => component.OnUserLeft(guild, user));
        }

        private async Task ClientOnChannelCreated(SocketChannel channel)
        {
            await InvokeComponentMethod(component => component.OnChannelCreated(channel));
        }

        private async Task ClientOnChannelDestroyed(SocketChannel channel)
        {
            await InvokeComponentMethod(component => component.OnChannelDestroyed(channel));
        }

        private async Task ClientOnChannelUpdated(SocketChannel oldChannel, SocketChannel newChannel)
        {
            await InvokeComponentMethod(component => component.OnChannelUpdated(oldChannel, newChannel));
        }

        private async Task ClientOnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState newState)
        {
            await InvokeComponentMethod(component => component.OnUserVoiceStateUpdated(user, previousState, newState));
        }

        private async Task ClientOnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            await InvokeComponentMethod(component => component.OnReactionAdded(message, channel, reaction));
        }

        private async Task ClientOnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            await InvokeComponentMethod(component => component.OnReactionRemoved(message, channel, reaction));
        }

        private async Task ClientOnReactionsCleared(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            await InvokeComponentMethod(component => component.OnReactionsCleared(message, channel));
        }

        private async Task ClientOnUserUpdated(SocketUser oldState, SocketUser newState)
        {
            await InvokeComponentMethod(component => component.OnUserUpdated(oldState, newState));
        }

        private async Task ClientOnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> oldState, SocketGuildUser newState)
        {
            await InvokeComponentMethod(component => component.OnGuildMemberUpdated(oldState, newState));
        }

        private async Task ClientOnJoinedGuild(SocketGuild guild)
        {
            Settings.Instance.RegisterServerForSettings(guild);

            await InvokeComponentMethod(component => component.OnJoinedGuild(guild));
        }

        private async Task ClientOnLeftGuild(SocketGuild guild)
        {
            await InvokeComponentMethod(component => component.OnLeftGuild(guild));
        }

        private async Task ClientOnInteractionCreated(SocketInteraction interaction)
        {
            await InvokeComponentMethod(component => component.OnInteractionCreated(interaction));

            if (interaction is SocketSlashCommand slashCommand)
            {
                await InvokeComponentMethod(component => component.OnSlashCommandReceived(slashCommand));
            }
        }

        #endregion
    }
}
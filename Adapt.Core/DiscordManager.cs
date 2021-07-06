﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Adapt.Lib;
using Discord;
using Discord.Rest;
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
        private RestGlobalCommand RefreshCommand { get; set; }

        public async Task InitializeDiscordConnection()
        {
            // Validate bot token
            if (string.IsNullOrWhiteSpace(Settings.Instance.Discord.BotToken))
            {
                Log.Warning($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Please set a valid Discord bot token!");
                Log.Warning($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Discord connection aborted!");
                return;
            }

            // Create Discord client instance
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                DefaultRetryMode = RetryMode.AlwaysRetry,
                AlwaysAcknowledgeInteractions = true
            });

            #region Event Handler Registration

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
            Log.Information($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Loading Discord components...");

            #region Assembly Loading

            try
            {
                // Create directory
                Directory.CreateDirectory(CoreSettings.ComponentAssemblyFolder);

                // Get a list of assemblies inside the folder
                var fileList = Directory.GetFiles(CoreSettings.ComponentAssemblyFolder, "*.dll");
                Log.Debug($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Found a total of {fileList.Length} files!");

                var type = typeof(IDiscordComponent);

                foreach (var file in fileList)
                {
                    Log.Debug($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: {file}");

                    var fileName = Path.GetFileName(file);

                    try
                    {
                        // Load assembly
                        var assembly = Assembly.LoadFile(Path.GetFullPath(file));
                        var types = assembly.GetTypes().Where(o => type.IsAssignableFrom(o)).ToList();

                        Log.Debug($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Found a total of {types.Count} valid types!");

                        foreach (var component in types)
                        {
                            try
                            {
                                var instance = (IDiscordComponent) Activator.CreateInstance(component);
                                Components[fileName] = instance;

                                var methodInfo = component.GetMethod(nameof(IDiscordComponent.Initialize));
                                if (methodInfo != null)
                                {
                                    methodInfo.Invoke(instance, new object[] { this });
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, $"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Failed to apply reflection on '{component.FullName}' in '{file}'!");

                                Components[fileName] = null;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, $"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Failed to load assembly '{file}'!");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Failed to load Discord component!");
            }

            #endregion

            // Inform about the loaded components
            Log.Information($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Loaded the following Discord components:");
            foreach (var component in Components.Values)
            {
                Log.Information($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}:  - " + component.ComponentName);
                Log.Information($"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}:        " + component.ComponentDescription);
            }

            try
            {
                // Login and start the Discord client
                await Client.LoginAsync(TokenType.Bot, Settings.Instance.Discord.BotToken);
                await Client.StartAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, $"{nameof(DiscordManager)}.{nameof(InitializeDiscordConnection)}: Failed to start Discord client!");
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

        private async Task InvokeComponentMethod(Func<IDiscordComponent, Task> invokeFunc)
        {
            foreach (var component in Components.Values)
            {
                try
                {
                    await invokeFunc.Invoke(component);
                }
                catch (Exception e)
                {
                    Log.Error(e, $"{nameof(DiscordManager)}.{nameof(InvokeComponentMethod)}: Failed to invoke component method for `{component.ComponentName}`!");
                }
            }
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

            if (RefreshCommand == null)
            {
                // Create the refresh command
                RefreshCommand = await Client.Rest.CreateGlobalCommand(new SlashCommandCreationProperties
                {
                    Name = "refresh-commands",
                    Description = "Deletes all commands and forces the components to re-register their commands."
                });

                // Let the components create their commands
                await InvokeComponentMethod(component => component.CreateCommands(Client.Rest));
            }
            
            await InvokeComponentMethod(component => component.OnReady());
        }

        private async Task ClientOnLog(LogMessage logMessage)
        {
            var message = $"{nameof(DiscordManager)}.{logMessage.Source}: {logMessage.Message}";

            if (logMessage.Exception != null)
            {
                Log.Error(logMessage.Exception, message);
            }

            switch (logMessage.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    Log.Error(message);
                    break;
                case LogSeverity.Warning:
                    Log.Warning(message);
                    break;
                case LogSeverity.Info:
                    Log.Information(message);
                    break;
                case LogSeverity.Verbose:
                    Log.Verbose(message);
                    break;
                case LogSeverity.Debug:
                    Log.Debug(message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
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

        private async Task ClientOnUserLeft(SocketGuildUser user)
        {
            await InvokeComponentMethod(component => component.OnUserLeft(user));
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
                // Internal Refresh Command handling
                if (slashCommand.Data.Id == RefreshCommand.Id)
                {
                    Log.Information("A client requested the refreshing of all commands.");

                    try
                    {
                        // Get all global commands except this one
                        var globalCommands = (await Client.Rest.GetGlobalApplicationCommands()).Where(o => o.Id != RefreshCommand.Id).ToList();
                        
                        Log.Information($"Deleting {globalCommands.Count} global commands.");
                        foreach (var command in globalCommands)
                        {
                            await command.DeleteAsync();
                        }

                        foreach (var guild in Client.Guilds)
                        {
                            var guildCommands = await Client.Rest.GetGuildApplicationCommands(guild.Id);

                            Log.Information($"Deleting {guildCommands.Count} guild commands for the server {guild.Name} ({guild.Id}).");
                        }

                        Log.Information("Successfully cleared all commands.");
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error during command clearing!");
                    }

                    Log.Information($"Invoking {nameof(IDiscordComponent.CreateCommands)}() for all components loaded.");
                    await InvokeComponentMethod(component => component.CreateCommands(Client.Rest));

                    await slashCommand.FollowupAsync("All commands refreshed!");
                    return;
                }

                // All other commands
                await InvokeComponentMethod(component => component.OnSlashCommandReceived(slashCommand));
            }
        }

        #endregion
    }
}
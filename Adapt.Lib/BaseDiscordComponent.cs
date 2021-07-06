using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Adapt.Lib
{
    public abstract class BaseDiscordComponent<TGlobal, TServer> : BaseDiscordComponent where TGlobal : IGlobalSettings where TServer : IServerSettings
    {
        /// <summary>
        ///     The <see cref="IGlobalSettings" /> instance.
        /// </summary>
        public TGlobal GlobalSettings { get; protected set; }

        /// <summary>
        ///     The Dictionary containing all <see cref="IServerSettings" /> for each server.
        /// </summary>
        public Dictionary<ulong, TServer> ServerSettings { get; protected set; } = new();

        public override bool Initialize(IDiscordManager manager)
        {
            // Initialize base
            base.Initialize(manager);

            // Reflection initialize Settings instance
            GlobalSettings = Activator.CreateInstance<TGlobal>();

            // For Server Settings
            ServerSettings = new Dictionary<ulong, TServer>();

            // Register settings
            GlobalSettings = manager.RegisterSettings(this, GlobalSettings);
            manager.RegisterSettings(this, ServerSettings);

            // Indicate component initialized successfully
            return true;
        }

        public TServer GetServerSettings(ulong serverId)
        {
            return ServerSettings.ContainsKey(serverId) ? ServerSettings[serverId] : default;
        }
    }

    public abstract class BaseDiscordComponent : IDiscordComponent
    {
        public IDiscordManager Manager { get; set; }

        public abstract string ComponentName { get; protected set; }
        public abstract string ComponentDescription { get; protected set; }

        public virtual bool Initialize(IDiscordManager manager)
        {
            // Apply manager instance
            Manager = manager;

            return true;
        }

        public virtual Task CreateCommands(DiscordSocketRestClient client)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnReady()
        {
            return Task.CompletedTask;
        }

        public virtual Task OnLog(LogMessage logMessage)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnMessageReceived(SocketMessage message)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnUserJoined(SocketGuildUser user)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnUserLeft(SocketGuildUser user)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnChannelCreated(SocketChannel channel)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnChannelDestroyed(SocketChannel channel)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnChannelUpdated(SocketChannel previousChannel, SocketChannel newChannel)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState newState)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnReactionsCleared(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnUserUpdated(SocketUser oldState, SocketUser newState)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> oldState, SocketGuildUser newState)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnJoinedGuild(SocketGuild guild)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnLeftGuild(SocketGuild guild)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnInteractionCreated(SocketInteraction interaction)
        {
            return Task.CompletedTask;
        }

        public virtual Task OnSlashCommandReceived(SocketSlashCommand slashCommand)
        {
            return Task.CompletedTask;
        }

        public virtual void Dispose() { }
    }
}
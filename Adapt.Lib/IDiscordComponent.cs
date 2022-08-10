using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Adapt.Lib
{
    public interface IDiscordComponent : INamedComponent, IDisposable
    {
        public IDiscordManager Manager { get; set; }

        /// <summary>
        ///     Used to initialize the component.
        /// </summary>
        /// <param name="manager">The <see cref="IDiscordManager" /> instance.</param>
        /// <returns>Whether the method succeeded.</returns>
        bool Initialize(IDiscordManager manager);

        Task CreateCommands(DiscordSocketRestClient client);

        Task OnLog(LogMessage logMessage);
        Task OnReady();

        Task OnMessageReceived(SocketMessage message);
        Task OnMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel);

        Task OnUserJoined(SocketGuildUser user);
        Task OnUserLeft(SocketGuild guild, SocketUser user);

        Task OnChannelCreated(SocketChannel channel);
        Task OnChannelDestroyed(SocketChannel channel);
        Task OnChannelUpdated(SocketChannel previousChannel, SocketChannel newChannel);

        Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState newState);

        Task OnReactionsCleared(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel);

        Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction);

        Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction);

        Task OnUserUpdated(SocketUser oldState, SocketUser newState);
        Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> oldState, SocketGuildUser newState);

        Task OnJoinedGuild(SocketGuild guild);
        Task OnLeftGuild(SocketGuild guild);

        Task OnInteractionCreated(SocketInteraction interaction);
        Task OnSlashCommandReceived(SocketSlashCommand slashCommand);
    }
}
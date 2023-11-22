using Discord.WebSocket;

namespace Adapt.InfiniteVoice
{
    public class TemporaryChannel(ulong parentChannelId, ulong channelId)
    {
        public ulong ParentChannelId { get; set; } = parentChannelId;

        public ulong ChannelId { get; set; } = channelId;

        public SocketVoiceChannel GetParent(SocketVoiceChannel temporaryChannel)
        {
            return temporaryChannel.Guild.GetVoiceChannel(ParentChannelId);
        }

        public SocketVoiceChannel GetHandle(SocketVoiceChannel parentChannel)
        {
            return parentChannel.Guild.GetVoiceChannel(ChannelId);
        }
    }
}
using Adapt.Lib;
using Discord.WebSocket;

namespace Adapt.InfiniteVoice
{
    public class InfiniteVoice : BaseDiscordComponent<GlobalSettings, ServerSettings>
    {
        public override string ComponentName { get; protected set; } = nameof(InfiniteVoice);
        public override string ComponentDescription { get; protected set; } = "Creates an infinite amount of voice channels once a channel gets occupied.";

        public override Task OnReady()
        {
            foreach (var server in Manager.Guilds)
            {
                // Temporary channel checks
                var channels = GeTemporaryChannels(server.Id);
                foreach (var tempChannel in channels.ToArray())
                {
                    // Get the channel
                    var channel = server.GetChannel(tempChannel.ChannelId);
                    if (channel == null)
                    {
                        // Delete the non-existing temporary channel from the list
                        channels.Remove(tempChannel);
                    }
                }
            }

            // Save the settings
            SaveSettings();

            return Task.CompletedTask;
        }

        public override async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState newState)
        {
            // Check for impossible scenario
            if (previousState.VoiceChannel == null && newState.VoiceChannel == null)
            {
                return;
            }

            // Check for disconnect
            if (newState.VoiceChannel == null && previousState.VoiceChannel != null)
            {
                await ChannelLeft(previousState.VoiceChannel);
                return;
            }

            // Check for connect
            if (previousState.VoiceChannel == null && newState.VoiceChannel != null)
            {
                await ChannelJoined(newState.VoiceChannel);
                return;
            }

            // Check for move
            if (previousState.VoiceChannel != newState.VoiceChannel)
            {
                await ChannelMoved(previousState.VoiceChannel!, newState.VoiceChannel!);
            }
        }

        private async Task ChannelLeft(SocketVoiceChannel channel)
        {
            // Check if the channel is now empty
            if (channel.ConnectedUsers.Count == 0)
            {
                // Check if it's a temprorary channel
                if (IsTemporaryChannel(channel))
                {
                    // We can safely delete the temporary channel here
                    await DeleteTemporaryChannel(channel);
                }
                else
                {
                    // TODO: Check for all temp channels by this parent
                }
            }
        }

        private async Task ChannelJoined(SocketVoiceChannel channel) { }

        private async Task ChannelMoved(SocketVoiceChannel previousChannel, SocketVoiceChannel newChannel) { }

        private List<TemporaryChannel> GeTemporaryChannels(ulong serverId)
        {
            // Get all temporary channels from this server
            var allTempChannels = GetServerSettings(serverId).TemporaryChannels;

            // Check if there is already an instance for this server
            if (!allTempChannels.ContainsKey(serverId))
            {
                // Create new instance
                allTempChannels.Add(serverId, new List<TemporaryChannel>());

                // Save settings
                SaveSettings();
            }

            return allTempChannels[serverId];
        }
        
        private bool IsTemporaryChannel(SocketVoiceChannel channel)
        {
            return GeTemporaryChannels(channel.Guild.Id).Any(o => o.ChannelId == channel.Id);
        }

        private async Task DeleteTemporaryChannel(SocketVoiceChannel channel)
        {
            // Get the temp channels collection
            var channels = GeTemporaryChannels(channel.Guild.Id);

            // Get the temp channel instance
            var tempChannel = channels.Find(o => o.ChannelId == channel.Id);
            if (tempChannel != null)
            {
                // Remove from collection
                channels.Remove(tempChannel);

                // Save the config
                SaveSettings();
            }

            // Delete the channel from the server
            await channel.DeleteAsync();
        }
    }
}
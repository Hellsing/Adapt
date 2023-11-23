using Adapt.Lib;
using Discord.WebSocket;

namespace Adapt.InfiniteVoice;

public class InfiniteVoice : BaseDiscordComponent<GlobalSettings, ServerSettings>
{
    public override string ComponentName { get; protected set; } = nameof(InfiniteVoice);

    public override string ComponentDescription { get; protected set; } = "Creates an infinite amount of voice channels once a channel gets occupied.";

    public override Task OnReady()
    {
        // Temporary helper variable
        var changed = false;

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
                    changed = true;
                }
            }
        }

        if (changed)
        {
            // Save the settings
            SaveSettings();
        }

        return Task.CompletedTask;
    }

    public override async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState newState)
    {
        // TODO: Check for black-/whitelist
        // TODO: Check for permissions on channel

        // Grab properties
        var previousCh = previousState.VoiceChannel;
        var newCh = newState.VoiceChannel;

        // Check for impossible scenario
        if (previousCh == null && newCh == null)
        {
            return;
        }

        // Check for disconnect
        if (newCh == null && previousCh != null)
        {
            await ChannelLeft(previousCh);
            return;
        }

        // Check for connect
        if (previousCh == null && newCh != null)
        {
            await ChannelJoined(newCh);
            return;
        }

        // Check for move
        if (previousCh != newCh)
        {
            await ChannelMoved(previousCh!, newCh!);
        }
    }

    private async Task ChannelLeft(SocketVoiceChannel channel)
    {
        // Check if the channel is now empty
        if (channel.ConnectedUsers.Count == 0)
        {
            // Check if it's a temporary channel
            if (IsTemporaryChannel(channel))
            {
                // We can safely delete the temporary channel here
                await DeleteTemporaryChannel(channel);
            }
        }

        // TODO: Check for all temp channels by this parent
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
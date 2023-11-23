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
        // TODO: Include channel moved check new/old for black-/whitelist and permissions

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
            else
            {
                // Get all child channels by this parent
                var childChannels = GetChildChannels(channel);

                // Check if any of them is empty
                foreach (var temporaryChannel in childChannels.ToArray())
                {
                    var childHandle = temporaryChannel.GetHandle(channel);
                    if (childHandle.ConnectedUsers.Count == 0)
                    {
                        // Delete the empty child channel
                        await DeleteTemporaryChannel(childHandle);
                    }
                }
            }
        }
    }

    private async Task ChannelJoined(SocketVoiceChannel channel)
    {
        // Check if user count is now exactly 1, meaning it's a fresh join
        if (channel.ConnectedUsers.Count != 1)
        {
            return;
        }

        // Get the parent channel
        var parent = GetParentChannel(channel);

        // Create a new temporary channel, as the channel is now occupied
        await CreateTemporaryChannel(parent);
    }

    private async Task ChannelMoved(SocketVoiceChannel previousChannel, SocketVoiceChannel newChannel)
    {
        // Get the parent channels for both channels
        var parent1 = GetParentChannel(previousChannel);
        var parent2 = GetParentChannel(newChannel);

        // Check if they share the parent
        var sharedParent = parent1.Id == parent2.Id;
        if (!sharedParent)
        {
            // Call left/joined methods for those channels as they don't share the parent
            await ChannelJoined(newChannel);
            await ChannelLeft(previousChannel);

            Log.Here().Debug("User moved to different parent channel cluster");
            return;
        }

        // Get the user counts
        var previousCount = previousChannel.ConnectedUsers.Count;
        var newCount = newChannel.ConnectedUsers.Count;

        // Check if move doesn't affect anything
        if ((previousCount > 0 && newCount > 1) ||
            (previousCount == 0 && newCount == 1))
        {
            Log.Here().Debug("User channel move doesn't affect temporary channels");
            return;
        }

        // TODO: Include white and blacklist checks and permissions for the next part

        // Check the previous channel needs to be deleted
        if (previousCount == 0 && newCount > 1)
        {
            // TODO
        }
    }

    private List<TemporaryChannel> GeTemporaryChannels(ulong serverId)
    {
        // Get all temporary channels from this server
        var allTempChannels = GetServerSettings(serverId).TemporaryChannels;

        // Check if there is already an instance for this server
        if (!allTempChannels.TryGetValue(serverId, out var value))
        {
            // Create a new collection
            value = new();

            // Create new instance
            allTempChannels.Add(serverId, value);

            // Save settings
            SaveSettings();
        }

        return value;
    }

    private List<TemporaryChannel> GetChildChannels(ulong serverId, ulong channelId)
    {
        return GetServerSettings(serverId).TemporaryChannels.GetValueOrDefault(channelId) ?? new();
    }

    private List<TemporaryChannel> GetChildChannels(SocketVoiceChannel channel)
    {
        return GetChildChannels(channel.Guild.Id, channel.Id);
    }

    private SocketVoiceChannel GetParentChannel(SocketVoiceChannel channel)
    {
        foreach (var entry in GetServerSettings(channel.Guild.Id).TemporaryChannels)
        {
            // Check if the key is matching the requested channel, indicating it's a parent channel
            if (entry.Key == channel.Id)
            {
                return channel;
            }

            // Try to get the channel as temporary channel
            var tempChannel = entry.Value.Find(o => o.ChannelId == channel.Id);
            if (tempChannel != null)
            {
                // Return the parent channel
                return tempChannel.GetParent(channel);
            }
        }

        // Channel not recognized, meaning it's a parent channel
        return channel;
    }

    private bool IsTemporaryChannel(SocketVoiceChannel channel)
    {
        return GeTemporaryChannels(channel.Guild.Id).Any(o => o.ChannelId == channel.Id);
    }

    private async Task CreateTemporaryChannel(SocketVoiceChannel parentChannel) { }

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adapt.Lib;
using Discord.WebSocket;
using Serilog;

namespace Discord.InfinityVoice
{
    public class InfinityVoice : BaseDiscordComponent<GlobalSettings, ServerSettings>
    {
        public override string ComponentName { get; protected set; } = nameof(InfinityVoice);
        public override string ComponentDescription { get; protected set; } = "Creates infinite voice channels per channel or category.";

        public static InfinityVoice Instance { get; private set; }

        public InfinityVoice()
        {
            // Apply instance singleton
            Instance = this;
        }

        public override async Task OnReady()
        {
            // Validate parent channels are existing
            foreach (var channelId in GeneratedChannel.Channels.Keys.ToArray())
            {
                if (Discord.GetVoiceChannel(channelId) == null)
                {
                    // Remove all remaining generated channels
                    foreach (var generated in GeneratedChannel.Channels[channelId])
                    {
                        var voiceChannel = generated.VoiceChannel;
                        if (voiceChannel != null)
                        {
                            // Delete the generated channel
                            await voiceChannel.DeleteAsync();
                        }
                    }

                    // Remove the entry from the collection
                    GeneratedChannel.Channels.Remove(channelId);

                    // Save file
                    GeneratedChannel.SaveToFile();
                }
            }

            // Validate all generated channels
            foreach (var entry in GeneratedChannel.Channels.ToArray())
            {
                foreach (var generated in entry.Value.ToArray())
                {
                    // Verify the voice channel
                    var voiceChannel = generated.VoiceChannel;
                    if (voiceChannel == null)
                    {
                        // Remove missing generated voice channel
                        GeneratedChannel.Channels[entry.Key].Remove(generated);

                        // Save file
                        GeneratedChannel.SaveToFile();
                    }
                    else
                    {
                        // Check if channel is empty
                        if (voiceChannel.Users.Count == 0)
                        {
                            // Trigger left channel method
                            await CheckLeftChannel(voiceChannel);
                        }
                        else
                        {
                            // Trigger joined channel method
                            await CheckJoinedChannel(voiceChannel);
                        }
                    }
                }

                if (GeneratedChannel.Channels[entry.Key].Count == 0)
                {
                    // Remove empty entry
                    GeneratedChannel.Channels.Remove(entry.Key);

                    // Save file
                    GeneratedChannel.SaveToFile();
                }
            }

            // Validate non-generated voice channels on load with at least 1 user in it
            foreach (var voiceChannel in Discord.Server.VoiceChannels.Where(o => o.Users.Count > 0))
            {
                // Verify it's not a generated channel
                if (GeneratedChannel.Channels.SelectMany(o => o.Value).All(o => o.Id != voiceChannel.Id))
                {
                    await CheckJoinedChannel(voiceChannel);
                }
            }
        }

        public override Task CreateCommands(DiscordSocketRestClient client)
        {
            return base.CreateCommands(client);
        }

        private async Task CheckLeftChannel(SocketVoiceChannel channel, ServerSettings settings)
        {
            // Verify it's not the AFK channel
            if (channel.Guild.AFKChannel != null && channel.Id == channel.Guild.AFKChannel.Id)
            {
                return;
            }

            // Ignore occupied channels
            if (channel.Users.Count > 0)
            {
                //Logger.Debug("Channel left is not empty");
                return;
            }

            // Special case parent channel
            //if (_channelIds.Contains(channel.Id))
            if (GeneratedChannel.Channels.ContainsKey(channel.Id))
            {
                // Delete empty side channels as parent got empty
                foreach (var sideChannel in GeneratedChannel.Channels[channel.Id].ToArray())
                {
                    if (sideChannel.VoiceChannel != null && sideChannel.VoiceChannel.Users.Count > 0)
                    {
                        continue;
                    }

                    GeneratedChannel.Channels[channel.Id].Remove(sideChannel);

                    if (sideChannel.VoiceChannel != null)
                    {
                        await sideChannel.VoiceChannel.DeleteAsync();
                    }
                }

                // Save modifications
                GeneratedChannel.SaveToFile();

                // Do not continue as it's the parent channel
                return;
            }

            // Get generated channel handle
            var generatedChannel = GeneratedChannel.Channels.SelectMany(o => o.Value).FirstOrDefault(o => o.Id == channel.Id);
            if (generatedChannel == null)
            {
                throw new Exception("Generated channel handle was not found!");
            }

            // Get parent channel
            var parent = channel.Guild.GetVoiceChannel(generatedChannel.ParentId);
            if (parent == null)
            {
                throw new Exception("Parent voice channel was not found!");
            }

            // Get possible side channels
            var sideChannels = GeneratedChannel.Channels[parent.Id];

            // Ignore continuing as this is the only side channel and parent is not empty
            if (sideChannels.Count == 1 && parent.Users.Count > 0)
            {
                //Logger.Debug("Do not delete channel left, it's the only one and parent is not empty");
                return;
            }

            // Check for other empty side channels
            var emptyOtherSideChannels = sideChannels.FindAll(o => o.Id != channel.Id && o.VoiceChannel.Users.Count == 0);
            if (emptyOtherSideChannels.Count == 0)
            {
                // No other emtpy channels, check if parent is empty
                if (parent.Users.Count == 0)
                {
                    //Logger.Debug("Delete channel left as parent is empty");

                    // Remove from save file
                    GeneratedChannel.Channels[parent.Id].Remove(generatedChannel);
                    GeneratedChannel.SaveToFile();

                    // Delete this channel
                    await channel.DeleteAsync();
                }
            }
            else
            {
                // Determine which of the empty channels to delete
                var channelsToDelete = emptyOtherSideChannels.Concat(new[] { generatedChannel }).OrderBy(o => o.Index).Skip(1);

                // Delete the channels
                foreach (var toDelete in channelsToDelete)
                {
                    // Remove from save file
                    GeneratedChannel.Channels[parent.Id].Remove(toDelete);

                    // Delete from server
                    await toDelete.VoiceChannel.DeleteAsync();
                }

                // Save file changes
                GeneratedChannel.SaveToFile();
            }
        }

        private async Task CheckJoinedChannel(SocketVoiceChannel channel, ServerSettings settings)
        {
            // Verify it's not the AFK channel
            if (channel.Guild.AFKChannel != null && channel.Id == channel.Guild.AFKChannel.Id)
            {
                return;
            }

            if (channel.Users.Count > 1)
            {
                //Logger.Debug("Channel joined is not empty");
                return;
            }

            // Check if channel is a parent channel
            //var isParent = _channelIds.Contains(channel.Id);
            var isParent = GeneratedChannel.Channels.ContainsKey(channel.Id) ||
                           GeneratedChannel.Channels.SelectMany(o => o.Value).All(o => o.Id != channel.Id);
            if (isParent)
            {
                if (!GeneratedChannel.Channels.ContainsKey(channel.Id))
                {
                    GeneratedChannel.Channels[channel.Id] = new List<GeneratedChannel>();
                }
            }

            // Helpers
            var parentChannel = isParent ? channel : channel.Guild.GetVoiceChannel(GeneratedChannel.Channels.SelectMany(o => o.Value).First(o => o.Id == channel.Id).ParentId);
            var createNewChannel = false;
            var newChannelIndex = -1;

            // Get side channels
            var sideChannels = GeneratedChannel.Channels[parentChannel.Id];

            // Joined side channel
            if (channel.Id != parentChannel.Id)
            {
                // Joined an empty side channel
                if (channel.Users.Count == 1 && parentChannel.Users.Count > 0 && sideChannels.All(o => o.VoiceChannel.Users.Count > 0))
                {
                    createNewChannel = true;
                    newChannelIndex = sideChannels.Max(o => o.Index) + 1;
                }
            }
            // Joined parent channel
            else
            {
                // No other side channels, create first new one
                if (sideChannels.Count == 0)
                {
                    createNewChannel = true;
                    newChannelIndex = 2;
                }
                // All side channels occupied, create another one
                else if (sideChannels.All(o => o.VoiceChannel.Users.Count > 0))
                {
                    createNewChannel = true;
                    newChannelIndex = sideChannels.Max(o => o.Index) + 1;
                }
            }

            if (createNewChannel)
            {
                // Create generated channel instance
                var generatedChannel = new GeneratedChannel
                {
                    ParentChannelName = parentChannel.Name,
                    ParentId = parentChannel.Id,
                    Index = newChannelIndex
                };

                // Create voice channel
                var voiceChannel = await channel.Guild.CreateVoiceChannelAsync(generatedChannel.ChannelName, properties =>
                {
                    properties.Bitrate = parentChannel.Bitrate;
                    properties.UserLimit = parentChannel.UserLimit;
                    properties.CategoryId = parentChannel.CategoryId;
                    properties.Position = parentChannel.Position;
                });

                // Add channel Id to generated channel
                generatedChannel.Id = voiceChannel.Id;

                // Save to file
                sideChannels.Add(generatedChannel);
                GeneratedChannel.SaveToFile();

                // Special case first channel, set the position again
                if (parentChannel.Position == 0)
                {
                    await voiceChannel.ModifyAsync(properties => { properties.Position = 0; });
                }

                // Apply parent channel permissions to the new voice channel created
                await ApplyPermissions(parentChannel, voiceChannel);
            }
        }

        private async Task ApplyPermissions(IGuildChannel sourceChannel, IGuildChannel targetChannel)
        {
            var parentPerms = sourceChannel.PermissionOverwrites;
            foreach (var perm in targetChannel.PermissionOverwrites.ToArray())
            {
                try
                {
                    dynamic target;
                    switch (perm.TargetType)
                    {
                        case PermissionTarget.Role:
                            target = sourceChannel.Guild.GetRole(perm.TargetId);
                            break;

                        case PermissionTarget.User:
                            target = await sourceChannel.Guild.GetUserAsync(perm.TargetId);
                            break;

                        default:
                            Log.Logger.Here().Warning("Could not find Permission target type: " + perm.TargetType);
                            continue;
                    }

                    // Check if permission target is found in parent permissions
                    var parentPerm = parentPerms.FirstOrDefault(o => o.TargetId == perm.TargetId);
                    if (parentPerm.TargetId == 0)
                    {
                        // Remove redundant permission
                        await targetChannel.RemovePermissionOverwriteAsync(target);
                    }
                    else
                    {
                        // Validate permission integrity
                        if (parentPerm.Permissions.AllowValue != perm.Permissions.AllowValue ||
                            parentPerm.Permissions.DenyValue != perm.Permissions.DenyValue)
                        {
                            await targetChannel.RemovePermissionOverwriteAsync(target);
                            await targetChannel.AddPermissionOverwriteAsync(target, parentPerm.Permissions);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, "Failed to apply permission overwrites!");
                }
            }

            // Add possible missing parent permissions
            foreach (var perm in parentPerms)
            {
                try
                {
                    dynamic target;
                    switch (perm.TargetType)
                    {
                        case PermissionTarget.Role:
                            target = sourceChannel.Guild.GetRole(perm.TargetId);
                            break;

                        case PermissionTarget.User:
                            target = await sourceChannel.Guild.GetUserAsync(perm.TargetId);
                            break;

                        default:
                            Log.Logger.Here().Warning("Could not find Permission target type: " + perm.TargetType);
                            continue;
                    }

                    if (targetChannel.PermissionOverwrites.All(o => o.TargetId != perm.TargetId))
                    {
                        // Add missing permission
                        await targetChannel.AddPermissionOverwriteAsync(target, perm.Permissions);
                    }
                }
                catch (Exception e)
                {
                    Log.Logger.Here().Error(e, "Failed to apply permission overwrites!");
                }
            }
        }

        public override async Task OnChannelDestroyed(SocketChannel channel)
        {
            if (!(channel is SocketVoiceChannel))
            {
                return;
            }

            if (GeneratedChannel.Channels.ContainsKey(channel.Id))
            {
                foreach (var generated in GeneratedChannel.Channels[channel.Id])
                {
                    var voiceChannel = generated.VoiceChannel;
                    if (voiceChannel != null)
                    {
                        await voiceChannel.DeleteAsync();
                    }
                }

                GeneratedChannel.Channels.Remove(channel.Id);
                GeneratedChannel.SaveToFile();

                return;
            }

            foreach (var entry in GeneratedChannel.Channels)
            {
                foreach (var generated in entry.Value)
                {
                    if (generated.Id == channel.Id)
                    {
                        GeneratedChannel.Channels[entry.Key].Remove(generated);
                        GeneratedChannel.SaveToFile();

                        return;
                    }
                }
            }
        }       
        
        public override async Task OnChannelUpdated(SocketChannel previousChannel, SocketChannel newChannel)
        {
            // Only handle parent channels with side channels
            if (!GeneratedChannel.Channels.ContainsKey(previousChannel.Id) || GeneratedChannel.Channels[previousChannel.Id].Count == 0)
            {
                return;
            }

            var oldChannel = (SocketVoiceChannel) previousChannel;
            var voiceChannel = (SocketVoiceChannel) newChannel;

            // Only handle name changes
            if (oldChannel.Name == voiceChannel.Name)
            {
                return;
            }

            // Update all unchanged side channels
            foreach (var generatedChannel in GeneratedChannel.Channels[voiceChannel.Id])
            {
                // Only update channels with unchanged names since creation
                if (generatedChannel.ParentChannelName == oldChannel.Name)
                {
                    generatedChannel.ParentChannelName = voiceChannel.Name;
                    await generatedChannel.VoiceChannel.ModifyAsync(properties => { properties.Name = generatedChannel.ChannelName; });
                }
            }
        }

        public override async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState newState)
        {
            // Only handle guild channels
            var guildUser = user as SocketGuildUser;
            if (guildUser == null)
            {
                return;
            }

            // Only handle join/leave/move
            if (previousState.VoiceChannel == newState.VoiceChannel)
            {
                return;
            }

            // Get the server settings
            var settings = GetServerSettings(guildUser.Guild.Id);
            if (settings == null)
            {
                return;
            }

            // Get the voice channels
            var previousChannel = previousState.VoiceChannel;
            var currentChannel = newState.VoiceChannel;

            // Check if a channel was joined/switched
            var hasJoinedChannel = currentChannel != null;
            var hasSwitchedChannel = previousChannel != null && hasJoinedChannel;

            // Determine which channels to verify
            var checkPrevious = previousChannel != null && (hasSwitchedChannel || !hasJoinedChannel);
            var checkCurrent = currentChannel != null && (hasSwitchedChannel || hasJoinedChannel);

            if (checkPrevious)
            {
                await CheckLeftChannel(previousChannel, settings);
            }

            if (checkCurrent)
            {
                await CheckJoinedChannel(currentChannel, settings);
            }
        }
    }
}
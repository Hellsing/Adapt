using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adapt.Lib;
using Discord.Rest;
using Discord.WebSocket;

namespace Discord.VoiceHistory
{
    public class VoiceHistory : BaseDiscordComponent<GlobalSettings, ServerSettings>
    {
        public override string ComponentName { get; protected set; } = nameof(VoiceHistory);
        public override string ComponentDescription { get; protected set; } = "Logs all voice activity into the specified channel.";

        private RestGlobalCommand MainCommand { get; set; }

        public override async Task CreateCommands(DiscordSocketRestClient client)
        {
            // Create the commands
            MainCommand = await client.CreateGlobalCommand(new SlashCommandCreationProperties
            {
                Name = nameof(VoiceHistory).SeparateUpperCase('-').ToLower(),
                Description = $"Everything regarding the {nameof(VoiceHistory)} component.",
                Options = new List<ApplicationCommandOptionProperties>
                {
                    new()
                    {
                        Type = ApplicationCommandOptionType.Channel,
                        Name = "target",
                        Description = "Defines the target channel to log the activities to.",
                        Required = true
                    }
                }
            });
        }

        public override async Task OnSlashCommandReceived(SocketSlashCommand slashCommand)
        {
            // Verify command
            if (slashCommand.Data.Id != MainCommand.Id)
            {
                await slashCommand.FollowupAsync("Command ID mismatch!");
                return;
            }

            // Verify it's used on a server
            var user = slashCommand.User as SocketGuildUser;
            if (user == null)
            {
                await slashCommand.FollowupAsync("This command can only be used on a server!");
                return;
            }
            
            // Get the command options
            var options = slashCommand.Data?.Options;
            if (options == null || options.Count == 0)
            {
                await slashCommand.FollowupAsync("Command syntax failed!");
                return;
            }

            // Get the server settings
            var settings = GetServerSettings(user.Guild.Id);
            if (settings == null)
            {
                await slashCommand.FollowupAsync("Server settings not found!");
                return;
            }

            foreach (var option in options)
            {
                switch (option.Name)
                {
                    case "target":
                    {
                        if (option.Value is SocketTextChannel targetChannel)
                        {
                            // Apply target channel argument in the settings and save
                            settings.TargetChannelId = targetChannel.Id;
                            SaveSettings();

                            await slashCommand.FollowupAsync("Target channel successfully updated to " + targetChannel.Mention);
                            return;
                        }

                        break;
                    }
                }
            }

            Log.Here().Debug("Command unsuccessfully handled.");
            await slashCommand.FollowupAsync("Unsuccessful");
        }

        public override async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState previousState, SocketVoiceState newState)
        {
            var guildUser = user as SocketGuildUser;
            if (guildUser == null)
            {
                return;
            }

            // Get the settings
            var settings = GetServerSettings(guildUser.Guild.Id);
            if (settings == null)
            {
                return;
            }

            // Get the channel to log to
            var logChannel = guildUser.Guild.GetTextChannel(settings.TargetChannelId);
            if (logChannel == null)
            {
                return;
            }

            var oldChannel = previousState.VoiceChannel;
            var newChannel = newState.VoiceChannel;

            var hasPermission = guildUser.GuildPermissions.Administrator;
            if (!hasPermission)
            {
                foreach (var perm in logChannel.PermissionOverwrites)
                {
                    if (hasPermission)
                    {
                        break;
                    }

                    switch (perm.TargetType)
                    {
                        case PermissionTarget.Role:
                        {
                            if (guildUser.Roles.Any(o => o.Id == perm.TargetId))
                            {
                                hasPermission = perm.Permissions.ViewChannel == PermValue.Allow;
                            }

                            break;
                        }

                        case PermissionTarget.User:
                        {
                            if (perm.TargetId == guildUser.Id)
                            {
                                hasPermission = perm.Permissions.ViewChannel == PermValue.Allow;
                            }

                            break;
                        }
                    }
                }
            }

            var userName = hasPermission ? $"`{guildUser.Username}#{guildUser.Discriminator}`" : guildUser.Mention;

            if (oldChannel != null)
            {
                if (newChannel == null)
                {
                    // User left a channel
                    await logChannel.SendMessageAsync($"> {userName} left the voice channel `{oldChannel.Name}` (`{oldChannel.Id}`)");
                    return;
                }

                if (oldChannel.Id != newChannel.Id)
                {
                    // User moved to a different channel
                    await logChannel.SendMessageAsync($"> {userName} moved from `{oldChannel.Name}` to `{newChannel.Name}`");
                    return;
                }
            }
            else
            {
                if (newChannel != null)
                {
                    // User joined a channel
                    await logChannel.SendMessageAsync($"> {userName} joined the voice channel `{newChannel.Name}` (`{newChannel.Id}`)");
                    return;
                }
            }

            // User toggled streaming
            if (oldChannel != null && previousState.IsStreaming != newState.IsStreaming)
            {
                var startedStreaming = newState.IsStreaming;
                await logChannel.SendMessageAsync($"> {userName} {(startedStreaming ? "started" : "stopped")} streaming in voice channel `{oldChannel.Name}` (`{oldChannel.Id}`)");
            }
        }
    }
}
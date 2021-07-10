using System.Collections.Immutable;
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
            MainCommand = await client.CreateGlobalCommand(new SlashCommandBuilder()
                                                          .WithName(nameof(VoiceHistory).SeparateByUpperCase('-').ToLower())
                                                          .WithDescription($"Everything regarding the {nameof(VoiceHistory)} component.")
                                                          .AddOption(new SlashCommandOptionBuilder()
                                                                    .WithName("target")
                                                                    .WithType(ApplicationCommandOptionType.Channel)
                                                                    .WithDescription("Defines the target channel to log the activities to.")
                                                                    .WithRequired(true))
                                                          .Build());

            MainCommand.ListenOptions(OnTargetCommand, "target");
        }

        private async Task OnTargetCommand(SocketSlashCommand command, ImmutableDictionary<string, SocketSlashCommandDataOption> options)
        {
            // Verify it's used on a server
            var user = command.User as SocketGuildUser;
            if (user == null)
            {
                await command.FollowupAsync("This command can only be used on a server!");
                return;
            }

            // Get the server settings
            var settings = GetServerSettings(user.Guild.Id);

            var channel = options["target"].Value as SocketChannel;
            if (channel is SocketTextChannel targetChannel)
            {
                // Apply target channel argument in the settings and save
                settings.TargetChannelId = targetChannel.Id;
                SaveSettings();

                await command.FollowupAsync("Target channel successfully updated to " + targetChannel.Mention);
                return;
            }

            // Invalid channel type given
            await command.FollowupAsync("Channel type given was not a text channel!");
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
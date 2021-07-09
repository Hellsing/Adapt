using System.Collections.Immutable;
using System.Threading.Tasks;
using Adapt.Lib;
using Discord.WebSocket;

namespace Discord.TestPlugin
{
    public class TestPlugin : BaseDiscordComponent
    {
        public override string ComponentName { get; protected set; } = nameof(TestPlugin);
        public override string ComponentDescription { get; protected set; } = "This is a test plugin.";

        public override async Task CreateCommands(DiscordSocketRestClient client)
        {
            var command = await client.CreateGuildCommand(new SlashCommandBuilder()
                                                         .WithName("test")
                                                         .WithDescription("This is a test command!")
                                                         .AddOption(new SlashCommandOptionBuilder()
                                                                   .WithName("option")
                                                                   .WithDescription("Super option.")
                                                                   .WithType(ApplicationCommandOptionType.Boolean)
                                                                   .WithRequired(true))
                                                         .Build(), 480709090000109579);

            //command.ListenOptions(OnOptionCommand, "option");
            command.Listen(OnCommand);
        }

        private async Task OnCommand(SocketSlashCommand command)
        {
            await command.FollowupAsync("Beep boop!");
        }

        private async Task OnOptionCommand(SocketSlashCommand command, ImmutableDictionary<string, SocketSlashCommandDataOption> options)
        {
            await command.FollowupAsync($"Du hast {options["option"].Value.GetType().FullName} ausgewählt!");
        }
    }
}
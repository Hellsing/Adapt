using System.Threading.Tasks;
using Adapt.Lib;

namespace Discord.StatusMessage
{
    public class StatusMessage : BaseDiscordComponent<GlobalSettings, ServerSettings>
    {
        public override string ComponentName { get; protected set; } = nameof(StatusMessage);
        public override string ComponentDescription { get; protected set; } = "Sets the Bot status to the config value.";

        public override async Task OnReady()
        {
            // Set the status
            await Manager.Client.SetGameAsync(GlobalSettings.StatusMessage, GlobalSettings.StreamUrl, GlobalSettings.StatusType);

            Log.Information($"{nameof(StatusMessage)}.{nameof(OnReady)}: Set the Bot status to '{GlobalSettings.StatusType} {GlobalSettings.StatusMessage}'!");
        }
    }
}
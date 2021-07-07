using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adapt.Lib;
using Discord.WebSocket;

namespace Discord.ReactToUser
{
    public class ReactToUser : BaseDiscordComponent<GlobalSettings, ServerSettings>
    {
        public override string ComponentName { get; protected set; } = nameof(ReactToUser);
        public override string ComponentDescription { get; protected set; } = "Adds a reaction to each user with their own emotes in the settings.";

        public override async Task OnMessageReceived(SocketMessage message)
        {
            // Only handle guild messages
            if (message.Channel is not SocketGuildChannel channel)
            {
                return;
            }

            // Get the guild from the message channel
            var guild = channel.Guild;

            // Get the settings
            var settings = GetServerSettings(guild.Id);
            if (settings == null)
            {
                Log.Debug($"Settings were not found for the server {guild.Name} ({guild.Id})!");
                return;
            }

            // Temporary collection of all users to handle
            var usersToHandle = new HashSet<ulong>();

            // Check if the author of the message is tracked by the settings
            if (settings.UserEmotes.ContainsKey(message.Author.Id))
            {
                usersToHandle.Add(message.Author.Id);
            }

            // Check if one or more of the mentions is tracked by the settings
            foreach (var mention in message.MentionedUsers)
            {
                if (settings.UserEmotes.ContainsKey(mention.Id))
                {
                    usersToHandle.Add(mention.Id);
                }
            }

            // Add reactions for each user found in the message
            foreach (var user in usersToHandle)
            {
                await AddUserReactions(user, message, guild, settings);
            }
        }

        private async Task AddUserReactions(ulong settingsUser, IMessage message, IGuild guild, ServerSettings settings)
        {
            // Get the list of emotes bound to the user id
            var emoteIds = settings.UserEmotes[settingsUser].ToHashSet();
            var finalEmotes = new List<IEmote>();

            foreach (var emoteId in settings.UserEmotes[settingsUser])
            {
                // Try to get the emote from the guild list of emotes
                var emote = guild.Emotes.FirstOrDefault(o => o.Name.Equals(emoteId, StringComparison.InvariantCultureIgnoreCase));
                if (emote == null)
                {
                    continue;
                }

                // Add the emote to the list
                finalEmotes.Add(emote);

                // Remove the successful emote catch from the list of emotes to handle
                emoteIds.Remove(emoteId);
            }

            // Parse the remaining emotes as unicode emojis
            finalEmotes.AddRange(emoteIds.Select(o => new Emoji(o)));

            // Add all emotes reactions to the message
            foreach (var emote in finalEmotes)
            {
                try
                {
                    await message.AddReactionAsync(emote);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to add emote reaction to message!");
                }
            }
        }
    }
}
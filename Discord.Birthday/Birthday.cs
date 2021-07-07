using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Adapt.Lib;
using Discord.WebSocket;

namespace Discord.Birthday
{
    public class Birthday : BaseDiscordComponent<GlobalSettings, ServerSettings>
    {
        public override string ComponentName { get; protected set; } = nameof(Birthday);
        public override string ComponentDescription { get; protected set; } = "Gives a birthday notification reminder in a selected Discord Channel.";

        private static readonly Regex DatePattern = new("(\\d+\\.\\d+)");

        public static Birthday Instance { get; private set; }
        private static TimeSpan TimeTillNextRotation => DateTime.Today.AddDays(1).AddSeconds(30) - DateTime.Now;

        private Timer RotationTimer { get; }

        public Birthday()
        {
            // Apply instance singleton
            Instance = this;

            // Create a timer to iterate once per day
            RotationTimer = new Timer(TimeTillNextRotation.TotalMilliseconds)
            {
                AutoReset = false
            };
            RotationTimer.Elapsed += RotationTimerOnElapsed;
        }

        public override Task OnReady()
        {
            // Start the timer
            if (!RotationTimer.Enabled)
            {
                RotationTimer.Interval = TimeTillNextRotation.TotalMilliseconds;
                RotationTimer.Start();
            }

            // Call rotation method once per OnReady manually
            RotationTimerOnElapsed(this, null);

            return Task.CompletedTask;
        }

        private async void RotationTimerOnElapsed(object sender, ElapsedEventArgs args)
        {
            Log.Information("It's another day, time to check for birthdays...");

            // Verify all members who currently have the birthday role
            foreach (var guild in Manager.Guilds)
            {
                // Get the settings
                var settings = GetServerSettings(guild.Id);
                if (settings == null)
                {
                    Log.Debug($"Settings was not found for server {guild.Name} ({guild.Id})!");
                    continue;
                }

                // Get the birthday role
                var birthdayRole = guild.GetRole(settings.BirthdayRoleId);
                if (birthdayRole == null)
                {
                    Log.Debug($"Birthday role \"{settings.BirthdayRoleId}\" was not found!");
                    continue;
                }

                // Get the notification channel
                var notificationChannel = guild.GetChannel(settings.NotificationChannelId) as SocketTextChannel;
                if (notificationChannel == null)
                {
                    Log.Debug("Notification channel was not set or found! Continuing anyway...");
                }

                // Download all guild users first
                await guild.DownloadUsersAsync();

                // Loop through all users
                foreach (var user in guild.Users)
                {
                    try
                    {
                        // Check if user has the role
                        var userHasRole = user.Roles.Any(o => o.Id == birthdayRole.Id);

                        if (Database.Instance.IsBirthdayToday(user))
                        {
                            if (!userHasRole)
                            {
                                // Add birthday role to user
                                await user.AddRoleAsync(birthdayRole);

                                // Post notification about birthday in channel
                                if (notificationChannel != null)
                                {
                                    await notificationChannel.SendMessageAsync($"> It's {user.Mention}'s birthday today!");
                                }
                            }
                        }
                        else
                        {
                            if (userHasRole)
                            {
                                // Remove birthday role from user
                                await user.RemoveRoleAsync(birthdayRole);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error while handling birthday user!");
                    }
                }
            }

            Log.Debug("Time till next rotation: " + TimeTillNextRotation);

            // Update rotation interval
            RotationTimer.Stop();
            RotationTimer.Interval = TimeTillNextRotation.TotalMilliseconds;
            RotationTimer.Start();
        }

        private async Task ParseBirthdayChannel()
        {
            /*
            if (Manager.Client.Guilds.First().GetChannel(Settings.ChannelId) is not SocketTextChannel channel)
            {
                return;
            }

            var oldest = DateTime.Now;
            var oldestName = string.Empty;

            var asyncEnumerator = channel.GetMessagesAsync(int.MaxValue).GetAsyncEnumerator();

            while (await asyncEnumerator.MoveNextAsync())
            {
                Log.Debug($"Parsing through batch of {asyncEnumerator.Current.Count} messages...");

                foreach (var message in asyncEnumerator.Current)
                {
                    var match = DatePattern.Match(message.Content);
                    if (match.Success)
                    {
                        var groupSplit = match.Groups[1].Value.Split('.').Select(int.Parse).ToArray();
                        var day = groupSplit[0];
                        var month = groupSplit[1];

                        if (day is < 1 or > 31)
                        {
                            Log.Debug($"Invalid day given from {message.Author.Username} in message \"{message.Content}\"");
                            continue;
                        }

                        if (month is < 1 or > 12)
                        {
                            Log.Debug($"Invalid month given from {message.Author.Username} in message \"{message.Content}\"");
                            continue;
                        }

                        if (message.Timestamp.DateTime < oldest)
                        {
                            oldest = message.Timestamp.DateTime;
                            oldestName = message.Author.Username;
                        }

                        //Log.Debug($"Found birthday for {message.Author.Username}: {day}.{month}");

                        try
                        {
                            Database.Instance.AddEntry(message.Author, day, month);
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, $"Something went wrong! Day {day} Month {month} with user {message.Author.Username}!");
                        }
                    }
                }
            }

            Log.Debug($"Oldest entry was on {oldest} by {oldestName}");
            Log.Debug($"Found a total of {Database.Instance.Birthdays.Count} birthday entries");*/
        }
    }
}
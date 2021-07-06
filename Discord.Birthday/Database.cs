using System;
using System.Collections.Generic;
using System.IO;
using Adapt.Lib;
using Newtonsoft.Json;
using Serilog;

namespace Discord.Birthday
{
    public class Database
    {
        public const string FileName = nameof(Database) + ".json";
        public static readonly string FilePath = Path.Combine(CoreSettings.DataDirectoryPath, FileName);

        public static Database Instance { get; private set; }

        static Database()
        {
            // Apply instance singleton
            Instance = new Database();

            LoadDatabase();
        }

        public static void SaveDatabase()
        {
            try
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(Instance));
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to write {FileName}!");
            }
        }

        private static void LoadDatabase()
        {
            // Check if the folder exists (should always be true due to the framework)
            if (!Directory.Exists(CoreSettings.DataDirectoryPath))
            {
                Directory.CreateDirectory(CoreSettings.DataDirectoryPath);
            }

            // Check if database file exists
            if (File.Exists(FilePath))
            {
                try
                {
                    // Deserialize database json into the instance
                    Instance = JsonConvert.DeserializeObject<Database>(File.ReadAllText(FilePath)) ?? new Database();

                    Log.Debug($"Loaded database with {Instance.Birthdays.Count} birthday entries!");
                }
                catch (Exception e)
                {
                    Log.Error(e, $"Failed to load {FileName}!");
                }
            }
            else
            {
                // Create database file as it does not exist with blank values
                SaveDatabase();
            }
        }

        [JsonProperty]
        public List<BirthdayEntry> Birthdays { get; } = new();

        public void AddEntry(ulong discordUserId, int day, int month)
        {
            var currentEntry = Birthdays.Find(o => o.DiscordId == discordUserId);
            if (currentEntry != null)
            {
                // Skip identical entries
                if (currentEntry.Date.Day == day && currentEntry.Date.Month == month)
                {
                    return;
                }

                // Update existing value
                currentEntry.Date = new BirthdayEntry.BirthdayDate(day, month);

                Log.Debug($"Updated {discordUserId} in the database!");
            }
            else
            {
                // Create a new entry
                Birthdays.Add(new BirthdayEntry
                {
                    DiscordId = discordUserId,
                    Date = new BirthdayEntry.BirthdayDate(day, month)
                });

                Log.Debug($"Added {discordUserId} to the database!");
            }

            SortDatabase();
            SaveDatabase();
        }

        public void AddEntry(IUser user, int day, int month)
        {
            AddEntry(user.Id, day, month);
        }

        public void RemoveEntry(ulong discordUserId)
        {
            var entry = Birthdays.Find(o => o.DiscordId == discordUserId);
            if (entry != null)
            {
                // Remove the found entry from the database
                Birthdays.Remove(entry);

                Log.Debug($"Removed {discordUserId} from the database!");
            }

            SaveDatabase();
        }

        public void RemoveEntry(IUser user)
        {
            RemoveEntry(user.Id);
        }

        private void SortDatabase()
        {
            // Sort the list ascending by birthday
            Birthdays.Sort((x, y) => x.Date.DateTime.CompareTo(y.Date.DateTime));
        }

        public bool IsBirthdayToday(ulong discordUserId)
        {
            return Birthdays.Find(o => o.DiscordId == discordUserId)?.IsBirthdayToday ?? false;
        }

        public bool IsBirthdayToday(IUser user)
        {
            return IsBirthdayToday(user.Id);
        }

        public class BirthdayEntry
        {
            [JsonProperty]
            public ulong DiscordId { get; set; }

            [JsonProperty]
            public BirthdayDate Date { get; set; }

            [JsonIgnore]
            public bool IsBirthdayToday => Date.Day == DateTime.Now.Day && Date.Month == DateTime.Now.Month;

            public struct BirthdayDate
            {
                [JsonProperty]
                public int Day;

                [JsonProperty]
                public int Month;

                private DateTime? _dateTime;

                [JsonIgnore]
                public DateTime DateTime => _dateTime ??= new DateTime(2000, Month, Day);

                public BirthdayDate(int day, int month)
                {
                    // Apply fields
                    _dateTime = null;
                    Day = day;
                    Month = month;
                }
            }
        }
    }
}
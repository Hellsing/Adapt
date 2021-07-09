using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord.Rest;
using Discord.WebSocket;
using Serilog;

namespace Adapt.Lib
{
    public static class SlashCommandListener
    {
        private static Dictionary<ulong, List<Func<SocketSlashCommand, Task>>> Listeners { get; } = new();
        private static Dictionary<ulong, Dictionary<HashSet<string>, List<Func<SocketSlashCommand, ImmutableDictionary<string, SocketSlashCommandDataOption>, Task>>>> OptionsListeners { get; } = new();

        private static bool _initialized;

        internal static void Initialize(IDiscordManager manager)
        {
            // Only initialize once
            if (_initialized)
            {
                return;
            }

            // Listen to the application created event which is needed for the slash command handling
            manager.Client.InteractionCreated += OnInteractionCreated;

            // Mark as initialized
            _initialized = true;
        }

        internal static void Clear()
        {
            // Clear all handler collections
            Listeners.Clear();
            OptionsListeners.Clear();
        }

        private static async Task OnInteractionCreated(SocketInteraction interaction)
        {
            // Only handle slash commands
            var slashCommand = interaction as SocketSlashCommand;
            if (slashCommand == null)
            {
                return;
            }

            // Get the command id
            var commandId = slashCommand.Data.Id;

            // Check for general handlers
            if (Listeners.ContainsKey(commandId))
            {
                foreach (var handler in Listeners[commandId])
                {
                    try
                    {
                        // Invoke handler method
                        await handler.Invoke(slashCommand);
                    }
                    catch (Exception e)
                    {
                        Log.Logger.Here().Error(e, "An error occurred while invoking a slash command listener!");
                    }
                }
            }

            // Check for options handlers
            if (OptionsListeners.ContainsKey(commandId) && slashCommand.Data.Options != null)
            {
                // Create options dictionary
                var optionsDict = new Dictionary<string, SocketSlashCommandDataOption>();
                foreach (var option in slashCommand.Data.Options)
                {
                    optionsDict[option.Name] = option;
                }

                var finalOptions = optionsDict.ToImmutableDictionary();

                // Loop through all option listeners
                foreach (var (options, handlerList) in OptionsListeners[commandId])
                {
                    if (options.All(o => slashCommand.Data.Options.Any(option => option.Name == o)))
                    {
                        foreach (var listener in handlerList)
                        {
                            try
                            {
                                // Invoke handler method
                                await listener.Invoke(slashCommand, finalOptions);
                            }
                            catch (Exception e)
                            {
                                Log.Logger.Here().Error(e, "An error occurred while invoking an options slash command listener!");
                            }
                        }
                    }
                }
            }
        }

        public static void Listen(this RestApplicationCommand command, Func<SocketSlashCommand, Task> handlerFunc)
        {
            // Validate command and handler
            Validate(command, handlerFunc);

            if (!Listeners.ContainsKey(command.Id))
            {
                Listeners.Add(command.Id, new List<Func<SocketSlashCommand, Task>>());
            }

            // Add to handlers
            Listeners[command.Id].Add(handlerFunc);
        }

        public static void ListenOptions(this RestApplicationCommand command, Func<SocketSlashCommand, ImmutableDictionary<string, SocketSlashCommandDataOption>, Task> handlerFunc, params string[] options)
        {
            // Validate command, handler and options
            Validate(command, handlerFunc, true);

            // Validate options to listen to
            if (options.Length == 0 || options.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("Can't listen without options given!", nameof(options));
            }

            // Convert options to HashSet as lowercase
            var optionsSet = options.Select(o => o.ToLower()).ToHashSet();
            foreach (var option in optionsSet)
            {
                // Validate command is found in slash command options
                if (command.Options.All(o => o.Name != option))
                {
                    throw new ArgumentException($"Command must contain the given option name: \"{option}\"", nameof(options));
                }
            }

            if (!OptionsListeners.ContainsKey(command.Id))
            {
                OptionsListeners.Add(command.Id, new Dictionary<HashSet<string>, List<Func<SocketSlashCommand, ImmutableDictionary<string, SocketSlashCommandDataOption>, Task>>>(HashSet<string>.CreateSetComparer()));
            }

            if (!OptionsListeners[command.Id].ContainsKey(optionsSet))
            {
                OptionsListeners[command.Id].Add(optionsSet, new List<Func<SocketSlashCommand, ImmutableDictionary<string, SocketSlashCommandDataOption>, Task>>());
            }

            // Add options handler to the list
            OptionsListeners[command.Id][optionsSet].Add(handlerFunc);
        }

        private static void Validate(RestApplicationCommand command, object handlerAction, bool validateOptions = false)
        {
            // Validation of the command
            if (command is not RestGlobalCommand and not RestGuildCommand)
            {
                throw new ArgumentException("Failed to register command listener of type: " + command.GetType().FullName, nameof(command));
            }

            // Further validation of the arguments
            if (handlerAction == null)
            {
                throw new ArgumentException("Handler function can't be null!", nameof(handlerAction));
            }


            if (!validateOptions)
            {
                return;
            }

            // Validate options
            if (command.Options == null)
            {
                throw new ArgumentException("Command options can't be null!", nameof(command));
            }
        }
    }
}
using System.Collections.Generic;
using Discord.WebSocket;

namespace Adapt.Lib
{
    public interface IDiscordManager
    {
        /// <summary>
        ///     The managed <see cref="DiscordSocketClient" /> instance.
        /// </summary>
        DiscordSocketClient Client { get; }

        /// <summary>
        ///     The collection of <see cref="SocketGuild" /> available to the current bot instance.
        /// </summary>
        IReadOnlyCollection<SocketGuild> Guilds { get; }

        /// <summary>
        ///     Register settings file to be managed by the manager instance.
        /// </summary>
        /// <typeparam name="T">The <see cref="IGlobalSettings" /> type.</typeparam>
        /// <param name="component">The calling component (this).</param>
        /// <param name="settings">The settings instance to be registered.</param>
        T RegisterSettings<T>(IDiscordComponent component, T settings) where T : IGlobalSettings;

        /// <summary>
        ///     Register server settings file to be managed by the manager instance.
        /// </summary>
        /// <typeparam name="T">The <see cref="IServerSettings" /> type.</typeparam>
        /// <param name="component">The calling component (this).</param>
        /// <param name="settings">The server settings instance to be registered.</param>
        void RegisterSettings<T>(IDiscordComponent component, IDictionary<ulong, T> settings) where T : IServerSettings;

        /// <summary>
        ///     Save all managed settings.
        /// </summary>
        void SaveSettings();
    }
}
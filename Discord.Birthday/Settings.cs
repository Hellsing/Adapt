using Adapt.Lib;

namespace Discord.Birthday
{
    public class ServerSettings : IServerSettings
    {
        /// <summary>
        ///     The Discord role to hand out to users whose it's birthday.
        /// </summary>
        public ulong BirthdayRoleId { get; set; } = 860213773859553321;

        /// <summary>
        ///     The Discord channel id of the birthday entries to track.
        /// </summary>
        public ulong TrackingChannelId { get; set; } = 495929081700024339;

        /// <summary>
        ///     The channel to post post notifications about birthday role changes to.
        /// </summary>
        public ulong NotificationChannelId { get; set; } = 480709090000109581;
    }

    public class GlobalSettings : IGlobalSettings { }
}
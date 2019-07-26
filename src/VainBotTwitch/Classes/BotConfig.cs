﻿using Microsoft.Extensions.Configuration;

namespace VainBotTwitch.Classes
{
    public class BotConfig
    {
        public BotConfig(IConfiguration config)
        {
            ConnectionString = config["connectionString"];
            TwitchUsername = config["twitchUsername"];
            TwitchOAuth = config["twitchOauth"];
            TwitchClientId = config["twitchClientId"];
            TwitchChannel = config["twitchChannel"];
            TwitchChannelId = config["twitchChannelId"];
            OpenWeatherMapApiKey = config["openWeatherMapApiKey"];
            TrackSubPoints = bool.Parse(config["trackSubPoints"]);
            SubPointsAccessToken = config["subPointsAccessToken"];
            SubPointsRefreshToken = config["subPointsRefreshToken"];
            SubPointsApiUrl = config["subPointsApiUrl"];
            SubPointsApiSecret = config["subPointsApiSecret"];
        }

        public string ConnectionString { get; set; }

        public string TwitchUsername { get; set; }

        public string TwitchOAuth { get; set; }

        public string TwitchClientId { get; set; }

        public string TwitchChannel { get; set; }

        public string TwitchChannelId { get; set; }

        public string OpenWeatherMapApiKey { get; set; }

        public bool TrackSubPoints { get; set; }

        public string SubPointsAccessToken { get; set; }

        public string SubPointsRefreshToken { get; set; }

        public string SubPointsApiUrl { get; set; }

        public string SubPointsApiSecret { get; set; }
    }
}

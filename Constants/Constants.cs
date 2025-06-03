using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FootballPortal
{
    public static class Constants
    {
        public static string TelegramToken =>
        Environment.GetEnvironmentVariable("TelegramToken") ?? throw new Exception("Missing TelegramToken");

        public static string FootballApiToken =>
            Environment.GetEnvironmentVariable("FootballApiToken") ?? throw new Exception("Missing FootballApiToken");

        public static string ApiBaseUrl =>
            Environment.GetEnvironmentVariable("ApiBaseUrl") ?? "https://api-football-v1.p.rapidapi.com/v3/";

        public static string DbConnection =>
            Environment.GetEnvironmentVariable("DbConnection") ?? throw new Exception("Missing DbConnection");

        public static readonly Dictionary<int, string> PopularLeagues = new()
        {
            { 39, "Premier League (ENG)" },
            { 140, "La Liga (ESP)" },
            { 78, "Bundesliga (GER)" },
            { 135, "Serie A (ITA)" },
            { 61, "Ligue 1 (FRA)" },
            { 2, "UEFA Champions League" }
        };

    }
}

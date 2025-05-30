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
        Environment.GetEnvironmentVariable("7391260321:AAHsyQvxpFzCOY6bvKmZlLwWoAt7BDLLde4") ?? throw new Exception("Missing TelegramToken");

        public static string FootballApiToken =>
            Environment.GetEnvironmentVariable("4454f3c5c76ff8560dba8803687d301a") ?? throw new Exception("Missing FootballApiToken");

        public static string ApiBaseUrl =>
            Environment.GetEnvironmentVariable("ApiBaseUrl") ?? "https://v3.football.api-sports.io/";

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
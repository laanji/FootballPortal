using FootballPortal.Models;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FootballPortal.Services 
{ 
    public class FootballApiService
    {
        private readonly HttpClient _http;
        public FootballApiService(HttpClient http)
        {
            _http = http;
        }

        /* 
            /live
        */
        public async Task<List<string>> GetLiveMatchesAsync()
        {
            var response = await _http.GetAsync("fixtures?live=all");

            if (!response.IsSuccessStatusCode)
                return new List<string> { "❌ Не вдалося отримати список матчів." };

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var matches = new List<string>();

            foreach (var match in doc.RootElement.GetProperty("response").EnumerateArray())
            {
                var league = match.GetProperty("league").GetProperty("name").GetString();
                var home = match.GetProperty("teams").GetProperty("home").GetProperty("name").GetString();
                var away = match.GetProperty("teams").GetProperty("away").GetProperty("name").GetString();
                var goalsHome = match.GetProperty("goals").GetProperty("home").GetInt32();
                var goalsAway = match.GetProperty("goals").GetProperty("away").GetInt32();
                var time = match.GetProperty("fixture").GetProperty("status").GetProperty("elapsed").GetInt32();

                matches.Add($"⚽ {home} {goalsHome}:{goalsAway} {away}\n🏟️ {league}, ⏱️ {time} хв");
            }

            return matches.Count > 0 ? matches : new List<string> { "🔴 Зараз немає матчів у прямому ефірі." };
        }

        private async Task<List<string>> TryGetScorers(int leagueId, int season, int count)
        {
            var response = await _http.GetAsync($"players/topscorers?league={leagueId}&season={season}");

            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var scorers = new List<string>();
            int index = 1;

            foreach (var player in doc.RootElement.GetProperty("response").EnumerateArray().Take(count))
            {
                var name = player.GetProperty("player").GetProperty("name").GetString();
                var team = player.GetProperty("statistics")[0].GetProperty("team").GetProperty("name").GetString();
                var goals = player.GetProperty("statistics")[0].GetProperty("goals").GetProperty("total").GetInt32();

                scorers.Add($"#{index++} {name} ({team}) — ⚽ {goals}");
            }

            return scorers;
        }

        /* 
            /topscorers [league] 15
            Топ 15 
        */
        public async Task<List<string>> GetTopScorersAsync(int leagueId, int count = 10)
        {
            var currentYear = DateTime.UtcNow.Year;
            var yearsToTry = new[] { currentYear, currentYear - 1, currentYear - 2, currentYear - 3 };

            foreach (var year in yearsToTry)
            {
                var scorers = await TryGetScorers(leagueId, year, count);

                if (scorers.Count > 0)
                {
                    scorers.Insert(0, $"📅 Сезон: {year}");
                    return scorers;
                }
            }

            return new List<string>();
        }

        /* 
            /standings [league]
            Турнірна таблиця для La Liga у поточному або попередніх сезонах
        */
        public async Task<List<string>> GetStandingsAsync(int leagueId, int count = 10)
        {
            var currentYear = DateTime.UtcNow.Year;
            var yearsToTry = new[] { currentYear, currentYear - 1, currentYear - 2 };

            foreach (var year in yearsToTry)
            {
                var response = await _http.GetAsync($"standings?league={leagueId}&season={year}");

                if (!response.IsSuccessStatusCode)
                    continue;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var data = doc.RootElement.GetProperty("response");
                if (data.GetArrayLength() == 0)
                    continue;

                var standingsArray = data[0].GetProperty("league").GetProperty("standings")[0];

                var lines = new List<string> { $"📅 Сезон: {year}" };

                foreach (var team in standingsArray.EnumerateArray().Take(count))
                {
                    var rank = team.GetProperty("rank").GetInt32();
                    var name = team.GetProperty("team").GetProperty("name").GetString();
                    var points = team.GetProperty("points").GetInt32();
                    var played = team.GetProperty("all").GetProperty("played").GetInt32();

                    lines.Add($"#{rank} {name} — {points} очок ({played} матчів)");
                }

                return lines;
            }

            return new List<string>();
        }

        /* 
            /search team [team]
        */
        public async Task<List<string>> SearchTeamAsync(string name)
        {
            var response = await _http.GetAsync($"teams?search={Uri.EscapeDataString(name)}");
            if (!response.IsSuccessStatusCode) return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var results = new List<string>();

            foreach (var team in doc.RootElement.GetProperty("response").EnumerateArray())
            {
                var teamName = team.GetProperty("team").GetProperty("name").GetString();
                var country = team.GetProperty("team").GetProperty("country").GetString();
                var venue = team.GetProperty("venue");

                string stadium = "❌ Невідомо";
                if (venue.TryGetProperty("name", out var stadiumElement) &&
                    stadiumElement.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(stadiumElement.GetString()))
                {
                    stadium = stadiumElement.GetString();
                }

                string capacityText = "";
                if (venue.TryGetProperty("capacity", out var capElement) && capElement.ValueKind == JsonValueKind.Number)
                {
                    var capacity = capElement.GetInt32();
                    capacityText = $" ({capacity} місць)";
                }

                results.Add(
                    $"""
                    🏟️ {teamName} ({country})
                    🆔 ID: {team.GetProperty("team").GetProperty("id").GetInt32()}
                    📍 Стадіон: {stadium}{capacityText}
                    """);
            }

            return results;
        }

        public async Task<List<(int Id, string Name, string Country)>> SearchTeamsAsync(string name)
        {
            var r = await _http.GetAsync($"teams?search={Uri.EscapeDataString(name)}");

            var rawJson = await r.Content.ReadAsStringAsync();

            if (!r.IsSuccessStatusCode) return new();

            using var doc = JsonDocument.Parse(rawJson);
            var list = new List<(int, string, string)>();

            foreach (var item in doc.RootElement.GetProperty("response").EnumerateArray())
            {
                try
                {
                    var team = item.GetProperty("team");
                    var teamId = team.GetProperty("id").GetInt32();
                    var teamName = team.GetProperty("name").GetString();
                    var country = team.TryGetProperty("country", out var c) ? c.GetString() ?? "N/A" : "N/A";

                    list.Add((teamId, teamName, country));
                }
                catch { continue; }
            }

            return list;
        }
        
        /* 
            /search league [league]
        */
        public async Task<List<string>> SearchLeagueAsync(string name)
        {
            var response = await _http.GetAsync($"leagues?search={Uri.EscapeDataString(name)}");
            if (!response.IsSuccessStatusCode) return new List<string>();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var results = new List<string>();

            foreach (var league in doc.RootElement.GetProperty("response").EnumerateArray())
            {
                var leagueName = league.GetProperty("league").GetProperty("name").GetString();
                var country = league.GetProperty("country").GetProperty("name").GetString();
                var type = league.GetProperty("league").GetProperty("type").GetString();

                var seasons = league.GetProperty("seasons").EnumerateArray().ToList();
                var lastSeason = seasons[seasons.Count - 1];
                var season = lastSeason.GetProperty("year").GetInt32();

                results.Add($"🏆 {leagueName} ({type})\n🌍 {country}, 📅 останній сезон: {season}");
            }

            return results;
        }

        /* 
            /playerid [id]
            Профіль гравця Neymar
        */
        public async Task<string> GetPlayerProfileTextAsync(int playerId)
        {
            var response = await _http.GetAsync($"players/profiles?player={playerId}");
            if (!response.IsSuccessStatusCode)
                return $"⚠️ Не вдалося завантажити профіль для ID {playerId}.";

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var responseArray) || responseArray.GetArrayLength() == 0)
                return $"⚠️ Профіль гравця з ID {playerId} не знайдено.";

            var player = responseArray[0].GetProperty("player");

            string GetSafeString(JsonElement parent, string prop) =>
                parent.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String
                    ? val.GetString() ?? "–"
                    : "–";

            var name = GetSafeString(player, "name");
            var nationality = GetSafeString(player, "nationality");
            var position = GetSafeString(player, "position");
            var photo = GetSafeString(player, "photo");

            var birth = player.TryGetProperty("birth", out var birthEl) ? birthEl : default;

            var birthDate = GetSafeString(birth, "date");
            var birthPlace = GetSafeString(birth, "place");
            var birthCountry = GetSafeString(birth, "country");

            var height = GetSafeString(player, "height");
            var weight = GetSafeString(player, "weight");

            var number = player.TryGetProperty("number", out var numProp) && numProp.ValueKind == JsonValueKind.Number
                ? numProp.GetInt32().ToString()
                : "–";

            return
            $"""
            👤 <b>{name}</b>
            🌍 {nationality}
            🎂 {birthDate} ({birthPlace}, {birthCountry})
            🏃‍♂️ Позиція: {position}
            🔢 Номер: {number}
            📏 Зріст: {height}
            ⚖️ Вага: {weight}
            <a href="{photo}">🖼️ Фото</a>
            """;
        }


        /* 
            /teaminfo [id]
        */
        public async Task<string> GetTeamInfoAsync(int teamId)
        {
            var response = await _http.GetAsync($"teams?id={teamId}");
            if (!response.IsSuccessStatusCode)
                return $"❌ Не вдалося завантажити інформацію про команду ID {teamId}.";

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var responseArray) ||
                responseArray.GetArrayLength() == 0)
            {
                return $"⚠️ Команду з ID {teamId} не знайдено.";
            }

            var teamData = responseArray[0];

            var team = teamData.GetProperty("team");
            var venue = teamData.GetProperty("venue");

            var name = team.GetProperty("name").GetString();
            var code = team.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null;
            var country = team.GetProperty("country").GetString();
            var founded = team.TryGetProperty("founded", out var fProp) && fProp.ValueKind == JsonValueKind.Number ? fProp.GetInt32().ToString() : "–";
            var logo = team.GetProperty("logo").GetString();

            var stadium = venue.GetProperty("name").GetString();
            var city = venue.GetProperty("city").GetString();
            var capacity = venue.TryGetProperty("capacity", out var capProp) && capProp.ValueKind == JsonValueKind.Number ? capProp.GetInt32().ToString() : "–";

            return $"""
                🏟️ <b>{name}</b> ({code})  
                🌍 {country} | 🗓️ Засновано: {founded}  
                📍 Стадіон: {stadium} ({city}, {capacity} місць)  
                <a href="{logo}">🖼️ Логотип</a>
                """;
        }

        /* 
            /leagues [country]
        */
        public async Task<List<string>> GetLeaguesAsync(string? countryFilter = null)
        {
            var year = 2023; 
            var response = await _http.GetAsync($"leagues?season={year}");

            if (!response.IsSuccessStatusCode)
                return new List<string> { "❌ Не вдалося отримати список ліг." };

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var leagues = new List<string>();

            foreach (var item in doc.RootElement.GetProperty("response").EnumerateArray())
            {
                var league = item.GetProperty("league");
                var country = item.GetProperty("country");

                var leagueName = league.GetProperty("name").GetString();
                var leagueId = league.GetProperty("id").GetInt32();
                var leagueType = league.GetProperty("type").GetString();
                var countryName = country.GetProperty("name").GetString();

                var normalizedFilter = countryFilter?.Trim().ToLower();
                var normalizedCountry = countryName.Trim().ToLower();

                if (!string.IsNullOrWhiteSpace(normalizedFilter) &&
                    !normalizedCountry.Contains(normalizedFilter))
                    continue;


                leagues.Add($"🏆 <b>{leagueName}</b> ({leagueType})\n🌍 {countryName} | ID: {leagueId}");
            }

            return leagues;
        }

        /* 
            /stats team [id] [league] [season]
            Статистика команди Barcelona у La Liga за сезон 2023
        */
        public async Task<string> GetTeamStatsTextAsync(int teamId, int leagueId, int season)
        {
            var url = $"teams/statistics?team={teamId}&league={leagueId}&season={season}";
            var r = await _http.GetAsync(url);

            if (!r.IsSuccessStatusCode)
                return $"❌ Статистика не знайдена для команди ID {teamId}.";

            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());

            if (!doc.RootElement.TryGetProperty("response", out var root) || root.ValueKind != JsonValueKind.Object)
                return $"❌ Статистика команди з ID {teamId} не знайдена.";

            string SafeGetString(JsonElement parent, string prop) =>
                parent.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? "–" : "–";

            string SafeGetInt(JsonElement parent, string prop) =>
                parent.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32().ToString() : "0";

            var teamName = SafeGetString(root.GetProperty("team"), "name");

            var fixtures = root.GetProperty("fixtures");
            var played = SafeGetInt(fixtures.GetProperty("played"), "total");
            var wins = SafeGetInt(fixtures.GetProperty("wins"), "total");
            var draws = SafeGetInt(fixtures.GetProperty("draws"), "total");
            var loses = SafeGetInt(fixtures.GetProperty("loses"), "total");

            var goals = root.GetProperty("goals");
            var goalsFor = SafeGetInt(goals.GetProperty("for").GetProperty("total"), "total");
            var goalsAgainst = SafeGetInt(goals.GetProperty("against").GetProperty("total"), "total");

            var formRaw = root.TryGetProperty("form", out var f) ? f.GetString() ?? "" : "";
            var formFormatted = string.Join(" ", formRaw
                .Take(10)
                .Select(c => c switch
                {
                    'W' => "🟢",
                    'D' => "⚪",
                    'L' => "🔴",
                    _ => "❓"
                }));

            return $"""
            📊 <b>Статистика:</b> {teamName} (ID {teamId})
            🏟️ Матчів: {played}
            ✅ Перемог: {wins}
            ➖ Нічиїх: {draws}
            ❌ Поразок: {loses}
            ⚽ Голів: {goalsFor} забито / {goalsAgainst} пропущено
            🔁 Серія: {formFormatted}
            """;
        }


        /* 
            /stats player [playerid] [league] [season] 
        */
        public async Task<string> GetPlayerStatsTextAsync(int playerId, int season)
        {
            var url = $"players?id={playerId}&season={season}";
            var r = await _http.GetAsync(url);

            if (!r.IsSuccessStatusCode)
                return $"❌ Статистика гравця з ID {playerId} не знайдена.";

            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());

            var response = doc.RootElement.GetProperty("response");
            if (response.GetArrayLength() == 0)
                return $"❌ Статистику гравця з ID {playerId} за сезон {season} не знайдено.";

            var player = response[0];

            var info = player.GetProperty("player");
            var name = info.TryGetProperty("name", out var n) ? n.GetString() ?? "?" : "?";
            var age = info.TryGetProperty("age", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : 0;

            var stats = player.GetProperty("statistics")[0];

            string SafeStr(JsonElement el, string key) =>
                el.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() ?? "–" : "–";
            int SafeInt(JsonElement el, string key) =>
                el.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Number ? val.GetInt32() : 0;

            var team = SafeStr(stats.GetProperty("team"), "name");
            var position = SafeStr(stats.GetProperty("games"), "position");
            var appearances = SafeInt(stats.GetProperty("games"), "appearences");
            var goals = SafeInt(stats.GetProperty("goals"), "total");
            var assists = stats.GetProperty("goals").TryGetProperty("assists", out var ass) && ass.ValueKind == JsonValueKind.Number ? ass.GetInt32() : 0;
            var yellow = SafeInt(stats.GetProperty("cards"), "yellow");
            var red = SafeInt(stats.GetProperty("cards"), "red");

            return $"""
            👤 <b>{name}</b> ({age} років)
            🏟️ Команда: {team}
            📌 Позиція: {position}
            📅 Матчів: {appearances}
            ⚽ Голів: {goals} | 🅰️ Асистів: {assists}
            🟨 Жовті: {yellow} | 🟥 Червоні: {red}
            """;
        }

        /* 
            /schedule [team] [season] [number to show]
        */
        public async Task<List<string>> GetTeamScheduleAsync(int teamId, int season = 2023, int count = 5)
        {
            var response = await _http.GetAsync($"fixtures?team={teamId}&season={season}");

            if (!response.IsSuccessStatusCode)
                return new List<string> { "❌ Не вдалося отримати розклад матчів." };

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            var fixtures = doc.RootElement.GetProperty("response")
                .EnumerateArray()
                .OrderByDescending(f => f.GetProperty("fixture").GetProperty("date").GetString())
                .Take(count)
                .Select(f =>
                {
                    var fixture = f.GetProperty("fixture");
                    var league = f.GetProperty("league").GetProperty("name").GetString();
                    var date = DateTime.Parse(fixture.GetProperty("date").GetString()).ToLocalTime();
                    var status = fixture.GetProperty("status").GetProperty("short").GetString();

                    var home = f.GetProperty("teams").GetProperty("home").GetProperty("name").GetString();
                    var away = f.GetProperty("teams").GetProperty("away").GetProperty("name").GetString();
                    var goalsHome = f.GetProperty("goals").GetProperty("home").GetInt32();
                    var goalsAway = f.GetProperty("goals").GetProperty("away").GetInt32();

                    var icon = status switch
                    {
                        "NS" => "🕒", // not started
                        "FT" => "🏁", // full time
                        "1H" or "2H" or "LIVE" => "🔴", // live
                        _ => "📅"
                    };

                    return $"""
                    {icon} <b>{date:dd.MM HH:mm}</b> ({status})  
                    🏟️ {league}  
                    {home} {goalsHome}:{goalsAway} {away}
                    """;
                }).ToList();

            return fixtures;
        }

        /* 
            /compare teams [team1] [team2] [league] [season] 
        */
        public async Task<string> CompareTeamsAsync(int teamId1, int teamId2, int leagueId, int season)
        {
            var t1 = await GetTeamStatsTextAsync(teamId1, leagueId, season);
            var t2 = await GetTeamStatsTextAsync(teamId2, leagueId, season);

            return $"""
            🔵 <b>Команда A</b> (ID {teamId1})
            {t1}

            🟢 <b>Команда B</b> (ID {teamId2})
            {t2}
            """;
                }

        /* 
            /compare players [player1]  [player2]  [season] 
        */
        public async Task<string> ComparePlayersAsync(int playerId1, int playerId2, int season)
        {
            var p1 = await GetPlayerStatsTextAsync(playerId1, season);
            var p2 = await GetPlayerStatsTextAsync(playerId2, season);

            return $"""
            🔵 <b>Гравець A</b> (ID {playerId1})
            {p1}

            🟢 <b>Гравець B</b> (ID {playerId2})
            {p2}
            """;
        }

        public async Task<List<LiveMatchInfo>> GetLiveMatchesRawAsync()
        {
            var r = await _http.GetAsync("fixtures?live=all");
            if (!r.IsSuccessStatusCode) return new();

            var json = await r.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var matches = new List<LiveMatchInfo>();
            foreach (var m in doc.RootElement.GetProperty("response").EnumerateArray())
            {
                var fixture = m.GetProperty("fixture");
                var teams = m.GetProperty("teams");
                var goals = m.GetProperty("goals");

                matches.Add(new LiveMatchInfo
                {
                    Id = fixture.GetProperty("id").GetInt32(),
                    Status = fixture.GetProperty("status").GetProperty("short").GetString() ?? "",
                    GoalsHome = goals.GetProperty("home").GetInt32(),
                    GoalsAway = goals.GetProperty("away").GetInt32(),
                    HomeTeam = teams.GetProperty("home").GetProperty("name").GetString() ?? "?",
                    AwayTeam = teams.GetProperty("away").GetProperty("name").GetString() ?? "?",
                    TeamIds = new HashSet<int>
            {
                teams.GetProperty("home").GetProperty("id").GetInt32(),
                teams.GetProperty("away").GetProperty("id").GetInt32()
            }
                });
            }

            return matches;
        }

        /* 
            /myteamnews
        */
        public async Task<List<string>> GetRecentTeamMatchesAsync(int teamId, int count = 5)
        {
            var possibleSeasons = new[] { 2024, 2023, 2022 };

            foreach (var season in possibleSeasons)
            {
                var response = await _http.GetAsync($"fixtures?team={teamId}&season={season}");

                if (!response.IsSuccessStatusCode)
                    continue;

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

                var matches = doc.RootElement.GetProperty("response")
                    .EnumerateArray()
                    .Where(f =>
                    {
                        var status = f.GetProperty("fixture").GetProperty("status").GetProperty("short").GetString();
                        return status == "FT"; 
                    })
                    .OrderByDescending(f => f.GetProperty("fixture").GetProperty("date").GetString())
                    .Take(count)
                    .Select(f =>
                    {
                        var fixture = f.GetProperty("fixture");
                        var date = DateTime.Parse(fixture.GetProperty("date").GetString()).ToLocalTime();

                        var league = f.GetProperty("league").GetProperty("name").GetString();
                        var home = f.GetProperty("teams").GetProperty("home").GetProperty("name").GetString();
                        var away = f.GetProperty("teams").GetProperty("away").GetProperty("name").GetString();
                        var goalsH = f.GetProperty("goals").GetProperty("home").GetInt32();
                        var goalsA = f.GetProperty("goals").GetProperty("away").GetInt32();

                        return $"📅 {date:dd.MM.yyyy} | {home} {goalsH}:{goalsA} {away} ({league})";
                    }).ToList();

                if (matches.Count > 0)
                    return matches;
            }

            return new List<string> { "🔇 Немає завершених матчів для цієї команди." };
        }

        /* 
            /highlightsteam [teams]
        */
        public async Task<string> GetTeamHighlightAsync(string teamName)
        {
            using var client = new HttpClient();
            var response = await client.GetAsync("https://www.scorebat.com/video-api/v3/");
            if (!response.IsSuccessStatusCode)
                return "❌ Не вдалося отримати відеоогляди.";

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("response", out var highlights))
                return "📼 Відеоогляди недоступні.";

            var match = highlights
                .EnumerateArray()
                .FirstOrDefault(m =>
                {
                    if (!m.TryGetProperty("title", out var titleProp)) return false;
                    var title = titleProp.GetString()?.ToLower() ?? "";
                    return title.Contains(teamName.ToLower());
                });

            if (match.ValueKind == JsonValueKind.Undefined)
                return $"📼 Відеоогляд для \"{teamName}\" не знайдено.";

            var title = match.GetProperty("title").GetString();

            if (!match.TryGetProperty("videos", out var videos) || videos.GetArrayLength() == 0)
                return $"📼 Відеоогляд для \"{title}\" доступний, але відео відсутнє.";

            var embed = videos[0].GetProperty("embed").GetString();

            var matchUrl = Regex.Match(embed ?? "", @"src=['""](?<url>[^'""]+)['""]");
            var url = matchUrl.Success ? matchUrl.Groups["url"].Value : null;

            if (string.IsNullOrWhiteSpace(url))
                return $"📼 Відеоогляд для \"{title}\" знайдено, але посилання не витягнуто.";

            return $"📺 <b>{title}</b>\n<a href=\"{url}\">Переглянути відео</a>";
        }


    }
}

using FootballPortal.Services;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using FootballPortal.Models;
using FootballPortal.Data;

namespace FootballPortal.Handlers 
{
    public class UpdateHandler : IUpdateHandler
    {
        private readonly ILogger<UpdateHandler> _logger;
        private readonly FootballApiService _footballApi;
        private readonly IServiceScopeFactory _scopeFactory;


        private static readonly Dictionary<long, List<string>> LiveMatchCache = new();

        public UpdateHandler(ILogger<UpdateHandler> logger, FootballApiService footballApi, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _footballApi = footballApi;
            _scopeFactory = scopeFactory;
        }

        public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery is not null)
            {
                await HandleCallbackAsync(botClient, update.CallbackQuery, cancellationToken);
                return;
            }

            if (update.Type == UpdateType.Message && update.Message?.Text is { } text)
            {
                var chatId = update.Message.Chat.Id;

                if (text == "/start")
                {
                    var message =
                        """
                        👋 Вітаю! Це <b>FootballPortal</b> ⚽

                        Цей бот дозволяє:

                        📆 Переглядати матчі наживо  
                        📊 Дивитись турнірні таблиці та бомбардирів  
                        📌 Шукати команди, ліги  
                        🧍‍♂️ Отримувати профілі гравців  
                        🏟️ Дивитись повну інформацію про команди  
                        📈 Аналізувати статистику гравців/команд  
                        ⚔️ Порівнювати дві команди або гравців  
                        📺 Переглядати відеоогляди матчів  

                        ➡️ Надрукуй /help для перегляду повного списку команд
                        """;

                    await botClient.SendTextMessageAsync(chatId, message,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken);
                }

                else if (text == "/live")
                {
                    chatId = update.Message.Chat.Id;
                    var matches = await _footballApi.GetLiveMatchesAsync();
                    if (matches.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(chatId, "🔴 Зараз немає матчів у прямому ефірі.", cancellationToken: cancellationToken);
                        return;
                    }

                    TelegramBotService.LiveMatchCache[chatId] = matches;

                    var pageItems = matches.Take(10).ToList();
                    var message = string.Join("\n\n", pageItems);

                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("➡️ Далі", "live_page_1")
                    });

                    await botClient.SendTextMessageAsync(chatId, message,
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/topscorers"))
                {
                    chatId = update.Message.Chat.Id;
                    _logger.LogInformation("📡 Отримано команду /topscorers: {Text}", text);

                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 1)
                    {
                        var leagues = string.Join("\n", Constants.PopularLeagues
                            .Select(kvp => $"/topscorers {kvp.Key} [кількість] — {kvp.Value}"));
                        await botClient.SendTextMessageAsync(chatId,
                            "📋 Оберіть лігу, вказавши її ID:\n\n" + leagues,
                            cancellationToken: cancellationToken);
                        return;
                    }

                    if (!int.TryParse(parts[1], out var leagueId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "❌ Невірний формат ID ліги.", cancellationToken: cancellationToken);
                        return;
                    }

                    int limit = 10;
                    if (parts.Length > 2)
                    {
                        if (parts[2].ToLower() == "all")
                            limit = 100;
                        else if (int.TryParse(parts[2], out var parsedLimit))
                            limit = parsedLimit;
                    }

                    var scorers = await _footballApi.GetTopScorersAsync(leagueId, limit); 

                    if (scorers.Count <= 1)
                    {
                        await botClient.SendTextMessageAsync(chatId, "⚠️ Не знайдено бомбардирів для цієї ліги.", cancellationToken: cancellationToken);
                        return;
                    }

                    var message = string.Join("\n\n", scorers); 
                    await botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/standings"))
                {
                    chatId = update.Message.Chat.Id;
                    _logger.LogInformation("📡 Отримано команду /standings: {Text}", text);

                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 1)
                    {
                        var leagues = string.Join("\n", Constants.PopularLeagues
                            .Select(kvp => $"/standings {kvp.Key} — {kvp.Value}"));
                        await botClient.SendTextMessageAsync(chatId,
                            "📋 Оберіть лігу, вказавши її ID:\n\n" + leagues,
                            cancellationToken: cancellationToken);
                        return;
                    }

                    if (!int.TryParse(parts[1], out var leagueId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "❌ Невірний формат ID ліги.", cancellationToken: cancellationToken);
                        return;
                    }

                    int count = 10;
                    if (parts.Length > 2 && int.TryParse(parts[2], out var parsedCount))
                        count = parsedCount;

                    var standings = await _footballApi.GetStandingsAsync(leagueId, count);

                    if (!standings.Any())
                    {
                        await botClient.SendTextMessageAsync(chatId, "⚠️ Не вдалося знайти турнірну таблицю для цієї ліги.", cancellationToken: cancellationToken);
                        return;
                    }

                    var message = string.Join("\n\n", standings);
                    await botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/searchteam"))
                {
                    var parts = text.Split(' ', 2);
                    if (parts.Length < 2)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Введіть назву команди, напр.: /searchteam Real Madrid", cancellationToken: cancellationToken);
                        return;
                    }

                    var query = parts[1];
                    var teams = await _footballApi.SearchTeamsAsync(query);

                    if (!teams.Any())
                    {
                        await botClient.SendTextMessageAsync(chatId, "❌ Команд не знайдено.", cancellationToken: cancellationToken);
                        return;
                    }

                    var top = teams.Take(10).ToList();

                    var keyboard = new InlineKeyboardMarkup(
                        top.Select(t =>
                            InlineKeyboardButton.WithCallbackData($"#{t.Id} | {t.Name} ({t.Country})", $"select_team_{t.Id}")
                        ).Select(b => new[] { b }) 
                    );

                    await botClient.SendTextMessageAsync(chatId,
                        $"🔎 Знайдені команди: {teams.Count}\n⬇️ Оберіть одну з перших 10:",
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/search"))
                {
                    chatId = update.Message.Chat.Id;
                    _logger.LogInformation("📡 Отримано команду /search: {Text}", text);

                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 3)
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❌ Формат команди:\n/search team <назва>\n/search league <назва>",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var type = parts[1].ToLower();
                    var query = parts[2];

                    List<string> results = new();

                    switch (type)
                    {
                        case "team":
                            results = await _footballApi.SearchTeamAsync(query);
                            break;

                        case "league":
                            results = await _footballApi.SearchLeagueAsync(query);
                            break;

                        default:
                            await botClient.SendTextMessageAsync(chatId,
                                "❌ Невідомий тип пошуку: використовуйте team / league.",
                                cancellationToken: cancellationToken);
                            return;
                    }

                    if (results.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "🔍 Нічого не знайдено за запитом.",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var message = string.Join("\n\n", results.Take(10));
                    await botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/playerid"))
                {
                    chatId = update.Message.Chat.Id;
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2 || !int.TryParse(parts[1], out var playerId))
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❌ Використання: /playerid <id>\nНаприклад: /playerid 276",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var profile = await _footballApi.GetPlayerProfileTextAsync(playerId);

                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: profile,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        disableWebPagePreview: false,
                        cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/teaminfo"))
                {
                    chatId = update.Message.Chat.Id;
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 2 || !int.TryParse(parts[1], out var teamId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "❌ Формат: /teaminfo <id>", cancellationToken: cancellationToken);
                        return;
                    }

                    var info = await _footballApi.GetTeamInfoAsync(teamId);

                    await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: info,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        disableWebPagePreview: false,
                        cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/leagues"))
                {
                     chatId = update.Message.Chat.Id;
                    var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    var country = parts.Length == 2 ? parts[1] : null;

                    var leagues = await _footballApi.GetLeaguesAsync(country);

                    if (leagues.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(chatId, "⚠️ Ліги не знайдено.", cancellationToken: cancellationToken);
                        return;
                    }

                    TelegramBotService.LeagueSearchCache[chatId] = leagues;

                    var message = string.Join("\n\n", leagues.Take(10));
                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("➡️ Далі", "leagues_page_1") }
                    });

                    await botClient.SendTextMessageAsync(chatId, message,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/stats team"))
                {
                    var parts = text.Split(' ');
                    if (parts.Length < 4)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: /stats team [teamId] [leagueId] [season]", cancellationToken: cancellationToken);
                        return;
                    }

                    int teamId = int.Parse(parts[2]);
                    int leagueId = int.Parse(parts[3]);
                    int season = int.Parse(parts[4]);

                    var result = await _footballApi.GetTeamStatsTextAsync(teamId, leagueId, season);
                    await botClient.SendTextMessageAsync(chatId, result, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/stats player"))
                {
                    var parts = text.Split(' ');
                    if (parts.Length < 3)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: /stats player [playerId] [season]", cancellationToken: cancellationToken);
                        return;
                    }

                    int playerId = int.Parse(parts[2]);
                    int season = parts.Length >= 4 ? int.Parse(parts[3]) : DateTime.UtcNow.Year;

                    var result = await _footballApi.GetPlayerStatsTextAsync(playerId, season);
                    await botClient.SendTextMessageAsync(chatId, result, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/schedule"))
                {
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2 || !int.TryParse(parts[1], out var teamId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: /schedule [teamId] [season] [кількість]", cancellationToken: cancellationToken);
                        return;
                    }

                    var season = parts.Length >= 3 && int.TryParse(parts[2], out var s) ? s : DateTime.UtcNow.Year;
                    var count = parts.Length >= 4 && int.TryParse(parts[3], out var c) ? c : 5;

                    var schedule = await _footballApi.GetTeamScheduleAsync(teamId, season, count);

                    if (schedule.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            $"❌ Немає даних про матчі команди ID {teamId} за сезон {season}.",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    await botClient.SendTextMessageAsync(chatId,
                        string.Join("\n\n", schedule),
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);

                }

                else if (text.StartsWith("/compare teams"))
                {
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 5)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: /compare teams [teamId1] [teamId2] [leagueId] [season]", cancellationToken: cancellationToken);
                        return;
                    }

                    var t1 = int.Parse(parts[2]);
                    var t2 = int.Parse(parts[3]);
                    var leagueId = int.Parse(parts[4]);
                    var season = int.Parse(parts[5]);

                    var result = await _footballApi.CompareTeamsAsync(t1, t2, leagueId, season);
                    await botClient.SendTextMessageAsync(chatId, result, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/compare players"))
                {
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: /compare players [playerId1] [playerId2] [season]", cancellationToken: cancellationToken);
                        return;
                    }

                    var p1 = int.Parse(parts[2]);
                    var p2 = int.Parse(parts[3]);
                    var season = int.Parse(parts[4]);

                    var result = await _footballApi.ComparePlayersAsync(p1, p2, season);
                    await botClient.SendTextMessageAsync(chatId, result, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }

                else if (text == "/myteamnews")
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await db.Users.FindAsync(chatId);

                    if (user == null || user.FavoriteTeamId == null)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Спочатку оберіть улюблену команду через /myteam", cancellationToken: cancellationToken);
                        return;
                    }

                    var matches = await _footballApi.GetRecentTeamMatchesAsync(user.FavoriteTeamId.Value, count: 50); // забираємо багато одразу
                    if (matches.Count == 0)
                    {
                        await botClient.SendTextMessageAsync(chatId, "🔇 Немає завершених матчів для цієї команди.", cancellationToken: cancellationToken);
                        return;
                    }

                    TelegramBotService.MyTeamNewsCache[chatId] = matches;

                    var firstPage = matches.Take(10).ToList();
                    var message = string.Join("\n\n", firstPage);

                    var keyboard = new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithCallbackData("➡️ Далі", "myteamnews_page_1")
                    });

                    await botClient.SendTextMessageAsync(chatId, $"📰 <b>Останні матчі вашої команди</b>\n\n{message}",
                        parseMode: ParseMode.Html,
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/highlightsteam"))
                {
                    var teamName = text.Replace("/highlightsteam", "").Trim();
                    if (string.IsNullOrWhiteSpace(teamName))
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❗ Формат: /highlightsteam [назва команди]",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    var result = await _footballApi.GetTeamHighlightAsync(teamName);
                    await botClient.SendTextMessageAsync(chatId, result,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/myteam"))
                {
                    var parts = text.Split(' ');
                    if (parts.Length < 2 || !int.TryParse(parts[1], out var teamId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: /myteam [teamId]", cancellationToken: cancellationToken);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var user = await db.Users.FindAsync(chatId);
                    if (user == null)
                    {
                        user = new AppUser { ChatId = chatId };
                        db.Users.Add(user);
                    }

                    user.FavoriteTeamId = teamId;
                    await db.SaveChangesAsync(cancellationToken);

                    await botClient.SendTextMessageAsync(chatId, $"✅ Улюблена команда збережена: ID {teamId}", cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/notifications"))
                {
                    var mode = text.Split(' ').LastOrDefault()?.ToLower();

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var user = await db.Users.FindAsync(chatId);
                    if (user == null)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Спочатку оберіть команду через /myteam", cancellationToken: cancellationToken);
                        return;
                    }

                    if (mode == "on") user.NotificationsEnabled = true;
                    else if (mode == "off") user.NotificationsEnabled = false;
                    else
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Використання: /notifications [on|off]", cancellationToken: cancellationToken);
                        return;
                    }

                    await db.SaveChangesAsync(cancellationToken);
                    await botClient.SendTextMessageAsync(chatId, $"🔔 Сповіщення {(user.NotificationsEnabled ? "увімкнено" : "вимкнено")}", cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/profile"))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var user = await db.Users.FindAsync(chatId);
                    if (user == null)
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Ви ще не зареєстровані. Використайте /myteam або /myplayer.", cancellationToken: cancellationToken);
                        return;
                    }

                    string profile =
                        $"""
                    🧾 <b>Ваш профіль</b>
                    🆔 ID: {user.ChatId}
                    ⚽ Улюблена команда: {(user.FavoriteTeamId?.ToString() ?? "❌ не вибрано")}
                    👤 Улюблений гравець: {(user.FavoritePlayerId?.ToString() ?? "❌ не вибрано")}
                    🔔 Сповіщення: {(user.NotificationsEnabled ? "Увімкнено" : "Вимкнено")}
                    """;

                    await botClient.SendTextMessageAsync(chatId, profile, parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }

                else if (text.StartsWith("/myplayer"))
                {
                    var parts = text.Split(' ');
                    if (parts.Length < 2 || !int.TryParse(parts[1], out var playerId))
                    {
                        await botClient.SendTextMessageAsync(chatId, "❗ Формат: /myplayer [playerId]", cancellationToken: cancellationToken);
                        return;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var user = await db.Users.FindAsync(chatId);
                    if (user == null)
                    {
                        user = new AppUser { ChatId = chatId };
                        db.Users.Add(user);
                    }

                    user.FavoritePlayerId = playerId;
                    await db.SaveChangesAsync(cancellationToken);

                    await botClient.SendTextMessageAsync(chatId, $"✅ Улюблений гравець збережений: ID {playerId}", cancellationToken: cancellationToken);
                }

                else if (text == "/help")
                {
                    var helpMessage =
                    """
                        📖 <b>Довідка по командам FootballPortal</b>

                        <b>/live</b>  
                        🎮 Матчі, які йдуть зараз

                        <b>/leagues [країна]</b>  
                        📋 Список ліг у вказаній країні  
                        Наприклад: <code>/leagues Spain</code>

                        <b>/standings [id_ліги] [кількість]</b>  
                        📊 Турнірна таблиця  
                        Наприклад: <code>/standings 140 10</code>

                        <b>/topscorers [id_ліги] [кількість або all]</b>  
                        ⚽ Топ бомбардирів ліги  
                        Наприклад: <code>/topscorers 39 15</code>

                        <b>/search team [назва]</b>  
                        🔍 Пошук команди за назвою  
                        Наприклад: <code>/search team Arsenal</code>

                        <b>/search league [назва]</b>  
                        🔍 Пошук ліги за назвою  
                        Наприклад: <code>/search league Bundesliga</code>

                        <b>/teaminfo [id_команди]</b>  
                        🏟️ Повна інформація про команду

                        <b>/playerid [id_гравця]</b>  
                        👤 Профіль гравця

                        <b>/stats player [id_гравця] [сезон]</b>  
                        📈 Статистика гравця

                        <b>/compare players [id1] [id2] [сезон]</b>  
                        ⚔️ Порівняння двох гравців

                        <b>/stats team [id_команди] [id_ліги] [сезон]</b>  
                        📈 Статистика команди

                        <b>/compare teams [id1] [id2] [id_ліги] [сезон]</b>  
                        ⚔️ Порівняння двох команд

                        <b>/schedule [id_команди] [сезон] [кількість]</b>  
                        🗓️ Розклад або історія матчів команди

                        <b>/highlightsteam [назва_команди]</b>  
                        📺 Відеоогляд останнього матчу команди

                        <b>/myteam [id_команди]</b>  
                        ⚽ Задати улюблену команду

                        <b>/myplayer [id_гравця]</b>  
                        👤 Задати улюбленого гравця

                        <b>/notifications [on|off]</b>  
                        🔔 Увімкнути або вимкнути сповіщення

                        <b>/myteamnews</b>  
                        📰 Матчі улюбленої команди

                        <b>/profile</b>  
                        🧾 Показ профілю користувача
                        """;

                    await botClient.SendTextMessageAsync(chatId, helpMessage,
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken);
                }

            }
        }

        public async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var data = callbackQuery.Data;

            try
            {
                if (data.StartsWith("leagues_page_"))
                {
                    var page = int.Parse(data.Replace("leagues_page_", ""));
                    if (!TelegramBotService.LeagueSearchCache.TryGetValue(chatId, out var allLeagues))
                        return;

                    int pageSize = 10;
                    int skip = page * pageSize;
                    var pageItems = allLeagues.Skip(skip).Take(pageSize).ToList();

                    if (pageItems.Count == 0)
                    {
                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Немає більше сторінок.");
                        return;
                    }

                    var message = string.Join("\n\n", pageItems);

                    var buttons = new List<InlineKeyboardButton[]>();

                    if (page > 0)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"leagues_page_{page - 1}") });

                    if (skip + pageSize < allLeagues.Count)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️ Далі", $"leagues_page_{page + 1}") });

                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⏹️ Закрити", "leagues_close") });

                    await botClient.EditMessageTextAsync(chatId,
                        callbackQuery.Message.MessageId,
                        message,
                        replyMarkup: new InlineKeyboardMarkup(buttons),
                        parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
                        cancellationToken: cancellationToken);
                }

                else if (data == "leagues_close")
                {
                    TelegramBotService.LeagueSearchCache.Remove(chatId);

                    await botClient.EditMessageTextAsync(chatId,
                        callbackQuery.Message.MessageId,
                        "✅ Закрито.",
                        cancellationToken: cancellationToken);
                }

                else if (data.StartsWith("live_page_"))
                {
                    var page = int.Parse(data.Replace("live_page_", ""));
                    if (!TelegramBotService.LiveMatchCache.TryGetValue(chatId, out var matches))
                        return;

                    int pageSize = 10;
                    int skip = page * pageSize;
                    var pageItems = matches.Skip(skip).Take(pageSize).ToList();

                    if (pageItems.Count == 0)
                    {
                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Немає більше матчів.");
                        return;
                    }

                    var message = string.Join("\n\n", pageItems);
                    var buttons = new List<InlineKeyboardButton[]>();

                    if (page > 0)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"live_page_{page - 1}") });

                    if (skip + pageSize < matches.Count)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️ Далі", $"live_page_{page + 1}") });

                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⏹️ Закрити", "live_close") });

                    await botClient.EditMessageTextAsync(chatId,
                        callbackQuery.Message.MessageId,
                        message,
                        replyMarkup: new InlineKeyboardMarkup(buttons),
                        cancellationToken: cancellationToken);
                }

                else if (data == "live_close")
                {
                    TelegramBotService.LiveMatchCache.Remove(chatId);

                    await botClient.EditMessageTextAsync(chatId,
                        callbackQuery.Message.MessageId,
                        "✅ Список матчів закрито.",
                        cancellationToken: cancellationToken);
                }

                else if (data.StartsWith("select_team_"))
                {
                    var teamId = int.Parse(data.Replace("select_team_", ""));

                    await botClient.SendTextMessageAsync(chatId,
                        $"✅ Ви обрали команду з ID: {teamId}\n",
                        cancellationToken: cancellationToken);
                }

                else if (data.StartsWith("myteamnews_page_"))
                {
                    var page = int.Parse(data.Replace("myteamnews_page_", ""));
                    if (!TelegramBotService.MyTeamNewsCache.TryGetValue(chatId, out var matches))
                        return;

                    var pageSize = 10;
                    var skip = page * pageSize;
                    var pageItems = matches.Skip(skip).Take(pageSize).ToList();
                    var message = string.Join("\n\n", pageItems);

                    var buttons = new List<InlineKeyboardButton[]>();

                    if (page > 0)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"myteamnews_page_{page - 1}") });

                    if (skip + pageSize < matches.Count)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️ Далі", $"myteamnews_page_{page + 1}") });

                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Закрити", "close") });

                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId,
                        $"📰 <b>Останні матчі вашої команди</b>\n\n{message}",
                        parseMode: ParseMode.Html,
                        replyMarkup: new InlineKeyboardMarkup(buttons),
                        cancellationToken: cancellationToken);
                }

                else if (data.StartsWith("popular_"))
                {
                    var parts = data.Replace("popular_", "").Split("_page_");
                    var category = parts[0];
                    var page = int.Parse(parts[1]);

                    if (!TelegramBotService.PopularCache.TryGetValue(chatId, out var list)) return;

                    var pageSize = 10;
                    var skip = page * pageSize;
                    var pageItems = list.Skip(skip).Take(pageSize).ToList();

                    if (pageItems.Count == 0)
                    {
                        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "❌ Більше елементів немає.");
                        return;
                    }

                    var message = string.Join("\n\n", pageItems);
                    var buttons = new List<InlineKeyboardButton[]>();

                    if (page > 0)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"popular_{category}_page_{page - 1}") });

                    if (skip + pageSize < list.Count)
                        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️ Далі", $"popular_{category}_page_{page + 1}") });

                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("❌ Закрити", "close") });

                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId, message,
                        replyMarkup: new InlineKeyboardMarkup(buttons), parseMode: ParseMode.Html, cancellationToken: cancellationToken);
                }

                else if (data == "close")
                {
                    await botClient.DeleteMessageAsync(chatId, callbackQuery.Message.MessageId, cancellationToken);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Помилка в HandleCallbackAsync");
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "⚠️ Сталася помилка.");
            }
        }

        public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "❌ TelegramBot error");
            return Task.CompletedTask;
        }
    }
}

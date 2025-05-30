using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;
using FootballPortal.Data;
using Microsoft.EntityFrameworkCore;
using FootballPortal.Models;

namespace FootballPortal.Services;

public class LiveNotifier : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITelegramBotClient _bot;
    private readonly FootballApiService _api;
    private readonly ILogger<LiveNotifier> _log;

    private readonly ConcurrentDictionary<int, (int homeGoals, int awayGoals, string status)> _matchCache = new();

    public LiveNotifier(IServiceScopeFactory scopeFactory, ITelegramBotClient bot,
                        FootballApiService api, ILogger<LiveNotifier> log)
    {
        _scopeFactory = scopeFactory;
        _bot = bot;
        _api = api;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var users = await db.Users
                    .Where(u => u.NotificationsEnabled && u.FavoriteTeamId != null)
                    .ToListAsync(ct);

                var liveMatches = await _api.GetLiveMatchesRawAsync(); 
                foreach (var match in liveMatches)
                {
                    var fixtureId = match.Id;
                    var status = match.Status;
                    var homeGoals = match.GoalsHome;
                    var awayGoals = match.GoalsAway;
                    var home = match.HomeTeam;
                    var away = match.AwayTeam;

                    if (_matchCache.TryGetValue(fixtureId, out var last))
                    {
                        if (last.homeGoals != homeGoals || last.awayGoals != awayGoals)
                        {
                            var msg = $"⚽ ГОЛ!\n{home} {homeGoals}:{awayGoals} {away}";
                            await NotifyInterestedUsers(users, match.TeamIds, msg, ct);
                        }

                        if (last.status != status)
                        {
                            string statusMsg = status switch
                            {
                                "1H" or "2H" or "LIVE" => $"🔴 Почався матч!\n{home} vs {away}",
                                "FT" => $"🏁 Матч завершено:\n{home} {homeGoals}:{awayGoals} {away}",
                                _ => null
                            };
                            if (statusMsg != null)
                                await NotifyInterestedUsers(users, match.TeamIds, statusMsg, ct);
                        }
                    }
                    else
                    {
                        _matchCache.TryAdd(fixtureId, (homeGoals, awayGoals, status));
                    }

                    _matchCache[fixtureId] = (homeGoals, awayGoals, status);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "❌ Помилка в LiveNotifier");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private async Task NotifyInterestedUsers(List<AppUser> users, HashSet<int> teamIds, string message, CancellationToken ct)
    {
        var interested = users.Where(u => u.FavoriteTeamId is not null && teamIds.Contains(u.FavoriteTeamId.Value));
        foreach (var user in interested)
        {
            await _bot.SendTextMessageAsync(user.ChatId, message, cancellationToken: ct);
        }
    }
}

using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using FootballPortal.Handlers;
using Telegram.Bot.Types.Enums;

namespace FootballPortal.Services
{
    public class TelegramBotService
    {
        private readonly ITelegramBotClient _bot;
        private readonly UpdateHandler _handler;
        public static readonly Dictionary<long, List<string>> LeagueSearchCache = new();
        public static readonly Dictionary<long, List<string>> LiveMatchCache = new();
        public static readonly Dictionary<long, List<string>> MyTeamNewsCache = new();
        public static readonly Dictionary<long, List<string>> PopularCache = new();

        public TelegramBotService(ITelegramBotClient bot, UpdateHandler handler)
        {
            _bot = bot;
            _handler = handler;
        }

        public void Start()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            _bot.StartReceiving(
                updateHandler: _handler,
                receiverOptions: receiverOptions,
                cancellationToken: CancellationToken.None
            );

            Console.WriteLine("Telegram бот запущено");
        }
    }
}

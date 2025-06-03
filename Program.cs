using Telegram.Bot;
using FootballPortal;
using FootballPortal.Services;
using FootballPortal.Handlers;
using FootballPortal.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(Constants.TelegramToken));
builder.Services.AddSingleton<UpdateHandler>();
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddHostedService<LiveNotifier>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(Constants.DbConnection));

builder.Services.AddHttpClient<FootballApiService>(client =>
{
    client.BaseAddress = new Uri(Constants.ApiBaseUrl);
    client.DefaultRequestHeaders.Add("x-rapidapi-key", Constants.FootballApiToken);
    client.DefaultRequestHeaders.Add("x-rapidapi-host", "https://api-football-v1.p.rapidapi.com");
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging(logging => logging.AddConsole());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); 
}

app.UseSwagger();
app.UseSwaggerUI();

var bot = app.Services.GetRequiredService<TelegramBotService>();
bot.Start();

app.MapControllers();
await app.RunAsync();


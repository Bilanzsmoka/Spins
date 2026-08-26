using Microsoft.EntityFrameworkCore;
using PokerProOS.Api.Middleware;
using PokerProOS.Application.Charts.Interfaces;
using PokerProOS.Application.Sessions.Interfaces;
using PokerProOS.Application.Trainer.Interfaces;
using PokerProOS.Infrastructure.Database;
using PokerProOS.Infrastructure.Repositories;
using PokerProOS.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SQL Server
builder.Services.AddDbContext<PokerProOSDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped<IChartRepository, ChartStrategyRepository>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ITrainerRepository, TrainerRepository>();

// Services
builder.Services.AddScoped<ChartImportService>();

// Handlers
builder.Services.AddScoped<PokerProOS.Application.Charts.Queries.GetChartByStackHandler>();
builder.Services.AddScoped<PokerProOS.Application.Trainer.Queries.EvaluateAnswerHandler>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Middleware pipeline
app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Serve React SPA from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

// SPA fallback - any non-API route serves index.html
app.MapFallbackToFile("index.html");

// Seed data on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PokerProOSDbContext>();
    await context.Database.EnsureCreatedAsync();

    var importer = scope.ServiceProvider.GetRequiredService<ChartImportService>();
    var seedPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "seed-data");
    if (Directory.Exists(seedPath))
    {
        var result = await importer.ImportFromDirectoryAsync(seedPath);
        Console.WriteLine($"Seed: {result.TotalRows} rows from {result.FilesProcessed} files");
    }
}

app.Run();

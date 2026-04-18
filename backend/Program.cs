using DashboardApp;
using DashboardBackend.Hubs;
using DashboardBackend.Models;
using DashboardBackend.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddSingleton<CpuCalculator>();
builder.Services.AddSingleton<GpuCalculator>();
builder.Services.AddSingleton<RamCalculator>();
builder.Services.AddSingleton<NetworkCalculator>();

builder.Services.AddSingleton<AuthService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

app.MapHub<MetricsHub>("/MetricsHub");
app.MapGet("/", () => "Backend is running");

var hubContext = app.Services.GetRequiredService<IHubContext<MetricsHub>>();
var cpuCalculator = app.Services.GetRequiredService<CpuCalculator>();
var gpuCalculator = app.Services.GetRequiredService<GpuCalculator>();
var ramCalculator = app.Services.GetRequiredService<RamCalculator>();
var netCalculator = app.Services.GetRequiredService<NetworkCalculator>();
var authService = app.Services.GetRequiredService<AuthService>();

_ = Task.Run(async () =>
{
    var random = new Random();

    while (true)
    {
        var (networkIn, networkOut) = netCalculator.GetNetworkUsage();

        var usage = new SystemUsage
        {
            Cpu = cpuCalculator.GetCpuUsage(),
            Gpu = gpuCalculator.GetGpuUsage(),
            Ram = ramCalculator.GetRamUsage(),
            NetworkIn = networkIn,
            NetworkOut = networkOut,
        };

        await hubContext.Clients.All.SendAsync("ReceiveMetrics", usage);
        await Task.Delay(1000);
    }
});

_ = Task.Run(async () =>
{
    try
    {
        await authService.InitializeAsync();

        if (!string.IsNullOrWhiteSpace(authService.AccessToken))
        {
            var youtubeService = new YouTubeService(authService.AccessToken);
            var uploads = await youtubeService.GetUploadsAsync();

            await hubContext.Clients.All.SendAsync("ReceiveUploads", uploads);
            Console.WriteLine($"YouTube uploads refreshed at {DateTime.Now}");
        }
        else
        {
            Console.WriteLine("YouTube refresh skipped: no access token available.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"YouTube auto-refresh failed: {ex.Message}");
    }

    await Task.Delay(TimeSpan.FromMinutes(15));
});

app.MapPost("/api/youtube/refresh", async(IHubContext<MetricsHub> hubContext, AuthService authService) =>
{
    try{
    await authService.InitializeAsync();

    if (string.IsNullOrWhiteSpace(authService.AccessToken))
    {
        return Results.BadRequest("Access token was not available.");
    }

    var youtubeService = new YouTubeService(authService.AccessToken);
    var uploads = await youtubeService.GetUploadsAsync();

    await hubContext.Clients.All.SendAsync("ReceiveUploads", uploads);

    return Results.Ok(uploads);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Manual YouTube refresh failed: {ex.Message}");
    }
});

app.Run();
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
builder.Services.AddSingleton<StorageCalculator>();

builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<CurrentStateService>();
builder.Services.AddSingleton<MetricsLogger>();

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
var storCalculator = app.Services.GetRequiredService<StorageCalculator>();
var authService = app.Services.GetRequiredService<AuthService>();

var logger = app.Services.GetRequiredService<MetricsLogger>();
var currentStateService = app.Services.GetRequiredService<CurrentStateService>();

async Task RefreshYouTubeUploadsAsync()
{
    Console.WriteLine($"[YT] Refresh started at {DateTime.Now}");

    await authService.InitializeAsync();

    if (string.IsNullOrWhiteSpace(authService.AccessToken))
    {
        Console.WriteLine("[YT] Refresh skipped: no access token available.");
        return;
    }

    var youtubeService = new YouTubeService(authService.AccessToken);
    var uploads = await youtubeService.GetUploadsAsync();

    await hubContext.Clients.All.SendAsync("ReceiveUploads", uploads);

    Console.WriteLine($"[YT] Refresh complete!");
}

_ = Task.Run(async () =>
{
    DateTime lastLogTime = DateTime.MinValue;

    while (true)
    {
        try
        {
            var (networkIn, networkOut) = netCalculator.GetNetworkUsage();
            var storage = storCalculator.GetStorageUsage();

            var storageDrives = storage.Select(d => new StorageDrive
            {
                Name = d.Name,
                Size = d.Size,
                InUse = d.InUse
            }).ToList();

            var usage = new SystemUsage
            {
                Cpu = cpuCalculator.GetCpuUsage(),
                Gpu = gpuCalculator.GetGpuUsage(),
                Ram = ramCalculator.GetRamUsage(),
                NetworkIn = networkIn,
                NetworkOut = networkOut,
                Storage = storageDrives,
            };

            await hubContext.Clients.All.SendAsync("ReceiveMetrics", usage);

            if ((DateTime.Now - lastLogTime) >= TimeSpan.FromSeconds(5))
            {
                await logger.SaveSnapshotAsync(
                    usage.Cpu,
                    usage.Gpu,
                    usage.Ram,
                    usage.NetworkIn,
                    usage.NetworkOut,
                    currentStateService.CurrentState
                );

                lastLogTime = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Metrics] Error: {ex.Message}");
        }
        
        await Task.Delay(1000);
    }
});

_ = Task.Run(async () =>
{
    try
    {
        await RefreshYouTubeUploadsAsync();

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await RefreshYouTubeUploadsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YT Auto] Error: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[YT Auto] Fatal loop error: {ex.Message}");
    }
});

app.MapPost("/api/youtube/refresh", async() =>
{
    try
    {
        await RefreshYouTubeUploadsAsync();
        return Results.Ok("YouTube uploads refreshed.");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Manual YouTube refresh failed: {ex.Message}");
    }
});

app.MapPost("/api/state/update", (StateUpdateRequest request, CurrentStateService stateService) =>
{
    stateService.SetState(request.State);
    Console.WriteLine($"[State] Current state updated to: {request.State}");

    return Results.Ok();
});

app.Run();

public record StateUpdateRequest(string State);
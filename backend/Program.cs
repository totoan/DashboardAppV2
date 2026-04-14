using DashboardBackend.Hubs;
using DashboardBackend.Models;
using DashboardBackend.Services;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<CpuCalculator>();
builder.Services.AddSingleton<GpuCalculator>();
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

_ = Task.Run(async () =>
{
    var random = new Random();

    while (true)
    {
        var usage = new SystemUsage
        {
            Cpu = cpuCalculator.GetCpuUsage(),
            Gpu = gpuCalculator.GetGpuUsage(),
            Ram = 0,
            NetworkIn = 0,
            NetworkOut = 0,
        };

        await hubContext.Clients.All.SendAsync("ReceiveMetrics", usage);
        await Task.Delay(1000);
    }
});

app.Run();
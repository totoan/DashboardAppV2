using DashboardApp;
using DashboardBackend.Hubs;
using DashboardBackend.Models;
using DashboardBackend.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using System.Net.Sockets;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddSingleton<CpuCalculator>();
builder.Services.AddSingleton<GpuCalculator>();
builder.Services.AddSingleton<RamCalculator>();
builder.Services.AddSingleton<NetworkCalculator>();
builder.Services.AddSingleton<StorageCalculator>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<CurrentStateService>();

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
var currentStateService = app.Services.GetRequiredService<CurrentStateService>();
var latestYouTubeUploads = new List<SubscriptionVideo>();

var slideshowFolder = builder.Configuration["Slideshow:Folder"]
    ?? throw new InvalidOperationException(
        "Slideshow folder is not configured."
    );

var slideshowPIN = builder.Configuration["Slideshow:Pin"];

if (string.IsNullOrWhiteSpace(slideshowPIN))
{
    throw new InvalidOperationException(
        "Slideshow PIN is not configured."
    );
}

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

    latestYouTubeUploads = uploads.ToList();

    Console.WriteLine($"[YT] Refresh complete!");
}

_ = Task.Run(async () =>
{
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

if (!Directory.Exists(slideshowFolder))
{
    Directory.CreateDirectory(slideshowFolder);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(slideshowFolder),
    RequestPath = "/slideshow"
});

app.MapGet("/api/youtube/uploads", () =>
{
    return Results.Ok(latestYouTubeUploads);
});

app.MapGet("/api/slideshow/images", () =>
{
    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".bmp"
    };

    var images = Directory
        .EnumerateFiles(slideshowFolder)
        .Where(file => allowedExtensions.Contains(Path.GetExtension(file)))
        .Select(file => $"/slideshow/{Uri.EscapeDataString(Path.GetFileName(file))}")
        .OrderBy(path => path)
        .ToArray();

    return Results.Ok(images);
});

app.MapPost("/api/slideshow/upload", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart form data.");
    }

    var form = await request.ReadFormAsync();

    var submittenPin = form["pin"].ToString();

    if (submittenPin != slideshowPIN)
    {
        return Results.Unauthorized();
    }

    var files = form.Files;

    if (files.Count == 0)
    {
        return Results.BadRequest("No files uploaded.");
    }

    var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    var uploaded = new List<string>();

    foreach (var file in files)
    {
        if (file.Length == 0)
            continue;

        var extension = Path.GetExtension(file.FileName);
        
        if (!allowedExtensions.Contains(extension))
            continue;

        var safeFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";

        var destination = Path.Combine(slideshowFolder, safeFileName);

        await using var stream = File.Create(destination);
        await file.CopyToAsync(stream);

        uploaded.Add(safeFileName);
    }

    if (uploaded.Count == 0)
    {
        return Results.BadRequest("No supported image files were uploaded.");
    }

    return Results.Ok(new
    {
        count = uploaded.Count,
        files = uploaded
    });
});

app.MapGet("/upload", () => Results.Content("""
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">

    <meta
        name="viewport"
        content="width=device-width, initial-scale=1, viewport-fit=cover"
    >

    <link
        rel="icon"
        type="image/png"
        href="/upload-icon.png"
    >

    <title>Slideshow Upload</title>

    <style>
        * {
            box-sizing: border-box;
        }

        body {
            margin: 0;
            padding: 24px;
            font-family: Arial, sans-serif;
            background-color: #1b1616;
            color: white;
        }

        .container {
            width: 100%;
            max-width: 500px;
            margin: 0 auto;
        }

        h2 {
            text-align: center;
            margin-bottom: 30px;
        }

        #pin {
            width: 100%;
            padding: 14px;
            margin-bottom: 20px;

            font-size: 28px;
            text-align: center;
            letter-spacing: 10px;

            background-color: #292424;
            color: white;

            border: 1px solid #666;
            border-radius: 8px;
        }

        #files {
            width: 100%;
            margin-bottom: 20px;

            font-size: 17px;
            color: white;
        }

        #uploadButton {
            display: block;

            width: 100%;
            min-height: 56px;

            padding: 14px;

            font-size: 19px;
            font-weight: bold;

            color: white;
            background-color: #444;

            border: 1px solid #777;
            border-radius: 8px;

            cursor: pointer;

            -webkit-appearance: none;
            appearance: none;

            touch-action: manipulation;
        }

        #uploadButton:active {
            background-color: #666;
        }

        #uploadButton:disabled {
            opacity: 0.5;
        }

        #status {
            margin-top: 24px;
            min-height: 24px;

            text-align: center;
            font-size: 17px;
        }
    </style>
</head>

<body>

<div class="container">

    <h2>Slideshow Upload</h2>

    <form id="uploadForm">

        <input
            id="pin"
            name="pin"
            type="password"
            inputmode="numeric"
            pattern="[0-9]*"
            maxlength="4"
            placeholder="PIN"
            autocomplete="off"
        >

        <input
            id="files"
            name="files"
            type="file"
            accept="image/*"
            multiple
        >

        <button
            id="uploadButton"
            type="submit"
        >
            Upload Images
        </button>

    </form>

    <div id="status"></div>

</div>

<script>
    const form = document.getElementById("uploadForm");
    const pinInput = document.getElementById("pin");
    const fileInput = document.getElementById("files");
    const uploadButton = document.getElementById("uploadButton");
    const status = document.getElementById("status");

    form.addEventListener("submit", async function(event) {
        event.preventDefault();

        if (pinInput.value.length !== 4) {
            status.textContent = "Enter your 4-digit PIN.";
            return;
        }

        if (fileInput.files.length === 0) {
            status.textContent = "Choose at least one image.";
            return;
        }

        const data = new FormData();

        data.append("pin", pinInput.value);

        for (const file of fileInput.files) {
            data.append("files", file);
        }

        uploadButton.disabled = true;
        uploadButton.textContent = "Uploading...";
        status.textContent = "";

        try {
            const response = await fetch(
                "/api/slideshow/upload",
                {
                    method: "POST",
                    body: data
                }
            );

            if (response.status === 401) {
                status.textContent = "Incorrect PIN.";
                return;
            }

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText);
            }

            const result = await response.json();

            status.textContent =
                `Uploaded ${result.count} image(s).`;

            fileInput.value = "";
        }
        catch (error) {
            console.error(error);

            status.textContent =
                "Upload failed. Check the dashboard server.";
        }
        finally {
            uploadButton.disabled = false;
            uploadButton.textContent = "Upload Images";
        }
    });
</script>

</body>
</html>
""", "text/html"));

app.MapGet("/upload-icon.png", () =>
{
    var iconPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "upload-icon.png"
    );

    return Results.File(iconPath, "image/png");
});

app.MapGet("/api/nova/status", async () =>
{
    try
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var response = await httpClient.GetFromJsonAsync<NovaStatusResponse>(
            "http://127.0.0.1:8765/api/status"
        );

        return Results.Ok(new
        {
            status = response?.Status ?? "offline"
        });
    }
    catch
    {
        return Results.Ok(new
        {
            status = "offline"
        });
    }
});


app.Run();

public record StateUpdateRequest(string State);
public record NovaStatusResponse(string Status);
using System.Security.Cryptography;
using System.Text.Json;

namespace DashboardBackend.Wearable.Services;

public sealed class WearableSettings
{
    public int Port { get; set; } = 5186;
    public string PairingKey { get; set; } = "";
    public string[] AllowedOrigins { get; set; } =
    [
        "http://localhost:5173",
        "http://127.0.0.1:5173"
    ];

    public static WearableSettings Load(string contentRoot)
    {
        string folder = Path.Combine(contentRoot, "wearable", "local");
        string path = Path.Combine(folder, "settings.json");
        Directory.CreateDirectory(folder);

        if (!File.Exists(path))
        {
            var initial = new WearableSettings
            {
                PairingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            };

            string json = JsonSerializer.Serialize(initial, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }

        var settings = JsonSerializer.Deserialize<WearableSettings>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Wearable settings are invalid.");

        if (settings.Port is < 1024 or > 65535 || settings.PairingKey.Length < 32)
        {
            throw new InvalidOperationException("Check the wearable port and pairing key in wearable/local/settings.json.");
        }

        return settings;
    }
}

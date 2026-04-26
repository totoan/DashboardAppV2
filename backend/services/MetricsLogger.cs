using System.Globalization;

public class MetricsLogger
{
    private readonly string _dataFolder = "data";

    public MetricsLogger()
    {
        Directory.CreateDirectory(_dataFolder);
    }

    public async Task SaveSnapshotAsync(
        float cpu,
        float gpu,
        float ram,
        float networkIn,
        float networkOut,
        string label = "")
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string filePath = Path.Combine(_dataFolder, $"dashboard_{today}.csv");

        bool fileExists = File.Exists(filePath);

        await using StreamWriter writer = new StreamWriter(filePath, append: true);

        if (!fileExists)
        {
            await writer.WriteLineAsync(
                "timestamp,cpu,gpu,ram,network_in,network_out,label"
            );
        }

        string row = string.Join(",",
            DateTime.Now.ToString("O"),
            cpu.ToString(CultureInfo.InvariantCulture),
            gpu.ToString(CultureInfo.InvariantCulture),
            ram.ToString(CultureInfo.InvariantCulture),
            networkIn.ToString(CultureInfo.InvariantCulture),
            networkOut.ToString(CultureInfo.InvariantCulture),
            label
        );

        await writer.WriteLineAsync(row);
    }
}
namespace DashboardBackend.Models;

public class SystemUsage
{
    public float Cpu { get; set; }
    public float Gpu { get; set; }
    public float Ram { get; set; }
    public float NetworkIn { get; set; }
    public float NetworkOut { get; set; }
    public List<StorageDrive> Storage { get; set; } = new();

}
public class StorageDrive
{
    public string Name { get; set; } = "";
    public float Size { get; set; }
    public float InUse { get; set; }
}
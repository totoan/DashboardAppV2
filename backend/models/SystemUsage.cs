namespace DashboardBackend.Models;

public class SystemUsage
{
    public float Cpu { get; set; }
    public float Gpu { get; set; }
    public float Ram { get; set; }
    public float NetworkIn { get; set; }
    public float NetworkOut { get; set; }

}
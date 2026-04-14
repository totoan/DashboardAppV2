using System.Diagnostics;
using System.IO;

namespace DashboardBackend.Services
{
    public class CpuCalculator
    {
        private PerformanceCounter counter;
        
        public CpuCalculator()
        {
            counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            counter.NextValue();
        }

        public float GetCpuUsage()
        {
            float usage;
            
            usage = counter.NextValue();

            return usage;
        }
    }

    public class GpuCalculator
    {
        private PerformanceCounterCategory gpuCounterCat;
        private Dictionary<string, PerformanceCounter> gpuCounters;

        public GpuCalculator()
        {
            gpuCounterCat = new PerformanceCounterCategory("GPU Engine");
            gpuCounters = new Dictionary<string, PerformanceCounter>();

            RefreshCounters();
        }

        private void RefreshCounters()
        {
            string[] activeNames = gpuCounterCat.GetInstanceNames();

            List<string> validNames = new List<string>();

            foreach (string name in activeNames)
            {
                if (name.Contains("engtype_3D"))
                {
                    validNames.Add(name);

                    if (!gpuCounters.ContainsKey(name))
                    {
                        PerformanceCounter counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name);

                        counter.NextValue();
                        gpuCounters.Add(name, counter);
                    }
                }
            }
            List<string> inactiveNames = new List<string>();

            foreach (string savedName in gpuCounters.Keys)
            {
                if (!validNames.Contains(savedName))
                {
                    inactiveNames.Add(savedName);
                }
            }

            foreach (string name in inactiveNames)
            {
                gpuCounters[name].Dispose();
                gpuCounters.Remove(name);
            }
        }   

        public float GetGpuUsage()
        {
            RefreshCounters();

            float total = 0;
            List<string> badNames = new List<string>();

            foreach (var pair in gpuCounters)
            {
                try
                {
                    total += pair.Value.NextValue();
                }
                catch
                {
                    badNames.Add(pair.Key);
                }
            }

            foreach (string name in badNames)
            {
                gpuCounters[name].Dispose();
                gpuCounters.Remove(name);
            }

            return total;
        }
    }

    public class RamCalculator
    {
        private PerformanceCounter counter;

        public RamCalculator()
        {
            counter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            counter.NextValue();
        }

        public float GetRamUsage()
        {
            float usage;

            usage = counter.NextValue();

            return usage;
        }
    }

    public class NetworkCalculator
    {
        private PerformanceCounter incounter;
        private PerformanceCounter outcounter;

        public NetworkCalculator()
        {
            incounter = new PerformanceCounter("Network Adapter", "Bytes Received/sec", "realtek pcie 2.5gbe family controller");
            outcounter = new PerformanceCounter("Network Adapter", "Bytes Sent/sec", "realtek pcie 2.5gbe family controller");

            incounter.NextValue();
            outcounter.NextValue();
        }

        public (float In, float Out) GetNetworkUsage()
        {
            float usagein = incounter.NextValue();
            float usageout = outcounter.NextValue();

            return (usagein, usageout);
        }
    }

    public class StorageCalculator
    {
        public List<(string Name, float Size, float InUse)> GetStorageUsage()
        {
            var drivecounts = new List<(string name, float size, float inuse)>();
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo d in allDrives)
            {
                if (d.IsReady)
                {
                    string name = d.Name;
                    float size = d.TotalSize;
                    float inuse = d.TotalSize - d.AvailableFreeSpace;

                    drivecounts.Add((name, size, inuse));
                }
            }
            return drivecounts;
        }
    }
}
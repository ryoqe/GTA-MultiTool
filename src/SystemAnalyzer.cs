using System;
using System.Management;
using System.Collections.Generic;

public static class SystemAnalyzer
{
    public struct PcSpecs
    {
        public int Cores;
        public double RamGB;
        public string GpuName;
    }

    public struct NetworkAdapterInfo
    {
        public string Name;
        public string IpAddress;
        
        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, IpAddress);
        }
    }

    public static PcSpecs AnalyzeSpecs()
    {
        int cores = 0;
        double ramGB = 0;
        string gpuName = "Unknown";

        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT NumberOfLogicalProcessors FROM Win32_Processor"))
            using (ManagementObjectCollection coll = searcher.Get())
            {
                foreach (ManagementObject obj in coll)
                {
                    cores = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                    break;
                }
            }
        }
        catch {}

        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            using (ManagementObjectCollection coll = searcher.Get())
            {
                foreach (ManagementObject obj in coll)
                {
                    double bytes = Convert.ToDouble(obj["TotalPhysicalMemory"]);
                    ramGB = Math.Round(bytes / (1024 * 1024 * 1024));
                    break;
                }
            }
        }
        catch {}

        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
            using (ManagementObjectCollection coll = searcher.Get())
            {
                foreach (ManagementObject obj in coll)
                {
                    gpuName = Convert.ToString(obj["Name"]);
                    break;
                }
            }
        }
        catch {}

        return new PcSpecs { Cores = cores, RamGB = ramGB, GpuName = gpuName };
    }

    public static List<NetworkAdapterInfo> GetNetworkAdapters()
    {
        var list = new List<NetworkAdapterInfo>();
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Description, IPAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True"))
            using (ManagementObjectCollection coll = searcher.Get())
            {
                foreach (ManagementObject obj in coll)
                {
                    string desc = Convert.ToString(obj["Description"]);
                    string[] ips = (string[])obj["IPAddress"];
                    if (ips != null && ips.Length > 0)
                    {
                        list.Add(new NetworkAdapterInfo { Name = desc, IpAddress = ips[0] });
                    }
                }
            }
        }
        catch {}
        return list;
    }
}

public static class SystemMetrics
{
    private static System.Diagnostics.PerformanceCounter cpuCounter;

    static SystemMetrics()
    {
        try
        {
            cpuCounter = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuCounter.NextValue(); // First value is always 0
        }
        catch {}
    }

    public static float GetCpuUsage()
    {
        try
        {
            if (cpuCounter != null)
            {
                return cpuCounter.NextValue();
            }
        }
        catch {}
        return 0;
    }

    public static float GetRamUsagePercent()
    {
        try
        {
            var memStatus = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(memStatus))
            {
                return memStatus.dwMemoryLoad;
            }
        }
        catch {}
        return 0;
    }

    private static string cachedNvidiaSmiPath = null;
    private static bool nvidiaSmiChecked = false;

    private static string GetNvidiaSmiPath()
    {
        if (nvidiaSmiChecked)
            return cachedNvidiaSmiPath;

        nvidiaSmiChecked = true;
        
        string system32Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
        if (System.IO.File.Exists(system32Path))
        {
            cachedNvidiaSmiPath = system32Path;
            return cachedNvidiaSmiPath;
        }

        string progFilesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe");
        if (System.IO.File.Exists(progFilesPath))
        {
            cachedNvidiaSmiPath = progFilesPath;
            return cachedNvidiaSmiPath;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("where", "nvidia-smi");
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                if (proc != null)
                {
                    string path = proc.StandardOutput.ReadLine();
                    proc.WaitForExit();
                    if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path.Trim()))
                    {
                        cachedNvidiaSmiPath = path.Trim();
                        return cachedNvidiaSmiPath;
                    }
                }
            }
        }
        catch {}

        cachedNvidiaSmiPath = null;
        return null;
    }

    public static string GetGpuInfo(out float load, out float temp)
    {
        load = 0;
        temp = 0;

        string smiPath = GetNvidiaSmiPath();
        if (smiPath == null)
        {
            return "N/A";
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(smiPath, "--query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader,nounits");
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit();
                    if (!string.IsNullOrEmpty(output))
                    {
                        var parts = output.Split(',');
                        if (parts.Length >= 2)
                        {
                            float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out load);
                            float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out temp);
                            return string.Format("GPU: {0}% | {1}°C", parts[0].Trim(), parts[1].Trim());
                        }
                    }
                }
            }
        }
        catch {}
        return "N/A";
    }
}

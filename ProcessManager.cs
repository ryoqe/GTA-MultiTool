using System;
using System.IO;
using System.Diagnostics;

public static class ProcessManager
{
    public static void RunCommand(string fileName, string arguments)
    {
        try
        {
            using (Process proc = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                if (proc != null)
                {
                    proc.WaitForExit();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("RunCommand Error: " + ex.Message);
        }
    }

    public static void SuspendProcess(int pid)
    {
        string exeName = Environment.Is64BitOperatingSystem ? "pssuspend64.exe" : "pssuspend.exe";
        string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(string.Format("{0} was not found in the application directory. Please place Microsoft Sysinternals PsSuspend in the same directory.", exeName));
        }
        RunCommand(exePath, string.Format("-accepteula {0}", pid));
    }
    
    public static void ResumeProcess(int pid)
    {
        string exeName = Environment.Is64BitOperatingSystem ? "pssuspend64.exe" : "pssuspend.exe";
        string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exeName);
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(string.Format("{0} was not found in the application directory. Please place Microsoft Sysinternals PsSuspend in the same directory.", exeName));
        }
        RunCommand(exePath, string.Format("-accepteula -r {0}", pid));
    }

    public static int? FindGtaProcessId(string configuredExeName)
    {
        // 1. Try configured name if we have one
        if (!string.IsNullOrEmpty(configuredExeName))
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(configuredExeName);
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    int pid = procs[0].Id;
                    foreach (var p in procs) p.Dispose();
                    return pid;
                }
            }
            catch {}
        }

        // 2. Try default names
        foreach (string name in new string[] { "GTA5", "gta5_enhanced", "GTAIV", "PlayGTA5" })
        {
            var procs = Process.GetProcessesByName(name);
            if (procs.Length > 0)
            {
                int pid = procs[0].Id;
                foreach (var p in procs) p.Dispose();
                return pid;
            }
        }

        // 3. Scan all running processes for containing "gta5", "gtaiv", or "gta_"
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string pName = p.ProcessName;
                    if (pName.IndexOf("gta5", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pName.IndexOf("gtaiv", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        pName.IndexOf("gta_", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int pid = p.Id;
                        p.Dispose();
                        return pid;
                    }
                }
                catch {}
                finally { p.Dispose(); }
            }
        }
        catch {}

        return null;
    }

    public static int CleanRamWorkingSet()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return 1;
        }
        catch
        {
            return 0;
        }
    }
}

using System;
using System.Windows.Forms;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                NativeMethods.SetProcessDPIAware();
            }
        }
        catch {}

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool startSolo = false;
        bool startMinimized = false;
        bool runSafeMode = false;

        foreach (string arg in args)
        {
            if (arg.Equals("--solo", StringComparison.OrdinalIgnoreCase))
                startSolo = true;
            if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) || 
                arg.Equals("--tray", StringComparison.OrdinalIgnoreCase))
                startMinimized = true;
            if (arg.Equals("--safe", StringComparison.OrdinalIgnoreCase))
                runSafeMode = true;
        }

        if (runSafeMode)
        {
            string path = ConfigManager.Get("GTA5Path");
            if (string.IsNullOrEmpty(path))
            {
                path = GameController.AutoDetectGtaPath();
            }
            MainForm.PerformFullSystemRollback(path);
            MessageBox.Show("Safe Mode: All firewall rules, custom routes, registry modifications, autostart entries, and game configuration files have been restored to their defaults.", "Safe Mode Active", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool createdNew;
        using (System.Threading.Mutex mutex = new System.Threading.Mutex(true, "GTA_MultiTool_Unique_12345", out createdNew))
        {
            if (!createdNew)
            {
                // Broadcast WM_SHOWME
                uint wmShowMe = NativeMethods.RegisterWindowMessage("WM_SHOW_GTA_MULTITOOL");
                NativeMethods.PostMessage((IntPtr)0xffff, wmShowMe, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            Application.Run(new MainForm(startSolo, startMinimized));
        }
    }
}

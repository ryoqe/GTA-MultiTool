using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml;
using System.Windows.Forms;

public static class GameController
{
    private static string GetConfigPath()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gta_blocker_paths.conf");
        try
        {
            string tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_write.tmp");
            File.WriteAllText(tempFile, "test");
            File.Delete(tempFile);
            return path;
        }
        catch
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gta_blocker_paths.conf");
        }
    }

    public static string LoadGtaPath()
    {
        string path = GetConfigPath();
        if (File.Exists(path))
        {
            try
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (line.StartsWith("GTA5="))
                    {
                        return line.Substring(5).Trim();
                    }
                }
            }
            catch {}
        }
        return "";
    }

    public static void SaveGtaPath(string gtaPath)
    {
        string path = GetConfigPath();
        try
        {
            File.WriteAllText(path, "GTA5=" + gtaPath);
        }
        catch {}
    }

    public static string AutoDetectGtaPath()
    {
        // 1. Try default names first for performance
        foreach (string name in new string[] { "GTA5", "gta5_enhanced", "GTAIV", "PlayGTA5" })
        {
            var procs = Process.GetProcessesByName(name);
            if (procs.Length > 0)
            {
                try
                {
                    string path = procs[0].MainModule.FileName;
                    foreach (var p in procs) p.Dispose();
                    return path;
                }
                catch {}
                foreach (var p in procs) p.Dispose();
            }
        }
        
        // 2. Scan all processes for containing name
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
                        string path = p.MainModule.FileName;
                        p.Dispose();
                        return path;
                    }
                }
                catch {}
                finally { p.Dispose(); }
            }
        }
        catch {}

        string defaultPath = @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\GTA5.exe";
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        return "";
    }

    public static async Task FlushDnsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using (Process proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "ipconfig",
                    Arguments = "/flushdns",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    proc.WaitForExit();
                }
            }
            catch {}
        });
    }

    public static async Task<int> ClearCacheAsync()
    {
        return await Task.Run(() =>
        {
            int deletedCount = 0;
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Rockstar Games\GTA V");
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException("Cache directory does not exist.");
            }

            foreach (string file in Directory.GetFiles(path))
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch {}
            }

            foreach (string dir in Directory.GetDirectories(path))
            {
                try
                {
                    Directory.Delete(dir, true);
                    deletedCount++;
                }
                catch {}
            }

            return deletedCount;
        });
    }

    public static void GenerateCommandLine(string gtaPath, bool chkCores, bool chkNoQueue, bool chkLowEnd, bool chkNoMemRestrict, string customArgs)
    {
        if (string.IsNullOrEmpty(gtaPath))
        {
            throw new ArgumentException("Please select the game path first.");
        }

        string dir = Path.GetDirectoryName(gtaPath);
        string cmdPath = Path.Combine(dir, "commandline.txt");

        // Create backup of commandline.txt if exists and no backup exists yet
        string cmdBak = cmdPath + ".bak";
        if (File.Exists(cmdPath) && !File.Exists(cmdBak))
        {
            try { File.Copy(cmdPath, cmdBak, true); } catch {}
        }

        var args = new List<string>();
        if (chkCores) { args.Add("-USEALLAVAILABLECORES"); }
        if (chkNoQueue) { args.Add("-FrameQueueLimit 0"); }
        if (chkLowEnd) { args.Add("-DX10 -shadowQuality 0 -grassQuality 0 -reflectionQuality 0"); }
        if (chkNoMemRestrict) { args.Add("-nomemrestrict -norestrictions"); }

        if (!string.IsNullOrEmpty(customArgs))
        {
            string cleaned = Regex.Replace(customArgs, @"[^a-zA-Z0-9\s\-\_]", "");
            if (!string.IsNullOrEmpty(cleaned))
            {
                args.Add(cleaned);
            }
        }

        if (args.Count == 0)
        {
            if (File.Exists(cmdPath))
            {
                File.Delete(cmdPath);
            }
        }
        else
        {
            string content = string.Join(" ", args.ToArray());
            File.WriteAllText(cmdPath, content, new System.Text.UTF8Encoding(false));
        }
    }

    public static void OptimizeSettingsXml()
    {
        string docDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string xmlPath = Path.Combine(docDir, @"Rockstar Games\GTA V\settings.xml");
        if (!File.Exists(xmlPath))
        {
            throw new FileNotFoundException("GTA V settings.xml was not found in your Documents folder.");
        }

        // Backup settings.xml
        string xmlBak = xmlPath + ".bak";
        if (!File.Exists(xmlBak))
        {
            File.Copy(xmlPath, xmlBak, true);
        }

        XmlDocument doc = new XmlDocument();
        doc.Load(xmlPath);

        XmlNode graphicsNode = doc.SelectSingleNode("//graphics");
        if (graphicsNode != null)
        {
            SetXmlAttr(graphicsNode, "shadowQuality", "0");
            SetXmlAttr(graphicsNode, "reflectionQuality", "0");
            SetXmlAttr(graphicsNode, "grassQuality", "0");
            SetXmlAttr(graphicsNode, "msaaValue", "0");
            SetXmlAttr(graphicsNode, "waterQuality", "0");
            SetXmlAttr(graphicsNode, "particlesQuality", "0");
            SetXmlAttr(graphicsNode, "shadow_particleShadows", "false");
            SetXmlAttr(graphicsNode, "shaderQuality", "0");
            SetXmlAttr(graphicsNode, "lodScale", "0.000000");
            SetXmlAttr(graphicsNode, "pedLodBias", "0.000000");
            SetXmlAttr(graphicsNode, "vehicleLodBias", "0.000000");
            SetXmlAttr(graphicsNode, "tessellation", "0");
            SetXmlAttr(graphicsNode, "shadow_longShadows", "false");
            SetXmlAttr(graphicsNode, "shadow_splitShadows", "false");
            SetXmlAttr(graphicsNode, "shadow_softShadows", "0");
        }

        doc.Save(xmlPath);
    }

    private static void SetXmlAttr(XmlNode parent, string elName, string val)
    {
        XmlNode el = parent.SelectSingleNode(elName);
        if (el != null && el.Attributes != null)
        {
            XmlAttribute attr = el.Attributes["value"];
            if (attr != null)
            {
                attr.Value = val;
            }
        }
    }

    public static void RestoreBackups(string gtaPath)
    {
        int restored = 0;
        string docDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string xmlPath = Path.Combine(docDir, @"Rockstar Games\GTA V\settings.xml");
        string xmlBak = xmlPath + ".bak";
        if (File.Exists(xmlBak))
        {
            File.Copy(xmlBak, xmlPath, true);
            restored++;
        }

        if (!string.IsNullOrEmpty(gtaPath))
        {
            string dir = Path.GetDirectoryName(gtaPath);
            string cmdPath = Path.Combine(dir, "commandline.txt");
            string cmdBak = cmdPath + ".bak";
            if (File.Exists(cmdBak))
            {
                File.Copy(cmdBak, cmdPath, true);
                restored++;
            }
        }

        if (restored > 0)
        {
            MessageBox.Show(string.Format("Successfully restored {0} settings backup file(s)!", restored), "Backups Restored", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("No backup files (.bak) were found to restore.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

using System;
using System.IO;
using System.Collections.Generic;

public static class ConfigManager
{
    private static string GetConfigPath()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gta_multitool.conf");
        try
        {
            string tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_write.tmp");
            File.WriteAllText(tempFile, "test");
            File.Delete(tempFile);
            return path;
        }
        catch
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"GTA_MultiTool\gta_multitool.conf");
        }
    }

    public static string Get(string key, string defaultValue = "")
    {
        string path = GetConfigPath();
        if (File.Exists(path))
        {
            try
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0)
                    {
                        string k = line.Substring(0, idx).Trim();
                        string v = line.Substring(idx + 1).Trim();
                        if (k.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            return v;
                        }
                    }
                }
            }
            catch {}
        }
        return defaultValue;
    }

    public static void Set(string key, string value)
    {
        string path = GetConfigPath();
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    int idx = line.IndexOf('=');
                    if (idx > 0)
                    {
                        string k = line.Substring(0, idx).Trim();
                        string v = line.Substring(idx + 1).Trim();
                        dict[k] = v;
                    }
                }
            }

            if (value == null)
            {
                if (dict.ContainsKey(key))
                {
                    dict.Remove(key);
                }
            }
            else
            {
                dict[key] = value;
            }

            var newLines = new List<string>();
            foreach (var kvp in dict)
            {
                newLines.Add(string.Format("{0}={1}", kvp.Key, kvp.Value));
            }
            File.WriteAllLines(path, newLines.ToArray());
        }
        catch {}
    }
}

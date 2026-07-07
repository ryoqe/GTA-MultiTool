using System;
using Microsoft.Win32;
using System.Text.RegularExpressions;

public static class RegistryTools
{
    public static void Write(string keyPath, string valueName, string value, RegistryValueKind kind = RegistryValueKind.String)
    {
        string root = "";
        string subKey = keyPath;
        if (keyPath.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            root = "HKCU";
            subKey = keyPath.Substring(18);
        }
        else if (keyPath.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            root = "HKLM";
            subKey = keyPath.Substring(20);
        }
        else
        {
            root = "HKCU";
        }

        string typeStr = "REG_SZ";
        if (kind == RegistryValueKind.DWord) typeStr = "REG_DWORD";
        
        string args = string.Format("add \"{0}\\{1}\" /v \"{2}\" /t {3} /d \"{4}\" /f", root, subKey, valueName, typeStr, value);
        ProcessManager.RunCommand("reg", args);
    }
    
    public static void Delete(string keyPath, string valueName)
    {
        string root = "";
        string subKey = keyPath;
        if (keyPath.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            root = "HKCU";
            subKey = keyPath.Substring(18);
        }
        else if (keyPath.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            root = "HKLM";
            subKey = keyPath.Substring(20);
        }
        else
        {
            root = "HKCU";
        }

        string args = string.Format("delete \"{0}\\{1}\" /v \"{2}\" /f", root, subKey, valueName);
        ProcessManager.RunCommand("reg", args);
    }
    
    public static void Backup(string configKey, string currentValue)
    {
        if (ConfigManager.Get(configKey, null) == null)
        {
            ConfigManager.Set(configKey, string.IsNullOrEmpty(currentValue) ? "__NULL__" : currentValue);
        }
    }

    public static void AddCompatFlag(string path, string flagToAdd)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", false))
            {
                if (key != null)
                {
                    object existing = key.GetValue(path);
                    string val = existing != null ? existing.ToString() : "";
                    
                    Backup("Backup_CompatFlags_" + path, val);

                    if (!val.Contains(flagToAdd))
                    {
                        if (string.IsNullOrEmpty(val))
                        {
                            val = "~ " + flagToAdd;
                        }
                        else
                        {
                            if (!val.StartsWith("~"))
                            {
                                val = "~ " + val + " " + flagToAdd;
                            }
                            else
                            {
                                val = val + " " + flagToAdd;
                            }
                        }
                        val = Regex.Replace(val, @"\s+", " ").Trim();
                        Write(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", path, val);
                    }
                }
            }
        }
        catch {}
    }

    public static void RemoveCompatFlag(string path, string flagToRemove)
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", false))
            {
                if (key != null)
                {
                    object existing = key.GetValue(path);
                    if (existing != null)
                    {
                        string val = existing.ToString();
                        if (val.Contains(flagToRemove))
                        {
                            val = val.Replace(flagToRemove, "");
                            val = Regex.Replace(val, @"\s+", " ").Trim();
                            
                            if (val == "~" || string.IsNullOrEmpty(val))
                            {
                                Delete(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", path);
                            }
                            else
                            {
                                Write(@"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", path, val);
                            }
                        }
                    }
                }
            }
        }
        catch {}
    }
}

using System;
using System.Diagnostics;
using Microsoft.Win32;

public static class FirewallManager
{
    private const string RuleOutName = "GTA5_Block_Out";
    private const string RuleInName = "GTA5_Block_In";

    public static void InitRules(string gta5Path, bool p2pOnly)
    {
        // Delete existing rules first using netsh
        ProcessManager.RunCommand("netsh", string.Format("advfirewall firewall delete rule name=\"{0}\"", RuleOutName));
        ProcessManager.RunCommand("netsh", string.Format("advfirewall firewall delete rule name=\"{0}\"", RuleInName));

        if (string.IsNullOrEmpty(gta5Path)) return;

        // Create Outbound Rule (initially disabled)
        string outDesc = p2pOnly ? "GTA5 P2P Outbound Block" : "Blocked GTA5 outbound connection";
        string outArgs = string.Format("advfirewall firewall add rule name=\"{0}\" dir=out action=block program=\"{1}\" enable=no description=\"{2}\"", RuleOutName, gta5Path, outDesc);
        if (p2pOnly)
        {
            outArgs += " protocol=udp localport=6672";
        }
        ProcessManager.RunCommand("netsh", outArgs);

        // Create Inbound Rule (initially disabled)
        string inDesc = p2pOnly ? "GTA5 P2P Inbound Block" : "Blocked GTA5 inbound connection";
        string inArgs = string.Format("advfirewall firewall add rule name=\"{0}\" dir=in action=block program=\"{1}\" enable=no description=\"{2}\"", RuleInName, gta5Path, inDesc);
        if (p2pOnly)
        {
            inArgs += " protocol=udp localport=6672";
        }
        ProcessManager.RunCommand("netsh", inArgs);
    }

    public static void SetBlockState(bool block)
    {
        string state = block ? "yes" : "no";
        ProcessManager.RunCommand("netsh", string.Format("advfirewall firewall set rule name=\"{0}\" new enable={1}", RuleOutName, state));
        ProcessManager.RunCommand("netsh", string.Format("advfirewall firewall set rule name=\"{0}\" new enable={1}", RuleInName, state));
    }

    public static bool RulesExist()
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"))
            {
                if (key != null)
                {
                    foreach (string valueName in key.GetValueNames())
                    {
                        string val = key.GetValue(valueName) as string;
                        if (val != null && val.Contains("Name=" + RuleOutName))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Firewall rules exist check error: " + ex.Message);
        }
        return false;
    }

    public static bool IsBlocked()
    {
        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules"))
            {
                if (key != null)
                {
                    foreach (string valueName in key.GetValueNames())
                    {
                        string val = key.GetValue(valueName) as string;
                        if (val != null && val.Contains("Name=" + RuleOutName))
                        {
                            return val.Contains("Active=TRUE");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Firewall registry read error: " + ex.Message);
        }
        return false;
    }
}

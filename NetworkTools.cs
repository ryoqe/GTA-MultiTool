using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Management;
using System.Collections.Generic;
using System.Threading.Tasks;

public static class NetworkTools
{
    private static readonly HashSet<string> addedRoutes = new HashSet<string>();

    public static HashSet<string> AddedRoutes { get { return addedRoutes; } }

    public static string GetInterfaceNameByDescription(string description)
    {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.Description == description)
            {
                return nic.Name;
            }
        }
        return description;
    }

    public static string[] GetCurrentDnsAddresses(string description)
    {
        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.Description == description)
            {
                var dnsServers = nic.GetIPProperties().DnsAddresses;
                var list = new List<string>();
                foreach (var ip in dnsServers)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
                    {
                        list.Add(ip.ToString());
                    }
                }
                return list.ToArray();
            }
        }
        return null;
    }

    public static void SetDns(string description, string[] dns)
    {
        string interfaceName = GetInterfaceNameByDescription(description);
        
        // Back up current DNS settings if not already backed up
        if (ConfigManager.Get("OriginalDNS_" + description, null) == null)
        {
            string[] currentDns = GetCurrentDnsAddresses(description);
            string val = (currentDns != null && currentDns.Length > 0) ? string.Join(",", currentDns) : "__NULL__";
            ConfigManager.Set("OriginalDNS_" + description, val);
        }

        if (dns != null && dns.Length > 0)
        {
            // Set primary DNS
            string args = string.Format("interface ip set dns name=\"{0}\" source=static address={1} register=primary", interfaceName, dns[0]);
            ProcessManager.RunCommand("netsh", args);
            
            // Set secondary DNS if provided
            if (dns.Length > 1)
            {
                string args2 = string.Format("interface ip add dns name=\"{0}\" address={1} index=2", interfaceName, dns[1]);
                ProcessManager.RunCommand("netsh", args2);
            }
        }
        else
        {
            // Restore to DHCP
            string args = string.Format("interface ip set dns name=\"{0}\" source=dhcp", interfaceName);
            ProcessManager.RunCommand("netsh", args);
            
            // Clear the backup flag
            ConfigManager.Set("OriginalDNS_" + description, null);
        }
    }

    public static string GetGateway(string description)
    {
        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "SELECT DefaultIPGateway FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = True AND Description = '" + description.Replace("'", "\\'") + "'"))
            using (ManagementObjectCollection coll = searcher.Get())
            {
                foreach (ManagementObject obj in coll)
                {
                    string[] gateways = (string[])obj["DefaultIPGateway"];
                    if (gateways != null && gateways.Length > 0)
                    {
                        return gateways[0];
                    }
                }
            }
        }
        catch {}
        return null;
    }

    public static void AddRoute(string ip, string gateway)
    {
        string args = string.Format("add {0} mask 255.255.255.255 {1} metric 1", ip, gateway);
        using (Process p = Process.Start(new ProcessStartInfo {
            FileName = "route",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true
        }))
        {
            if (p != null) p.WaitForExit();
        }
        addedRoutes.Add(ip);
    }

    public static void DeleteRoute(string ip)
    {
        using (Process p = Process.Start(new ProcessStartInfo
        {
            FileName = "route",
            Arguments = "delete " + ip,
            UseShellExecute = false,
            CreateNoWindow = true
        }))
        {
            if (p != null) p.WaitForExit();
        }
        addedRoutes.Remove(ip);
    }

    public static async Task<long?> PingHostAsync(string host, string localIp)
    {
        return await Task.Run<long?>(() => {
            var sw = Stopwatch.StartNew();
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Bind(new IPEndPoint(IPAddress.Parse(localIp), 0));
                    
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    if (addresses.Length == 0) return null;

                    var result = socket.BeginConnect(new IPEndPoint(addresses[0], 443), null, null);
                    bool completed = result.AsyncWaitHandle.WaitOne(2000, true);
                    if (completed)
                    {
                        socket.EndConnect(result);
                        sw.Stop();
                        return sw.ElapsedMilliseconds;
                    }
                }
            }
            catch {}
            return null;
        });
    }

    public static async Task<string> GetPublicIpAsync(string localIp)
    {
        return await Task.Run(() => {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.ipify.org");
                request.Timeout = 4000;
                request.ServicePoint.BindIPEndPointDelegate = (servicePoint, remoteEndPoint, retryCount) => {
                    return new IPEndPoint(IPAddress.Parse(localIp), 0);
                };
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd().Trim();
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        });
    }

    public static void RestoreNetworkSettings()
    {
        // 1. Restore DNS
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                string desc = nic.Description;
                string backup = ConfigManager.Get("OriginalDNS_" + desc, null);
                if (backup != null)
                {
                    string interfaceName = nic.Name;
                    if (backup == "__NULL__")
                    {
                        string args = string.Format("interface ip set dns name=\"{0}\" source=dhcp", interfaceName);
                        ProcessManager.RunCommand("netsh", args);
                    }
                    else
                    {
                        string[] dns = backup.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (dns.Length > 0)
                        {
                            string args = string.Format("interface ip set dns name=\"{0}\" source=static address={1} register=primary", interfaceName, dns[0]);
                            ProcessManager.RunCommand("netsh", args);
                            if (dns.Length > 1)
                            {
                                string args2 = string.Format("interface ip add dns name=\"{0}\" address={1} index=2", interfaceName, dns[1]);
                                ProcessManager.RunCommand("netsh", args2);
                            }
                        }
                    }
                    ConfigManager.Set("OriginalDNS_" + desc, null);
                }
            }
        }
        catch {}

        // 2. Delete added routes
        try
        {
            if (addedRoutes.Count == 0)
            {
                string savedRoutes = ConfigManager.Get("AddedRoutes", "");
                if (!string.IsNullOrEmpty(savedRoutes))
                {
                    foreach (var r in savedRoutes.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        addedRoutes.Add(r);
                    }
                }
            }

            foreach (var ip in addedRoutes)
            {
                using (Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "route",
                    Arguments = "delete " + ip,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    if (p != null) p.WaitForExit();
                }
            }
            addedRoutes.Clear();
            ConfigManager.Set("AddedRoutes", null);
        }
        catch {}
    }
}

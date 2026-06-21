using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace InventoryManagementSystem.Services
{
    public class HardwareIdService
    {
        public string GetCompositeHardwareId()
        {
            var cpuId = GetCpuIdentifier();
            var diskId = GetDiskSerialNumber();
            var machineId = GetMachineIdentifier();
            var macAddress = GetFirstMacAddress();

            var rawId = $"{cpuId}|{diskId}|{machineId}|{macAddress}";
            var normalizedId = rawId.Replace(" ", "").ToUpperInvariant();

            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedId));
                var hex = Convert.ToHexString(hashBytes);
                // Return truncated 24 chars for readability
                return hex.Substring(0, 24);
            }
        }

        private string GetCpuIdentifier()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try { return File.ReadAllText("/proc/cpuinfo").Split('\n').FirstOrDefault(l => l.Contains("Serial"))?.Split(':')[1].Trim() ?? Environment.ProcessorCount.ToString(); }
                catch { return Environment.ProcessorCount.ToString(); }
            }
            return Environment.ProcessorCount.ToString();
        }

        private string GetWindowsDiskSerial()
        {
            if (!OperatingSystem.IsWindows()) return "NOT-WINDOWS";
            try
            {
                using (var searcher = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia WHERE Tag='Disk'"))
                {
                    foreach (System.Management.ManagementObject disk in searcher.Get())
                    {
                        var serial = disk["SerialNumber"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(serial)) return serial;
                    }
                }
            }
            catch { }
            return "UNKNOWN-WINDOWS-DISK";
        }

        private string GetLinuxDiskId()
        {
            try
            {
                // Try to get serial from /dev/disk/by-id/ (requires no root usually for reading some links)
                var diskPath = "/dev/disk/by-id";
                if (Directory.Exists(diskPath))
                {
                    // FIX: Sort to ensure deterministic selection
                    var disks = Directory.GetFiles(diskPath)
                        .Where(d => !d.Contains("-part"))
                        .OrderBy(d => d) 
                        .ToList();

                    // Prefer real hardware drives over virtual/usb if possible (optional heuristic)
                    var bestDisk = disks.FirstOrDefault(d => d.Contains("nvme") || d.Contains("ata") || d.Contains("scsi")) 
                                   ?? disks.FirstOrDefault();
                                   
                    if (bestDisk != null) return Path.GetFileName(bestDisk);
                }

                // Fallback to reading from /sys/block/
                var sysBlock = "/sys/block";
                if (Directory.Exists(sysBlock))
                {
                    var devices = Directory.GetDirectories(sysBlock).OrderBy(d => d).ToList();
                    foreach (var dev in devices)
                    {
                        var serialPath = Path.Combine(dev, "device/serial");
                        if (File.Exists(serialPath))
                        {
                            var serial = File.ReadAllText(serialPath).Trim();
                            if (!string.IsNullOrEmpty(serial)) return serial;
                        }
                    }
                }
            }
            catch { }
            return "LINUX-DISK-SERIAL-UNKNOWN";
        }
        private string GetMacDiskUuid()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ioreg",
                        Arguments = "-rd1 -c IOPlatformExpertDevice",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var match = Regex.Match(output, "\"IOPlatformUUID\" = \"(.+?)\"");
                if (match.Success)
                    return match.Groups[1].Value;
            }
            catch { }

            return "MAC-DISK-UNKNOWN";
        }

        private string GetDiskSerialNumber()
        {
            if (OperatingSystem.IsWindows())
                return GetWindowsDiskSerial();

            if (OperatingSystem.IsLinux())
                return GetLinuxDiskId();

            if (OperatingSystem.IsMacOS())
                return GetMacDiskUuid();

            return "UNKNOWN-DISK";
        }


        private string GetMachineIdentifier()
        {
            // LINUX: /etc/machine-id is the standard unique ID generated at install time.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try { return File.ReadAllText("/etc/machine-id").Trim(); }
                catch { try { return File.ReadAllText("/var/lib/dbus/machine-id").Trim(); } catch { } }
            }
            
            // WINDOWS: Use Win32_ComputerSystemProduct UUID (BIOS UUID)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    using (var searcher = new System.Management.ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                    {
                        foreach (System.Management.ManagementObject product in searcher.Get())
                        {
                            var uuid = product["UUID"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(uuid)) return uuid;
                        }
                    }
                }
                catch { }
            }

            return Environment.MachineName; // Fallback (least stable, changes with hostname)
        }

        private string GetFirstMacAddress()
        {
            try
            {
                // Filter only Ethernet/WiFi
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet || nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .ToList();

                // Advanced filtering to exclude virtual interfaces (Docker, VMs, VPNs)
                var physicalNics = nics.Where(nic => 
                {
                    var name = nic.Name.ToLowerInvariant();
                    var desc = nic.Description.ToLowerInvariant();

                    // Check common virtual naming conventions across platforms
                    if (name.Contains("docker") || name.Contains("veth") || name.Contains("br-") || name.Contains("bridge") ||
                        name.Contains("vbox") || name.Contains("virtual") || name.Contains("vmnet") || name.Contains("vboxnet") ||
                        name.Contains("vpn") || name.Contains("tun") || name.Contains("tap") || name.Contains("tailscale") ||
                        name.Contains("zerotier") || name.Contains("ppp") || name.Contains("lo") || name.Contains("wsl") ||
                        name.Contains("hyper-v") || name.Contains("microsoft wifi direct") || name.Contains("pseudo") || 
                        name.Contains("loopback") || name.Contains("wireguard") || name.Contains("wg") || name.StartsWith("br"))
                    {
                        return false;
                    }

                    if (desc.Contains("docker") || desc.Contains("virtual") || desc.Contains("vbox") || desc.Contains("vmware") ||
                        desc.Contains("hyper-v") || desc.Contains("vpn") || desc.Contains("tunnel") || desc.Contains("pseudo") ||
                        desc.Contains("loopback") || desc.Contains("microsoft wifi direct") || desc.Contains("bridge") ||
                        desc.Contains("tap") || desc.Contains("tailscale"))
                    {
                        return false;
                    }

                    // Linux-specific check: physical interfaces have a 'device' link in sysfs
                    if (OperatingSystem.IsLinux())
                    {
                        var sysDevicePath = $"/sys/class/net/{nic.Name}/device";
                        if (!Directory.Exists(sysDevicePath) && !File.Exists(sysDevicePath))
                        {
                            return false; // No physical hardware backing this interface
                        }
                    }

                    return true;
                })
                .OrderByDescending(nic => nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) // Ethernet first
                .ThenBy(nic => nic.Name) // Then alphabetically by name
                .ToList();

                foreach (var nic in physicalNics)
                {
                    var addr = nic.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(addr) && addr.Length > 0 && addr != "000000000000") 
                        return addr;
                }
                
                // Fallback to first non-empty MAC if physical list is empty
                foreach (var nic in nics)
                {
                    var addr = nic.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(addr) && addr.Length > 0 && addr != "000000000000") 
                        return addr;
                }
                
                return "000000000000";
            }
            catch { return "000000000000"; }
        }
    }
}

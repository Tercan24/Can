using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Web.Script.Serialization;

namespace TercanOptimizer
{
    internal sealed class CleanupTarget
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Recommended { get; set; }
        public List<string> Roots { get; set; }

        public CleanupTarget()
        {
            Roots = new List<string>();
        }
    }

    internal sealed class CleanupScan
    {
        public CleanupTarget Target { get; set; }
        public long Bytes { get; set; }
        public int FileCount { get; set; }
        public int InaccessibleCount { get; set; }
    }

    internal sealed class CleanupScanProgress
    {
        public string CategoryName { get; set; }
        public string FilePath { get; set; }
        public int FileCount { get; set; }
        public long Bytes { get; set; }
    }

    internal sealed class CleanupResult
    {
        public long ReleasedBytes { get; set; }
        public int DeletedFiles { get; set; }
        public int SkippedFiles { get; set; }
    }

    internal sealed class CleanupProgress
    {
        public string CategoryName { get; set; }
        public string FilePath { get; set; }
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public int DeletedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public long ReleasedBytes { get; set; }
    }

    internal static class SafeCleanupEngine
    {
        public static List<CleanupTarget> BuildCatalog()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string temp = Path.GetTempPath();

            List<CleanupTarget> targets = new List<CleanupTarget>();
            targets.Add(Target(
                "user-temp",
                "Kullanıcı geçici dosyaları",
                "Uygulamaların kullanıcı TEMP klasöründe bıraktığı geçici dosyalar. Kullanımdaki dosyalar atlanır.",
                true,
                temp));
            targets.Add(Target(
                "directx-cache",
                "DirectX gölgelendirici önbelleği",
                "Ekran sürücüsünün yeniden oluşturabildiği DirectX shader önbelleği. İlk oyun açılışı kısa süre yavaşlayabilir.",
                true,
                Path.Combine(local, "D3DSCache")));
            targets.Add(Target(
                "crash-dumps",
                "Uygulama çökme dökümleri",
                "Daha önce çöken uygulamaların hata ayıklama dosyaları. Aktif uygulama verilerine dokunmaz.",
                true,
                Path.Combine(local, "CrashDumps")));
            targets.Add(Target(
                "windows-temp",
                "Windows geçici dosyaları",
                "Windows TEMP klasöründeki artıklar. Kilitli ve kullanımda olan öğeler otomatik atlanır.",
                true,
                Path.Combine(windows, "Temp")));
            targets.Add(Target(
                "error-reports",
                "Windows hata raporları",
                "Gönderilmiş veya sıraya alınmış Windows hata raporları. Sorun incelemesi yapıyorsanız saklayın.",
                false,
                Path.Combine(common, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(common, "Microsoft", "Windows", "WER", "ReportQueue")));

            CleanupTarget browsers = Target(
                "browser-cache",
                "Tarayıcı önbellekleri",
                "Chrome, Edge ve Firefox web önbellekleri. Açık tarayıcıların kilitlediği dosyalar atlanır; oturumlar ve parolalar silinmez.",
                false);
            AddChromiumCaches(browsers.Roots, Path.Combine(local, "Google", "Chrome", "User Data"));
            AddChromiumCaches(browsers.Roots, Path.Combine(local, "Microsoft", "Edge", "User Data"));
            AddFirefoxCaches(browsers.Roots, Path.Combine(local, "Mozilla", "Firefox", "Profiles"));
            targets.Add(browsers);
            return targets;
        }

        public static CleanupScan Scan(CleanupTarget target)
        {
            return Scan(target, null);
        }

        public static CleanupScan Scan(CleanupTarget target, Action<CleanupScanProgress> progress)
        {
            if (target == null) throw new ArgumentNullException("target");
            CleanupScan scan = new CleanupScan { Target = target };
            foreach (string root in target.Roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!IsApprovedRoot(root))
                {
                    scan.InaccessibleCount++;
                    continue;
                }
                foreach (string file in EnumerateFiles(root, delegate { scan.InaccessibleCount++; }))
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        scan.Bytes += Math.Max(0, info.Length);
                        scan.FileCount++;
                        if (progress != null)
                        {
                            progress(new CleanupScanProgress
                            {
                                CategoryName = target.Name,
                                FilePath = file,
                                FileCount = scan.FileCount,
                                Bytes = scan.Bytes
                            });
                        }
                    }
                    catch
                    {
                        scan.InaccessibleCount++;
                    }
                }
            }
            return scan;
        }

        public static CleanupResult Clean(IEnumerable<CleanupScan> scans)
        {
            return Clean(scans, null);
        }

        public static CleanupResult Clean(IEnumerable<CleanupScan> scans, Action<CleanupProgress> progress)
        {
            CleanupResult result = new CleanupResult();
            List<CleanupScan> selectedScans = (scans ?? Enumerable.Empty<CleanupScan>())
                .Where(x => x != null && x.Target != null)
                .ToList();
            int totalFiles = Math.Max(1, selectedScans.Sum(x => Math.Max(0, x.FileCount)));
            int processedFiles = 0;

            foreach (CleanupScan scan in selectedScans)
            {
                foreach (string root in scan.Target.Roots.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!IsApprovedRoot(root)) continue;
                    List<string> files = EnumerateFiles(root, delegate { result.SkippedFiles++; }).ToList();
                    foreach (string file in files)
                    {
                        try
                        {
                            long length = 0;
                            try { length = new FileInfo(file).Length; } catch { }
                            File.Delete(file);
                            result.ReleasedBytes += Math.Max(0, length);
                            result.DeletedFiles++;
                        }
                        catch
                        {
                            result.SkippedFiles++;
                        }
                        processedFiles++;
                        if (progress != null)
                        {
                            progress(new CleanupProgress
                            {
                                CategoryName = scan.Target.Name,
                                FilePath = file,
                                ProcessedFiles = processedFiles,
                                TotalFiles = totalFiles,
                                DeletedFiles = result.DeletedFiles,
                                SkippedFiles = result.SkippedFiles,
                                ReleasedBytes = result.ReleasedBytes
                            });
                        }
                    }
                    RemoveEmptyChildren(root);
                }
            }
            Logger.Info("Güvenli temizlik tamamlandı. Dosya=" + result.DeletedFiles + ", Atlanan=" + result.SkippedFiles);
            return result;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024L * 1024L) return (bytes / 1024d).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024L * 1024L) return (bytes / 1024d / 1024d).ToString("0.0") + " MB";
            return (bytes / 1024d / 1024d / 1024d).ToString("0.00") + " GB";
        }

        private static CleanupTarget Target(string id, string name, string description, bool recommended, params string[] roots)
        {
            CleanupTarget target = new CleanupTarget
            {
                Id = id,
                Name = name,
                Description = description,
                Recommended = recommended
            };
            target.Roots.AddRange((roots ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)));
            return target;
        }

        private static void AddChromiumCaches(List<string> roots, string userData)
        {
            if (!Directory.Exists(userData)) return;
            try
            {
                foreach (string profile in Directory.GetDirectories(userData))
                {
                    string name = Path.GetFileName(profile);
                    if (!string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) &&
                        !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "Guest Profile", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    roots.Add(Path.Combine(profile, "Cache", "Cache_Data"));
                    roots.Add(Path.Combine(profile, "Code Cache"));
                    roots.Add(Path.Combine(profile, "GPUCache"));
                }
            }
            catch
            {
            }
        }

        private static void AddFirefoxCaches(List<string> roots, string profiles)
        {
            if (!Directory.Exists(profiles)) return;
            try
            {
                foreach (string profile in Directory.GetDirectories(profiles))
                {
                    roots.Add(Path.Combine(profile, "cache2"));
                }
            }
            catch
            {
            }
        }

        private static bool IsApprovedRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string drive = Path.GetPathRoot(full);
                if (string.IsNullOrWhiteSpace(drive) ||
                    string.Equals(full, drive.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string local = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string userTemp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
                string windowsTemp = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")).TrimEnd(Path.DirectorySeparatorChar);
                string wer = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Windows", "WER")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                return string.Equals(full, userTemp, StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(full, windowsTemp, StringComparison.OrdinalIgnoreCase) ||
                       full.StartsWith(local, StringComparison.OrdinalIgnoreCase) ||
                       full.StartsWith(wer, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> EnumerateFiles(string root, Action inaccessible)
        {
            if (!Directory.Exists(root)) yield break;
            Stack<string> pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] files;
                try { files = Directory.GetFiles(current); }
                catch
                {
                    inaccessible();
                    continue;
                }
                foreach (string file in files) yield return file;

                string[] directories;
                try { directories = Directory.GetDirectories(current); }
                catch
                {
                    inaccessible();
                    continue;
                }
                foreach (string directory in directories)
                {
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(directory);
                        if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        pending.Push(directory);
                    }
                    catch
                    {
                        inaccessible();
                    }
                }
            }
        }

        private static void RemoveEmptyChildren(string root)
        {
            if (!Directory.Exists(root)) return;
            try
            {
                List<string> directories = new List<string>();
                Stack<string> pending = new Stack<string>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    string current = pending.Pop();
                    string[] children;
                    try { children = Directory.GetDirectories(current); }
                    catch { continue; }
                    foreach (string child in children)
                    {
                        try
                        {
                            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                            directories.Add(child);
                            pending.Push(child);
                        }
                        catch
                        {
                        }
                    }
                }
                directories = directories.OrderByDescending(x => x.Length).ToList();
                foreach (string directory in directories)
                {
                    try
                    {
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                        if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory, false);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }

    internal sealed class StartupRecord
    {
        public string Identity { get; set; }
        public string Hive { get; set; }
        public string Path { get; set; }
        public int View { get; set; }
        public string Name { get; set; }
        public string Command { get; set; }
        public int Kind { get; set; }
        public bool Enabled { get; set; }
        public bool Protected { get; set; }
    }

    internal sealed class StartupBackupDocument
    {
        public int Version { get; set; }
        public List<StartupRecord> Disabled { get; set; }

        public StartupBackupDocument()
        {
            Version = 1;
            Disabled = new List<StartupRecord>();
        }
    }

    internal static class StartupManager
    {
        private sealed class Location
        {
            public RegistryHive Hive;
            public RegistryView View;
            public string Path;
            public string HiveLabel;
        }

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static List<StartupRecord> ReadAll()
        {
            Dictionary<string, StartupRecord> result = new Dictionary<string, StartupRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (Location location in Locations())
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(location.Hive, location.View))
                    using (RegistryKey key = root.OpenSubKey(location.Path, false))
                    {
                        if (key == null) continue;
                        foreach (string name in key.GetValueNames())
                        {
                            object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            if (value == null) continue;
                            StartupRecord record = CreateRecord(location, name, Convert.ToString(value), (int)key.GetValueKind(name), true);
                            record.Protected = IsProtected(record);
                            result[record.Identity] = record;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Başlangıç girdileri okunamadı: " + location.Path, ex);
                }
            }

            foreach (StartupRecord disabled in Load().Disabled)
            {
                if (disabled != null && !result.ContainsKey(disabled.Identity))
                {
                    disabled.Enabled = false;
                    disabled.Protected = IsProtected(disabled);
                    result[disabled.Identity] = disabled;
                }
            }
            return result.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static void SetEnabled(StartupRecord record, bool enabled)
        {
            if (record == null) throw new ArgumentNullException("record");
            if (!enabled && IsProtected(record))
            {
                throw new InvalidOperationException("Bu başlangıç girdisi güvenlik veya tek seferlik kurulum işlevi nedeniyle korunuyor.");
            }
            StartupBackupDocument document = Load();
            StartupRecord saved = document.Disabled.FirstOrDefault(
                x => string.Equals(x.Identity, record.Identity, StringComparison.OrdinalIgnoreCase));

            RegistryHive hive = string.Equals(record.Hive, "HKCU", StringComparison.OrdinalIgnoreCase)
                ? RegistryHive.CurrentUser
                : RegistryHive.LocalMachine;
            RegistryView view = (RegistryView)record.View;

            if (!enabled)
            {
                if (saved == null)
                {
                    StartupRecord backup = Clone(record);
                    backup.Enabled = false;
                    document.Disabled.Add(backup);
                    Save(document);
                }
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = root.OpenSubKey(record.Path, true))
                {
                    if (key != null) key.DeleteValue(record.Name, false);
                }
                Logger.Info("Başlangıç girdisi devre dışı bırakıldı: " + record.Name);
                return;
            }

            StartupRecord source = saved ?? record;
            RegistryValueKind kind = (RegistryValueKind)source.Kind;
            if (kind != RegistryValueKind.String && kind != RegistryValueKind.ExpandString)
            {
                kind = RegistryValueKind.String;
            }
            using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey key = root.CreateSubKey(source.Path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                key.SetValue(source.Name, source.Command ?? string.Empty, kind);
            }
            document.Disabled.RemoveAll(x => string.Equals(x.Identity, record.Identity, StringComparison.OrdinalIgnoreCase));
            Save(document);
            Logger.Info("Başlangıç girdisi yeniden etkinleştirildi: " + record.Name);
        }

        private static StartupRecord CreateRecord(Location location, string name, string command, int kind, bool enabled)
        {
            StartupRecord record = new StartupRecord
            {
                Hive = location.HiveLabel,
                View = (int)location.View,
                Path = location.Path,
                Name = name,
                Command = command,
                Kind = kind,
                Enabled = enabled
            };
            record.Identity = record.Hive + "|" + record.View + "|" + record.Path + "|" + record.Name;
            return record;
        }

        private static StartupRecord Clone(StartupRecord source)
        {
            return new StartupRecord
            {
                Identity = source.Identity,
                Hive = source.Hive,
                Path = source.Path,
                View = source.View,
                Name = source.Name,
                Command = source.Command,
                Kind = source.Kind,
                Enabled = source.Enabled
            };
        }

        private static bool IsProtected(StartupRecord record)
        {
            if (record == null) return true;
            if ((record.Path ?? string.Empty).EndsWith(@"\RunOnce", StringComparison.OrdinalIgnoreCase)) return true;
            string combined = (record.Name ?? string.Empty) + " " + (record.Command ?? string.Empty);
            string[] protectedTerms =
            {
                "SecurityHealth", "WindowsDefender", "MSASCuiL", "Microsoft\\Windows Defender",
                "Tercan", "GameTuneUltimate", "GameTuneOptimizer"
            };
            return protectedTerms.Any(x => combined.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static List<Location> Locations()
        {
            string run = @"Software\Microsoft\Windows\CurrentVersion\Run";
            string runOnce = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
            return new List<Location>
            {
                new Location { Hive = RegistryHive.CurrentUser, View = RegistryView.Registry64, Path = run, HiveLabel = "HKCU" },
                new Location { Hive = RegistryHive.CurrentUser, View = RegistryView.Registry64, Path = runOnce, HiveLabel = "HKCU" },
                new Location { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64, Path = run, HiveLabel = "HKLM" },
                new Location { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64, Path = runOnce, HiveLabel = "HKLM" },
                new Location { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry32, Path = run, HiveLabel = "HKLM" },
                new Location { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry32, Path = runOnce, HiveLabel = "HKLM" }
            };
        }

        private static StartupBackupDocument Load()
        {
            try
            {
                if (File.Exists(AppPaths.StartupBackupFile))
                {
                    StartupBackupDocument document = Serializer.Deserialize<StartupBackupDocument>(
                        File.ReadAllText(AppPaths.StartupBackupFile, Encoding.UTF8));
                    if (document != null)
                    {
                        if (document.Disabled == null) document.Disabled = new List<StartupRecord>();
                        return document;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Başlangıç yedeği okunamadı", ex);
            }
            return new StartupBackupDocument();
        }

        private static void Save(StartupBackupDocument document)
        {
            AppPaths.Ensure();
            string temp = AppPaths.StartupBackupFile + ".tmp";
            File.WriteAllText(temp, Serializer.Serialize(document), Encoding.UTF8);
            if (File.Exists(AppPaths.StartupBackupFile)) File.Replace(temp, AppPaths.StartupBackupFile, null);
            else File.Move(temp, AppPaths.StartupBackupFile);
        }
    }

    internal sealed class NetworkAdapterSnapshot
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public List<string> Addresses { get; set; }
        public List<string> Gateways { get; set; }
        public List<string> DnsServers { get; set; }

        public NetworkAdapterSnapshot()
        {
            Addresses = new List<string>();
            Gateways = new List<string>();
            DnsServers = new List<string>();
        }

        public override string ToString()
        {
            return Name + "  •  " + Status;
        }
    }

    internal sealed class DnsPreset
    {
        public string Name { get; set; }
        public string Primary { get; set; }
        public string Secondary { get; set; }
        public string Note { get; set; }

        public override string ToString()
        {
            return Name + "  (" + Primary + ")";
        }
    }

    internal sealed class DnsBackupRecord
    {
        public string InterfaceId { get; set; }
        public string InterfaceName { get; set; }
        public bool UseDhcp { get; set; }
        public List<string> Servers { get; set; }

        public DnsBackupRecord()
        {
            Servers = new List<string>();
        }
    }

    internal sealed class DnsBackupDocument
    {
        public int Version { get; set; }
        public List<DnsBackupRecord> Records { get; set; }

        public DnsBackupDocument()
        {
            Version = 1;
            Records = new List<DnsBackupRecord>();
        }
    }

    internal static class NetworkTools
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static List<NetworkAdapterSnapshot> ReadAdapters()
        {
            List<NetworkAdapterSnapshot> result = new List<NetworkAdapterSnapshot>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }
                NetworkAdapterSnapshot snapshot = new NetworkAdapterSnapshot
                {
                    Id = adapter.Id,
                    Name = adapter.Name,
                    Description = adapter.Description,
                    Status = adapter.OperationalStatus == OperationalStatus.Up ? "Bağlı" : "Bağlı değil"
                };
                try
                {
                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    snapshot.Addresses.AddRange(properties.UnicastAddresses
                        .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(x => x.Address.ToString()));
                    snapshot.Gateways.AddRange(properties.GatewayAddresses
                        .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(x => x.Address.ToString()));
                    snapshot.DnsServers.AddRange(properties.DnsAddresses
                        .Where(x => x.AddressFamily == AddressFamily.InterNetwork)
                        .Select(x => x.ToString()));
                }
                catch
                {
                }
                result.Add(snapshot);
            }
            return result
                .OrderByDescending(x => x.Status == "Bağlı")
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static List<DnsPreset> Presets()
        {
            return new List<DnsPreset>
            {
                new DnsPreset { Name = "Cloudflare", Primary = "1.1.1.1", Secondary = "1.0.0.1", Note = "Hız ve gizlilik odaklı yaygın genel DNS." },
                new DnsPreset { Name = "Google Public DNS", Primary = "8.8.8.8", Secondary = "8.8.4.4", Note = "Yüksek erişilebilirlik sunan genel DNS." },
                new DnsPreset { Name = "Quad9", Primary = "9.9.9.9", Secondary = "149.112.112.112", Note = "Bilinen zararlı alan adlarını engellemeye odaklı DNS." }
            };
        }

        public static string PingHost(string host)
        {
            host = (host ?? string.Empty).Trim();
            if (host.Length == 0 || host.Length > 253) throw new InvalidDataException("Geçerli bir sunucu adı veya IP girin.");
            using (Ping ping = new Ping())
            {
                PingReply reply = ping.Send(host, 4000);
                if (reply == null) return "Yanıt alınamadı.";
                if (reply.Status != IPStatus.Success) return "Sonuç: " + reply.Status;
                return "Yanıt: " + reply.RoundtripTime + " ms  •  IP: " + reply.Address;
            }
        }

        public static void SetDns(NetworkAdapterSnapshot adapter, DnsPreset preset)
        {
            if (adapter == null || preset == null) throw new ArgumentNullException();
            IPAddress parsed;
            if (!IPAddress.TryParse(preset.Primary, out parsed) || !IPAddress.TryParse(preset.Secondary, out parsed))
            {
                throw new InvalidDataException("DNS adresleri geçersiz.");
            }
            ValidateInterfaceName(adapter.Name);

            DnsBackupDocument document = LoadBackups();
            if (!document.Records.Any(x => SameId(x.InterfaceId, adapter.Id)))
            {
                document.Records.Add(CaptureDns(adapter));
                SaveBackups(document);
            }

            ProcessResult first = ProcessRunner.Run(
                "netsh.exe",
                "interface ipv4 set dnsservers name=\"" + adapter.Name + "\" source=static address=" +
                preset.Primary + " validate=no",
                30000);
            if (first.ExitCode != 0) throw new InvalidOperationException(CombineOutput(first));

            ProcessResult second = ProcessRunner.Run(
                "netsh.exe",
                "interface ipv4 add dnsservers name=\"" + adapter.Name + "\" address=" +
                preset.Secondary + " index=2 validate=no",
                30000);
            if (second.ExitCode != 0) throw new InvalidOperationException(CombineOutput(second));
            FlushDns();
            Logger.Info("DNS değiştirildi: " + adapter.Name + " -> " + preset.Name);
        }

        public static bool HasBackup(NetworkAdapterSnapshot adapter)
        {
            return adapter != null && LoadBackups().Records.Any(x => SameId(x.InterfaceId, adapter.Id));
        }

        public static void RestoreDns(NetworkAdapterSnapshot adapter)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            ValidateInterfaceName(adapter.Name);
            DnsBackupDocument document = LoadBackups();
            DnsBackupRecord backup = document.Records.FirstOrDefault(x => SameId(x.InterfaceId, adapter.Id));
            if (backup == null) throw new InvalidOperationException("Bu bağdaştırıcı için Tercan DNS yedeği yok.");

            ProcessResult result;
            if (backup.UseDhcp || backup.Servers.Count == 0)
            {
                result = ProcessRunner.Run(
                    "netsh.exe",
                    "interface ipv4 set dnsservers name=\"" + adapter.Name + "\" source=dhcp",
                    30000);
            }
            else
            {
                result = ProcessRunner.Run(
                    "netsh.exe",
                    "interface ipv4 set dnsservers name=\"" + adapter.Name + "\" source=static address=" +
                    backup.Servers[0] + " validate=no",
                    30000);
                if (result.ExitCode == 0)
                {
                    for (int i = 1; i < backup.Servers.Count; i++)
                    {
                        ProcessResult additional = ProcessRunner.Run(
                            "netsh.exe",
                            "interface ipv4 add dnsservers name=\"" + adapter.Name + "\" address=" +
                            backup.Servers[i] + " index=" + (i + 1) + " validate=no",
                            30000);
                        if (additional.ExitCode != 0)
                        {
                            throw new InvalidOperationException(CombineOutput(additional));
                        }
                    }
                }
            }
            if (result.ExitCode != 0) throw new InvalidOperationException(CombineOutput(result));
            document.Records.RemoveAll(x => SameId(x.InterfaceId, adapter.Id));
            SaveBackups(document);
            FlushDns();
            Logger.Info("DNS yedeği geri yüklendi: " + adapter.Name);
        }

        public static ProcessResult FlushDns()
        {
            return ProcessRunner.Run("ipconfig.exe", "/flushdns", 30000);
        }

        private static DnsBackupRecord CaptureDns(NetworkAdapterSnapshot adapter)
        {
            DnsBackupRecord result = new DnsBackupRecord
            {
                InterfaceId = adapter.Id,
                InterfaceName = adapter.Name,
                UseDhcp = false
            };
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT SettingID, DNSServerSearchOrder FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=TRUE"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        if (!SameId(Convert.ToString(item["SettingID"]), adapter.Id)) continue;
                        string[] servers = item["DNSServerSearchOrder"] as string[];
                        result.UseDhcp = UsesDhcpDns(adapter.Id);
                        if (servers != null) result.Servers.AddRange(servers.Where(IsIpAddress));
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("DNS yedeği WMI üzerinden alınamadı", ex);
            }
            result.Servers.AddRange(adapter.DnsServers.Where(IsIpAddress));
            result.UseDhcp = UsesDhcpDns(adapter.Id);
            return result;
        }

        private static bool UsesDhcpDns(string interfaceId)
        {
            try
            {
                string normalized = (interfaceId ?? string.Empty).Trim().Trim('{', '}');
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{" + normalized + "}", false))
                {
                    if (key == null) return true;
                    string staticServers = Convert.ToString(
                        key.GetValue("NameServer", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames));
                    return string.IsNullOrWhiteSpace(staticServers);
                }
            }
            catch
            {
                return true;
            }
        }

        private static bool IsIpAddress(string value)
        {
            IPAddress parsed;
            return IPAddress.TryParse(value, out parsed);
        }

        private static bool SameId(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim().Trim('{', '}'),
                (right ?? string.Empty).Trim().Trim('{', '}'),
                StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateInterfaceName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
            {
                throw new InvalidDataException("Ağ bağdaştırıcısı adı geçersiz.");
            }
        }

        private static string CombineOutput(ProcessResult result)
        {
            string text = ((result.Error ?? string.Empty) + Environment.NewLine + (result.Output ?? string.Empty)).Trim();
            return string.IsNullOrWhiteSpace(text) ? "Ağ komutu tamamlanamadı. Kod: " + result.ExitCode : text;
        }

        private static DnsBackupDocument LoadBackups()
        {
            try
            {
                if (File.Exists(AppPaths.NetworkBackupFile))
                {
                    DnsBackupDocument document = Serializer.Deserialize<DnsBackupDocument>(
                        File.ReadAllText(AppPaths.NetworkBackupFile, Encoding.UTF8));
                    if (document != null)
                    {
                        if (document.Records == null) document.Records = new List<DnsBackupRecord>();
                        foreach (DnsBackupRecord record in document.Records)
                        {
                            if (record.Servers == null) record.Servers = new List<string>();
                        }
                        return document;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("DNS yedeği okunamadı", ex);
            }
            return new DnsBackupDocument();
        }

        private static void SaveBackups(DnsBackupDocument document)
        {
            AppPaths.Ensure();
            string temp = AppPaths.NetworkBackupFile + ".tmp";
            File.WriteAllText(temp, Serializer.Serialize(document), Encoding.UTF8);
            if (File.Exists(AppPaths.NetworkBackupFile)) File.Replace(temp, AppPaths.NetworkBackupFile, null);
            else File.Move(temp, AppPaths.NetworkBackupFile);
        }
    }

    internal static class HardwareReport
    {
        public static string Build()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("TERCAN.EXE - DONANIM VE SİSTEM RAPORU");
            report.AppendLine("Oluşturma: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine(new string('=', 72));
            AppendQuery(report, "İŞLETİM SİSTEMİ",
                "SELECT Caption, Version, BuildNumber, OSArchitecture, LastBootUpTime FROM Win32_OperatingSystem",
                "Caption", "Version", "BuildNumber", "OSArchitecture", "LastBootUpTime");
            AppendQuery(report, "İŞLEMCİ",
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor",
                "Name", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed");
            AppendQuery(report, "EKRAN KARTI",
                "SELECT Name, DriverVersion, AdapterRAM, VideoModeDescription FROM Win32_VideoController",
                "Name", "DriverVersion", "AdapterRAM", "VideoModeDescription");
            AppendQuery(report, "ANAKART",
                "SELECT Manufacturer, Product, Version FROM Win32_BaseBoard",
                "Manufacturer", "Product", "Version");
            AppendQuery(report, "BIOS",
                "SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS",
                "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate");
            AppendQuery(report, "BELLEK MODÜLLERİ",
                "SELECT Manufacturer, Capacity, Speed, PartNumber FROM Win32_PhysicalMemory",
                "Manufacturer", "Capacity", "Speed", "PartNumber");
            AppendQuery(report, "FİZİKSEL DİSKLER",
                "SELECT Model, InterfaceType, Size, MediaType, Status FROM Win32_DiskDrive",
                "Model", "InterfaceType", "Size", "MediaType", "Status");
            AppendQuery(report, "SÜRÜCÜLER",
                "SELECT DeviceID, FileSystem, Size, FreeSpace, VolumeName FROM Win32_LogicalDisk WHERE DriveType=3",
                "DeviceID", "FileSystem", "Size", "FreeSpace", "VolumeName");

            report.AppendLine();
            report.AppendLine("[AĞ BAĞDAŞTIRICILARI]");
            foreach (NetworkAdapterSnapshot adapter in NetworkTools.ReadAdapters())
            {
                report.AppendLine("- " + adapter.Name + " | " + adapter.Status);
                report.AppendLine("  IP: " + JoinOrDash(adapter.Addresses));
                report.AppendLine("  DNS: " + JoinOrDash(adapter.DnsServers));
                report.AppendLine("  Ağ geçidi: " + JoinOrDash(adapter.Gateways));
            }
            return report.ToString();
        }

        private static void AppendQuery(StringBuilder report, string title, string query, params string[] properties)
        {
            report.AppendLine();
            report.AppendLine("[" + title + "]");
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    int index = 0;
                    foreach (ManagementObject item in searcher.Get())
                    {
                        index++;
                        if (index > 1) report.AppendLine("--");
                        foreach (string property in properties)
                        {
                            object value = item[property];
                            report.AppendLine(property + ": " + FormatValue(property, value));
                        }
                    }
                    if (index == 0) report.AppendLine("Bilgi bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("Okunamadı: " + ex.Message);
            }
        }

        private static string FormatValue(string property, object value)
        {
            if (value == null) return "-";
            string text = Convert.ToString(value).Trim();
            ulong bytes;
            if ((property == "Capacity" || property == "Size" || property == "FreeSpace" || property == "AdapterRAM") &&
                ulong.TryParse(text, out bytes))
            {
                return (bytes / 1024d / 1024d / 1024d).ToString("0.00") + " GB";
            }
            if (property == "LastBootUpTime" || property == "ReleaseDate")
            {
                try { return ManagementDateTimeConverter.ToDateTime(text).ToString("yyyy-MM-dd HH:mm"); }
                catch { }
            }
            if (property == "MaxClockSpeed" && text.Length > 0) return text + " MHz";
            if (property == "Speed" && text.Length > 0) return text + " MT/s";
            return string.IsNullOrWhiteSpace(text) ? "-" : text;
        }

        private static string JoinOrDash(IEnumerable<string> values)
        {
            string result = string.Join(", ", values ?? Enumerable.Empty<string>());
            return string.IsNullOrWhiteSpace(result) ? "-" : result;
        }
    }

    internal static class HostsManager
    {
        public static string HostsPath
        {
            get { return Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts"); }
        }

        public static string Read()
        {
            if (!File.Exists(HostsPath)) return string.Empty;
            return File.ReadAllText(HostsPath, Encoding.UTF8);
        }

        public static void Save(string content)
        {
            Validate(content);
            AppPaths.Ensure();
            if (File.Exists(HostsPath))
            {
                string backup = Path.Combine(
                    AppPaths.HostsBackupFolder,
                    "hosts-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".bak");
                File.Copy(HostsPath, backup, false);
            }
            File.WriteAllText(HostsPath, Normalize(content), new UTF8Encoding(false));
            Logger.Info("HOSTS dosyası yedeklenerek kaydedildi.");
        }

        public static bool HasBackup()
        {
            return Directory.Exists(AppPaths.HostsBackupFolder) &&
                   Directory.GetFiles(AppPaths.HostsBackupFolder, "hosts-*.bak").Length > 0;
        }

        public static string RestoreLatest()
        {
            AppPaths.Ensure();
            string latest = Directory.GetFiles(AppPaths.HostsBackupFolder, "hosts-*.bak")
                .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (latest == null) throw new FileNotFoundException("Geri yüklenecek HOSTS yedeği yok.");
            File.Copy(latest, HostsPath, true);
            Logger.Info("HOSTS dosyası yedekten geri yüklendi: " + Path.GetFileName(latest));
            return Read();
        }

        public static void Validate(string content)
        {
            content = content ?? string.Empty;
            if (Encoding.UTF8.GetByteCount(content) > 1024 * 1024)
            {
                throw new InvalidDataException("HOSTS dosyası 1 MB sınırını aşamaz.");
            }
            string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int comment = line.IndexOf('#');
                if (comment >= 0) line = line.Substring(0, comment).Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                IPAddress address;
                if (parts.Length < 2 || !IPAddress.TryParse(parts[0], out address))
                {
                    throw new InvalidDataException((i + 1) + ". satır geçersiz. Örnek: 0.0.0.0 alanadi.example");
                }
                for (int p = 1; p < parts.Length; p++)
                {
                    if (!IsValidHost(parts[p]))
                    {
                        throw new InvalidDataException((i + 1) + ". satırda geçersiz alan adı: " + parts[p]);
                    }
                }
            }
        }

        private static bool IsValidHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Length > 253) return false;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
            string[] labels = host.TrimEnd('.').Split('.');
            foreach (string label in labels)
            {
                if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-') return false;
                foreach (char c in label)
                {
                    if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_')) return false;
                }
            }
            return true;
        }

        private static string Normalize(string content)
        {
            return (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", Environment.NewLine);
        }
    }
}

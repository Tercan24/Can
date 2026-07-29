using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace TercanOptimizer
{
    internal enum RiskLevel
    {
        Safe,
        Caution,
        Experimental
    }

    internal enum ImpactLevel
    {
        Low,
        Medium,
        High
    }

    internal enum SpecialTweakKind
    {
        None,
        HighPerformancePower,
        MemoryCompressionOff
    }

    internal sealed class RegistryMutation
    {
        public string Hive { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public object AppliedValue { get; set; }
        public RegistryValueKind Kind { get; set; }
        public bool DeleteWhenApplied { get; set; }
    }

    internal sealed class ServiceMutation
    {
        public string ServiceName { get; set; }
        public int AppliedStartValue { get; set; }
        public bool StopWhenApplied { get; set; }
    }

    internal sealed class TweakDefinition
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
        public string Category { get; set; }
        public string SourceLabel { get; set; }
        public string SourceUrl { get; set; }
        public string Compatibility { get; set; }
        public RiskLevel Risk { get; set; }
        public ImpactLevel Impact { get; set; }
        public bool Recommended { get; set; }
        public bool RequiresRestart { get; set; }
        public List<RegistryMutation> RegistryChanges { get; set; }
        public List<ServiceMutation> ServiceChanges { get; set; }
        public SpecialTweakKind SpecialKind { get; set; }

        public TweakDefinition()
        {
            RegistryChanges = new List<RegistryMutation>();
            ServiceChanges = new List<ServiceMutation>();
            Compatibility = "Windows 10 / 11";
        }
    }

    internal sealed class BackupDocument
    {
        public int Version { get; set; }
        public Dictionary<string, TweakBackup> Tweaks { get; set; }

        public BackupDocument()
        {
            Version = 1;
            Tweaks = new Dictionary<string, TweakBackup>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal sealed class TweakBackup
    {
        public string CapturedAt { get; set; }
        public List<RegistrySnapshot> RegistryValues { get; set; }
        public Dictionary<string, string> SpecialValues { get; set; }

        public TweakBackup()
        {
            RegistryValues = new List<RegistrySnapshot>();
            SpecialValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal sealed class RegistrySnapshot
    {
        public string Hive { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public bool Exists { get; set; }
        public int Kind { get; set; }
        public string Data { get; set; }

        public string Identity
        {
            get { return Hive + "\\" + Path + "::" + Name; }
        }
    }

    internal sealed class BackupStore
    {
        private readonly object sync = new object();
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        public BackupDocument Document { get; private set; }

        public BackupStore()
        {
            AppPaths.Ensure();
            Load();
        }

        private void Load()
        {
            lock (sync)
            {
                try
                {
                    if (File.Exists(AppPaths.BackupFile))
                    {
                        Document = serializer.Deserialize<BackupDocument>(File.ReadAllText(AppPaths.BackupFile, Encoding.UTF8));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Yedek dosyası okunamadı", ex);
                }

                if (Document == null)
                {
                    Document = new BackupDocument();
                }

                if (Document.Tweaks == null)
                {
                    Document.Tweaks = new Dictionary<string, TweakBackup>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        public TweakBackup GetOrCreate(string tweakId)
        {
            lock (sync)
            {
                TweakBackup backup;
                if (!Document.Tweaks.TryGetValue(tweakId, out backup))
                {
                    backup = new TweakBackup();
                    backup.CapturedAt = DateTime.Now.ToString("o");
                    Document.Tweaks[tweakId] = backup;
                }
                return backup;
            }
        }

        public bool TryGet(string tweakId, out TweakBackup backup)
        {
            lock (sync)
            {
                return Document.Tweaks.TryGetValue(tweakId, out backup);
            }
        }

        public void Remove(string tweakId)
        {
            lock (sync)
            {
                Document.Tweaks.Remove(tweakId);
                Save();
            }
        }

        public void Save()
        {
            lock (sync)
            {
                AppPaths.Ensure();
                string temp = AppPaths.BackupFile + ".tmp";
                File.WriteAllText(temp, serializer.Serialize(Document), Encoding.UTF8);
                if (File.Exists(AppPaths.BackupFile))
                {
                    File.Replace(temp, AppPaths.BackupFile, null);
                }
                else
                {
                    File.Move(temp, AppPaths.BackupFile);
                }
            }
        }
    }

    internal static class RegistryTools
    {
        public static RegistryKey OpenBase(string hive, bool writable)
        {
            string normalized = (hive ?? string.Empty).ToUpperInvariant();
            if (normalized == "HKCU" || normalized == "HKEY_CURRENT_USER")
            {
                return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            }
            if (normalized == "HKLM" || normalized == "HKEY_LOCAL_MACHINE")
            {
                return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            throw new InvalidOperationException("Desteklenmeyen kayıt defteri kökü: " + hive);
        }

        public static object Read(RegistryMutation mutation)
        {
            using (RegistryKey root = OpenBase(mutation.Hive, false))
            using (RegistryKey key = root.OpenSubKey(mutation.Path, false))
            {
                return key == null ? null : key.GetValue(mutation.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
        }

        public static bool Exists(RegistryMutation mutation)
        {
            using (RegistryKey root = OpenBase(mutation.Hive, false))
            using (RegistryKey key = root.OpenSubKey(mutation.Path, false))
            {
                if (key == null)
                {
                    return false;
                }
                return key.GetValueNames().Any(n => string.Equals(n, mutation.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static void Write(RegistryMutation mutation)
        {
            using (RegistryKey root = OpenBase(mutation.Hive, true))
            using (RegistryKey key = root.CreateSubKey(mutation.Path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (mutation.DeleteWhenApplied)
                {
                    key.DeleteValue(mutation.Name, false);
                }
                else
                {
                    key.SetValue(mutation.Name, mutation.AppliedValue, mutation.Kind);
                }
            }
        }

        public static void Delete(RegistryMutation mutation)
        {
            using (RegistryKey root = OpenBase(mutation.Hive, true))
            using (RegistryKey key = root.OpenSubKey(mutation.Path, true))
            {
                if (key != null)
                {
                    key.DeleteValue(mutation.Name, false);
                }
            }
        }

        public static RegistrySnapshot Capture(RegistryMutation mutation)
        {
            RegistrySnapshot snapshot = new RegistrySnapshot();
            snapshot.Hive = mutation.Hive;
            snapshot.Path = mutation.Path;
            snapshot.Name = mutation.Name;

            using (RegistryKey root = OpenBase(mutation.Hive, false))
            using (RegistryKey key = root.OpenSubKey(mutation.Path, false))
            {
                if (key == null || !key.GetValueNames().Any(n => string.Equals(n, mutation.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    snapshot.Exists = false;
                    snapshot.Kind = (int)mutation.Kind;
                    snapshot.Data = string.Empty;
                    return snapshot;
                }

                snapshot.Exists = true;
                RegistryValueKind kind = key.GetValueKind(mutation.Name);
                object value = key.GetValue(mutation.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                snapshot.Kind = (int)kind;
                snapshot.Data = EncodeRegistryData(value, kind);
                return snapshot;
            }
        }

        public static void Restore(RegistrySnapshot snapshot)
        {
            using (RegistryKey root = OpenBase(snapshot.Hive, true))
            using (RegistryKey key = root.CreateSubKey(snapshot.Path, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (!snapshot.Exists)
                {
                    key.DeleteValue(snapshot.Name, false);
                    return;
                }

                RegistryValueKind kind = (RegistryValueKind)snapshot.Kind;
                key.SetValue(snapshot.Name, DecodeRegistryData(snapshot.Data, kind), kind);
            }
        }

        public static bool ValueEquals(object current, object expected)
        {
            if (current == null && expected == null)
            {
                return true;
            }
            if (current == null || expected == null)
            {
                return false;
            }
            try
            {
                if (expected is int)
                {
                    return Convert.ToInt32(current) == Convert.ToInt32(expected);
                }
                if (expected is long)
                {
                    return Convert.ToInt64(current) == Convert.ToInt64(expected);
                }
                return string.Equals(Convert.ToString(current), Convert.ToString(expected), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string EncodeRegistryData(object value, RegistryValueKind kind)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (kind == RegistryValueKind.Binary)
            {
                return Convert.ToBase64String((byte[])value);
            }
            if (kind == RegistryValueKind.MultiString)
            {
                return string.Join("\u001f", (string[])value);
            }
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static object DecodeRegistryData(string data, RegistryValueKind kind)
        {
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    return int.Parse(data, System.Globalization.CultureInfo.InvariantCulture);
                case RegistryValueKind.QWord:
                    return long.Parse(data, System.Globalization.CultureInfo.InvariantCulture);
                case RegistryValueKind.Binary:
                    return Convert.FromBase64String(data);
                case RegistryValueKind.MultiString:
                    return data.Split(new[] { '\u001f' }, StringSplitOptions.None);
                default:
                    return data;
            }
        }
    }

    internal sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }

    internal static class ProcessRunner
    {
        public static ProcessResult Run(string fileName, string arguments, int timeoutMilliseconds)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = fileName;
            start.Arguments = arguments;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.StandardOutputEncoding = Encoding.UTF8;
            start.StandardErrorEncoding = Encoding.UTF8;

            using (Process process = Process.Start(start))
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    throw new System.TimeoutException(fileName + " zaman aşımına uğradı.");
                }
                process.WaitForExit();
                Task.WaitAll(new Task[] { outputTask, errorTask }, 5000);
                string output = outputTask.IsCompleted ? outputTask.Result : string.Empty;
                string error = errorTask.IsCompleted ? errorTask.Result : string.Empty;
                return new ProcessResult { ExitCode = process.ExitCode, Output = output, Error = error };
            }
        }

        public static ProcessResult Run(string fileName, string arguments)
        {
            return Run(fileName, arguments, 30000);
        }

        public static void Open(string target)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = target;
            start.UseShellExecute = true;
            Process.Start(start);
        }
    }

    internal static class Logger
    {
        private static readonly object Sync = new object();

        public static void Info(string message)
        {
            Write("BİLGİ", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("HATA", message + " | " + ex.Message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    AppPaths.Ensure();
                    File.AppendAllText(
                        AppPaths.LogFile,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        public static string[] ReadRecent(int count)
        {
            try
            {
                if (!File.Exists(AppPaths.LogFile))
                {
                    return new string[0];
                }
                return File.ReadAllLines(AppPaths.LogFile, Encoding.UTF8).Reverse().Take(count).Reverse().ToArray();
            }
            catch
            {
                return new string[0];
            }
        }
    }

    internal sealed class TweakEngine
    {
        private readonly BackupStore store;

        public TweakEngine(BackupStore store)
        {
            this.store = store;
        }

        public bool IsApplied(TweakDefinition tweak)
        {
            try
            {
                if (tweak.SpecialKind == SpecialTweakKind.HighPerformancePower)
                {
                    string guid = PowerPlanTools.GetActiveSchemeGuid();
                    return string.Equals(guid, PowerPlanTools.HighPerformanceGuid, StringComparison.OrdinalIgnoreCase);
                }

                if (tweak.SpecialKind == SpecialTweakKind.MemoryCompressionOff)
                {
                    return !MemoryCompressionTools.IsEnabled();
                }

                foreach (RegistryMutation change in tweak.RegistryChanges)
                {
                    bool exists = RegistryTools.Exists(change);
                    if (change.DeleteWhenApplied)
                    {
                        if (exists) return false;
                    }
                    else if (!exists || !RegistryTools.ValueEquals(RegistryTools.Read(change), change.AppliedValue))
                    {
                        return false;
                    }
                }

                foreach (ServiceMutation service in tweak.ServiceChanges)
                {
                    RegistryMutation startValue = ServiceStartMutation(service.ServiceName, service.AppliedStartValue);
                    if (!RegistryTools.Exists(startValue) ||
                        !RegistryTools.ValueEquals(RegistryTools.Read(startValue), service.AppliedStartValue))
                    {
                        return false;
                    }
                }

                return tweak.RegistryChanges.Count > 0 || tweak.ServiceChanges.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public void Apply(TweakDefinition tweak)
        {
            TweakBackup backup = store.GetOrCreate(tweak.Id);

            foreach (RegistryMutation change in tweak.RegistryChanges)
            {
                CaptureRegistryOnce(backup, change);
                RegistryTools.Write(change);
            }

            foreach (ServiceMutation service in tweak.ServiceChanges)
            {
                RegistryMutation startValue = ServiceStartMutation(service.ServiceName, service.AppliedStartValue);
                CaptureRegistryOnce(backup, startValue);
                string runningKey = "service:" + service.ServiceName + ":running";
                if (!backup.SpecialValues.ContainsKey(runningKey))
                {
                    backup.SpecialValues[runningKey] = ServiceTools.IsRunning(service.ServiceName) ? "1" : "0";
                }
                if (service.StopWhenApplied)
                {
                    ServiceTools.Stop(service.ServiceName);
                }
                RegistryTools.Write(startValue);
            }

            if (tweak.SpecialKind == SpecialTweakKind.HighPerformancePower)
            {
                if (!backup.SpecialValues.ContainsKey("power:active"))
                {
                    backup.SpecialValues["power:active"] = PowerPlanTools.GetActiveSchemeGuid();
                }
                PowerPlanTools.SetHighPerformance();
            }
            else if (tweak.SpecialKind == SpecialTweakKind.MemoryCompressionOff)
            {
                if (!backup.SpecialValues.ContainsKey("memory:compression"))
                {
                    backup.SpecialValues["memory:compression"] = MemoryCompressionTools.IsEnabled() ? "1" : "0";
                }
                MemoryCompressionTools.SetEnabled(false);
            }

            store.Save();
            Logger.Info("Ayar uygulandı: " + tweak.Title);
        }

        public void Revert(TweakDefinition tweak)
        {
            TweakBackup backup;
            if (!store.TryGet(tweak.Id, out backup))
            {
                DisableWithoutBackup(tweak);
                Logger.Info("Önceden etkin olan ayar Windows varsayılanına döndürüldü: " + tweak.Title);
                return;
            }

            foreach (RegistrySnapshot snapshot in backup.RegistryValues.AsEnumerable().Reverse())
            {
                RegistryTools.Restore(snapshot);
            }

            if (tweak.SpecialKind == SpecialTweakKind.HighPerformancePower)
            {
                string guid;
                if (backup.SpecialValues.TryGetValue("power:active", out guid) && !string.IsNullOrWhiteSpace(guid))
                {
                    PowerPlanTools.SetScheme(guid);
                }
            }
            else if (tweak.SpecialKind == SpecialTweakKind.MemoryCompressionOff)
            {
                string enabled;
                if (backup.SpecialValues.TryGetValue("memory:compression", out enabled))
                {
                    MemoryCompressionTools.SetEnabled(enabled == "1");
                }
            }

            foreach (ServiceMutation service in tweak.ServiceChanges)
            {
                string running;
                if (backup.SpecialValues.TryGetValue("service:" + service.ServiceName + ":running", out running) && running == "1")
                {
                    ServiceTools.Start(service.ServiceName);
                }
            }

            store.Remove(tweak.Id);
            Logger.Info("Ayar geri alındı: " + tweak.Title);
        }

        private static void DisableWithoutBackup(TweakDefinition tweak)
        {
            foreach (RegistryMutation change in tweak.RegistryChanges)
            {
                RegistryTools.Delete(change);
            }

            foreach (ServiceMutation service in tweak.ServiceChanges)
            {
                RegistryTools.Write(ServiceStartMutation(service.ServiceName, 2));
                ServiceTools.Start(service.ServiceName);
            }

            if (tweak.SpecialKind == SpecialTweakKind.HighPerformancePower)
            {
                PowerPlanTools.SetScheme(PowerPlanTools.BalancedGuid);
            }
            else if (tweak.SpecialKind == SpecialTweakKind.MemoryCompressionOff)
            {
                MemoryCompressionTools.SetEnabled(true);
            }
        }

        private static RegistryMutation ServiceStartMutation(string serviceName, int value)
        {
            return new RegistryMutation
            {
                Hive = "HKLM",
                Path = @"SYSTEM\CurrentControlSet\Services\" + serviceName,
                Name = "Start",
                AppliedValue = value,
                Kind = RegistryValueKind.DWord
            };
        }

        private static void CaptureRegistryOnce(TweakBackup backup, RegistryMutation change)
        {
            string identity = change.Hive + "\\" + change.Path + "::" + change.Name;
            if (!backup.RegistryValues.Any(x => string.Equals(x.Identity, identity, StringComparison.OrdinalIgnoreCase)))
            {
                backup.RegistryValues.Add(RegistryTools.Capture(change));
            }
        }
    }

    internal static class ServiceTools
    {
        public static bool IsRunning(string name)
        {
            try
            {
                using (ServiceController service = new ServiceController(name))
                {
                    return service.Status == ServiceControllerStatus.Running ||
                           service.Status == ServiceControllerStatus.StartPending;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void Stop(string name)
        {
            try
            {
                using (ServiceController service = new ServiceController(name))
                {
                    if (service.Status != ServiceControllerStatus.Stopped &&
                        service.Status != ServiceControllerStatus.StopPending)
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(8));
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        public static void Start(string name)
        {
            try
            {
                using (ServiceController service = new ServiceController(name))
                {
                    if (service.Status == ServiceControllerStatus.Stopped)
                    {
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(8));
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    internal static class PowerPlanTools
    {
        public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        public const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

        public static string GetActiveSchemeGuid()
        {
            ProcessResult result = ProcessRunner.Run("powercfg.exe", "/getactivescheme");
            Match match = Regex.Match(result.Output + " " + result.Error, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return match.Success ? match.Value.ToLowerInvariant() : string.Empty;
        }

        public static string GetActiveSchemeName()
        {
            string guid = GetActiveSchemeGuid();
            if (guid == BalancedGuid) return "Dengeli";
            if (guid == "a1841308-3541-4fab-bc81-f71556f20b4a") return "Güç tasarrufu";
            if (guid == HighPerformanceGuid) return "Yüksek performans";
            if (guid == "e9a42b02-d5df-448d-aa00-03f14749eb61") return "Üstün performans";

            ProcessResult result = ProcessRunner.Run("powercfg.exe", "/getactivescheme");
            Match match = Regex.Match(result.Output, @"\(([^\)]+)\)");
            return match.Success ? match.Groups[1].Value : "Bilinmiyor";
        }

        public static void SetHighPerformance()
        {
            ProcessResult result = ProcessRunner.Run("powercfg.exe", "/setactive SCHEME_MIN");
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("Yüksek performans güç planı etkinleştirilemedi: " + result.Error);
            }
        }

        public static void SetScheme(string guid)
        {
            ProcessResult result = ProcessRunner.Run("powercfg.exe", "/setactive " + guid);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("Önceki güç planı geri yüklenemedi: " + result.Error);
            }
        }
    }

    internal static class MemoryCompressionTools
    {
        public static bool IsEnabled()
        {
            ProcessResult result = ProcessRunner.Run(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"[bool](Get-MMAgent).MemoryCompression\"");
            return result.Output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        public static void SetEnabled(bool enabled)
        {
            string command = enabled
                ? "Enable-MMAgent -MemoryCompression"
                : "Disable-MMAgent -MemoryCompression";
            ProcessResult result = ProcessRunner.Run(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"");
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("Bellek sıkıştırma ayarı değiştirilemedi: " + result.Error);
            }
        }
    }

    internal static class RestorePointTools
    {
        public static ProcessResult Create(string description)
        {
            string safe = (description ?? "Tercan ayarları").Replace("'", "''");
            return ProcessRunner.Run(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description '" +
                safe + "' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop\"",
                120000);
        }
    }

    internal sealed class SystemInfoSnapshot
    {
        public string OperatingSystem { get; set; }
        public string Cpu { get; set; }
        public string Gpu { get; set; }
        public ulong TotalRamBytes { get; set; }
        public string PowerPlan { get; set; }
        public bool IsLaptop { get; set; }
    }

    internal sealed class MemorySnapshot
    {
        public long AvailableMb { get; set; }
        public long StandbyMb { get; set; }
        public long TotalMb { get; set; }
    }

    internal static class SystemProbe
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RtlOsVersionInfo
        {
            public uint Size;
            public uint Major;
            public uint Minor;
            public uint Build;
            public uint PlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ServicePack;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("ntdll.dll", CharSet = CharSet.Unicode)]
        private static extern int RtlGetVersion(ref RtlOsVersionInfo versionInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

        public static bool IsSupportedWindows()
        {
            RtlOsVersionInfo version = ReadRealWindowsVersion();
            return Environment.OSVersion.Platform == PlatformID.Win32NT && version.Major >= 10;
        }

        public static SystemInfoSnapshot Read()
        {
            SystemInfoSnapshot result = new SystemInfoSnapshot();
            result.OperatingSystem = ReadFirstProperty("SELECT Caption, Version FROM Win32_OperatingSystem", "Caption");
            result.Cpu = ReadFirstProperty("SELECT Name FROM Win32_Processor", "Name");
            result.Gpu = ReadFirstProperty("SELECT Name FROM Win32_VideoController", "Name");
            result.PowerPlan = SafePowerPlanName();

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory, PCSystemType FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        result.TotalRamBytes = Convert.ToUInt64(item["TotalPhysicalMemory"]);
                        int type = item["PCSystemType"] == null ? 0 : Convert.ToInt32(item["PCSystemType"]);
                        result.IsLaptop = type == 2;
                        break;
                    }
                }
            }
            catch
            {
            }

            MemoryStatusEx memory = ReadGlobalMemory();
            if (result.TotalRamBytes == 0)
            {
                result.TotalRamBytes = memory.TotalPhysical;
            }

            if (string.IsNullOrWhiteSpace(result.OperatingSystem))
            {
                RtlOsVersionInfo version = ReadRealWindowsVersion();
                string product = version.Build >= 22000 ? "Windows 11" : "Windows 10";
                result.OperatingSystem = product + " (derleme " + version.Build + ")";
            }
            if (string.IsNullOrWhiteSpace(result.Cpu)) result.Cpu = "İşlemci algılanamadı";
            if (string.IsNullOrWhiteSpace(result.Gpu)) result.Gpu = "Ekran kartı algılanamadı";
            return result;
        }

        public static MemorySnapshot ReadMemory()
        {
            MemorySnapshot snapshot = new MemorySnapshot();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT AvailableMBytes, StandbyCacheCoreBytes, StandbyCacheNormalPriorityBytes, StandbyCacheReserveBytes FROM Win32_PerfFormattedData_PerfOS_Memory"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        snapshot.AvailableMb = ToLong(item["AvailableMBytes"]);
                        long standbyBytes = ToLong(item["StandbyCacheCoreBytes"]) +
                                            ToLong(item["StandbyCacheNormalPriorityBytes"]) +
                                            ToLong(item["StandbyCacheReserveBytes"]);
                        snapshot.StandbyMb = standbyBytes / 1024L / 1024L;
                        break;
                    }
                }

                using (ManagementObjectSearcher system = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject item in system.Get())
                    {
                        snapshot.TotalMb = (long)(Convert.ToUInt64(item["TotalPhysicalMemory"]) / 1024UL / 1024UL);
                        break;
                    }
                }
            }
            catch
            {
            }

            MemoryStatusEx memory = ReadGlobalMemory();
            if (snapshot.TotalMb <= 0)
            {
                snapshot.TotalMb = (long)(memory.TotalPhysical / 1024UL / 1024UL);
            }
            if (snapshot.AvailableMb <= 0)
            {
                snapshot.AvailableMb = (long)(memory.AvailablePhysical / 1024UL / 1024UL);
            }
            return snapshot;
        }

        private static RtlOsVersionInfo ReadRealWindowsVersion()
        {
            RtlOsVersionInfo info = new RtlOsVersionInfo();
            info.Size = (uint)Marshal.SizeOf(typeof(RtlOsVersionInfo));
            if (RtlGetVersion(ref info) != 0)
            {
                Version fallback = Environment.OSVersion.Version;
                info.Major = (uint)fallback.Major;
                info.Minor = (uint)fallback.Minor;
                info.Build = (uint)fallback.Build;
            }
            return info;
        }

        private static MemoryStatusEx ReadGlobalMemory()
        {
            MemoryStatusEx status = new MemoryStatusEx();
            status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
            GlobalMemoryStatusEx(ref status);
            return status;
        }

        private static string SafePowerPlanName()
        {
            try { return PowerPlanTools.GetActiveSchemeName(); }
            catch { return "Bilinmiyor"; }
        }

        private static string ReadFirstProperty(string query, string property)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        return Convert.ToString(item[property]).Trim();
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private static long ToLong(object value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt64(value); }
            catch { return 0; }
        }
    }

    internal static class StandbyListPurger
    {
        private const int SystemMemoryListInformation = 80;
        private const int MemoryPurgeStandbyList = 4;
        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const uint SePrivilegeEnabled = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenPrivileges
        {
            public uint PrivilegeCount;
            public Luid Luid;
            public uint Attributes;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int systemInformationClass, ref int systemInformation, int systemInformationLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool LookupPrivilegeValue(string systemName, string name, out Luid luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TokenPrivileges newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        public static void Purge()
        {
            EnableProfilePrivilege();
            int command = MemoryPurgeStandbyList;
            int status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
            if (status != 0)
            {
                throw new InvalidOperationException("Standby bellek listesi temizlenemedi. NTSTATUS: 0x" + status.ToString("X8"));
            }
            Logger.Info("Standby bellek listesi temizlendi.");
        }

        private static void EnableProfilePrivilege()
        {
            IntPtr token;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAdjustPrivileges | TokenQuery, out token))
            {
                throw new InvalidOperationException("Bellek temizleme ayrıcalığı alınamadı.");
            }
            try
            {
                Luid luid;
                if (!LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out luid))
                {
                    throw new InvalidOperationException("Windows bellek ayrıcalığı bulunamadı.");
                }
                TokenPrivileges privileges = new TokenPrivileges();
                privileges.PrivilegeCount = 1;
                privileges.Luid = luid;
                privileges.Attributes = SePrivilegeEnabled;
                if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                {
                    throw new InvalidOperationException("Bellek ayrıcalığı etkinleştirilemedi.");
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
    }

    internal static class FileHash
    {
        public static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }

    internal static class IslcIntegration
    {
        public const string Version = "1.0.4.6";
        public const string OfficialPage = "https://www.wagnardsoft.com/forums/viewtopic.php?t=1256";
        public const string PortableUrl = "https://download.wagnardsoft.com/ISLC/ISLC%20v1.0.4.6.exe";
        public const string PortableSha256 = "606DCBA965AF417D97486B125723BBC5CCE92F830C7791DEF06B0C542A10DF50";

        public static string DownloadPath
        {
            get { return Path.Combine(AppPaths.DownloadFolder, "ISLC v" + Version + ".exe"); }
        }

        public static bool IsVerified()
        {
            try
            {
                return File.Exists(DownloadPath) &&
                       string.Equals(FileHash.Sha256(DownloadPath), PortableSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static void StartConfigured(int freeMemoryMb, int standbyListMb)
        {
            if (!IsVerified())
            {
                throw new InvalidOperationException("ISLC dosyası doğrulanmadı.");
            }

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = DownloadPath;
            start.Arguments = "-minimized -polling 1000 -listsize " +
                              standbyListMb + " -freememory " + freeMemoryMb;
            start.UseShellExecute = true;
            Process.Start(start);
            Logger.Info(
                "ISLC önerilen ayarlarla başlatıldı. Free=" + freeMemoryMb +
                " MB, Standby=" + standbyListMb + " MB, Polling=1000 ms.");
        }
    }

    internal sealed class LocalSettingsDocument
    {
        public decimal MemoryFreeThresholdMb { get; set; }
        public decimal MemoryStandbyThresholdMb { get; set; }
    }

    internal static class LocalSettingsStore
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static LocalSettingsDocument Load(decimal defaultFreeThreshold)
        {
            LocalSettingsDocument defaults = new LocalSettingsDocument
            {
                MemoryFreeThresholdMb = defaultFreeThreshold,
                MemoryStandbyThresholdMb = 1024
            };

            try
            {
                if (!File.Exists(AppPaths.LocalSettingsFile)) return defaults;
                LocalSettingsDocument loaded = Serializer.Deserialize<LocalSettingsDocument>(
                    File.ReadAllText(AppPaths.LocalSettingsFile, Encoding.UTF8));
                if (loaded == null) return defaults;
                if (loaded.MemoryFreeThresholdMb < 512 || loaded.MemoryFreeThresholdMb > 32768)
                {
                    loaded.MemoryFreeThresholdMb = defaults.MemoryFreeThresholdMb;
                }
                if (loaded.MemoryStandbyThresholdMb < 512 || loaded.MemoryStandbyThresholdMb > 65536)
                {
                    loaded.MemoryStandbyThresholdMb = defaults.MemoryStandbyThresholdMb;
                }
                return loaded;
            }
            catch (Exception ex)
            {
                Logger.Error("Yerel ayarlar okunamadı", ex);
                return defaults;
            }
        }

        public static void Save(decimal freeThreshold, decimal standbyThreshold)
        {
            try
            {
                AppPaths.Ensure();
                LocalSettingsDocument document = new LocalSettingsDocument
                {
                    MemoryFreeThresholdMb = freeThreshold,
                    MemoryStandbyThresholdMb = standbyThreshold
                };
                File.WriteAllText(AppPaths.LocalSettingsFile, Serializer.Serialize(document), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error("Yerel ayarlar kaydedilemedi", ex);
            }
        }
    }

    internal sealed class SoftwarePackageDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Publisher { get; set; }
        public bool Essential { get; set; }
        public bool Gaming { get; set; }
        public bool Creator { get; set; }
    }

    internal static class SoftwareCatalog
    {
        public static List<SoftwarePackageDefinition> Build()
        {
            return new List<SoftwarePackageDefinition>
            {
                Package("RARLab.WinRAR", "WinRAR", "Temel", "RAR ve ZIP arşivlerini açar ve oluşturur.", "RARLAB", true, true, false),
                Package("7zip.7zip", "7-Zip", "Temel", "Ücretsiz ve açık kaynak arşiv yöneticisi.", "Igor Pavlov", false, false, false),
                Package("Google.Chrome", "Google Chrome", "İnternet", "Google hesabı eşitlemeli web tarayıcısı.", "Google", true, false, false),
                Package("Mozilla.Firefox", "Mozilla Firefox", "İnternet", "Açık kaynak, gizlilik odaklı web tarayıcısı.", "Mozilla", false, false, false),
                Package("Brave.Brave", "Brave", "İnternet", "Reklam engelleme özellikli Chromium tarayıcısı.", "Brave Software", false, false, false),
                Package("VideoLAN.VLC", "VLC media player", "Medya", "Çok sayıda ses ve video biçimini oynatır.", "VideoLAN", true, false, true),
                Package("Spotify.Spotify", "Spotify", "Medya", "Müzik ve podcast masaüstü uygulaması.", "Spotify", false, false, false),
                Package("Notepad++.Notepad++", "Notepad++", "Temel", "Hızlı metin ve kod düzenleyicisi.", "Notepad++ Team", true, false, false),
                Package("voidtools.Everything", "Everything", "Temel", "Dosya ve klasörleri anında arar.", "voidtools", true, false, false),
                Package("Microsoft.PowerToys", "Microsoft PowerToys", "Araçlar", "Windows için gelişmiş pencere ve üretkenlik araçları.", "Microsoft", true, false, true),
                Package("Valve.Steam", "Steam", "Oyun", "Steam oyun mağazası ve başlatıcısı.", "Valve", false, true, false),
                Package("EpicGames.EpicGamesLauncher", "Epic Games Launcher", "Oyun", "Epic Games mağazası ve oyun başlatıcısı.", "Epic Games", false, true, false),
                Package("Discord.Discord", "Discord", "Oyun", "Sesli sohbet ve oyuncu toplulukları.", "Discord", false, true, true),
                Package("ElectronicArts.EADesktop", "EA app", "Oyun", "Electronic Arts oyun başlatıcısı.", "Electronic Arts", false, true, false),
                Package("Ubisoft.Connect", "Ubisoft Connect", "Oyun", "Ubisoft oyun başlatıcısı ve mağazası.", "Ubisoft", false, true, false),
                Package("GOG.Galaxy", "GOG Galaxy", "Oyun", "GOG oyun kitaplığı ve başlatıcısı.", "GOG", false, true, false),
                Package("OBSProject.OBSStudio", "OBS Studio", "İçerik", "Oyun kaydı ve canlı yayın uygulaması.", "OBS Project", false, false, true),
                Package("Audacity.Audacity", "Audacity", "İçerik", "Ses kaydı ve düzenleme uygulaması.", "Audacity Team", false, false, true),
                Package("ShareX.ShareX", "ShareX", "Araçlar", "Ekran görüntüsü, kayıt ve paylaşım aracı.", "ShareX Team", false, false, true),
                Package("qBittorrent.qBittorrent", "qBittorrent", "İnternet", "Açık kaynak BitTorrent istemcisi.", "qBittorrent Project", false, false, false)
            };
        }

        private static SoftwarePackageDefinition Package(
            string id,
            string name,
            string category,
            string description,
            string publisher,
            bool essential,
            bool gaming,
            bool creator)
        {
            return new SoftwarePackageDefinition
            {
                Id = id,
                Name = name,
                Category = category,
                Description = description,
                Publisher = publisher,
                Essential = essential,
                Gaming = gaming,
                Creator = creator
            };
        }
    }

    internal static class WinGetTools
    {
        public const string AppInstallerStoreUri = "ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1";

        public static string ResolveExecutable()
        {
            string localAlias = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps",
                "winget.exe");
            if (File.Exists(localAlias)) return localAlias;

            try
            {
                ProcessResult where = ProcessRunner.Run("where.exe", "winget.exe", 5000);
                string first = where.Output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .FirstOrDefault(File.Exists);
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }
            catch
            {
            }
            return string.Empty;
        }

        public static bool IsAvailable()
        {
            return !string.IsNullOrWhiteSpace(ResolveExecutable());
        }

        public static ProcessResult Install(SoftwarePackageDefinition package)
        {
            if (package == null || !Regex.IsMatch(package.Id ?? string.Empty, @"^[A-Za-z0-9._+-]+$"))
            {
                throw new InvalidDataException("Geçersiz WinGet paket kimliği.");
            }

            string executable = ResolveExecutable();
            if (string.IsNullOrWhiteSpace(executable))
            {
                throw new FileNotFoundException("Windows Paket Yöneticisi (WinGet) bulunamadı.");
            }

            string arguments =
                "install --id " + package.Id +
                " --exact --source winget --silent --no-upgrade" +
                " --accept-package-agreements --accept-source-agreements";
            Logger.Info("Uygulama kurulumu başlatıldı: " + package.Name + " [" + package.Id + "]");
            ProcessResult result = ProcessRunner.Run(executable, arguments, 900000);
            if (result.ExitCode == 0)
            {
                Logger.Info("Uygulama kurulumu tamamlandı: " + package.Name);
            }
            else
            {
                Logger.Info("Uygulama kurulumu başarısız: " + package.Name + " / " + result.Error.Trim());
            }
            return result;
        }
    }

    internal sealed class FocusProcessDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public string Warning { get; set; }
        public bool SafeDefault { get; set; }
        public bool ForceClose { get; set; }
        public List<string> ProcessNames { get; set; }

        public FocusProcessDefinition()
        {
            ProcessNames = new List<string>();
        }
    }

    internal static class FocusProcessCatalog
    {
        public static List<FocusProcessDefinition> Build()
        {
            return new List<FocusProcessDefinition>
            {
                Focus("onedrive", "Microsoft OneDrive", "Senkronizasyon", "Oyun sırasında dosya eşitlemesini geçici olarak durdurur.", "Kaydedilmemiş eşitlemeler oyun modu kapatılınca devam eder.", true, true, "OneDrive"),
                Focus("dropbox", "Dropbox", "Senkronizasyon", "Dropbox arka plan eşitlemesini geçici olarak kapatır.", "Dosya eşitlemesi oyun modu boyunca bekler.", true, true, "Dropbox"),
                Focus("gdrive", "Google Drive", "Senkronizasyon", "Google Drive masaüstü eşitlemesini geçici olarak kapatır.", "Dosya eşitlemesi oyun modu boyunca bekler.", true, true, "GoogleDriveFS"),
                Focus("spotify", "Spotify", "Medya", "Spotify masaüstü uygulamasını ve yardımcı süreçlerini kapatır.", "Oyun oynarken müzik dinliyorsanız seçmeyin.", false, true, "Spotify"),
                Focus("chrome", "Google Chrome", "Tarayıcı", "Tüm Chrome sekmelerini ve yardımcı süreçlerini kapatır.", "Gönderilmemiş formları veya gizli oturumları kaydedin.", false, true, "chrome"),
                Focus("edge", "Microsoft Edge", "Tarayıcı", "Edge pencerelerini ve arka plan süreçlerini kapatır.", "Gönderilmemiş formları veya gizli oturumları kaydedin.", false, true, "msedge"),
                Focus("firefox", "Mozilla Firefox", "Tarayıcı", "Firefox pencerelerini ve yardımcı süreçlerini kapatır.", "Gönderilmemiş formları veya gizli oturumları kaydedin.", false, true, "firefox"),
                Focus("brave", "Brave", "Tarayıcı", "Brave pencerelerini ve yardımcı süreçlerini kapatır.", "Gönderilmemiş formları veya gizli oturumları kaydedin.", false, true, "brave"),
                Focus("opera", "Opera", "Tarayıcı", "Opera pencerelerini ve yardımcı süreçlerini kapatır.", "Gönderilmemiş formları veya gizli oturumları kaydedin.", false, true, "opera", "opera_gx"),
                Focus("teams", "Microsoft Teams", "İletişim", "Teams masaüstü süreçlerini kapatır.", "Aktif toplantınız varsa seçmeyin.", false, true, "ms-teams", "Teams", "msteams"),
                Focus("discord", "Discord", "İletişim", "Discord'u ve yardımcı süreçlerini kapatır.", "Sesli sohbet kullanıyorsanız seçmeyin.", false, true, "Discord"),
                Focus("telegram", "Telegram Desktop", "İletişim", "Telegram masaüstü uygulamasını kapatır.", "Açık mesajlar ve çağrılar kapanır.", false, true, "Telegram"),
                Focus("adobecc", "Adobe Creative Cloud", "Yardımcı", "Creative Cloud kullanıcı arayüzü ve CCX yardımcı sürecini kapatır.", "Adobe uygulaması açacaksanız seçmeyin.", false, true, "Creative Cloud", "CCXProcess"),
                Focus("overwolf", "Overwolf", "Oyun eklentisi", "Overwolf ve web yardımcı süreçlerini kapatır.", "Overwolf kullanan oyun içi eklentiler çalışmaz.", false, true, "Overwolf", "OverwolfBrowser"),
                Focus("phonelink", "Telefon Bağlantısı", "Yardımcı", "Telefon Bağlantısı arka plan sürecini kapatır.", "Telefon bildirimleri oyun modu boyunca gelmeyebilir.", true, true, "PhoneExperienceHost"),
                Focus("widgets", "Windows Widget'ları", "Yardımcı", "Widget panosunun arka plan sürecini kapatır.", "Widget içeriği yeniden açılana kadar güncellenmez.", true, true, "Widgets")
            };
        }

        public static int RunningCount(FocusProcessDefinition definition)
        {
            List<Process> processes = GetProcesses(definition);
            int count = processes.Count;
            foreach (Process process in processes) process.Dispose();
            return count;
        }

        public static long RunningWorkingSet(FocusProcessDefinition definition)
        {
            long total = 0;
            foreach (Process process in GetProcesses(definition))
            {
                try { total += process.WorkingSet64; }
                catch { }
                finally { process.Dispose(); }
            }
            return total;
        }

        public static List<Process> GetProcesses(FocusProcessDefinition definition)
        {
            List<Process> processes = new List<Process>();
            HashSet<int> seen = new HashSet<int>();
            int currentProcessId = Process.GetCurrentProcess().Id;
            foreach (string name in definition.ProcessNames)
            {
                string normalized = Path.GetFileNameWithoutExtension(name);
                try
                {
                    foreach (Process process in Process.GetProcessesByName(normalized))
                    {
                        if (process.Id != currentProcessId && seen.Add(process.Id))
                        {
                            processes.Add(process);
                        }
                        else
                        {
                            process.Dispose();
                        }
                    }
                }
                catch
                {
                }
            }
            return processes;
        }

        private static FocusProcessDefinition Focus(
            string id,
            string name,
            string category,
            string description,
            string warning,
            bool safeDefault,
            bool forceClose,
            params string[] processNames)
        {
            FocusProcessDefinition definition = new FocusProcessDefinition
            {
                Id = id,
                Name = name,
                Category = category,
                Description = description,
                Warning = warning,
                SafeDefault = safeDefault,
                ForceClose = forceClose
            };
            definition.ProcessNames.AddRange(processNames);
            return definition;
        }
    }

    internal sealed class FocusClosedApplication
    {
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public long ReleasedBytes { get; set; }
        public int ClosedProcessCount { get; set; }
    }

    internal sealed class FocusServiceDefinition
    {
        public string ServiceName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    internal static class FocusServiceCatalog
    {
        public static List<FocusServiceDefinition> Build()
        {
            return new List<FocusServiceDefinition>
            {
                Service("DiagTrack", "Bağlı Kullanıcı Deneyimleri ve Telemetri", "Tanılama verisi toplama etkinliğini oyun oturumu boyunca bekletir."),
                Service("WerSvc", "Windows Hata Raporlama", "Arka plandaki hata raporu hazırlama ve gönderme işlerini bekletir."),
                Service("WMPNetworkSvc", "Windows Media Player Ağ Paylaşımı", "Yerel ağdaki medya paylaşımını oyun oturumu boyunca durdurur."),
                Service("MapsBroker", "İndirilen Haritalar Yöneticisi", "Çevrimdışı harita güncelleme görevlerini bekletir."),
                Service("Fax", "Faks", "Kullanılmayan faks hizmetini geçici olarak durdurur."),
                Service("RetailDemo", "Perakende Gösteri Hizmeti", "Mağaza tanıtım moduna ait arka plan hizmetini durdurur."),
                Service("RemoteRegistry", "Uzak Kayıt Defteri", "Uzak kayıt defteri erişimini oyun oturumu boyunca kapatır."),
                Service("TrkWks", "Dağıtılmış Bağlantı İzleme İstemcisi", "Ağdaki NTFS bağlantı izleme işlerini bekletir."),
                Service("lfsvc", "Konum Belirleme Hizmeti", "Konum kullanan arka plan uygulamalarını oyun oturumu boyunca bekletir."),
                Service("PhoneSvc", "Telefon Hizmeti", "Telefon bağlantılı arka plan özelliklerini geçici durdurur."),
                Service("icssvc", "Windows Mobil Etkin Nokta", "Mobil etkin nokta paylaşımı kullanılmıyorken arka plan hizmetini durdurur."),
                Service("WalletService", "Cüzdan Hizmeti", "Dijital cüzdan arka plan görevlerini oyun oturumu boyunca bekletir."),
                Service("wisvc", "Windows Insider Hizmeti", "Insider önizleme görevlerini geçici olarak durdurur."),
                Service("WebClient", "WebClient", "WebDAV bağlantıları kullanılmıyorken istemci hizmetini durdurur.")
            };
        }

        private static FocusServiceDefinition Service(string serviceName, string displayName, string description)
        {
            return new FocusServiceDefinition
            {
                ServiceName = serviceName,
                DisplayName = displayName,
                Description = description
            };
        }
    }

    internal sealed class FocusStoppedService
    {
        public string ServiceName { get; set; }
        public string DisplayName { get; set; }
    }

    internal sealed class FocusRegistryBackup
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool Existed { get; set; }
        public int OriginalDword { get; set; }
        public int AppliedDword { get; set; }
    }

    internal sealed class FocusModeSession
    {
        public string StartedAt { get; set; }
        public string PreviousPowerGuid { get; set; }
        public int TargetProcessId { get; set; }
        public int TargetPreviousPriority { get; set; }
        public List<FocusClosedApplication> ClosedApplications { get; set; }
        public List<FocusStoppedService> StoppedServices { get; set; }
        public List<FocusRegistryBackup> RegistryBackups { get; set; }

        public FocusModeSession()
        {
            ClosedApplications = new List<FocusClosedApplication>();
            StoppedServices = new List<FocusStoppedService>();
            RegistryBackups = new List<FocusRegistryBackup>();
        }
    }

    internal sealed class FocusModeResult
    {
        public int ClosedProcessCount { get; set; }
        public int RestartedApplicationCount { get; set; }
        public int StoppedServiceCount { get; set; }
        public int RestoredServiceCount { get; set; }
        public int AppliedGameSettingCount { get; set; }
        public int RestoredGameSettingCount { get; set; }
        public long ReleasedBytes { get; set; }
        public List<string> Messages { get; set; }

        public FocusModeResult()
        {
            Messages = new List<string>();
        }
    }

    internal sealed class FocusModeEngine
    {
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private FocusModeSession session;

        public bool IsActive
        {
            get { return session != null; }
        }

        public FocusModeSession Session
        {
            get { return session; }
        }

        public FocusModeResult Activate(
            IEnumerable<FocusProcessDefinition> definitions,
            int targetProcessId,
            bool useHighPerformance,
            bool useHighPriority)
        {
            return Activate(
                definitions,
                targetProcessId,
                useHighPerformance,
                useHighPriority,
                false);
        }

        public FocusModeResult Activate(
            IEnumerable<FocusProcessDefinition> definitions,
            int targetProcessId,
            bool useHighPerformance,
            bool useHighPriority,
            bool applySystemBoost)
        {
            if (IsActive) throw new InvalidOperationException("Oyun Odak Modu zaten etkin.");

            FocusModeResult result = new FocusModeResult();
            session = new FocusModeSession { StartedAt = DateTime.Now.ToString("o") };
            SaveSession();

            if (useHighPerformance)
            {
                try
                {
                    session.PreviousPowerGuid = PowerPlanTools.GetActiveSchemeGuid();
                    SaveSession();
                    PowerPlanTools.SetHighPerformance();
                }
                catch (Exception ex)
                {
                    result.Messages.Add("Güç planı değiştirilemedi: " + ex.Message);
                }
            }

            if (applySystemBoost)
            {
                ApplyGameSettings(result);
                StopSafeServices(result);
            }

            foreach (FocusProcessDefinition definition in definitions ?? Enumerable.Empty<FocusProcessDefinition>())
            {
                CloseDefinition(definition, result);
                SaveSession();
            }

            if (useHighPriority && targetProcessId > 0)
            {
                try
                {
                    using (Process target = Process.GetProcessById(targetProcessId))
                    {
                        session.TargetProcessId = targetProcessId;
                        session.TargetPreviousPriority = (int)target.PriorityClass;
                        target.PriorityClass = ProcessPriorityClass.High;
                        SaveSession();
                    }
                }
                catch (Exception ex)
                {
                    result.Messages.Add("Oyun işlem önceliği değiştirilemedi: " + ex.Message);
                }
            }

            Logger.Info(
                "Oyun Odak Modu başlatıldı. Kapatılan süreç=" + result.ClosedProcessCount +
                ", durdurulan servis=" + result.StoppedServiceCount +
                ", oyun ayarı=" + result.AppliedGameSettingCount +
                ", boşaltılan çalışma kümesi=" + result.ReleasedBytes);
            return result;
        }

        public FocusModeResult Deactivate(bool restartApplications)
        {
            FocusModeResult result = new FocusModeResult();
            if (session == null)
            {
                session = LoadSession();
            }
            if (session == null) return result;

            RestoreSession(session, restartApplications, result);
            session = null;
            DeleteSessionFile();
            Logger.Info("Oyun Odak Modu kapatıldı ve geçici ayarlar geri yüklendi.");
            return result;
        }

        public void RecoverStaleSession()
        {
            FocusModeSession stale = LoadSession();
            if (stale == null) return;
            FocusModeResult result = new FocusModeResult();
            RestoreSession(stale, true, result);
            DeleteSessionFile();
            Logger.Info("Önceki yarım kalan Oyun Odak Modu oturumu otomatik geri yüklendi.");
        }

        private void CloseDefinition(FocusProcessDefinition definition, FocusModeResult result)
        {
            List<Process> processes = FocusProcessCatalog.GetProcesses(definition);
            if (processes.Count == 0) return;

            FocusClosedApplication record = new FocusClosedApplication
            {
                DefinitionId = definition.Id,
                Name = definition.Name
            };

            foreach (Process process in processes)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(record.ExecutablePath))
                    {
                        try { record.ExecutablePath = process.MainModule.FileName; }
                        catch { }
                    }
                    try { record.ReleasedBytes += process.WorkingSet64; }
                    catch { }

                    bool requestedClose = false;
                    try { requestedClose = process.CloseMainWindow(); }
                    catch { }

                    if (requestedClose)
                    {
                        try { process.WaitForExit(1800); }
                        catch { }
                    }

                    if (!process.HasExited && definition.ForceClose)
                    {
                        process.Kill();
                        process.WaitForExit(2500);
                    }

                    if (process.HasExited)
                    {
                        record.ClosedProcessCount++;
                        result.ClosedProcessCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.Messages.Add(definition.Name + " kapatılamadı: " + ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (record.ClosedProcessCount > 0)
            {
                session.ClosedApplications.Add(record);
                result.ReleasedBytes += record.ReleasedBytes;
            }
        }

        private void ApplyGameSettings(FocusModeResult result)
        {
            ApplyGameDword(
                @"Software\Microsoft\GameBar",
                "AutoGameModeEnabled",
                1,
                result);
            ApplyGameDword(
                @"System\GameConfigStore",
                "GameDVR_Enabled",
                0,
                result);
            ApplyGameDword(
                @"Software\Microsoft\Windows\CurrentVersion\GameDVR",
                "AppCaptureEnabled",
                0,
                result);
        }

        private void ApplyGameDword(string path, string name, int appliedValue, FocusModeResult result)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(path))
                {
                    object original = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    bool existed = original != null;
                    int originalDword = existed ? Convert.ToInt32(original) : 0;
                    if (existed && originalDword == appliedValue) return;

                    session.RegistryBackups.Add(new FocusRegistryBackup
                    {
                        Path = path,
                        Name = name,
                        Existed = existed,
                        OriginalDword = originalDword,
                        AppliedDword = appliedValue
                    });
                    SaveSession();
                    key.SetValue(name, appliedValue, RegistryValueKind.DWord);
                    result.AppliedGameSettingCount++;
                }
            }
            catch (Exception ex)
            {
                result.Messages.Add("Oyun ayarı uygulanamadı (" + name + "): " + ex.Message);
            }
        }

        private void StopSafeServices(FocusModeResult result)
        {
            foreach (FocusServiceDefinition definition in FocusServiceCatalog.Build())
            {
                StopSafeService(definition, result);
            }
        }

        private void StopSafeService(FocusServiceDefinition definition, FocusModeResult result)
        {
            FocusStoppedService record = null;
            try
            {
                using (ServiceController controller = new ServiceController(definition.ServiceName))
                {
                    controller.Refresh();
                    if (controller.Status != ServiceControllerStatus.Running &&
                        controller.Status != ServiceControllerStatus.Paused)
                    {
                        return;
                    }
                    if (!controller.CanStop || HasRunningDependents(controller))
                    {
                        return;
                    }

                    record = new FocusStoppedService
                    {
                        ServiceName = definition.ServiceName,
                        DisplayName = definition.DisplayName
                    };
                    session.StoppedServices.Add(record);
                    SaveSession();
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                    controller.Refresh();
                    if (controller.Status == ServiceControllerStatus.Stopped)
                    {
                        result.StoppedServiceCount++;
                        return;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Bu Windows kurulumunda servis bulunmuyor veya artık erişilebilir değil.
            }
            catch (Exception ex)
            {
                result.Messages.Add(definition.DisplayName + " durdurulamadı: " + ex.Message);
            }

            // Kayıt kasıtlı olarak oturumda tutulur. Durdurma çağrısı zaman aşımına
            // uğrasa bile kapanışta yeniden başlatma denemesi yapmak güvenli geri
            // alma davranışını korur; zaten çalışan servisler olduğu gibi bırakılır.
        }

        private static bool HasRunningDependents(ServiceController controller)
        {
            ServiceController[] dependents;
            try { dependents = controller.DependentServices; }
            catch { return true; }

            try
            {
                foreach (ServiceController dependent in dependents)
                {
                    dependent.Refresh();
                    if (dependent.Status != ServiceControllerStatus.Stopped &&
                        dependent.Status != ServiceControllerStatus.StopPending)
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                foreach (ServiceController dependent in dependents) dependent.Dispose();
            }
        }

        private void RestoreSession(FocusModeSession restore, bool restartApplications, FocusModeResult result)
        {
            if (!string.IsNullOrWhiteSpace(restore.PreviousPowerGuid))
            {
                try { PowerPlanTools.SetScheme(restore.PreviousPowerGuid); }
                catch (Exception ex) { result.Messages.Add("Güç planı geri yüklenemedi: " + ex.Message); }
            }

            if (restore.TargetProcessId > 0)
            {
                try
                {
                    using (Process target = Process.GetProcessById(restore.TargetProcessId))
                    {
                        target.PriorityClass = (ProcessPriorityClass)restore.TargetPreviousPriority;
                    }
                }
                catch
                {
                }
            }

            RestoreServices(restore, result);
            RestoreGameSettings(restore, result);

            if (!restartApplications) return;
            foreach (FocusClosedApplication app in restore.ClosedApplications ?? new List<FocusClosedApplication>())
            {
                if (string.IsNullOrWhiteSpace(app.ExecutablePath) || !File.Exists(app.ExecutablePath)) continue;
                try
                {
                    ProcessStartInfo start = new ProcessStartInfo();
                    start.FileName = app.ExecutablePath;
                    start.UseShellExecute = true;
                    Process.Start(start);
                    result.RestartedApplicationCount++;
                }
                catch (Exception ex)
                {
                    result.Messages.Add(app.Name + " yeniden açılamadı: " + ex.Message);
                }
            }
        }

        private static void RestoreServices(FocusModeSession restore, FocusModeResult result)
        {
            List<FocusStoppedService> services =
                restore.StoppedServices ?? new List<FocusStoppedService>();
            foreach (FocusStoppedService stopped in services.AsEnumerable().Reverse())
            {
                try
                {
                    using (ServiceController controller = new ServiceController(stopped.ServiceName))
                    {
                        controller.Refresh();
                        if (controller.Status == ServiceControllerStatus.Running) continue;
                        if (controller.Status == ServiceControllerStatus.StopPending)
                        {
                            controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(5));
                            controller.Refresh();
                        }
                        if (controller.Status == ServiceControllerStatus.Stopped)
                        {
                            controller.Start();
                            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(7));
                            result.RestoredServiceCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Messages.Add(stopped.DisplayName + " yeniden başlatılamadı: " + ex.Message);
                }
            }
        }

        private static void RestoreGameSettings(FocusModeSession restore, FocusModeResult result)
        {
            List<FocusRegistryBackup> backups =
                restore.RegistryBackups ?? new List<FocusRegistryBackup>();
            foreach (FocusRegistryBackup backup in backups.AsEnumerable().Reverse())
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(backup.Path))
                    {
                        if (backup.Existed)
                        {
                            key.SetValue(backup.Name, backup.OriginalDword, RegistryValueKind.DWord);
                        }
                        else
                        {
                            key.DeleteValue(backup.Name, false);
                        }
                    }
                    result.RestoredGameSettingCount++;
                }
                catch (Exception ex)
                {
                    result.Messages.Add("Oyun ayarı geri yüklenemedi (" + backup.Name + "): " + ex.Message);
                }
            }
        }

        private void SaveSession()
        {
            AppPaths.Ensure();
            File.WriteAllText(AppPaths.FocusSessionFile, serializer.Serialize(session), Encoding.UTF8);
        }

        private FocusModeSession LoadSession()
        {
            try
            {
                if (!File.Exists(AppPaths.FocusSessionFile)) return null;
                return serializer.Deserialize<FocusModeSession>(
                    File.ReadAllText(AppPaths.FocusSessionFile, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Logger.Error("Oyun Odak Modu oturumu okunamadı", ex);
                return null;
            }
        }

        private static void DeleteSessionFile()
        {
            try
            {
                if (File.Exists(AppPaths.FocusSessionFile)) File.Delete(AppPaths.FocusSessionFile);
            }
            catch
            {
            }
        }
    }

    internal sealed class AppxDefinition
    {
        public string PackageName { get; set; }
        public string DisplayName { get; set; }
        public string Note { get; set; }
        public bool SafeSelection { get; set; }
    }

    internal static class AppxTools
    {
        public static List<AppxDefinition> Catalog()
        {
            return new List<AppxDefinition>
            {
                new AppxDefinition { PackageName = "Clipchamp.Clipchamp", DisplayName = "Clipchamp", Note = "Video düzenleyiciyi kullanmıyorsanız kaldırılabilir.", SafeSelection = true },
                new AppxDefinition { PackageName = "Microsoft.BingNews", DisplayName = "Microsoft News", Note = "Haber uygulaması ve arka plan içeriği.", SafeSelection = true },
                new AppxDefinition { PackageName = "Microsoft.BingWeather", DisplayName = "Microsoft Hava Durumu", Note = "Hava durumu uygulamasını kullanmıyorsanız.", SafeSelection = true },
                new AppxDefinition { PackageName = "Microsoft.Getstarted", DisplayName = "Windows İpuçları", Note = "Başlangıç ve tanıtım içerikleri.", SafeSelection = true },
                new AppxDefinition { PackageName = "Microsoft.MicrosoftSolitaireCollection", DisplayName = "Solitaire Collection", Note = "Hazır gelen oyun paketi.", SafeSelection = true },
                new AppxDefinition { PackageName = "Microsoft.WindowsFeedbackHub", DisplayName = "Geri Bildirim Merkezi", Note = "Windows geri bildirim uygulaması.", SafeSelection = true },
                new AppxDefinition { PackageName = "Microsoft.WindowsMaps", DisplayName = "Windows Haritalar", Note = "Çevrimdışı harita uygulaması.", SafeSelection = true },
                new AppxDefinition { PackageName = "Microsoft.ZuneVideo", DisplayName = "Filmler ve TV", Note = "Microsoft video oynatıcısı.", SafeSelection = false },
                new AppxDefinition { PackageName = "MicrosoftTeams", DisplayName = "Microsoft Teams (kişisel)", Note = "Teams kullanıyorsanız kaldırmayın.", SafeSelection = false },
                new AppxDefinition { PackageName = "MSTeams", DisplayName = "Yeni Microsoft Teams", Note = "İş veya okul hesabıyla Teams kullanıyorsanız kaldırmayın.", SafeSelection = false },
                new AppxDefinition { PackageName = "Microsoft.XboxApp", DisplayName = "Xbox Uygulaması", Note = "Game Pass veya Xbox özellikleri için gereklidir.", SafeSelection = false },
                new AppxDefinition { PackageName = "Microsoft.PowerAutomateDesktop", DisplayName = "Power Automate", Note = "Masaüstü otomasyonları kullanıyorsanız kaldırmayın.", SafeSelection = false }
            };
        }

        public static HashSet<string> InstalledPackageNames()
        {
            ProcessResult result = ProcessRunner.Run(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage | ForEach-Object { $_.Name }\"",
                60000);
            return new HashSet<string>(
                result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        public static ProcessResult RemoveForCurrentUser(string packageName)
        {
            string safe = packageName.Replace("'", "''");
            return ProcessRunner.Run(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -Command \"Get-AppxPackage -Name '" + safe +
                "' | Remove-AppxPackage -ErrorAction Stop\"",
                120000);
        }
    }

    internal sealed class CustomModuleDocument
    {
        public string id { get; set; }
        public string title { get; set; }
        public string summary { get; set; }
        public string details { get; set; }
        public string category { get; set; }
        public string risk { get; set; }
        public string impact { get; set; }
        public bool requiresRestart { get; set; }
        public List<CustomRegistryDocument> registry { get; set; }
    }

    internal sealed class CustomRegistryDocument
    {
        public string hive { get; set; }
        public string path { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public object enabledValue { get; set; }
    }

    internal static class CustomModuleLoader
    {
        public static List<TweakDefinition> Load()
        {
            List<TweakDefinition> result = new List<TweakDefinition>();
            AppPaths.Ensure();
            JavaScriptSerializer serializer = new JavaScriptSerializer();

            foreach (string file in Directory.GetFiles(AppPaths.ModulesFolder, "*.json"))
            {
                try
                {
                    CustomModuleDocument doc = serializer.Deserialize<CustomModuleDocument>(File.ReadAllText(file, Encoding.UTF8));
                    if (doc == null || string.IsNullOrWhiteSpace(doc.id) || string.IsNullOrWhiteSpace(doc.title))
                    {
                        continue;
                    }
                    if (doc.registry == null || doc.registry.Count == 0)
                    {
                        continue;
                    }

                    TweakDefinition tweak = new TweakDefinition();
                    tweak.Id = "custom." + doc.id;
                    tweak.Title = doc.title;
                    tweak.Summary = doc.summary ?? string.Empty;
                    tweak.Details = doc.details ?? string.Empty;
                    tweak.Category = string.IsNullOrWhiteSpace(doc.category) ? "Eklentiler" : doc.category;
                    tweak.Risk = ParseRisk(doc.risk);
                    tweak.Impact = ParseImpact(doc.impact);
                    tweak.RequiresRestart = doc.requiresRestart;
                    tweak.SourceLabel = "Yerel eklenti";
                    tweak.SourceUrl = file;

                    foreach (CustomRegistryDocument reg in doc.registry)
                    {
                        ValidateCustomRegistry(reg);
                        RegistryValueKind kind = ParseKind(reg.type);
                        tweak.RegistryChanges.Add(new RegistryMutation
                        {
                            Hive = reg.hive,
                            Path = reg.path,
                            Name = reg.name,
                            Kind = kind,
                            AppliedValue = ConvertCustomValue(reg.enabledValue, kind)
                        });
                    }
                    result.Add(tweak);
                }
                catch (Exception ex)
                {
                    Logger.Error("Eklenti yüklenemedi: " + Path.GetFileName(file), ex);
                }
            }
            return result;
        }

        private static void ValidateCustomRegistry(CustomRegistryDocument reg)
        {
            if (reg == null || string.IsNullOrWhiteSpace(reg.hive) || string.IsNullOrWhiteSpace(reg.path) || string.IsNullOrWhiteSpace(reg.name))
            {
                throw new InvalidDataException("Eklenti kayıt defteri alanları eksik.");
            }
            string hive = reg.hive.ToUpperInvariant();
            if (hive != "HKCU" && hive != "HKLM")
            {
                throw new InvalidDataException("Eklentiler yalnızca HKCU veya HKLM kullanabilir.");
            }
            if (reg.path.Contains(".."))
            {
                throw new InvalidDataException("Geçersiz kayıt defteri yolu.");
            }
        }

        private static RegistryValueKind ParseKind(string value)
        {
            if (string.Equals(value, "String", StringComparison.OrdinalIgnoreCase)) return RegistryValueKind.String;
            if (string.Equals(value, "QWord", StringComparison.OrdinalIgnoreCase)) return RegistryValueKind.QWord;
            return RegistryValueKind.DWord;
        }

        private static object ConvertCustomValue(object value, RegistryValueKind kind)
        {
            if (kind == RegistryValueKind.DWord) return Convert.ToInt32(value);
            if (kind == RegistryValueKind.QWord) return Convert.ToInt64(value);
            return Convert.ToString(value);
        }

        private static RiskLevel ParseRisk(string value)
        {
            if (string.Equals(value, "Experimental", StringComparison.OrdinalIgnoreCase)) return RiskLevel.Experimental;
            if (string.Equals(value, "Caution", StringComparison.OrdinalIgnoreCase)) return RiskLevel.Caution;
            return RiskLevel.Safe;
        }

        private static ImpactLevel ParseImpact(string value)
        {
            if (string.Equals(value, "High", StringComparison.OrdinalIgnoreCase)) return ImpactLevel.High;
            if (string.Equals(value, "Medium", StringComparison.OrdinalIgnoreCase)) return ImpactLevel.Medium;
            return ImpactLevel.Low;
        }
    }

    internal static class TweakCatalog
    {
        public static List<TweakDefinition> Build()
        {
            List<TweakDefinition> list = new List<TweakDefinition>();

            list.Add(new TweakDefinition
            {
                Id = "gaming.game_mode",
                Title = "Windows Oyun Modu",
                Summary = "Oyun açıkken arka plan kaynak kullanımını sınırlandırmaya yardımcı olur.",
                Details = "Microsoft'un Oyun Modu özelliğini etkinleştirir. FPS artışı donanıma ve arka plan yüküne bağlıdır; asıl hedef kare süresi tutarlılığıdır.",
                Category = "Oyun",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Medium,
                Recommended = true,
                SourceLabel = "Microsoft – Game Mode",
                SourceUrl = "https://learn.microsoft.com/en-us/windows/apps/develop/settings/settings-windows-11",
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gaming.disable_capture",
                Title = "Arka plan oyun kaydını kapat",
                Summary = "Oyun kliplerinin sürekli arka planda kaydedilmesini durdurur.",
                Details = "Game DVR ve arka plan yakalama kapatılır. Xbox Game Bar'ın ekran görüntüsü ve son anları kaydetme özellikleri çalışmayabilir.",
                Category = "Oyun",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Medium,
                Recommended = true,
                SourceLabel = "Microsoft – Game DVR ayarları",
                SourceUrl = "https://learn.microsoft.com/en-us/windows/apps/develop/settings/settings-windows-11",
                RegistryChanges =
                {
                    Dword("HKCU", @"System\GameConfigStore", "GameDVR_Enabled", 0),
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "gaming.high_performance_power",
                Title = "Yüksek performans güç planı",
                Summary = "İşlemci ve sistemin güç tasarrufu yerine tepki hızını öncelemesini sağlar.",
                Details = "Masaüstü bilgisayarlarda ve prize takılı kullanımda daha tutarlı saat hızları sağlayabilir. Dizüstünde pil tüketimi, fan sesi ve sıcaklık artabilir.",
                Category = "Oyun",
                Risk = RiskLevel.Caution,
                Impact = ImpactLevel.High,
                Recommended = true,
                SourceLabel = "Microsoft – Güç performansı",
                SourceUrl = "https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-power-slider",
                SpecialKind = SpecialTweakKind.HighPerformancePower
            });

            list.Add(new TweakDefinition
            {
                Id = "gaming.hags",
                Title = "Donanım hızlandırmalı GPU zamanlaması",
                Summary = "GPU iş zamanlamasının bir bölümünü ekran kartına devreder.",
                Details = "Uyumlu GPU ve güncel sürücü gerekir. Bazı sistemlerde gecikmeyi iyileştirirken bazılarında takılma yapabilir; açtıktan sonra aynı oyun sahnesinde ölçüm yapın.",
                Category = "Oyun",
                Risk = RiskLevel.Caution,
                Impact = ImpactLevel.Medium,
                RequiresRestart = true,
                SourceLabel = "Windows grafik ayarı",
                SourceUrl = "ms-settings:display-advancedgraphics",
                RegistryChanges =
                {
                    Dword("HKLM", @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "visual.transparency",
                Title = "Şeffaflık efektlerini kapat",
                Summary = "Başlat menüsü ve görev çubuğundaki saydam çizimi azaltır.",
                Details = "Masaüstü GPU kullanımını çok az azaltabilir. Oyun FPS'sine etkisi genellikle düşüktür; zayıf veya entegre GPU'lu sistemlerde arayüz daha akıcı hissedilebilir.",
                Category = "Görünüm",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "visual.effects",
                Title = "Windows görsel efektlerini azalt",
                Summary = "Animasyon ve gölge efektleri yerine tepki hızını önceler.",
                Details = "Pencere animasyonları ve bazı görsel süsler azalır. Oyun içi grafik ayarlarını değiştirmez; masaüstü ve düşük donanımlı sistemlerde daha hızlı hissedilir.",
                Category = "Görünüm",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "visual.widgets",
                Title = "Görev çubuğu widget'larını gizle",
                Summary = "Windows 11 widget panelini görev çubuğundan kaldırır.",
                Details = "Haber ve widget görünümünü gizler. Arka plan bileşenleri Windows sürümüne göre tamamen kapanmayabilir; performans etkisi düşüktür.",
                Category = "Görünüm",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                Compatibility = "Windows 11",
                SourceLabel = "Microsoft – Windows 11 ayar referansı",
                SourceUrl = "https://learn.microsoft.com/en-us/windows/apps/develop/settings/settings-windows-11",
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "visual.search",
                Title = "Görev çubuğu arama kutusunu gizle",
                Summary = "Arama kutusunu görev çubuğundan kaldırır; Windows araması çalışmaya devam eder.",
                Details = "Yalnızca görev çubuğu görünümünü sadeleştirir. Başlat menüsünde yazarak arama kullanılabilir.",
                Category = "Görünüm",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "background.suggestions",
                Title = "Öneri ve tanıtım içeriklerini kapat",
                Summary = "Başlat menüsü, bildirimler ve Ayarlar içindeki önerileri azaltır.",
                Details = "Microsoft önerileri ve uygulama tanıtımları kapatılır. Güvenlik veya Windows Update etkilenmez.",
                Category = "Arka Plan",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0),
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0),
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0),
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "background.apps",
                Title = "Mağaza uygulamalarının arka planını kısıtla",
                Summary = "Microsoft Store uygulamalarının arka planda çalışmasını genel olarak sınırlar.",
                Details = "Bazı uygulamaların bildirimleri, canlı kutucukları veya eşitlemesi gecikebilir. Discord, Steam ve klasik masaüstü uygulamalarını doğrudan kapatmaz.",
                Category = "Arka Plan",
                Risk = RiskLevel.Caution,
                Impact = ImpactLevel.Medium,
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "background.search_service",
                Title = "Windows arama dizinini durdur",
                Summary = "Arka plan disk taramasını azaltır, ancak dosya aramaları yavaşlar.",
                Details = "Özellikle yavaş disklerde arka plan G/Ç'sini azaltabilir. Başlat ve Dosya Gezgini aramaları daha yavaş veya eksik olabilir.",
                Category = "Arka Plan",
                Risk = RiskLevel.Caution,
                Impact = ImpactLevel.Medium,
                ServiceChanges =
                {
                    new ServiceMutation { ServiceName = "WSearch", AppliedStartValue = 4, StopWhenApplied = true }
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "background.telemetry_service",
                Title = "Bağlı kullanıcı deneyimleri hizmetini durdur",
                Summary = "DiagTrack tanılama hizmetinin arka plan çalışmasını kapatır.",
                Details = "Performans etkisi genellikle küçüktür; esas etkisi gizliliktir. Bazı tanılama ve geri bildirim işlevleri azalabilir.",
                Category = "Gizlilik",
                Risk = RiskLevel.Caution,
                Impact = ImpactLevel.Low,
                ServiceChanges =
                {
                    new ServiceMutation { ServiceName = "DiagTrack", AppliedStartValue = 4, StopWhenApplied = true }
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "background.activity_history",
                Title = "Etkinlik geçmişi eşitlemesini kapat",
                Summary = "Uygulama ve etkinlik geçmişinin buluta yayımlanmasını sınırlar.",
                Details = "Cihazlar arası etkinlik özellikleri çalışmayabilir. Oyun performansına etkisi çok düşüktür; gizlilik odaklıdır.",
                Category = "Gizlilik",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                RegistryChanges =
                {
                    Dword("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0),
                    Dword("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0),
                    Dword("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "background.advertising_id",
                Title = "Reklam kimliğini kapat",
                Summary = "Uygulamaların kişiselleştirilmiş reklam kimliğini kullanmasını engeller.",
                Details = "FPS etkisi beklenmez. Gizlilik amaçlıdır ve uygulamalarda daha az kişiselleştirilmiş reklam gösterilebilir.",
                Category = "Gizlilik",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "network.delivery_optimization",
                Title = "Güncelleme eşler arası paylaşımını kapat",
                Summary = "Windows güncellemelerinin başka bilgisayarlara yüklenmesini engeller.",
                Details = "Windows Update çalışmaya devam eder; yalnızca eşler arası indirme/yükleme devre dışı kalır. Oyun sırasında ağ kullanımını azaltabilir.",
                Category = "Ağ",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                RegistryChanges =
                {
                    Dword("HKLM", @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "system.long_paths",
                Title = "Uzun dosya yollarını etkinleştir",
                Summary = "Modern uygulamalarda 260 karakterden uzun dosya yollarına izin verir.",
                Details = "Windows 10/11 uzun yol desteğini etkinleştirir. Eski uygulamaların kendi sınırlamaları devam edebilir. Değişikliğin tamamı yeniden başlatmadan sonra geçerli olur.",
                Category = "Sistem",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                RequiresRestart = true,
                RegistryChanges =
                {
                    Dword("HKLM", @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 1)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "system.error_reporting",
                Title = "Windows hata raporlamasını kapat",
                Summary = "Çöken uygulamaların Microsoft'a hata raporu hazırlamasını durdurur.",
                Details = "Arka plan hata raporu oluşturmayı azaltabilir; ancak çökme tanılaması ve Microsoft'a gönderilen hata bilgileri devre dışı kalır. Tek tık önerilen profile dahil değildir.",
                Category = "Sistem",
                Risk = RiskLevel.Caution,
                Impact = ImpactLevel.Low,
                RegistryChanges =
                {
                    Dword("HKLM", @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "windows11.taskbar_chat",
                Title = "Görev çubuğu Sohbet simgesini gizle",
                Summary = "Windows 11 kişisel sohbet simgesini görev çubuğundan kaldırır.",
                Details = "Yalnız görev çubuğu görünümünü sadeleştirir. Microsoft Teams veya Discord kurulumu kaldırılmaz.",
                Category = "Windows 11",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                Compatibility = "Windows 11",
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarMn", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "windows11.copilot",
                Title = "Windows Copilot'u gizle",
                Summary = "Copilot düğmesini ve Windows Copilot girişini kapatır.",
                Details = "Copilot kullanıyorsanız seçmeyin. Oyun FPS etkisi beklenmez; arayüzü ve arka plan özelliklerini sadeleştirmek içindir.",
                Category = "Windows 11",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Compatibility = "Windows 11",
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "windows11.compact_explorer",
                Title = "Dosya Gezgini kompakt görünümü",
                Summary = "Dosya Gezgini satır aralıklarını küçülterek daha fazla öğe gösterir.",
                Details = "Yalnızca Dosya Gezgini görünümünü değiştirir. Performans etkisi yok denecek kadar azdır.",
                Category = "Windows 11",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Compatibility = "Windows 11",
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "UseCompactMode", 1)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "windows11.classic_context",
                Title = "Klasik sağ tık menüsünü etkinleştir",
                Summary = "Windows 11'de tam sağ tık menüsünü ilk tıklamada açar.",
                Details = "Yeni sade sağ tık menüsü yerine klasik menüyü gösterir. Explorer veya Windows yeniden başladıktan sonra tamamen etkinleşir.",
                Category = "Windows 11",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Compatibility = "Windows 11",
                RequiresRestart = true,
                RegistryChanges =
                {
                    StringValue(
                        "HKCU",
                        @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                        string.Empty,
                        string.Empty)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "windows11.edge_sidebar",
                Title = "Edge kenar çubuğunu kapat",
                Summary = "Microsoft Edge yan panelini ve Discover düğmesini devre dışı bırakır.",
                Details = "Edge içindeki kenar çubuğu özelliklerini kullanıyorsanız seçmeyin. Windows güvenliği ve diğer tarayıcılar etkilenmez.",
                Category = "Windows 11",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Compatibility = "Windows 10 / 11 • Microsoft Edge",
                RegistryChanges =
                {
                    Dword("HKLM", @"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "windows11.cloud_clipboard",
                Title = "Bulut pano geçmişini kapat",
                Summary = "Pano geçmişinin cihazlar arasında eşitlenmesini sınırlar.",
                Details = "Windows+V geçmişi ve cihazlar arası pano kullanıyorsanız seçmeyin. Oyun performansı etkisi çok düşüktür; gizlilik amaçlıdır.",
                Category = "Windows 11",
                Risk = RiskLevel.Caution,
                Impact = ImpactLevel.Low,
                Compatibility = "Windows 10 / 11",
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Clipboard", "EnableClipboardHistory", 0),
                    Dword("HKCU", @"Software\Microsoft\Clipboard", "EnableCloudClipboard", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "privacy.tailored_experiences",
                Title = "Kişiselleştirilmiş deneyimleri kapat",
                Summary = "Tanılama verisine dayalı öneri ve kişiselleştirmeyi sınırlar.",
                Details = "Windows güvenlik güncellemelerini etkilemez. FPS etkisi beklenmez; gizlilik ve arayüz sadeliği içindir.",
                Category = "Gizlilik",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.Low,
                Recommended = true,
                RegistryChanges =
                {
                    Dword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "repair.common_tools",
                Title = "Yaygın Windows araçlarını geri aç",
                Summary = "Görev Yöneticisi, Komut İstemi, Denetim Masası, Çalıştır, sağ tık ve Kayıt Defteri erişim engellerini kaldırır.",
                Details = "Zararlı yazılım veya hatalı bir ayar tarafından kapatılmış yaygın Windows araçlarını yeniden erişilebilir yapar. Güvenlik Duvarı, Defender ve Windows Update ayarlarına dokunmaz.",
                Category = "Onarım",
                Risk = RiskLevel.Safe,
                Impact = ImpactLevel.High,
                Recommended = true,
                RequiresRestart = true,
                RegistryChanges =
                {
                    DeleteDword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr"),
                    DeleteDword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools"),
                    DeleteDword("HKCU", @"Software\Policies\Microsoft\Windows\System", "DisableCMD"),
                    DeleteDword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoControlPanel"),
                    DeleteDword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun"),
                    DeleteDword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoViewContextMenu"),
                    DeleteDword("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoFolderOptions")
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "experimental.sysmain",
                Title = "SysMain hizmetini kapat",
                Summary = "Windows ön yükleme hizmetini durdurur.",
                Details = "Bazı sistemlerde arka plan disk etkinliğini azaltabilir; bazı sistemlerde uygulama açılışlarını ve oyun yükleme sürelerini kötüleştirebilir. Yalnızca ölçerek deneyin.",
                Category = "Deneysel",
                Risk = RiskLevel.Experimental,
                Impact = ImpactLevel.Medium,
                ServiceChanges =
                {
                    new ServiceMutation { ServiceName = "SysMain", AppliedStartValue = 4, StopWhenApplied = true }
                }
            });

            list.Add(new TweakDefinition
            {
                Id = "experimental.memory_compression",
                Title = "Windows bellek sıkıştırmasını kapat",
                Summary = "Unlost 2026 rehberindeki Disable-MMAgent ayarını uygular.",
                Details = "Daha fazla boş RAM varmış gibi görünmesini sağlamaz. Düşük RAM'li sistemlerde sayfalama artabilir ve performans kötüleşebilir. Sadece ölçüm yapacak deneyimli kullanıcılar için.",
                Category = "Deneysel",
                Risk = RiskLevel.Experimental,
                Impact = ImpactLevel.High,
                RequiresRestart = true,
                SourceLabel = "Unlost 2026 / Microsoft MMAgent",
                SourceUrl = "https://learn.microsoft.com/en-us/powershell/module/mmagent/enable-mmagent",
                SpecialKind = SpecialTweakKind.MemoryCompressionOff
            });

            list.AddRange(CustomModuleLoader.Load());
            return list;
        }

        private static RegistryMutation Dword(string hive, string path, string name, int value)
        {
            return new RegistryMutation
            {
                Hive = hive,
                Path = path,
                Name = name,
                AppliedValue = value,
                Kind = RegistryValueKind.DWord
            };
        }

        private static RegistryMutation DeleteDword(string hive, string path, string name)
        {
            return new RegistryMutation
            {
                Hive = hive,
                Path = path,
                Name = name,
                Kind = RegistryValueKind.DWord,
                DeleteWhenApplied = true
            };
        }

        private static RegistryMutation StringValue(string hive, string path, string name, string value)
        {
            return new RegistryMutation
            {
                Hive = hive,
                Path = path,
                Name = name,
                AppliedValue = value,
                Kind = RegistryValueKind.String
            };
        }
    }

    internal static class SelfTest
    {
        public static int Run()
        {
            try
            {
                AppPaths.Ensure();
                List<TweakDefinition> catalog = TweakCatalog.Build();
                if (catalog.Count < 25) throw new InvalidOperationException("Ayar kataloğu eksik.");
                if (catalog.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                {
                    throw new InvalidOperationException("Yinelenen ayar kimliği bulundu.");
                }
                TweakDefinition repairTools = catalog.FirstOrDefault(x => x.Id == "repair.common_tools");
                if (repairTools == null ||
                    repairTools.RegistryChanges.Count < 7 ||
                    repairTools.RegistryChanges.Any(x => !x.DeleteWhenApplied))
                {
                    throw new InvalidOperationException("Windows araç onarım kataloğu geçersiz.");
                }
                List<SoftwarePackageDefinition> software = SoftwareCatalog.Build();
                if (software.Count < 15 ||
                    software.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1) ||
                    software.Any(x => !Regex.IsMatch(x.Id ?? string.Empty, @"^[A-Za-z0-9._+-]+$")))
                {
                    throw new InvalidOperationException("Uygulama kurulum kataloğu geçersiz.");
                }
                HashSet<string> protectedProcesses = new HashSet<string>(
                    new[] { "explorer", "dwm", "svchost", "csrss", "lsass", "msmpeng", "steam", "epicgameslauncher" },
                    StringComparer.OrdinalIgnoreCase);
                List<FocusProcessDefinition> focus = FocusProcessCatalog.Build();
                if (focus.Count < 8 ||
                    focus.SelectMany(x => x.ProcessNames).Any(x => protectedProcesses.Contains(Path.GetFileNameWithoutExtension(x))))
                {
                    throw new InvalidOperationException("Oyun Odak Modu güvenlik listesi geçersiz.");
                }
                HashSet<string> protectedServices = new HashSet<string>(
                    new[]
                    {
                        "RpcSs", "DcomLaunch", "RpcEptMapper", "EventLog", "AudioSrv", "AudioEndpointBuilder",
                        "Dhcp", "Dnscache", "NlaSvc", "WlanSvc", "WinDefend", "mpssvc", "wuauserv",
                        "UsoSvc", "CryptSvc", "BITS", "LanmanWorkstation", "Power", "Schedule"
                    },
                    StringComparer.OrdinalIgnoreCase);
                List<FocusServiceDefinition> focusServices = FocusServiceCatalog.Build();
                if (focusServices.Count < 10 ||
                    focusServices.GroupBy(x => x.ServiceName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1) ||
                    focusServices.Any(x => protectedServices.Contains(x.ServiceName)))
                {
                    throw new InvalidOperationException("Oyun Kipi servis güvenlik listesi geçersiz.");
                }
                List<CleanupTarget> cleanup = SafeCleanupEngine.BuildCatalog();
                if (cleanup.Count < 6 ||
                    cleanup.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                {
                    throw new InvalidOperationException("Temizlik kataloğu geçersiz.");
                }
                List<DnsPreset> dns = NetworkTools.Presets();
                if (dns.Count < 3 || dns.Any(x => string.IsNullOrWhiteSpace(x.Primary) || string.IsNullOrWhiteSpace(x.Secondary)))
                {
                    throw new InvalidOperationException("DNS kataloğu geçersiz.");
                }
                List<GoodbyeDpiProfile> dpiProfiles = GoodbyeDpiIntegration.Profiles();
                if (dpiProfiles.Count < 2 ||
                    dpiProfiles.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1) ||
                    dpiProfiles.Any(x => string.IsNullOrWhiteSpace(x.Arguments)) ||
                    GoodbyeDpiIntegration.OfficialZipSha256.Length != 64)
                {
                    throw new InvalidOperationException("GoodbyeDPI profil veya bütünlük kataloğu geçersiz.");
                }
                TercanUpdateService.ValidateManifestForTest(new UpdateManifest
                {
                    Version = "9.9.9.0",
                    SetupUrl = "https://github.com/Tercan24/Can/releases/download/v9.9.9/tercan-setup.exe",
                    SetupSha256 = new string('A', 64)
                });
                bool unsafeUpdateRejected = false;
                try
                {
                    TercanUpdateService.ValidateManifestForTest(new UpdateManifest
                    {
                        Version = "9.9.9.0",
                        SetupUrl = "https://example.invalid/tercan-setup.exe",
                        SetupSha256 = new string('A', 64)
                    });
                }
                catch (InvalidOperationException)
                {
                    unsafeUpdateRejected = true;
                }
                if (!unsafeUpdateRejected)
                {
                    throw new InvalidOperationException("Güvenilmeyen güncelleme adresi reddedilmedi.");
                }
                string dpiTestArchive = Environment.GetEnvironmentVariable("TERCAN_GOODBYEDPI_TEST_ZIP");
                if (!string.IsNullOrWhiteSpace(dpiTestArchive))
                {
                    if (!GoodbyeDpiIntegration.IsArchiveVerified(dpiTestArchive))
                    {
                        throw new InvalidOperationException("GoodbyeDPI test paketi doğrulanamadı.");
                    }
                    GoodbyeDpiIntegration.InstallVerifiedArchive(dpiTestArchive);
                    if (!GoodbyeDpiIntegration.IsInstalledAndVerified())
                    {
                        throw new InvalidOperationException("GoodbyeDPI güvenli çıkarma testi başarısız.");
                    }
                }
                HostsManager.Validate("# Tercan testi\r\n127.0.0.1 localhost\r\n0.0.0.0 example.invalid");
                List<NetworkAdapterSnapshot> adapters = NetworkTools.ReadAdapters();
                List<StartupRecord> startup = StartupManager.ReadAll();
                string hardwareReport = HardwareReport.Build();
                if (hardwareReport.Length < 200 || hardwareReport.IndexOf("[İŞLEMCİ]", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Donanım raporu oluşturulamadı.");
                }
                SystemInfoSnapshot system = SystemProbe.Read();
                MemorySnapshot memory = SystemProbe.ReadMemory();
                SystemScanReport scan = SystemScanEngine.Scan(catalog, new TweakEngine(new BackupStore()), system);
                if (scan.ReadinessScore < 0 || scan.ReadinessScore > 100 ||
                    scan.TargetScore < scan.ReadinessScore || scan.TargetScore > 100 ||
                    scan.EstimatedImpactScore < 0 || scan.EstimatedImpactScore > 100)
                {
                    throw new InvalidOperationException("Akıllı tarama puanları geçersiz.");
                }
                Console.WriteLine("SELFTEST_OK");
                Console.WriteLine("Tweaks=" + catalog.Count);
                Console.WriteLine("SoftwarePackages=" + software.Count);
                Console.WriteLine("FocusGroups=" + focus.Count);
                Console.WriteLine("FocusServices=" + focusServices.Count);
                Console.WriteLine("CleanupCategories=" + cleanup.Count);
                Console.WriteLine("DnsPresets=" + dns.Count);
                Console.WriteLine("GoodbyeDpiProfiles=" + dpiProfiles.Count);
                Console.WriteLine("UpdateManifestSecurity=VERIFIED");
                Console.WriteLine("GoodbyeDpiPackageTest=" +
                    (string.IsNullOrWhiteSpace(dpiTestArchive) ? "SKIPPED" : "VERIFIED"));
                Console.WriteLine("NetworkAdapters=" + adapters.Count);
                Console.WriteLine("StartupEntries=" + startup.Count);
                Console.WriteLine("HardwareReportChars=" + hardwareReport.Length);
                Console.WriteLine("ScanReadiness=" + scan.ReadinessScore);
                Console.WriteLine("ScanFindings=" + scan.Findings.Count);
                Console.WriteLine("OS=" + system.OperatingSystem);
                Console.WriteLine("RAM_MB=" + memory.TotalMb);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("SELFTEST_FAIL: " + ex);
                return 1;
            }
        }
    }
}

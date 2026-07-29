using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Web.Script.Serialization;

namespace TercanOptimizer
{
    internal sealed class GoodbyeDpiProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Arguments { get; set; }
        public bool Caution { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class GoodbyeDpiPreferences
    {
        public string ProfileId { get; set; }
    }

    internal sealed class GoodbyeDpiSession
    {
        public int ProcessId { get; set; }
        public string ExecutablePath { get; set; }
        public string ProfileId { get; set; }
        public string StartedAt { get; set; }
    }

    internal sealed class GoodbyeDpiStatus
    {
        public bool PackageVerified { get; set; }
        public bool TemporaryProcessRunning { get; set; }
        public bool ServiceInstalled { get; set; }
        public bool ServiceRunning { get; set; }
        public bool ManagedService { get; set; }
        public bool ExternalService { get; set; }

        public bool IsRunning
        {
            get { return TemporaryProcessRunning || ServiceRunning; }
        }

        public bool StartsWithWindows
        {
            get { return ServiceInstalled && ManagedService; }
        }
    }

    internal static class GoodbyeDpiIntegration
    {
        public const string Version = "0.2.3rc3-turkey";
        public const string OfficialRepository = "https://github.com/cagritaskn/GoodbyeDPI-Turkey";
        public const string OfficialReleasePage = "https://github.com/cagritaskn/GoodbyeDPI-Turkey/releases/tag/release-0.2.3rc3-turkey";
        public const string OfficialDownloadUrl = "https://github.com/cagritaskn/GoodbyeDPI-Turkey/releases/download/release-0.2.3rc3-turkey/goodbyedpi-0.2.3rc3-turkey.zip";
        public const string OfficialZipSha256 = "B1F93B2E9434D93C5321275C4A3D0A87F3B822C552ECEABDBEB1610C879E1863";
        public const string LicenseUrl = "https://github.com/cagritaskn/GoodbyeDPI-Turkey/blob/master/LICENSE";
        public const string ReplacementRepository = "https://github.com/cagritaskn/SplitWire-Turkey";
        private const string ServiceName = "GoodbyeDPI";
        private const string MarkerName = ".tercan-verified";

        private static readonly Dictionary<string, string> RequiredHashes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { @"x86_64\goodbyedpi.exe", "8D412B094BB9C137FF25BA9A794D1122ECC84BB776DEBFF6C249723A13CC31CD" },
                { @"x86_64\WinDivert.dll", "6110BFA44667405179C3E15E12AF1B62037E447ED59B054B19042032995E6C7E" },
                { @"x86_64\WinDivert64.sys", "E69B5BA3F0CD6CFB2983E442636E7F0B342B61B15264B0328317D4559C82CF50" },
                { @"x86\goodbyedpi.exe", "66E202C9FCE9E769E2BC791B7FD6F56F21EAB59F607F4ED0724E0C68C430DD1F" },
                { @"x86\WinDivert.dll", "625FFDD95BFABFF32D0E8A95BEABCD303C01C8BBA73B90402D4E84D6E15DD8E5" },
                { @"x86\WinDivert32.sys", "29CA5CEB59C9C6993A349E82B1FD46078E6F8A302764153AB84FA22E382FCDCA" },
                { @"x86\WinDivert64.sys", "E69B5BA3F0CD6CFB2983E442636E7F0B342B61B15264B0328317D4559C82CF50" }
            };

        public static string ArchivePath
        {
            get { return Path.Combine(AppPaths.DownloadFolder, "goodbyedpi-" + Version + ".zip"); }
        }

        public static string PartialArchivePath
        {
            get { return ArchivePath + ".part"; }
        }

        public static string InstallFolder
        {
            get { return Path.Combine(AppPaths.GoodbyeDpiFolder, Version); }
        }

        public static string ExecutablePath
        {
            get
            {
                string architecture = Environment.Is64BitOperatingSystem ? "x86_64" : "x86";
                return Path.Combine(InstallFolder, architecture, "goodbyedpi.exe");
            }
        }

        public static List<GoodbyeDpiProfile> Profiles()
        {
            return new List<GoodbyeDpiProfile>
            {
                new GoodbyeDpiProfile
                {
                    Id = "turkey-standard",
                    Name = "Türkiye • Standart",
                    Description = "Resmî Türkiye paketinin önerdiği DNS yönlendirme ve TTL profili.",
                    Arguments = "-5 --set-ttl 5 --dns-addr 77.88.8.8 --dns-port 1253 --dnsv6-addr 2a02:6b8::feed:0ff --dnsv6-port 1253"
                },
                new GoodbyeDpiProfile
                {
                    Id = "superonline-alt4",
                    Name = "SuperOnline • Alternatif 4",
                    Description = "Standart yöntem çalışmazsa denenebilir. TTL değiştirmez; her bağlantıda sonuç aynı olmayabilir.",
                    Arguments = "-5 --dns-addr 77.88.8.8 --dns-port 1253 --dnsv6-addr 2a02:6b8::feed:0ff --dnsv6-port 1253",
                    Caution = true
                }
            };
        }

        public static GoodbyeDpiProfile FindProfile(string id)
        {
            return Profiles().FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
                   ?? Profiles()[0];
        }

        public static bool IsArchiveVerified(string path)
        {
            try
            {
                return File.Exists(path) &&
                       string.Equals(FileHash.Sha256(path), OfficialZipSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsInstalledAndVerified()
        {
            try
            {
                string marker = Path.Combine(InstallFolder, MarkerName);
                if (!File.Exists(marker) ||
                    !string.Equals(File.ReadAllText(marker, Encoding.ASCII).Trim(), OfficialZipSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                foreach (KeyValuePair<string, string> required in RequiredHashes)
                {
                    string path = Path.Combine(InstallFolder, required.Key);
                    if (!File.Exists(path) ||
                        !string.Equals(FileHash.Sha256(path), required.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void InstallVerifiedArchive(string archivePath)
        {
            if (!IsArchiveVerified(archivePath))
            {
                throw new InvalidDataException("GoodbyeDPI paketinin SHA-256 özeti doğrulanamadı.");
            }

            AppPaths.Ensure();
            Directory.CreateDirectory(AppPaths.GoodbyeDpiFolder);
            if (IsInstalledAndVerified()) return;

            string temporaryFolder = Path.Combine(
                AppPaths.GoodbyeDpiFolder,
                ".installing-" + Guid.NewGuid().ToString("N"));
            EnsureManagedFolder(temporaryFolder);
            Directory.CreateDirectory(temporaryFolder);

            try
            {
                ExtractSafely(archivePath, temporaryFolder);
                VerifyExtractedFiles(temporaryFolder);
                File.WriteAllText(Path.Combine(temporaryFolder, MarkerName), OfficialZipSha256, Encoding.ASCII);

                if (Directory.Exists(InstallFolder))
                {
                    string quarantine = InstallFolder + ".quarantine-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    EnsureManagedFolder(quarantine);
                    Directory.Move(InstallFolder, quarantine);
                    Logger.Info("Doğrulanamayan eski GoodbyeDPI klasörü karantinaya taşındı: " + quarantine);
                }

                Directory.Move(temporaryFolder, InstallFolder);
                Logger.Info("GoodbyeDPI-Turkey " + Version + " kuruldu ve dosyaları doğrulandı.");
            }
            catch
            {
                try
                {
                    EnsureManagedFolder(temporaryFolder);
                    if (Directory.Exists(temporaryFolder)) Directory.Delete(temporaryFolder, true);
                }
                catch
                {
                }
                throw;
            }
        }

        public static GoodbyeDpiStatus ReadStatus()
        {
            bool serviceExists = ServiceExists();
            bool managed = serviceExists && IsManagedService();
            return new GoodbyeDpiStatus
            {
                PackageVerified = IsInstalledAndVerified(),
                TemporaryProcessRunning = IsTemporaryProcessRunning(),
                ServiceInstalled = serviceExists,
                ServiceRunning = serviceExists && IsServiceRunning(),
                ManagedService = managed,
                ExternalService = serviceExists && !managed
            };
        }

        public static void Start(GoodbyeDpiProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            EnsureReady();

            if (ServiceExists())
            {
                if (!IsManagedService())
                {
                    throw new InvalidOperationException(
                        "Bilgisayarda Tercan dışında kurulmuş bir GoodbyeDPI hizmeti bulundu. Tercan bu hizmeti değiştirmeyecek.");
                }

                EnableStartup(profile);
                return;
            }

            if (IsTemporaryProcessRunning()) return;
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = ExecutablePath;
            start.Arguments = profile.Arguments;
            start.WorkingDirectory = Path.GetDirectoryName(ExecutablePath);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.WindowStyle = ProcessWindowStyle.Hidden;

            Process process = Process.Start(start);
            if (process == null)
            {
                throw new InvalidOperationException("GoodbyeDPI motoru başlatılamadı.");
            }
            if (process.WaitForExit(350))
            {
                throw new InvalidOperationException(
                    "GoodbyeDPI motoru hemen kapandı (çıkış kodu " + process.ExitCode +
                    "). Başka bir ağ filtre aracı çalışıyor olabilir.");
            }

            GoodbyeDpiSession session = new GoodbyeDpiSession
            {
                ProcessId = process.Id,
                ExecutablePath = ExecutablePath,
                ProfileId = profile.Id,
                StartedAt = DateTime.Now.ToString("o")
            };
            WriteJson(AppPaths.GoodbyeDpiSessionFile, session);
            GoodbyeDpiPreferenceStore.Save(profile.Id);
            Logger.Info("GoodbyeDPI geçici oturumu başlatıldı. Profil=" + profile.Id + ", PID=" + process.Id);
        }

        public static void Stop()
        {
            if (ServiceExists())
            {
                if (!IsManagedService())
                {
                    throw new InvalidOperationException(
                        "Tercan dışında kurulmuş GoodbyeDPI hizmetine dokunulmadı.");
                }
                StopService(ServiceName);
            }

            StopTemporaryProcess();
            CleanupManagedWinDivertServices();
            Logger.Info("Tercan tarafından yönetilen GoodbyeDPI motoru durduruldu.");
        }

        public static void EnableStartup(GoodbyeDpiProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            EnsureReady();

            if (ServiceExists() && !IsManagedService())
            {
                throw new InvalidOperationException(
                    "Aynı adlı haricî GoodbyeDPI hizmeti bulundu. Güvenlik için üzerine yazılmadı.");
            }

            StopTemporaryProcess();
            string binaryCommand = "\"" + ExecutablePath + "\" " + profile.Arguments;
            ProcessResult result;
            if (ServiceExists())
            {
                StopService(ServiceName);
                result = RunSc("config \"" + ServiceName + "\" binPath= " +
                               QuoteArgument(binaryCommand) + " start= auto");
            }
            else
            {
                result = RunSc("create \"" + ServiceName + "\" binPath= " +
                               QuoteArgument(binaryCommand) + " start= auto");
            }

            EnsureScSuccess(result, "GoodbyeDPI başlangıç hizmeti oluşturulamadı");
            RunSc("description \"" + ServiceName + "\" \"Tercan • Türkiye DPI profili\"");
            ProcessResult startResult = RunSc("start \"" + ServiceName + "\"");
            if (startResult.ExitCode != 0 && !IsServiceRunning())
            {
                EnsureScSuccess(startResult, "GoodbyeDPI hizmeti başlatılamadı");
            }

            GoodbyeDpiPreferenceStore.Save(profile.Id);
            Logger.Info("GoodbyeDPI Windows başlangıcı etkinleştirildi. Profil=" + profile.Id);
        }

        public static void DisableStartup()
        {
            if (!ServiceExists()) return;
            if (!IsManagedService())
            {
                throw new InvalidOperationException(
                    "Tercan dışında kurulmuş GoodbyeDPI hizmetine dokunulmadı.");
            }

            StopService(ServiceName);
            EnsureScSuccess(RunSc("delete \"" + ServiceName + "\""), "GoodbyeDPI hizmeti kaldırılamadı");
            CleanupManagedWinDivertServices();
            Logger.Info("GoodbyeDPI Windows başlangıcı kapatıldı.");
        }

        private static void EnsureReady()
        {
            if (!AdminGuard.IsAdministrator())
            {
                throw new InvalidOperationException("GoodbyeDPI için Tercan'ı yönetici olarak açın.");
            }
            if (!IsInstalledAndVerified())
            {
                throw new InvalidOperationException("Önce resmî GoodbyeDPI paketini indirip doğrulayın.");
            }
        }

        private static void ExtractSafely(string archivePath, string destination)
        {
            string root = Path.GetFullPath(destination).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string normalizedName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    string target = Path.GetFullPath(Path.Combine(destination, normalizedName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Paket güvenli klasörün dışına dosya yazmaya çalıştı.");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(output);
                    }
                }
            }
        }

        private static void VerifyExtractedFiles(string folder)
        {
            foreach (KeyValuePair<string, string> required in RequiredHashes)
            {
                string path = Path.Combine(folder, required.Key);
                if (!File.Exists(path))
                {
                    throw new InvalidDataException("Paket içinde gerekli dosya yok: " + required.Key);
                }
                if (!string.Equals(FileHash.Sha256(path), required.Value, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Paket dosyası doğrulanamadı: " + required.Key);
                }
            }
        }

        private static bool ServiceExists()
        {
            try
            {
                using (ServiceController controller = new ServiceController(ServiceName))
                {
                    ServiceControllerStatus ignored = controller.Status;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsServiceRunning()
        {
            try
            {
                using (ServiceController controller = new ServiceController(ServiceName))
                {
                    return controller.Status == ServiceControllerStatus.Running ||
                           controller.Status == ServiceControllerStatus.StartPending;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool IsManagedService()
        {
            return IsManagedImagePath(ReadServiceImagePath(ServiceName));
        }

        private static bool IsManagedImagePath(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return false;
            string normalized = imagePath.Replace(@"\??\", string.Empty).Trim('"');
            string root = Path.GetFullPath(AppPaths.GoodbyeDpiFolder).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return normalized.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReadServiceImagePath(string serviceName)
        {
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (RegistryKey key = root.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, false))
                {
                    return key == null ? null : Convert.ToString(
                        key.GetValue("ImagePath", null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool IsTemporaryProcessRunning()
        {
            GoodbyeDpiSession session = ReadSession();
            if (session == null) return false;
            try
            {
                Process process = Process.GetProcessById(session.ProcessId);
                if (process.HasExited || !IsExpectedProcess(process, session.ExecutablePath))
                {
                    DeleteSession();
                    return false;
                }
                return true;
            }
            catch
            {
                DeleteSession();
                return false;
            }
        }

        private static void StopTemporaryProcess()
        {
            GoodbyeDpiSession session = ReadSession();
            if (session == null) return;
            try
            {
                Process process = Process.GetProcessById(session.ProcessId);
                if (!process.HasExited && IsExpectedProcess(process, session.ExecutablePath))
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch (ArgumentException)
            {
            }
            finally
            {
                DeleteSession();
            }
        }

        private static bool IsExpectedProcess(Process process, string expectedPath)
        {
            if (process == null || string.IsNullOrWhiteSpace(expectedPath)) return false;
            try
            {
                return string.Equals(
                    Path.GetFullPath(process.MainModule.FileName),
                    Path.GetFullPath(expectedPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static GoodbyeDpiSession ReadSession()
        {
            try
            {
                if (!File.Exists(AppPaths.GoodbyeDpiSessionFile)) return null;
                return new JavaScriptSerializer().Deserialize<GoodbyeDpiSession>(
                    File.ReadAllText(AppPaths.GoodbyeDpiSessionFile, Encoding.UTF8));
            }
            catch
            {
                return null;
            }
        }

        private static void DeleteSession()
        {
            try
            {
                if (File.Exists(AppPaths.GoodbyeDpiSessionFile)) File.Delete(AppPaths.GoodbyeDpiSessionFile);
            }
            catch
            {
            }
        }

        private static void StopService(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) return;
            ProcessResult result = RunSc("stop \"" + serviceName + "\"");
            if (result.ExitCode != 0)
            {
                try
                {
                    using (ServiceController controller = new ServiceController(serviceName))
                    {
                        if (controller.Status != ServiceControllerStatus.Stopped &&
                            controller.Status != ServiceControllerStatus.StopPending)
                        {
                            throw new InvalidOperationException("Hizmet durdurulamadı: " + serviceName);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch
                {
                }
            }
        }

        private static void CleanupManagedWinDivertServices()
        {
            foreach (string serviceName in new[] { "WinDivert", "WinDivert14" })
            {
                string imagePath = ReadServiceImagePath(serviceName);
                if (!IsManagedImagePath(imagePath)) continue;
                RunSc("stop \"" + serviceName + "\"");
                RunSc("delete \"" + serviceName + "\"");
            }
        }

        private static ProcessResult RunSc(string arguments)
        {
            return ProcessRunner.Run("sc.exe", arguments, 15000);
        }

        private static void EnsureScSuccess(ProcessResult result, string message)
        {
            if (result == null || result.ExitCode != 0)
            {
                string detail = result == null
                    ? string.Empty
                    : (result.Error + " " + result.Output).Trim();
                throw new InvalidOperationException(message +
                    (string.IsNullOrWhiteSpace(detail) ? "." : ": " + detail));
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static void EnsureManagedFolder(string path)
        {
            string root = Path.GetFullPath(AppPaths.GoodbyeDpiFolder).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("GoodbyeDPI klasör sınırı aşıldı.");
            }
        }

        private static void WriteJson<T>(string path, T document)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(document), Encoding.UTF8);
        }
    }

    internal static class GoodbyeDpiPreferenceStore
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static GoodbyeDpiPreferences Load()
        {
            try
            {
                if (!File.Exists(AppPaths.GoodbyeDpiSettingsFile))
                {
                    return new GoodbyeDpiPreferences { ProfileId = "turkey-standard" };
                }
                GoodbyeDpiPreferences loaded = Serializer.Deserialize<GoodbyeDpiPreferences>(
                    File.ReadAllText(AppPaths.GoodbyeDpiSettingsFile, Encoding.UTF8));
                if (loaded == null) loaded = new GoodbyeDpiPreferences();
                loaded.ProfileId = GoodbyeDpiIntegration.FindProfile(loaded.ProfileId).Id;
                return loaded;
            }
            catch
            {
                return new GoodbyeDpiPreferences { ProfileId = "turkey-standard" };
            }
        }

        public static void Save(string profileId)
        {
            try
            {
                GoodbyeDpiPreferences document = new GoodbyeDpiPreferences
                {
                    ProfileId = GoodbyeDpiIntegration.FindProfile(profileId).Id
                };
                File.WriteAllText(
                    AppPaths.GoodbyeDpiSettingsFile,
                    Serializer.Serialize(document),
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.Error("GoodbyeDPI tercihleri kaydedilemedi", ex);
            }
        }
    }

    internal static class TercanStartupManager
    {
        private const string TaskName = "Tercan Optimizer";

        public static bool IsEnabled()
        {
            try
            {
                return ProcessRunner.Run(
                    "schtasks.exe",
                    "/Query /TN \"" + TaskName + "\"",
                    10000).ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            if (!AdminGuard.IsAdministrator())
            {
                throw new InvalidOperationException("Başlangıç ayarı için Tercan'ı yönetici olarak açın.");
            }

            if (!enabled)
            {
                if (!IsEnabled()) return;
                ProcessResult remove = ProcessRunner.Run(
                    "schtasks.exe",
                    "/Delete /TN \"" + TaskName + "\" /F",
                    15000);
                if (remove.ExitCode != 0)
                {
                    throw new InvalidOperationException("Tercan başlangıç görevi kaldırılamadı.");
                }
                Logger.Info("Tercan Windows başlangıcı kapatıldı.");
                return;
            }

            string executable = Process.GetCurrentProcess().MainModule.FileName;
            string taskCommand = "\"" + executable + "\" --background";
            ProcessResult create = ProcessRunner.Run(
                "schtasks.exe",
                "/Create /TN \"" + TaskName + "\" /TR " +
                QuoteForSchtasks(taskCommand) +
                " /SC ONLOGON /RL HIGHEST /F",
                20000);
            if (create.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "Tercan başlangıç görevi oluşturulamadı: " +
                    (create.Error + " " + create.Output).Trim());
            }
            Logger.Info("Tercan düşük yükte Windows başlangıcına eklendi.");
        }

        private static string QuoteForSchtasks(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}

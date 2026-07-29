using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace TercanOptimizer
{
    internal sealed class GitHubReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }

    internal sealed class GitHubReleaseDocument
    {
        public string tag_name { get; set; }
        public string html_url { get; set; }
        public List<GitHubReleaseAsset> assets { get; set; }
    }

    internal sealed class UpdateManifest
    {
        public string Version { get; set; }
        public string SetupUrl { get; set; }
        public string SetupSha256 { get; set; }
        public string ReleaseUrl { get; set; }
        public string Notes { get; set; }
        public string PublishedAt { get; set; }
    }

    internal sealed class UpdateCheckResult
    {
        public Version CurrentVersion { get; set; }
        public Version LatestVersion { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public UpdateManifest Manifest { get; set; }
        public string Message { get; set; }
    }

    internal static class TercanUpdateService
    {
        public const string Repository = "Tercan24/Can";
        public const string LatestReleaseApi =
            "https://api.github.com/repos/Tercan24/Can/releases/latest";
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static Version CurrentVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version ??
                    new Version(0, 0, 0, 0);
            }
        }

        public static UpdateCheckResult CheckLatest()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (WebClient client = CreateClient())
            {
                GitHubReleaseDocument release;
                try
                {
                    release = Serializer.Deserialize<GitHubReleaseDocument>(
                        client.DownloadString(LatestReleaseApi));
                }
                catch (WebException ex)
                {
                    throw new InvalidOperationException(
                        "GitHub yayın bilgisine ulaşılamadı. Depo özel durumdaysa güncelleme " +
                        "dosyaları son kullanıcılara açılamaz; yayın deposunu herkese açık yapın.",
                        ex);
                }

                GitHubReleaseAsset manifestAsset = (release.assets ??
                    new List<GitHubReleaseAsset>()).FirstOrDefault(
                        x => string.Equals(x.name, "update.json", StringComparison.OrdinalIgnoreCase));
                if (manifestAsset == null || string.IsNullOrWhiteSpace(manifestAsset.browser_download_url))
                {
                    throw new InvalidOperationException(
                        "Son GitHub yayınında update.json bulunamadı.");
                }

                UpdateManifest manifest = Serializer.Deserialize<UpdateManifest>(
                    client.DownloadString(manifestAsset.browser_download_url));
                ValidateManifest(manifest);

                Version latest;
                if (!Version.TryParse(manifest.Version, out latest))
                {
                    throw new InvalidOperationException("Güncelleme sürüm numarası geçersiz.");
                }

                Version current = CurrentVersion;
                bool available = latest > current;
                return new UpdateCheckResult
                {
                    CurrentVersion = current,
                    LatestVersion = latest,
                    IsUpdateAvailable = available,
                    Manifest = manifest,
                    Message = available
                        ? "tercan.exe " + latest + " indirilmeye hazır."
                        : "En güncel sürümü kullanıyorsunuz."
                };
            }
        }

        public static string DownloadVerifiedSetup(UpdateManifest manifest)
        {
            ValidateManifest(manifest);
            string target = Path.Combine(
                Path.GetTempPath(),
                "tercan-setup-" + manifest.Version + ".exe");
            if (File.Exists(target)) File.Delete(target);

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (WebClient client = CreateClient())
            {
                client.DownloadFile(manifest.SetupUrl, target);
            }

            string actual = ComputeSha256(target);
            if (!string.Equals(actual, manifest.SetupSha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(target); }
                catch { }
                throw new InvalidOperationException(
                    "İndirilen kurulum dosyasının SHA-256 doğrulaması başarısız oldu.");
            }
            return target;
        }

        public static void LaunchInstaller(string setupPath)
        {
            if (string.IsNullOrWhiteSpace(setupPath) || !File.Exists(setupPath))
            {
                throw new FileNotFoundException("Güncelleme kurulum dosyası bulunamadı.");
            }
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = setupPath;
            start.Arguments = "/update /waitpid " + Process.GetCurrentProcess().Id;
            start.UseShellExecute = true;
            start.Verb = "runas";
            Process.Start(start);
        }

        internal static void ValidateManifestForTest(UpdateManifest manifest)
        {
            ValidateManifest(manifest);
        }

        private static WebClient CreateClient()
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "tercan.exe/" + CurrentVersion;
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return client;
        }

        private static void ValidateManifest(UpdateManifest manifest)
        {
            if (manifest == null)
            {
                throw new InvalidOperationException("Güncelleme dosyası okunamadı.");
            }
            if (!Regex.IsMatch(
                manifest.SetupSha256 ?? string.Empty,
                @"\A[A-Fa-f0-9]{64}\z"))
            {
                throw new InvalidOperationException("Güncelleme SHA-256 değeri geçersiz.");
            }
            Uri setupUri;
            if (!Uri.TryCreate(manifest.SetupUrl, UriKind.Absolute, out setupUri) ||
                setupUri.Scheme != Uri.UriSchemeHttps ||
                !string.Equals(setupUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                setupUri.AbsolutePath.IndexOf(
                    "/Tercan24/Can/releases/download/",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                throw new InvalidOperationException("Güncelleme adresi güvenilir GitHub yayını değil.");
            }
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }
}

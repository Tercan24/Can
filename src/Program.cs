using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace TercanOptimizer
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                return SelfTest.Run();
            }

            string renderArgument = args.FirstOrDefault(a => a.StartsWith("--render-preview=", StringComparison.OrdinalIgnoreCase));
            if (renderArgument != null)
            {
                string outputPath = renderArgument.Substring("--render-preview=".Length).Trim('"');
                string pageArgument = args.FirstOrDefault(a => a.StartsWith("--preview-page=", StringComparison.OrdinalIgnoreCase));
                string page = pageArgument == null
                    ? "scanner"
                    : pageArgument.Substring("--preview-page=".Length).Trim('"');
                return PreviewRenderer.Render(outputPath, page);
            }

            if (!SystemProbe.IsSupportedWindows())
            {
                MessageBox.Show(
                    "tercan.exe yalnızca Windows 10 ve Windows 11 için tasarlanmıştır.",
                    "Desteklenmeyen işletim sistemi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return 2;
            }

            bool backgroundStart = args.Any(a =>
                string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));
            bool createdNew;
            using (Mutex singleInstance = new Mutex(true, @"Local\TercanOptimizer", out createdNew))
            {
                if (!createdNew)
                {
                    if (!backgroundStart)
                    {
                        MessageBox.Show(
                            "tercan.exe zaten çalışıyor. Bildirim alanındaki T simgesinden açabilirsiniz.",
                            "tercan.exe",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    return 0;
                }

                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Logger.Error("Arayüz hatası yakalandı", e.Exception);
                    MessageBox.Show(
                        "Beklenmeyen bir hata güvenli biçimde durduruldu. Ayrıntı günlük dosyasına kaydedildi.",
                        "tercan.exe",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Exception exception = e.ExceptionObject as Exception;
                    Logger.Error("İşlenmeyen uygulama hatası", exception ?? new Exception("Bilinmeyen hata"));
                };

                if (!backgroundStart)
                {
                    using (BrandSplashForm splash = new BrandSplashForm())
                    {
                        splash.ShowDialog();
                    }
                }

                Application.Run(new MainForm(false, backgroundStart));
                return 0;
            }
        }
    }

    internal static class AppPaths
    {
        public static readonly string Root = ResolveRoot();

        public static readonly string BackupFile = Path.Combine(Root, "backups", "state.json");
        public static readonly string LogFile = Path.Combine(Root, "logs", "tercan.log");
        public static readonly string DownloadFolder = Path.Combine(Root, "downloads");
        public static readonly string ComponentsFolder = Path.Combine(Root, "components");
        public static readonly string GoodbyeDpiFolder = Path.Combine(ComponentsFolder, "goodbyedpi");
        public static readonly string LocalSettingsFile = Path.Combine(Root, "settings.json");
        public static readonly string GoodbyeDpiSettingsFile = Path.Combine(Root, "goodbyedpi-settings.json");
        public static readonly string GoodbyeDpiSessionFile = Path.Combine(Root, "goodbyedpi-session.json");
        public static readonly string FocusSessionFile = Path.Combine(Root, "focus-session.json");
        public static readonly string StartupBackupFile = Path.Combine(Root, "backups", "startup.json");
        public static readonly string NetworkBackupFile = Path.Combine(Root, "backups", "network.json");
        public static readonly string HostsBackupFolder = Path.Combine(Root, "backups", "hosts");

        private static string ResolveRoot()
        {
            string developmentRoot = Environment.GetEnvironmentVariable("TERCAN_DATA_ROOT");
            if (!string.IsNullOrWhiteSpace(developmentRoot))
            {
                return Path.GetFullPath(developmentRoot);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Tercan");
        }

        public static string ApplicationFolder
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string ModulesFolder
        {
            get { return Path.Combine(ApplicationFolder, "Modules"); }
        }

        public static void Ensure()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Path.GetDirectoryName(BackupFile));
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile));
            Directory.CreateDirectory(DownloadFolder);
            Directory.CreateDirectory(ComponentsFolder);
            Directory.CreateDirectory(GoodbyeDpiFolder);
            Directory.CreateDirectory(HostsBackupFolder);
            Directory.CreateDirectory(ModulesFolder);
        }
    }

    internal static class AppTheme
    {
        public static readonly Color Window = Color.FromArgb(5, 6, 9);
        public static readonly Color Sidebar = Color.FromArgb(8, 9, 18);
        public static readonly Color Surface = Color.FromArgb(17, 19, 29);
        public static readonly Color SurfaceRaised = Color.FromArgb(23, 26, 39);
        public static readonly Color Border = Color.FromArgb(44, 47, 65);
        public static readonly Color Accent = Color.FromArgb(155, 120, 255);
        public static readonly Color AccentSoft = Color.FromArgb(52, 41, 83);
        public static readonly Color Cyan = Color.FromArgb(95, 183, 255);
        public static readonly Color Green = Color.FromArgb(66, 227, 155);
        public static readonly Color Amber = Color.FromArgb(255, 178, 62);
        public static readonly Color Red = Color.FromArgb(255, 77, 97);
        public static readonly Color Text = Color.FromArgb(247, 247, 251);
        public static readonly Color TextMuted = Color.FromArgb(146, 150, 170);
        public static readonly Font Heading = new Font("Segoe UI Semibold", 22f, FontStyle.Bold);
        public static readonly Font Subheading = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
        public static readonly Font Body = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font Small = new Font("Segoe UI", 8.5f, FontStyle.Regular);
    }

    internal static class AdminGuard
    {
        public static bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class PreviewRenderer
    {
        public static int Render(string outputPath, string page)
        {
            try
            {
                AppPaths.Ensure();
                using (MainForm form = new MainForm(true))
                {
                    form.Show();
                    Application.DoEvents();
                    form.NavigatePreview(page);
                    Application.DoEvents();
                    form.Refresh();
                    using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    form.Hide();
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}

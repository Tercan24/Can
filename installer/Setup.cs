using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("tercan.exe Kurulumu")]
[assembly: AssemblyDescription("tercan.exe hızlı kurulum ve güncelleme programı")]
[assembly: AssemblyCompany("Tercan")]
[assembly: AssemblyProduct("tercan.exe Setup")]
[assembly: AssemblyVersion("1.8.0.0")]
[assembly: AssemblyFileVersion("1.8.0.0")]

namespace TercanSetup
{
    internal static class SetupProgram
    {
        internal const string ProductVersion = "1.8.0.0";
        internal const string ResourceApp = "Tercan.Setup.tercan.exe";
        internal const string ResourceModule = "Tercan.Setup.menu-delay.json";
        internal const string ResourceBrand = "Tercan.Setup.brand.png";

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string renderArgument = args.FirstOrDefault(
                x => x.StartsWith("/render-preview=", StringComparison.OrdinalIgnoreCase));
            if (renderArgument != null)
            {
                return RenderPreview(renderArgument.Substring("/render-preview=".Length).Trim('"'));
            }
            if (args.Any(x => string.Equals(x, "/uninstall-worker", StringComparison.OrdinalIgnoreCase)))
            {
                return RunUninstallWorker(args);
            }
            if (args.Any(x => string.Equals(x, "/uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                return BeginUninstall();
            }

            int waitPid = ReadIntegerArgument(args, "/waitpid");
            if (waitPid > 0) WaitForProcess(waitPid);
            bool update = args.Any(x => string.Equals(x, "/update", StringComparison.OrdinalIgnoreCase));
            if (update)
            {
                try
                {
                    Installer.Install(null, null);
                    Installer.StartInstalledApplication();
                    return 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.Message,
                        "tercan.exe güncellenemedi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 1;
                }
            }

            Application.Run(new SetupForm());
            return 0;
        }

        private static int RenderPreview(string outputPath)
        {
            try
            {
                using (SetupForm form = new SetupForm())
                {
                    form.Show();
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

        private static int BeginUninstall()
        {
            DialogResult answer = MessageBox.Show(
                "tercan.exe ve kısayolları bilgisayardan kaldırılacak.\n\n" +
                "Tercan'ın ProgramData altındaki geri alma ve günlük dosyaları korunacaktır. Devam edilsin mi?",
                "tercan.exe kaldır",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return 0;

            string temporary = Path.Combine(Path.GetTempPath(), "tercan-uninstall.exe");
            File.Copy(Application.ExecutablePath, temporary, true);
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = temporary;
            start.Arguments = "/uninstall-worker \"" + Installer.InstallFolder + "\" /waitpid " +
                Process.GetCurrentProcess().Id;
            start.UseShellExecute = true;
            start.Verb = "runas";
            Process.Start(start);
            return 0;
        }

        private static int RunUninstallWorker(string[] args)
        {
            int waitPid = ReadIntegerArgument(args, "/waitpid");
            if (waitPid > 0) WaitForProcess(waitPid);
            string target = ReadStringArgument(args, "/uninstall-worker");
            try
            {
                Installer.Uninstall(target);
                MessageBox.Show(
                    "tercan.exe bilgisayardan kaldırıldı.",
                    "Kaldırma tamamlandı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Kaldırma başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static int ReadIntegerArgument(string[] args, string name)
        {
            int index = Array.FindIndex(args, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            int value;
            return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out value)
                ? value
                : 0;
        }

        private static string ReadStringArgument(string[] args, string name)
        {
            int index = Array.FindIndex(args, x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : string.Empty;
        }

        private static void WaitForProcess(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    process.WaitForExit(30000);
                }
            }
            catch
            {
            }
        }
    }

    internal static class Installer
    {
        public static readonly string InstallFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tercan");
        private static readonly string ApplicationPath = Path.Combine(InstallFolder, "tercan.exe");
        private static readonly string UninstallerPath = Path.Combine(InstallFolder, "uninstall.exe");
        private static readonly string ModuleFolder = Path.Combine(InstallFolder, "Modules");
        private static readonly string ModulePath = Path.Combine(ModuleFolder, "menu-delay.json");
        private static readonly string StartMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            "Tercan");
        private static readonly string StartMenuShortcut = Path.Combine(StartMenuFolder, "tercan.exe.lnk");
        private static readonly string DesktopShortcut = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            "tercan.exe.lnk");
        private const string UninstallRegistry =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TercanOptimizer";

        public static void Install(bool? createDesktopShortcut, Action<int, string> progress)
        {
            StopInstalledApplication();
            Report(progress, 8, "Kurulum klasörü hazırlanıyor…");
            Directory.CreateDirectory(InstallFolder);
            Directory.CreateDirectory(ModuleFolder);

            Report(progress, 24, "tercan.exe doğrulanmış paketten çıkarılıyor…");
            WriteEmbeddedFile(SetupProgram.ResourceApp, ApplicationPath);
            Report(progress, 48, "Tercan modülleri kuruluyor…");
            WriteEmbeddedFile(SetupProgram.ResourceModule, ModulePath);
            File.Copy(Application.ExecutablePath, UninstallerPath, true);

            Report(progress, 68, "Başlat menüsü kısayolu oluşturuluyor…");
            Directory.CreateDirectory(StartMenuFolder);
            CreateShortcut(StartMenuShortcut, ApplicationPath, InstallFolder);
            if (createDesktopShortcut == true)
            {
                CreateShortcut(DesktopShortcut, ApplicationPath, InstallFolder);
            }
            else if (createDesktopShortcut == false)
            {
                TryDelete(DesktopShortcut);
            }

            Report(progress, 84, "Windows kaldırma kaydı oluşturuluyor…");
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(UninstallRegistry))
            {
                key.SetValue("DisplayName", "tercan.exe", RegistryValueKind.String);
                key.SetValue("DisplayVersion", SetupProgram.ProductVersion, RegistryValueKind.String);
                key.SetValue("Publisher", "Tercan", RegistryValueKind.String);
                key.SetValue("InstallLocation", InstallFolder, RegistryValueKind.String);
                key.SetValue("DisplayIcon", ApplicationPath, RegistryValueKind.String);
                key.SetValue("UninstallString", "\"" + UninstallerPath + "\" /uninstall", RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            Report(progress, 100, "Kurulum tamamlandı.");
        }

        public static void StartInstalledApplication()
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = ApplicationPath;
            start.UseShellExecute = true;
            Process.Start(start);
        }

        public static void Uninstall(string requestedFolder)
        {
            string expected = Path.GetFullPath(InstallFolder).TrimEnd('\\');
            string actual = Path.GetFullPath(requestedFolder ?? string.Empty).TrimEnd('\\');
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Güvenlik denetimi nedeniyle kaldırma hedefi reddedildi.");
            }

            StopInstalledApplication();
            TryDelete(StartMenuShortcut);
            TryDelete(DesktopShortcut);
            try
            {
                if (Directory.Exists(StartMenuFolder) &&
                    Directory.GetFileSystemEntries(StartMenuFolder).Length == 0)
                {
                    Directory.Delete(StartMenuFolder);
                }
            }
            catch
            {
            }
            try { Registry.LocalMachine.DeleteSubKeyTree(UninstallRegistry, false); }
            catch { }

            if (Directory.Exists(actual)) Directory.Delete(actual, true);
        }

        private static void StopInstalledApplication()
        {
            foreach (Process process in Process.GetProcessesByName("tercan"))
            {
                try
                {
                    string executable;
                    try { executable = process.MainModule.FileName; }
                    catch { continue; }
                    if (!string.Equals(
                        Path.GetFullPath(executable),
                        Path.GetFullPath(ApplicationPath),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool requestedClose = false;
                    try { requestedClose = process.CloseMainWindow(); }
                    catch { }
                    if (requestedClose)
                    {
                        try { process.WaitForExit(4000); }
                        catch { }
                    }
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static void WriteEmbeddedFile(string resourceName, string target)
        {
            string temporary = target + ".new";
            TryDelete(temporary);
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("Kurulum paketi eksik: " + resourceName);
                using (FileStream output = File.Create(temporary))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
            }
            TryDelete(target);
            File.Move(temporary, target);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("Windows kısayol hizmeti bulunamadı.");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = null;
            try
            {
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember(
                    "TargetPath",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { targetPath });
                shortcutType.InvokeMember(
                    "WorkingDirectory",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { workingDirectory });
                shortcutType.InvokeMember(
                    "IconLocation",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { targetPath + ",0" });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
                if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }

        private static void Report(Action<int, string> progress, int percent, string status)
        {
            if (progress != null) progress(percent, status);
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly Button installButton;
        private readonly CheckBox desktopShortcut;
        private readonly ProgressBar progress;
        private readonly Label status;

        public SetupForm()
        {
            Text = "tercan.exe Kurulumu";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(700, 430);
            BackColor = Color.FromArgb(7, 8, 14);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            Panel accent = new Panel();
            accent.Dock = DockStyle.Top;
            accent.Height = 5;
            accent.BackColor = Color.FromArgb(155, 120, 255);
            Controls.Add(accent);

            PictureBox logo = new PictureBox();
            logo.Size = new Size(104, 104);
            logo.Location = new Point(34, 32);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(SetupProgram.ResourceBrand))
            {
                if (stream != null)
                {
                    using (Image embedded = Image.FromStream(stream))
                    {
                        logo.Image = new Bitmap(embedded);
                    }
                }
            }
            Controls.Add(logo);

            Label title = NewLabel(
                "tercan.exe",
                new Font("Segoe UI Semibold", 25f, FontStyle.Bold),
                Color.White,
                new Point(158, 40));
            Controls.Add(title);
            Label version = NewLabel(
                "Windows 10 / 11 Performans Merkezi • " + SetupProgram.ProductVersion,
                new Font("Segoe UI", 10f),
                Color.FromArgb(145, 154, 184),
                new Point(161, 88));
            Controls.Add(version);

            Panel card = new Panel();
            card.Location = new Point(32, 158);
            card.Size = new Size(636, 178);
            card.BackColor = Color.FromArgb(18, 20, 31);
            Controls.Add(card);
            Label installTitle = NewLabel(
                "Hızlı kurulum",
                new Font("Segoe UI Semibold", 15f, FontStyle.Bold),
                Color.White,
                new Point(22, 18));
            card.Controls.Add(installTitle);
            Label copy = NewLabel(
                "Uygulama Program Files\\Tercan konumuna kurulur. Başlat menüsü kısayolu ve güvenli kaldırma kaydı otomatik oluşturulur.",
                new Font("Segoe UI", 9.5f),
                Color.FromArgb(166, 172, 196),
                new Point(24, 54));
            copy.AutoSize = false;
            copy.Size = new Size(585, 46);
            card.Controls.Add(copy);
            desktopShortcut = new CheckBox();
            desktopShortcut.Text = "Masaüstü kısayolu oluştur";
            desktopShortcut.Checked = true;
            desktopShortcut.AutoSize = true;
            desktopShortcut.Location = new Point(24, 112);
            desktopShortcut.ForeColor = Color.White;
            desktopShortcut.BackColor = Color.Transparent;
            card.Controls.Add(desktopShortcut);

            progress = new ProgressBar();
            progress.Location = new Point(32, 354);
            progress.Size = new Size(470, 9);
            progress.Style = ProgressBarStyle.Continuous;
            Controls.Add(progress);
            status = NewLabel(
                "Kuruluma hazır.",
                new Font("Segoe UI", 9f),
                Color.FromArgb(145, 154, 184),
                new Point(32, 374));
            Controls.Add(status);

            installButton = new Button();
            installButton.Text = "Şimdi kur";
            installButton.Location = new Point(526, 348);
            installButton.Size = new Size(142, 44);
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.FlatAppearance.BorderSize = 0;
            installButton.BackColor = Color.FromArgb(155, 120, 255);
            installButton.ForeColor = Color.White;
            installButton.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
            installButton.Cursor = Cursors.Hand;
            installButton.Click += delegate { BeginInstall(); };
            Controls.Add(installButton);
        }

        private void BeginInstall()
        {
            installButton.Enabled = false;
            desktopShortcut.Enabled = false;
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                Installer.Install(
                    (bool?)desktopShortcut.Checked,
                    delegate(int value, string message)
                    {
                        worker.ReportProgress(value, message);
                        Thread.Sleep(110);
                    });
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                progress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                status.Text = Convert.ToString(e.UserState);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                if (e.Error != null)
                {
                    installButton.Enabled = true;
                    desktopShortcut.Enabled = true;
                    status.Text = "Kurulum tamamlanamadı.";
                    MessageBox.Show(e.Error.Message, "Kurulum başarısız", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                progress.Value = 100;
                status.Text = "Kurulum tamamlandı.";
                DialogResult launch = MessageBox.Show(
                    "tercan.exe başarıyla kuruldu.\n\nUygulama şimdi açılsın mı?",
                    "Kurulum tamamlandı",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (launch == DialogResult.Yes) Installer.StartInstalledApplication();
                Close();
            };
            worker.RunWorkerAsync();
        }

        private static Label NewLabel(string text, Font font, Color color, Point location)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.Location = location;
            label.AutoSize = true;
            return label;
        }
    }
}

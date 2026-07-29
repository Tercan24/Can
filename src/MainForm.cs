using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TercanOptimizer
{
    internal sealed partial class MainForm : Form
    {
        private sealed class PackageInstallUpdate
        {
            public SoftwarePackageDefinition Package { get; set; }
            public ProcessResult Result { get; set; }
            public int Index { get; set; }
            public int Total { get; set; }
        }

        private sealed class FocusDisplayItem
        {
            public FocusProcessDefinition Definition { get; set; }
            public int ProcessCount { get; set; }
            public long WorkingSetBytes { get; set; }

            public override string ToString()
            {
                return Definition.Name + "  •  " + ProcessCount + " süreç  •  " +
                       (WorkingSetBytes / 1024d / 1024d).ToString("0") + " MB";
            }
        }

        private sealed class ProcessChoice
        {
            public int ProcessId { get; set; }
            public string Name { get; set; }
            public string WindowTitle { get; set; }

            public override string ToString()
            {
                if (ProcessId == 0) return "Oyun seçilmedi";
                return Name + (string.IsNullOrWhiteSpace(WindowTitle) ? string.Empty : " — " + WindowTitle);
            }
        }

        private const int EmSetCueBanner = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, string text);

        private readonly bool previewMode;
        private readonly bool backgroundStartMode;
        private readonly BackupStore backupStore;
        private readonly TweakEngine engine;
        private List<TweakDefinition> tweaks;
        private readonly Dictionary<string, bool> pending;
        private readonly Dictionary<string, TweakCard> visibleCards;
        private readonly List<Button> navigationButtons;
        private readonly SystemInfoSnapshot systemInfo;
        private readonly List<SoftwarePackageDefinition> softwarePackages;
        private readonly Dictionary<string, bool> softwareSelection;
        private readonly List<FocusProcessDefinition> focusDefinitions;
        private readonly FocusModeEngine focusEngine;

        private Panel mainArea;
        private Panel content;
        private Label pageTitle;
        private Label pageDescription;
        private Panel applyBar;
        private Label pendingLabel;
        private Button applyButton;
        private string currentPage;
        private string currentCategory;
        private Timer pageRevealTimer;
        private int pageRevealFrame;

        private Timer memoryTimer;
        private bool memoryCleanerActive;
        private Label memoryAvailableLabel;
        private Label memoryStandbyLabel;
        private Label memoryCleanerStatus;
        private ProgressBar memoryUsageBar;
        private NumericUpDown freeMemoryThreshold;
        private NumericUpDown standbyThreshold;
        private Button memoryStartButton;
        private Label islcStatus;
        private ProgressBar islcProgress;
        private Button islcDownloadButton;
        private Button islcRunButton;
        private NotifyIcon trayIcon;
        private decimal memoryFreeThresholdValue;
        private decimal memoryStandbyThresholdValue;
        private Label softwareSelectionLabel;
        private bool installationRunning;
        private long lastFocusReleasedBytes;
        private int lastFocusClosedCount;
        private int lastFocusStoppedServiceCount;
        private int lastFocusAppliedSettingCount;
        private bool quickGameModeBusy;

        public MainForm()
            : this(false, false)
        {
        }

        public MainForm(bool previewMode)
            : this(previewMode, false)
        {
        }

        public MainForm(bool previewMode, bool backgroundStartMode)
        {
            this.previewMode = previewMode;
            this.backgroundStartMode = backgroundStartMode;
            UiMotion.AnimationsEnabled = !previewMode;
            AppPaths.Ensure();
            backupStore = new BackupStore();
            engine = new TweakEngine(backupStore);
            tweaks = TweakCatalog.Build();
            pending = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            visibleCards = new Dictionary<string, TweakCard>(StringComparer.OrdinalIgnoreCase);
            navigationButtons = new List<Button>();
            systemInfo = SystemProbe.Read();
            softwarePackages = SoftwareCatalog.Build();
            softwareSelection = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            focusDefinitions = FocusProcessCatalog.Build();
            focusEngine = new FocusModeEngine();
            if (!previewMode)
            {
                focusEngine.RecoverStaleSession();
            }
            LocalSettingsDocument localSettings = LocalSettingsStore.Load(DefaultFreeThreshold());
            memoryFreeThresholdValue = localSettings.MemoryFreeThresholdMb;
            memoryStandbyThresholdValue = localSettings.MemoryStandbyThresholdMb;

            Text = "tercan.exe • Windows Oyun Optimizasyonu";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1360, 860);
            MinimumSize = new Size(1120, 720);
            BackColor = AppTheme.Window;
            ForeColor = AppTheme.Text;
            Font = AppTheme.Body;
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = CreateAppIcon();

            BuildShell();
            Navigate("scanner", null);
            BuildTrayIcon();
            ScheduleAutomaticUpdateCheck();

            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;
            Shown += delegate
            {
                if (this.backgroundStartMode && !this.previewMode)
                {
                    try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
                    Hide();
                    trayIcon.Visible = true;
                }
            };
        }

        internal void NavigatePreview(string page)
        {
            if (string.Equals(page, "oneclick-busy", StringComparison.OrdinalIgnoreCase))
            {
                PrepareOneClickBusyPreview();
                Navigate("scanner", null);
                FillOneClickBusyPreview();
                return;
            }

            if (string.Equals(page, "oneclick-results", StringComparison.OrdinalIgnoreCase))
            {
                PrepareOneClickPreview();
                Navigate("scanner", null);
                return;
            }

            if (string.IsNullOrWhiteSpace(page) || page == "dashboard")
            {
                Navigate("dashboard", null);
                return;
            }

            if (page.StartsWith("tweaks:", StringComparison.OrdinalIgnoreCase))
            {
                Navigate("tweaks", page.Substring("tweaks:".Length));
                return;
            }

            Navigate(page, null);
        }

        private void BuildShell()
        {
            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.Margin = Padding.Empty;
            shell.Padding = Padding.Empty;
            shell.ColumnCount = 2;
            shell.RowCount = 1;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 202f));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(shell);

            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.Margin = Padding.Empty;
            sidebar.BackColor = AppTheme.Sidebar;
            shell.Controls.Add(sidebar, 0, 0);

            BuildSidebar(sidebar);

            mainArea = new Panel();
            mainArea.Dock = DockStyle.Fill;
            mainArea.Margin = Padding.Empty;
            mainArea.BackColor = AppTheme.Window;
            shell.Controls.Add(mainArea, 1, 0);

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 96;
            header.BackColor = AppTheme.Window;
            header.Padding = new Padding(28, 17, 28, 10);
            mainArea.Controls.Add(header);

            pageTitle = UiFactory.Label("Genel Bakış", AppTheme.Heading, AppTheme.Text);
            pageTitle.Location = new Point(29, 18);
            header.Controls.Add(pageTitle);

            pageDescription = UiFactory.Label("Sisteminizin oyun hazırlığını görün.", AppTheme.Body, AppTheme.TextMuted);
            pageDescription.Location = new Point(31, 61);
            header.Controls.Add(pageDescription);

            Label adminBadge = UiFactory.Pill(
                AdminGuard.IsAdministrator() ? "YÖNETİCİ MODU" : "SINIRLI MOD",
                AdminGuard.IsAdministrator() ? AppTheme.Green : AppTheme.Amber);
            adminBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            adminBadge.Location = new Point(header.Width - 145, 29);
            adminBadge.AutoSize = true;
            header.Controls.Add(adminBadge);
            header.Resize += delegate { adminBadge.Left = header.ClientSize.Width - adminBadge.Width - 28; };

            applyBar = new Panel();
            applyBar.Dock = DockStyle.Bottom;
            applyBar.Height = 66;
            applyBar.BackColor = Color.FromArgb(23, 29, 41);
            applyBar.Visible = false;
            applyBar.Padding = new Padding(24, 13, 24, 13);
            mainArea.Controls.Add(applyBar);

            pendingLabel = UiFactory.Label("0 değişiklik bekliyor", AppTheme.Body, AppTheme.Text);
            pendingLabel.Location = new Point(24, 24);
            applyBar.Controls.Add(pendingLabel);

            Button discard = UiFactory.Button("Değişiklikleri bırak", AppTheme.SurfaceRaised, AppTheme.Text);
            discard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            discard.Click += delegate
            {
                pending.Clear();
                UpdateApplyBar();
                RefreshCurrentPage();
            };
            applyBar.Controls.Add(discard);

            applyButton = UiFactory.Button("Gözden geçir ve uygula", AppTheme.Accent, Color.White);
            applyButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            applyButton.Click += delegate { ApplyPendingChanges(); };
            applyBar.Controls.Add(applyButton);

            applyBar.Resize += delegate
            {
                applyButton.Left = applyBar.ClientSize.Width - applyButton.Width - 24;
                applyButton.Top = 14;
                discard.Left = applyButton.Left - discard.Width - 10;
                discard.Top = 14;
            };

            content = new PremiumBackdrop();
            content.Dock = DockStyle.Fill;
            content.BackColor = AppTheme.Window;
            mainArea.Controls.Add(content);
            content.BringToFront();
        }

        private void BuildSidebar(Panel sidebar)
        {
            Panel brand = new Panel();
            brand.Dock = DockStyle.Top;
            brand.Height = 96;
            brand.BackColor = AppTheme.Sidebar;
            sidebar.Controls.Add(brand);

            PictureBox mark = BrandAssets.CreateLogoBox(54, 54);
            mark.Location = new Point(10, 10);
            brand.Controls.Add(mark);

            Label name = UiFactory.Label("tercan.exe", new Font("Segoe UI Semibold", 16f, FontStyle.Bold), AppTheme.Text);
            name.Location = new Point(67, 16);
            brand.Controls.Add(name);

            Label tagline = UiFactory.Label("OYUN • PERFORMANS", new Font("Segoe UI", 7.2f, FontStyle.Bold), AppTheme.TextMuted);
            tagline.Location = new Point(68, 47);
            brand.Controls.Add(tagline);

            Panel separator = new Panel();
            separator.Dock = DockStyle.Bottom;
            separator.Height = 1;
            separator.BackColor = AppTheme.Border;
            brand.Controls.Add(separator);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 74;
            footer.BackColor = AppTheme.Sidebar;
            sidebar.Controls.Add(footer);

            Label version = UiFactory.Label("tercan.exe 1.8.0  •  Win 10/11", AppTheme.Small, AppTheme.TextMuted);
            version.Location = new Point(16, 16);
            footer.Controls.Add(version);
            Label reversible = UiFactory.Label("Ayarlar geri alınabilir.", new Font("Segoe UI", 7.8f), AppTheme.TextMuted);
            reversible.Location = new Point(16, 42);
            footer.Controls.Add(reversible);

            Panel navHost = new ModernScrollPanel();
            navHost.Dock = DockStyle.Fill;
            navHost.AutoScroll = true;
            navHost.Padding = new Padding(10, 12, 10, 10);
            sidebar.Controls.Add(navHost);
            navHost.BringToFront();

            AddNavButton(navHost, "⌂   Ana Sayfa", "scanner", null);
            AddNavButton(navHost, "⚡   Hızlandırma", "optimizer-settings", null);
            AddNavButton(navHost, "✦   Temizlik", "cleanup", null);
            AddNavButton(navHost, "⇩   Uygulamalar", "installer", null);
            AddNavButton(navHost, "◎   Discord / DPI", "goodbyedpi", null);
            AddNavButton(navHost, "↶   Geri Alma", "recovery", null);
            AddNavButton(navHost, "⇧   Güncellemeler", "updates", null);
        }

        private void AddNavButton(Panel host, string text, string page, string category)
        {
            AnimatedNavButton button = new AnimatedNavButton();
            button.Text = text;
            button.Tag = page + "|" + (category ?? string.Empty);
            button.Dock = DockStyle.Top;
            button.Height = 45;
            button.Padding = new Padding(0);
            button.Margin = new Padding(0, 0, 0, 5);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.ForeColor = AppTheme.TextMuted;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Click += delegate
            {
                SetActiveNav(button);
                Navigate(page, category);
            };
            host.Controls.Add(button);
            host.Controls.SetChildIndex(button, 0);
            navigationButtons.Add(button);
        }

        private void SetActiveNav(Button active)
        {
            foreach (Button button in navigationButtons)
            {
                bool selected = button == active;
                AnimatedNavButton animated = button as AnimatedNavButton;
                if (animated != null)
                {
                    animated.SelectedState = selected;
                }
                else
                {
                    button.BackColor = selected ? AppTheme.AccentSoft : AppTheme.Sidebar;
                    button.ForeColor = selected ? Color.White : AppTheme.TextMuted;
                }
            }
        }

        private void SelectNavigationFor(string page, string category)
        {
            string tag = page + "|" + (category ?? string.Empty);
            Button matching = navigationButtons.FirstOrDefault(
                button => string.Equals(Convert.ToString(button.Tag), tag, StringComparison.OrdinalIgnoreCase));
            if (matching != null) SetActiveNav(matching);
        }

        private void Navigate(string page, string category)
        {
            if (installationRunning && page != "installer")
            {
                MessageBox.Show(
                    "Uygulama kurulumu sürerken bu sayfadan ayrılamazsınız.",
                    "Kurulum devam ediyor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            currentPage = page;
            currentCategory = category;
            visibleCards.Clear();
            foreach (Control child in content.Controls.Cast<Control>().ToList())
            {
                child.Dispose();
            }
            content.Controls.Clear();
            SelectNavigationFor(page, category);

            if (page == "scanner")
            {
                ShowOneClickOptimizerPage();
            }
            else if (page == "optimizer-settings")
            {
                ShowOptimizerSettingsPage();
            }
            else if (page == "dashboard")
            {
                ShowDashboard();
            }
            else if (page == "tweaks")
            {
                ShowTweaks(category);
            }
            else if (page == "memory")
            {
                ShowMemoryPage();
            }
            else if (page == "installer")
            {
                ShowSoftwareInstallerPage();
            }
            else if (page == "focus")
            {
                ShowFocusModePage();
            }
            else if (page == "apps")
            {
                ShowAppsPage();
            }
            else if (page == "toolbox")
            {
                ShowToolboxPage();
            }
            else if (page == "cleanup")
            {
                ShowCleanupPage();
            }
            else if (page == "startup")
            {
                ShowStartupPage();
            }
            else if (page == "network-tools")
            {
                ShowNetworkToolsPage();
            }
            else if (page == "goodbyedpi")
            {
                ShowGoodbyeDpiPage();
            }
            else if (page == "repair")
            {
                ShowRepairPage();
            }
            else if (page == "hardware")
            {
                ShowHardwarePage();
            }
            else if (page == "hosts")
            {
                ShowHostsPage();
            }
            else if (page == "recovery")
            {
                ShowRecoveryPage();
            }
            else if (page == "updates")
            {
                ShowUpdatePage();
            }
            else
            {
                ShowAboutPage();
            }

            PageAccentSweep sweep = new PageAccentSweep();
            sweep.Location = Point.Empty;
            sweep.Width = content.ClientSize.Width;
            sweep.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            content.Controls.Add(sweep);
            sweep.BringToFront();
            sweep.StartAnimation();
            StartPageReveal();

            UpdateMemoryMonitorSchedule();
        }

        private void StartPageReveal()
        {
            if (pageRevealTimer != null)
            {
                pageRevealTimer.Stop();
                pageRevealTimer.Dispose();
                pageRevealTimer = null;
            }

            pageTitle.Location = new Point(29, 18);
            pageDescription.Location = new Point(31, 61);
            pageTitle.ForeColor = AppTheme.Text;
            pageDescription.ForeColor = AppTheme.TextMuted;
            if (!UiMotion.AnimationsEnabled) return;

            pageRevealFrame = 0;
            pageTitle.Top = 23;
            pageDescription.Top = 66;
            pageTitle.ForeColor = UiMotion.Blend(AppTheme.Window, AppTheme.Cyan, 0.68f);
            pageDescription.ForeColor = UiMotion.Blend(AppTheme.Window, AppTheme.TextMuted, 0.40f);

            pageRevealTimer = new Timer();
            pageRevealTimer.Interval = 16;
            pageRevealTimer.Tick += delegate
            {
                pageRevealFrame++;
                float t = Math.Min(1f, pageRevealFrame / 19f);
                float eased = 1f - (float)Math.Pow(1f - t, 3d);
                pageTitle.Top = 23 - (int)Math.Round(5f * eased);
                pageDescription.Top = 66 - (int)Math.Round(5f * eased);
                pageTitle.ForeColor = UiMotion.Blend(
                    UiMotion.Blend(AppTheme.Window, AppTheme.Cyan, 0.68f),
                    AppTheme.Text,
                    eased);
                pageDescription.ForeColor = UiMotion.Blend(
                    UiMotion.Blend(AppTheme.Window, AppTheme.TextMuted, 0.40f),
                    AppTheme.TextMuted,
                    eased);
                if (t < 1f) return;

                pageRevealTimer.Stop();
                pageRevealTimer.Dispose();
                pageRevealTimer = null;
            };
            pageRevealTimer.Start();
        }

        private void ShowDashboard()
        {
            pageTitle.Text = "Performans Paneli";
            pageDescription.Text = "Tercan ile sisteminizi ölçün, güvenli bir profil seçin ve her değişikliği kontrol edin.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel hero = new SmoothPanel();
            hero.Width = 1010;
            hero.Height = 235;
            hero.Margin = new Padding(0, 0, 0, 16);
            hero.BackColor = AppTheme.Surface;
            flow.Controls.Add(hero);

            int score = lastSystemScan == null ? CalculateReadinessScore() : lastSystemScan.ReadinessScore;
            PerformanceGauge gauge = new PerformanceGauge();
            gauge.Value = score;
            gauge.Caption = lastSystemScan == null ? "Temel ayar durumu" : "Tarama hazırlığı";
            gauge.Location = new Point(35, 36);
            hero.Controls.Add(gauge);

            Label heroTitle = UiFactory.Label(
                score >= 80
                    ? "Sisteminiz oyuna hazır görünüyor"
                    : lastSystemScan == null
                        ? "Önce Tek Tık Bakım taramasıyla başlayın"
                        : "Birkaç güvenli ayarla başlayabilirsiniz",
                new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
                AppTheme.Text);
            heroTitle.Location = new Point(225, 33);
            hero.Controls.Add(heroTitle);

            Label heroCopy = UiFactory.Label(
                "Bu puan bir FPS tahmini değildir. Yalnızca önerilen Windows ayarlarının durumunu gösterir. " +
                "Her oyunda gerçek sonucu aynı sahnede ölçmeniz gerekir.",
                AppTheme.Body,
                AppTheme.TextMuted);
            heroCopy.Location = new Point(227, 73);
            heroCopy.MaximumSize = new Size(700, 45);
            heroCopy.AutoSize = false;
            heroCopy.Size = new Size(700, 45);
            hero.Controls.Add(heroCopy);

            Button balanced = UiFactory.Button("Dengeli oyun profili", AppTheme.Accent, Color.White);
            balanced.Location = new Point(226, 137);
            balanced.Click += delegate { StageProfile("balanced"); };
            hero.Controls.Add(balanced);

            Button performance = UiFactory.Button("Maksimum performans", AppTheme.SurfaceRaised, AppTheme.Text);
            performance.Location = new Point(390, 137);
            performance.Click += delegate { StageProfile("performance"); };
            hero.Controls.Add(performance);

            Button unlost = UiFactory.Button("Unlost 2026 • güvenli seçim", Color.FromArgb(29, 77, 80), AppTheme.Cyan);
            unlost.Location = new Point(568, 137);
            unlost.Click += delegate { StageProfile("unlost"); };
            hero.Controls.Add(unlost);

            Label profileNote = UiFactory.Label(
                "Profiller yalnızca değişiklikleri hazırlar; siz gözden geçirip onaylamadan uygulanmaz.",
                AppTheme.Small,
                AppTheme.TextMuted);
            profileNote.Location = new Point(228, 190);
            hero.Controls.Add(profileNote);

            FlowLayoutPanel metrics = new FlowLayoutPanel();
            metrics.Width = 1010;
            metrics.Height = 132;
            metrics.WrapContents = false;
            metrics.Margin = new Padding(0, 0, 0, 16);
            metrics.BackColor = Color.Transparent;
            metrics.Controls.Add(new MetricCard(
                "İşletim sistemi",
                Shorten(systemInfo.OperatingSystem, 25),
                Environment.Is64BitOperatingSystem ? "64-bit Windows" : "32-bit Windows",
                AppTheme.Accent));
            metrics.Controls.Add(new MetricCard(
                "İşlemci",
                Shorten(systemInfo.Cpu, 24),
                "Oyun ve kare süresi",
                AppTheme.Cyan));
            metrics.Controls.Add(new MetricCard(
                "Bellek",
                FormatBytes(systemInfo.TotalRamBytes),
                "Toplam fiziksel RAM",
                AppTheme.Green));
            metrics.Controls.Add(new MetricCard(
                "Güç planı",
                Shorten(systemInfo.PowerPlan, 23),
                systemInfo.IsLaptop ? "Dizüstü • sıcaklığı izleyin" : "Masaüstü sistem",
                AppTheme.Amber));
            flow.Controls.Add(metrics);

            SmoothPanel systemCard = new SmoothPanel();
            systemCard.Width = 1010;
            systemCard.Height = 184;
            systemCard.Margin = new Padding(0, 0, 0, 16);
            systemCard.BackColor = AppTheme.Surface;
            flow.Controls.Add(systemCard);

            Label systemTitle = UiFactory.Label("Donanım görünümü", AppTheme.Subheading, AppTheme.Text);
            systemTitle.Location = new Point(22, 18);
            systemCard.Controls.Add(systemTitle);
            Label gpuLabel = UiFactory.Label("GPU", AppTheme.Small, AppTheme.TextMuted);
            gpuLabel.Location = new Point(24, 60);
            systemCard.Controls.Add(gpuLabel);
            Label gpuValue = UiFactory.Label(systemInfo.Gpu, AppTheme.Body, AppTheme.Text);
            gpuValue.Location = new Point(24, 82);
            gpuValue.MaximumSize = new Size(440, 25);
            systemCard.Controls.Add(gpuValue);

            Label safetyTitle = UiFactory.Label("Tercan güvenlik yaklaşımı", AppTheme.Small, AppTheme.TextMuted);
            safetyTitle.Location = new Point(515, 60);
            systemCard.Controls.Add(safetyTitle);
            Label safety = UiFactory.Label(
                "✓ Defender ve Windows Update'i kapatmaz\n✓ HPET / BCD zamanlayıcı hileleri uygulamaz\n✓ Her kayıt defteri değerini yedekler",
                AppTheme.Body,
                AppTheme.Text);
            safety.Location = new Point(515, 82);
            safety.MaximumSize = new Size(440, 80);
            systemCard.Controls.Add(safety);

            SmoothPanel activity = new SmoothPanel();
            activity.Width = 1010;
            activity.Height = 170;
            activity.Margin = new Padding(0, 0, 0, 24);
            activity.BackColor = AppTheme.Surface;
            flow.Controls.Add(activity);
            Label activityTitle = UiFactory.Label("Son işlemler", AppTheme.Subheading, AppTheme.Text);
            activityTitle.Location = new Point(22, 18);
            activity.Controls.Add(activityTitle);
            string[] recent = Logger.ReadRecent(5);
            Label log = UiFactory.Label(
                recent.Length == 0 ? "Henüz uygulanmış bir ayar yok." : string.Join(Environment.NewLine, recent),
                AppTheme.Small,
                AppTheme.TextMuted);
            log.Location = new Point(24, 52);
            log.MaximumSize = new Size(950, 100);
            log.AutoSize = false;
            log.Size = new Size(950, 100);
            activity.Controls.Add(log);
        }

        private void ShowTweaks(string category)
        {
            string shownCategory = category ?? "Tüm Ayarlar";
            pageTitle.Text = shownCategory;
            pageDescription.Text = CategoryDescription(shownCategory);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Padding = new Padding(24, 8, 24, 24);
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32f));
            content.Controls.Add(layout);

            Panel left = new Panel();
            left.Dock = DockStyle.Fill;
            left.Padding = new Padding(0, 0, 18, 0);
            left.BackColor = Color.Transparent;
            layout.Controls.Add(left, 0, 0);

            TableLayoutPanel resultLayout = new TableLayoutPanel();
            resultLayout.Dock = DockStyle.Fill;
            resultLayout.Margin = Padding.Empty;
            resultLayout.ColumnCount = 1;
            resultLayout.RowCount = 2;
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            left.Controls.Add(resultLayout);

            Panel searchHost = new Panel();
            searchHost.Dock = DockStyle.Fill;
            searchHost.Margin = Padding.Empty;
            searchHost.Padding = new Padding(0, 0, 0, 8);
            searchHost.BackColor = Color.Transparent;
            resultLayout.Controls.Add(searchHost, 0, 0);

            TextBox search = new TextBox();
            search.Dock = DockStyle.Fill;
            search.Font = AppTheme.Body;
            search.BackColor = AppTheme.SurfaceRaised;
            search.ForeColor = AppTheme.Text;
            search.BorderStyle = BorderStyle.FixedSingle;
            searchHost.Controls.Add(search);
            search.HandleCreated += delegate
            {
                SendMessage(search.Handle, EmSetCueBanner, new IntPtr(1), "Ayarlarda ara…");
            };
            if (search.IsHandleCreated)
            {
                SendMessage(search.Handle, EmSetCueBanner, new IntPtr(1), "Ayarlarda ara…");
            }

            FlowLayoutPanel cardsHost = new ModernScrollFlowPanel();
            cardsHost.Dock = DockStyle.Fill;
            cardsHost.FlowDirection = FlowDirection.TopDown;
            cardsHost.WrapContents = false;
            cardsHost.AutoScroll = true;
            cardsHost.Padding = new Padding(0, 14, 10, 20);
            cardsHost.BackColor = Color.Transparent;
            cardsHost.Margin = Padding.Empty;
            resultLayout.Controls.Add(cardsHost, 0, 1);

            search.TextChanged += delegate
            {
                BuildTweakCards(cardsHost, shownCategory, search.Text);
            };

            SmoothPanel inspector = BuildInspectorPanel();
            inspector.Dock = DockStyle.Fill;
            layout.Controls.Add(inspector, 1, 0);
            inspector.Tag = "inspector";

            BuildTweakCards(cardsHost, shownCategory, string.Empty);
            TweakDefinition first = FilterTweaks(shownCategory, string.Empty).FirstOrDefault();
            if (first != null) UpdateInspector(inspector, first);
        }

        private void BuildTweakCards(FlowLayoutPanel flow, string category, string query)
        {
            flow.SuspendLayout();
            flow.Controls.Clear();
            visibleCards.Clear();
            List<TweakDefinition> filtered = FilterTweaks(category, query).ToList();
            int width = Math.Max(560, flow.ClientSize.Width - 28);

            foreach (TweakDefinition tweak in filtered)
            {
                bool applied = engine.IsApplied(tweak);
                TweakCard card = new TweakCard(tweak, applied);
                card.Width = width;
                bool desired;
                card.UpdateState(applied, pending.TryGetValue(tweak.Id, out desired) ? (bool?)desired : null);
                card.RequestedChanged += TweakRequestedChanged;
                card.Selected += delegate(TweakDefinition selected)
                {
                    SmoothPanel inspector = FindInspector();
                    if (inspector != null) UpdateInspector(inspector, selected);
                };
                flow.Controls.Add(card);
                visibleCards[tweak.Id] = card;
            }

            if (filtered.Count == 0)
            {
                Label empty = UiFactory.Label(
                    category == "Eklentiler"
                        ? "Henüz yerel eklenti yok. Modules klasörüne bir JSON eklentisi ekleyebilirsiniz."
                        : "Aramanızla eşleşen ayar bulunamadı.",
                    AppTheme.Body,
                    AppTheme.TextMuted);
                empty.Margin = new Padding(8, 30, 0, 0);
                flow.Controls.Add(empty);
            }
            flow.ResumeLayout();
        }

        private IEnumerable<TweakDefinition> FilterTweaks(string category, string query)
        {
            IEnumerable<TweakDefinition> result = tweaks;
            if (category == "Eklentiler")
            {
                result = result.Where(x => x.Id.StartsWith("custom.", StringComparison.OrdinalIgnoreCase));
            }
            else if (!string.IsNullOrWhiteSpace(category) && category != "Tüm Ayarlar")
            {
                result = result.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                result = result.Where(x =>
                    (x.Title ?? string.Empty).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (x.Summary ?? string.Empty).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (x.Details ?? string.Empty).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }
            return result.OrderBy(x => x.Risk).ThenByDescending(x => x.Recommended).ThenBy(x => x.Title);
        }

        private SmoothPanel BuildInspectorPanel()
        {
            SmoothPanel panel = new ModernScrollSmoothPanel();
            panel.BackColor = AppTheme.Surface;
            panel.Padding = new Padding(24);
            panel.AutoScroll = true;
            return panel;
        }

        private void UpdateInspector(SmoothPanel panel, TweakDefinition tweak)
        {
            panel.SuspendLayout();
            panel.Controls.Clear();

            Label eyebrow = UiFactory.Label(tweak.Category.ToUpperInvariant(), AppTheme.Small, AppTheme.Cyan);
            eyebrow.Location = new Point(24, 24);
            panel.Controls.Add(eyebrow);

            Label title = UiFactory.Label(tweak.Title, new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 54);
            title.MaximumSize = new Size(Math.Max(220, panel.ClientSize.Width - 50), 60);
            panel.Controls.Add(title);

            FlowLayoutPanel pills = new FlowLayoutPanel();
            pills.Location = new Point(24, 118);
            pills.Width = Math.Max(220, panel.ClientSize.Width - 50);
            pills.Height = 30;
            pills.BackColor = Color.Transparent;
            pills.Controls.Add(UiFactory.Pill(UiFactory.RiskText(tweak.Risk), UiFactory.RiskColor(tweak.Risk)));
            pills.Controls.Add(UiFactory.Pill(UiFactory.ImpactText(tweak.Impact), AppTheme.Cyan));
            panel.Controls.Add(pills);

            Label whatTitle = UiFactory.Label("Ne değiştirir?", AppTheme.Subheading, AppTheme.Text);
            whatTitle.Location = new Point(24, 171);
            panel.Controls.Add(whatTitle);

            Label details = UiFactory.Label(tweak.Details, AppTheme.Body, AppTheme.TextMuted);
            details.Location = new Point(24, 202);
            details.MaximumSize = new Size(Math.Max(220, panel.ClientSize.Width - 50), 145);
            details.AutoSize = false;
            details.Size = new Size(Math.Max(220, panel.ClientSize.Width - 50), 145);
            panel.Controls.Add(details);

            Label compatibilityTitle = UiFactory.Label("Uyumluluk", AppTheme.Small, AppTheme.TextMuted);
            compatibilityTitle.Location = new Point(24, 354);
            panel.Controls.Add(compatibilityTitle);
            Label compatibility = UiFactory.Label(tweak.Compatibility, AppTheme.Body, AppTheme.Text);
            compatibility.Location = new Point(24, 376);
            panel.Controls.Add(compatibility);

            Label restart = UiFactory.Label(
                tweak.RequiresRestart ? "Değişiklik yeniden başlatma ister." : "Çoğu durumda oturumu kapatmak gerekmez.",
                AppTheme.Small,
                tweak.RequiresRestart ? AppTheme.Amber : AppTheme.Green);
            restart.Location = new Point(24, 412);
            panel.Controls.Add(restart);

            if (!string.IsNullOrWhiteSpace(tweak.SourceLabel))
            {
                Label sourceTitle = UiFactory.Label("Kaynak", AppTheme.Small, AppTheme.TextMuted);
                sourceTitle.Location = new Point(24, 457);
                panel.Controls.Add(sourceTitle);
                LinkLabel source = new LinkLabel();
                source.Text = tweak.SourceLabel;
                source.Font = AppTheme.Body;
                source.LinkColor = AppTheme.Cyan;
                source.ActiveLinkColor = Color.White;
                source.AutoSize = true;
                source.Location = new Point(24, 479);
                source.LinkClicked += delegate
                {
                    try { ProcessRunner.Open(tweak.SourceUrl); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Kaynak açılamadı"); }
                };
                panel.Controls.Add(source);
            }

            if (tweak.Risk == RiskLevel.Experimental)
            {
                SmoothPanel warning = new SmoothPanel();
                warning.BackColor = Color.FromArgb(57, 29, 34);
                warning.BorderColor = AppTheme.Red;
                warning.Location = new Point(24, 529);
                warning.Size = new Size(Math.Max(220, panel.ClientSize.Width - 50), 92);
                Label warningText = UiFactory.Label(
                    "Bu ayar varsayılan profillere eklenmez. Önce ölçüm alın, tek başına deneyin ve sonucu karşılaştırın.",
                    AppTheme.Small,
                    Color.FromArgb(255, 192, 190));
                warningText.Location = new Point(14, 15);
                warningText.MaximumSize = new Size(warning.Width - 28, 60);
                warning.Controls.Add(warningText);
                panel.Controls.Add(warning);
            }

            panel.ResumeLayout();
        }

        private SmoothPanel FindInspector()
        {
            return FindControlRecursive(content, delegate(Control c) { return c.Tag as string == "inspector"; }) as SmoothPanel;
        }

        private static Control FindControlRecursive(Control root, Predicate<Control> predicate)
        {
            foreach (Control child in root.Controls)
            {
                if (predicate(child)) return child;
                Control nested = FindControlRecursive(child, predicate);
                if (nested != null) return nested;
            }
            return null;
        }

        private void TweakRequestedChanged(TweakDefinition tweak, bool desired)
        {
            bool applied = engine.IsApplied(tweak);
            if (desired == applied)
            {
                pending.Remove(tweak.Id);
            }
            else
            {
                pending[tweak.Id] = desired;
            }
            UpdateApplyBar();
            RefreshVisibleCard(tweak);
        }

        private void RefreshVisibleCard(TweakDefinition tweak)
        {
            TweakCard card;
            if (visibleCards.TryGetValue(tweak.Id, out card))
            {
                bool applied = engine.IsApplied(tweak);
                bool desired;
                card.UpdateState(applied, pending.TryGetValue(tweak.Id, out desired) ? (bool?)desired : null);
            }
        }

        private void UpdateApplyBar()
        {
            int count = pending.Count;
            applyBar.Visible = count > 0;
            pendingLabel.Text = count + " değişiklik bekliyor • Önce gözden geçireceksiniz";
            content.Padding = count > 0 ? new Padding(0, 0, 0, 66) : Padding.Empty;
        }

        private void StageProfile(string profile)
        {
            string[] ids;
            if (profile == "balanced")
            {
                ids = new[]
                {
                    "gaming.game_mode",
                    "gaming.disable_capture",
                    "visual.transparency",
                    "background.suggestions",
                    "network.delivery_optimization"
                };
            }
            else
            {
                ids = new[]
                {
                    "gaming.game_mode",
                    "gaming.disable_capture",
                    "gaming.high_performance_power",
                    "visual.transparency",
                    "visual.effects",
                    "visual.widgets",
                    "background.suggestions",
                    "background.apps",
                    "network.delivery_optimization"
                };
            }

            foreach (string id in ids)
            {
                TweakDefinition tweak = tweaks.FirstOrDefault(x => x.Id == id);
                if (tweak != null && !engine.IsApplied(tweak))
                {
                    if (systemInfo.IsLaptop && tweak.Id == "gaming.high_performance_power" && profile == "balanced")
                    {
                        continue;
                    }
                    pending[tweak.Id] = true;
                }
            }

            if (profile == "unlost")
            {
                MessageBox.Show(
                    "Unlost 2026 güvenli seçiminde güç planı ve sadeleştirme ayarları hazırlandı. " +
                    "Bellek sıkıştırmayı kapatma ve toplu uygulama silme, ölçülebilir riskleri nedeniyle otomatik seçilmedi.",
                    "Unlost 2026 – gözden geçirilmiş profil",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            UpdateApplyBar();
            RefreshCurrentPage();
        }

        private void ApplyPendingChanges()
        {
            List<KeyValuePair<TweakDefinition, bool>> changes = pending
                .Select(x => new KeyValuePair<TweakDefinition, bool>(
                    tweaks.FirstOrDefault(t => t.Id == x.Key),
                    x.Value))
                .Where(x => x.Key != null)
                .ToList();

            if (changes.Count == 0)
            {
                pending.Clear();
                UpdateApplyBar();
                return;
            }

            using (ApplyReviewDialog dialog = new ApplyReviewDialog(changes))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.Confirmed) return;

                Cursor = Cursors.WaitCursor;
                List<string> errors = new List<string>();
                bool restartNeeded = false;
                try
                {
                    if (dialog.CreateRestorePoint)
                    {
                        try
                        {
                            ProcessResult restore = RestorePointTools.Create("Tercan ayarları");
                            if (restore.ExitCode != 0 && !string.IsNullOrWhiteSpace(restore.Error))
                            {
                                Logger.Info("Geri yükleme noktası oluşturulamadı: " + restore.Error.Trim());
                            }
                            else
                            {
                                Logger.Info("Windows geri yükleme noktası oluşturuldu.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Geri yükleme noktası oluşturulamadı", ex);
                        }
                    }

                    foreach (KeyValuePair<TweakDefinition, bool> change in changes)
                    {
                        try
                        {
                            if (change.Value) engine.Apply(change.Key);
                            else engine.Revert(change.Key);
                            pending.Remove(change.Key.Id);
                            restartNeeded = restartNeeded || change.Key.RequiresRestart;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(change.Key.Title + ": " + ex.Message);
                            Logger.Error("Ayar değiştirilemedi: " + change.Key.Title, ex);
                        }
                    }
                }
                finally
                {
                    Cursor = Cursors.Default;
                }

                lastSystemScan = null;
                UpdateApplyBar();
                RefreshCurrentPage();

                if (errors.Count > 0)
                {
                    MessageBox.Show(
                        "Bazı ayarlar uygulanamadı:\n\n" + string.Join("\n", errors),
                        "İşlem kısmen tamamlandı",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                if (changes.Count > errors.Count)
                {
                    ShowRestartRecommendationDialog(
                        (changes.Count - errors.Count) + " ayar uygulandı ve geri alma yedekleri kaydedildi." +
                        (restartNeeded ? " En az bir ayar yeniden başlatma gerektiriyor." : string.Empty),
                        errors.Count > 0);
                }
            }
        }

        private void ShowMemoryPage()
        {
            EnsureMemoryMonitor();
            pageTitle.Text = "Bellek / ISLC";
            pageDescription.Text = "Standby belleğini yalnızca gerçekten gerektiğinde temizleyin; boş RAM hedef değildir.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel memory = new SmoothPanel();
            memory.Width = 1010;
            memory.Height = 302;
            memory.Margin = new Padding(0, 0, 0, 16);
            memory.BackColor = AppTheme.Surface;
            flow.Controls.Add(memory);

            Label title = UiFactory.Label("Tercan Akıllı Bellek İzleyici", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            memory.Controls.Add(title);
            Label copy = UiFactory.Label(
                "ISLC benzeri eşik tabanlı çalışma: yalnızca boş bellek ve standby önbelleği aynı anda sınırı geçtiğinde temizler. " +
                "Windows önbelleğini normal durumda rahat bırakır.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(25, 58);
            copy.MaximumSize = new Size(920, 45);
            copy.AutoSize = false;
            copy.Size = new Size(920, 45);
            memory.Controls.Add(copy);

            memoryAvailableLabel = UiFactory.Label("Kullanılabilir: ölçülüyor…", AppTheme.Subheading, AppTheme.Green);
            memoryAvailableLabel.Location = new Point(26, 118);
            memory.Controls.Add(memoryAvailableLabel);
            memoryStandbyLabel = UiFactory.Label("Standby: ölçülüyor…", AppTheme.Subheading, AppTheme.Cyan);
            memoryStandbyLabel.Location = new Point(260, 118);
            memory.Controls.Add(memoryStandbyLabel);

            memoryUsageBar = new ProgressBar();
            memoryUsageBar.Location = new Point(26, 155);
            memoryUsageBar.Size = new Size(450, 12);
            memoryUsageBar.Style = ProgressBarStyle.Continuous;
            memory.Controls.Add(memoryUsageBar);

            Label freeTitle = UiFactory.Label("Boş bellek şu değerin altına düşerse", AppTheme.Small, AppTheme.TextMuted);
            freeTitle.Location = new Point(525, 115);
            memory.Controls.Add(freeTitle);
            freeMemoryThreshold = new NumericUpDown();
            freeMemoryThreshold.Location = new Point(526, 140);
            freeMemoryThreshold.Size = new Size(140, 30);
            freeMemoryThreshold.Minimum = 512;
            freeMemoryThreshold.Maximum = 32768;
            freeMemoryThreshold.Increment = 256;
            freeMemoryThreshold.Value = memoryFreeThresholdValue;
            freeMemoryThreshold.BackColor = AppTheme.SurfaceRaised;
            freeMemoryThreshold.ForeColor = AppTheme.Text;
            freeMemoryThreshold.ValueChanged += delegate
            {
                memoryFreeThresholdValue = freeMemoryThreshold.Value;
                LocalSettingsStore.Save(memoryFreeThresholdValue, memoryStandbyThresholdValue);
            };
            memory.Controls.Add(freeMemoryThreshold);
            Label mb1 = UiFactory.Label("MB", AppTheme.Body, AppTheme.TextMuted);
            mb1.Location = new Point(670, 143);
            memory.Controls.Add(mb1);

            Label standbyTitle = UiFactory.Label("Standby önbelleği şu değeri aşarsa", AppTheme.Small, AppTheme.TextMuted);
            standbyTitle.Location = new Point(725, 115);
            memory.Controls.Add(standbyTitle);
            standbyThreshold = new NumericUpDown();
            standbyThreshold.Location = new Point(726, 140);
            standbyThreshold.Size = new Size(140, 30);
            standbyThreshold.Minimum = 512;
            standbyThreshold.Maximum = 65536;
            standbyThreshold.Increment = 256;
            standbyThreshold.Value = memoryStandbyThresholdValue;
            standbyThreshold.BackColor = AppTheme.SurfaceRaised;
            standbyThreshold.ForeColor = AppTheme.Text;
            standbyThreshold.ValueChanged += delegate
            {
                memoryStandbyThresholdValue = standbyThreshold.Value;
                LocalSettingsStore.Save(memoryFreeThresholdValue, memoryStandbyThresholdValue);
            };
            memory.Controls.Add(standbyThreshold);
            Label mb2 = UiFactory.Label("MB", AppTheme.Body, AppTheme.TextMuted);
            mb2.Location = new Point(870, 143);
            memory.Controls.Add(mb2);

            memoryStartButton = UiFactory.Button(
                memoryCleanerActive ? "İzleyiciyi durdur" : "Arka planda başlat",
                memoryCleanerActive ? AppTheme.Red : AppTheme.Accent,
                Color.White);
            memoryStartButton.Location = new Point(25, 205);
            memoryStartButton.Click += delegate { ToggleMemoryCleaner(); };
            memory.Controls.Add(memoryStartButton);

            Button purge = UiFactory.Button("Şimdi bir kez temizle", AppTheme.SurfaceRaised, AppTheme.Text);
            purge.Location = new Point(195, 205);
            purge.Click += delegate { ManualPurge(); };
            memory.Controls.Add(purge);

            memoryCleanerStatus = UiFactory.Label(
                memoryCleanerActive ? "İzleyici çalışıyor • uygulama tepside çalışabilir" : "İzleyici kapalı",
                AppTheme.Small,
                memoryCleanerActive ? AppTheme.Green : AppTheme.TextMuted);
            memoryCleanerStatus.Location = new Point(27, 260);
            memory.Controls.Add(memoryCleanerStatus);

            SmoothPanel official = new SmoothPanel();
            official.Width = 1010;
            official.Height = 264;
            official.Margin = new Padding(0, 0, 0, 16);
            official.BackColor = AppTheme.Surface;
            flow.Controls.Add(official);

            Label officialTitle = UiFactory.Label("Resmî ISLC entegrasyonu", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            officialTitle.Location = new Point(24, 20);
            official.Controls.Add(officialTitle);
            Label officialCopy = UiFactory.Label(
                "Wagnardsoft ISLC v" + IslcIntegration.Version +
                " portable sürümünü resmî sunucudan indirir ve çalıştırmadan önce yayımlanan SHA-256 değerini doğrular. " +
                "Önerilen başlatma, yukarıdaki iki eşiği aktarır; özel zamanlayıcı çözünürlüğünü açmaz. " +
                "Tercan'ın kendi izleyicisini kullanıyorsanız ikisini aynı anda çalıştırmayın.",
                AppTheme.Body,
                AppTheme.TextMuted);
            officialCopy.Location = new Point(25, 59);
            officialCopy.MaximumSize = new Size(920, 52);
            officialCopy.AutoSize = false;
            officialCopy.Size = new Size(920, 52);
            official.Controls.Add(officialCopy);

            islcStatus = UiFactory.Label(
                IslcIntegration.IsVerified() ? "✓ İndirildi ve imza özeti doğrulandı" : "Henüz indirilmedi",
                AppTheme.Body,
                IslcIntegration.IsVerified() ? AppTheme.Green : AppTheme.TextMuted);
            islcStatus.Location = new Point(27, 126);
            official.Controls.Add(islcStatus);

            islcProgress = new ProgressBar();
            islcProgress.Location = new Point(27, 157);
            islcProgress.Size = new Size(425, 10);
            islcProgress.Visible = false;
            official.Controls.Add(islcProgress);

            islcDownloadButton = UiFactory.Button("Resmî ISLC'yi indir ve doğrula", AppTheme.Accent, Color.White);
            islcDownloadButton.Location = new Point(25, 193);
            islcDownloadButton.Click += delegate { DownloadIslc(); };
            official.Controls.Add(islcDownloadButton);

            islcRunButton = UiFactory.Button("Önerilen ayarla başlat", AppTheme.SurfaceRaised, AppTheme.Text);
            islcRunButton.Location = new Point(265, 193);
            islcRunButton.Width = 205;
            islcRunButton.Enabled = IslcIntegration.IsVerified();
            islcRunButton.Click += delegate
            {
                if (!IslcIntegration.IsVerified())
                {
                    MessageBox.Show("Dosya doğrulanamadı; yeniden indirin.", "ISLC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    if (memoryCleanerActive)
                    {
                        memoryCleanerActive = false;
                        trayIcon.Visible = false;
                    }
                    IslcIntegration.StartConfigured(
                        (int)memoryFreeThresholdValue,
                        (int)memoryStandbyThresholdValue);
                    islcStatus.Text = "✓ ISLC seçilen eşiklerle küçültülmüş olarak başlatıldı";
                    islcStatus.ForeColor = AppTheme.Green;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "ISLC başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            official.Controls.Add(islcRunButton);

            Button officialPage = UiFactory.Button("Wagnardsoft sayfası", AppTheme.SurfaceRaised, AppTheme.Cyan);
            officialPage.Location = new Point(485, 193);
            officialPage.Click += delegate { ProcessRunner.Open(IslcIntegration.OfficialPage); };
            official.Controls.Add(officialPage);

            SmoothPanel caution = new SmoothPanel();
            caution.Width = 1010;
            caution.Height = 118;
            caution.Margin = new Padding(0, 0, 0, 24);
            caution.BackColor = Color.FromArgb(48, 35, 21);
            caution.BorderColor = AppTheme.Amber;
            flow.Controls.Add(caution);
            Label cautionText = UiFactory.Label(
                "Bellek temizleyici ne zaman kullanılmalı?\n" +
                "Yalnızca oyunlarda standby belleği büyürken takılma yaşadığınızı ölçtüyseniz. Normal Windows önbelleği performans için faydalıdır; sürekli temizlemek yükleme sürelerini kötüleştirebilir.",
                AppTheme.Body,
                Color.FromArgb(250, 211, 145));
            cautionText.Location = new Point(22, 20);
            cautionText.MaximumSize = new Size(950, 75);
            caution.Controls.Add(cautionText);

            UpdateMemoryStats();
        }

        private void EnsureMemoryMonitor()
        {
            if (memoryTimer != null)
            {
                memoryTimer.Interval = currentPage == "memory" ? 3000 : 10000;
                if (!memoryTimer.Enabled) memoryTimer.Start();
                return;
            }
            memoryTimer = new Timer();
            memoryTimer.Interval = currentPage == "memory" ? 3000 : 10000;
            memoryTimer.Tick += MemoryTimer_Tick;
            memoryTimer.Start();
        }

        private void UpdateMemoryMonitorSchedule()
        {
            if (memoryCleanerActive || currentPage == "memory")
            {
                EnsureMemoryMonitor();
                return;
            }

            if (memoryTimer != null)
            {
                memoryTimer.Stop();
                memoryTimer.Dispose();
                memoryTimer = null;
            }
        }

        private void ToggleMemoryCleaner()
        {
            memoryCleanerActive = !memoryCleanerActive;
            memoryStartButton.Text = memoryCleanerActive ? "İzleyiciyi durdur" : "Arka planda başlat";
            memoryStartButton.BackColor = memoryCleanerActive ? AppTheme.Red : AppTheme.Accent;
            memoryCleanerStatus.Text = memoryCleanerActive
                ? "İzleyici çalışıyor • pencereyi küçültebilirsiniz"
                : "İzleyici kapalı";
            memoryCleanerStatus.ForeColor = memoryCleanerActive ? AppTheme.Green : AppTheme.TextMuted;
            trayIcon.Visible = memoryCleanerActive || backgroundStartMode;
            UpdateMemoryMonitorSchedule();
            Logger.Info(memoryCleanerActive ? "Bellek izleyici başlatıldı." : "Bellek izleyici durduruldu.");
        }

        private void MemoryTimer_Tick(object sender, EventArgs e)
        {
            UpdateMemoryStats();
        }

        private void UpdateMemoryStats()
        {
            MemorySnapshot snapshot = SystemProbe.ReadMemory();
            bool memoryPageVisible = memoryAvailableLabel != null &&
                                     !memoryAvailableLabel.IsDisposed &&
                                     memoryAvailableLabel.Parent != null;
            if (memoryPageVisible)
            {
                memoryAvailableLabel.Text = "Kullanılabilir: " + snapshot.AvailableMb.ToString("N0") + " MB";
                memoryStandbyLabel.Text = "Standby: " + snapshot.StandbyMb.ToString("N0") + " MB";
                if (snapshot.TotalMb > 0)
                {
                    int used = (int)Math.Max(0, Math.Min(100, 100 - snapshot.AvailableMb * 100 / snapshot.TotalMb));
                    memoryUsageBar.Value = used;
                }
            }

            if (memoryCleanerActive &&
                snapshot.AvailableMb < (long)memoryFreeThresholdValue &&
                snapshot.StandbyMb > (long)memoryStandbyThresholdValue)
            {
                try
                {
                    StandbyListPurger.Purge();
                    if (memoryPageVisible)
                    {
                        memoryCleanerStatus.Text = "Standby listesi " + DateTime.Now.ToString("HH:mm:ss") + " saatinde temizlendi.";
                        memoryCleanerStatus.ForeColor = AppTheme.Cyan;
                    }
                }
                catch (Exception ex)
                {
                    if (memoryPageVisible)
                    {
                        memoryCleanerStatus.Text = "Temizleme başarısız: " + ex.Message;
                        memoryCleanerStatus.ForeColor = AppTheme.Red;
                    }
                    Logger.Error("Otomatik bellek temizleme başarısız", ex);
                }
            }
        }

        private void ManualPurge()
        {
            DialogResult answer = MessageBox.Show(
                "Standby önbelleği şimdi temizlenecek. Windows ihtiyaç duyduğunda bu önbelleği yeniden oluşturur.\n\nDevam edilsin mi?",
                "Standby belleğini temizle",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            try
            {
                StandbyListPurger.Purge();
                UpdateMemoryStats();
                MessageBox.Show("Standby bellek listesi temizlendi.", "tercan.exe", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Bellek temizlenemedi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DownloadIslc()
        {
            if (IslcIntegration.IsVerified())
            {
                islcStatus.Text = "✓ Dosya zaten doğrulanmış durumda";
                islcStatus.ForeColor = AppTheme.Green;
                islcRunButton.Enabled = true;
                return;
            }

            DialogResult answer = MessageBox.Show(
                "ISLC v" + IslcIntegration.Version + " Wagnardsoft'un resmî sunucusundan indirilecek. " +
                "İndirme tamamlandığında SHA-256 doğrulaması yapılacak.\n\nİndirmek istiyor musunuz?",
                "Resmî ISLC indirmesi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            islcDownloadButton.Enabled = false;
            islcProgress.Visible = true;
            islcProgress.Value = 0;
            islcStatus.Text = "İndiriliyor…";
            islcStatus.ForeColor = AppTheme.Cyan;

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            WebClient client = new WebClient();
            client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e)
            {
                islcProgress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
            };
            client.DownloadFileCompleted += delegate(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
            {
                islcDownloadButton.Enabled = true;
                islcProgress.Visible = false;
                client.Dispose();

                if (e.Error != null || e.Cancelled)
                {
                    islcStatus.Text = "İndirme başarısız";
                    islcStatus.ForeColor = AppTheme.Red;
                    MessageBox.Show(e.Error == null ? "İndirme iptal edildi." : e.Error.Message, "ISLC indirilemedi");
                    return;
                }

                if (!IslcIntegration.IsVerified())
                {
                    try { File.Delete(IslcIntegration.DownloadPath); } catch { }
                    islcStatus.Text = "Güvenlik özeti eşleşmedi • dosya silindi";
                    islcStatus.ForeColor = AppTheme.Red;
                    MessageBox.Show(
                        "İndirilen dosyanın SHA-256 değeri Wagnardsoft'un yayımladığı değerle eşleşmedi. Dosya çalıştırılmadan silindi.",
                        "ISLC doğrulanamadı",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                islcStatus.Text = "✓ İndirildi ve SHA-256 doğrulandı";
                islcStatus.ForeColor = AppTheme.Green;
                islcRunButton.Enabled = true;
                Logger.Info("Resmî ISLC indirildi ve SHA-256 doğrulandı.");
            };
            client.DownloadFileAsync(new Uri(IslcIntegration.PortableUrl), IslcIntegration.DownloadPath);
        }

        private void ShowSoftwareInstallerPage()
        {
            pageTitle.Text = "Uygulama Kur";
            pageDescription.Text = "Yeni kurulan bilgisayar için gerekli uygulamaları seçin; Tercan sırayla indirip kursun.";

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 8, 24, 24);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 154f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82f));
            content.Controls.Add(root);

            SmoothPanel toolbar = new SmoothPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.Margin = new Padding(0, 0, 0, 12);
            toolbar.BackColor = AppTheme.Surface;
            root.Controls.Add(toolbar, 0, 0);

            Label title = UiFactory.Label("Yeni bilgisayar uygulama merkezi", AppTheme.Subheading, AppTheme.Text);
            title.Location = new Point(22, 16);
            toolbar.Controls.Add(title);

            bool wingetReady = WinGetTools.IsAvailable();
            Label managerStatus = UiFactory.Label(
                wingetReady
                    ? "✓ Windows Paket Yöneticisi hazır • paketler resmî WinGet kaynağından kurulur"
                    : "WinGet bulunamadı • Microsoft App Installer kurulmalı",
                AppTheme.Small,
                wingetReady ? AppTheme.Green : AppTheme.Amber);
            managerStatus.Location = new Point(24, 47);
            toolbar.Controls.Add(managerStatus);

            ComboBox category = new ComboBox();
            category.Location = new Point(24, 76);
            category.Size = new Size(170, 30);
            category.DropDownStyle = ComboBoxStyle.DropDownList;
            category.BackColor = AppTheme.SurfaceRaised;
            category.ForeColor = AppTheme.Text;
            category.Font = AppTheme.Body;
            category.Items.Add("Tüm kategoriler");
            foreach (string value in softwarePackages.Select(x => x.Category).Distinct().OrderBy(x => x))
            {
                category.Items.Add(value);
            }
            category.SelectedIndex = 0;
            toolbar.Controls.Add(category);

            TextBox search = new TextBox();
            search.Location = new Point(210, 77);
            search.Size = new Size(265, 28);
            search.BackColor = AppTheme.SurfaceRaised;
            search.ForeColor = AppTheme.Text;
            search.Font = AppTheme.Body;
            search.BorderStyle = BorderStyle.FixedSingle;
            toolbar.Controls.Add(search);
            search.HandleCreated += delegate
            {
                SendMessage(search.Handle, EmSetCueBanner, new IntPtr(1), "Uygulama ara…");
            };
            if (search.IsHandleCreated)
            {
                SendMessage(search.Handle, EmSetCueBanner, new IntPtr(1), "Uygulama ara…");
            }

            Button essential = UiFactory.Button("Yeni PC seti", AppTheme.Accent, Color.White);
            essential.Location = new Point(505, 74);
            toolbar.Controls.Add(essential);
            Button gaming = UiFactory.Button("Oyuncu seti", Color.FromArgb(29, 77, 80), AppTheme.Cyan);
            gaming.Location = new Point(625, 74);
            toolbar.Controls.Add(gaming);
            Button creator = UiFactory.Button("Yayıncı seti", AppTheme.SurfaceRaised, AppTheme.Text);
            creator.Location = new Point(750, 74);
            toolbar.Controls.Add(creator);
            Button clear = UiFactory.Button("Seçimi temizle", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            clear.Location = new Point(875, 74);
            toolbar.Controls.Add(clear);

            if (!wingetReady)
            {
                Button getWinget = UiFactory.Button("App Installer'ı aç", Color.FromArgb(85, 57, 19), AppTheme.Amber);
                getWinget.Location = new Point(760, 20);
                getWinget.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                getWinget.Click += delegate
                {
                    try { ProcessRunner.Open(WinGetTools.AppInstallerStoreUri); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Microsoft Store açılamadı"); }
                };
                toolbar.Controls.Add(getWinget);
            }

            FlowLayoutPanel packageFlow = new ModernScrollFlowPanel();
            packageFlow.Dock = DockStyle.Fill;
            packageFlow.Margin = Padding.Empty;
            packageFlow.Padding = new Padding(0, 4, 4, 16);
            packageFlow.AutoScroll = true;
            packageFlow.FlowDirection = FlowDirection.LeftToRight;
            packageFlow.WrapContents = true;
            packageFlow.BackColor = Color.Transparent;
            root.Controls.Add(packageFlow, 0, 1);

            SmoothPanel actionBar = new SmoothPanel();
            actionBar.Dock = DockStyle.Fill;
            actionBar.Margin = new Padding(0, 10, 0, 0);
            actionBar.BackColor = AppTheme.Surface;
            root.Controls.Add(actionBar, 0, 2);

            softwareSelectionLabel = UiFactory.Label("0 uygulama seçildi", AppTheme.Body, AppTheme.Text);
            softwareSelectionLabel.Location = new Point(22, 16);
            actionBar.Controls.Add(softwareSelectionLabel);

            Label installStatus = UiFactory.Label(
                wingetReady ? "Kurulumlar sırayla ve sessiz modda yapılır." : "Önce WinGet/App Installer kurulmalı.",
                AppTheme.Small,
                AppTheme.TextMuted);
            installStatus.Location = new Point(23, 43);
            actionBar.Controls.Add(installStatus);

            ProgressBar installProgress = new ProgressBar();
            installProgress.Location = new Point(340, 31);
            installProgress.Size = new Size(380, 10);
            installProgress.Visible = false;
            actionBar.Controls.Add(installProgress);

            Button install = UiFactory.Button("Seçilenleri indir ve kur", AppTheme.Green, Color.White);
            install.AutoSize = false;
            install.Size = new Size(205, 42);
            install.Location = new Point(780, 15);
            install.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            install.Enabled = wingetReady && !installationRunning;
            actionBar.Controls.Add(install);

            Action rebuild = delegate
            {
                string categoryValue = category.SelectedIndex <= 0 ? null : Convert.ToString(category.SelectedItem);
                BuildSoftwarePackageCards(packageFlow, categoryValue, search.Text);
            };

            category.SelectedIndexChanged += delegate { rebuild(); };
            search.TextChanged += delegate { rebuild(); };
            essential.Click += delegate { ApplySoftwareProfile("essential"); rebuild(); };
            gaming.Click += delegate { ApplySoftwareProfile("gaming"); rebuild(); };
            creator.Click += delegate { ApplySoftwareProfile("creator"); rebuild(); };
            clear.Click += delegate
            {
                softwareSelection.Clear();
                rebuild();
            };
            install.Click += delegate
            {
                StartSelectedSoftwareInstall(install, installProgress, installStatus);
            };

            rebuild();
        }

        private void BuildSoftwarePackageCards(FlowLayoutPanel host, string category, string query)
        {
            host.SuspendLayout();
            host.Controls.Clear();
            string normalized = (query ?? string.Empty).Trim();
            IEnumerable<SoftwarePackageDefinition> filtered = softwarePackages;
            if (!string.IsNullOrWhiteSpace(category))
            {
                filtered = filtered.Where(x => x.Category == category);
            }
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                filtered = filtered.Where(x =>
                    x.Name.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    x.Description.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    x.Publisher.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (SoftwarePackageDefinition package in filtered)
            {
                SmoothPanel card = new SmoothPanel();
                card.Size = new Size(493, 112);
                card.Margin = new Padding(0, 0, 12, 12);
                card.BackColor = AppTheme.Surface;

                SmoothPanel mark = new SmoothPanel();
                mark.Location = new Point(14, 13);
                mark.Size = new Size(52, 52);
                mark.BackColor = AppTheme.SurfaceRaised;
                mark.BorderColor = Color.FromArgb(75, PackageColor(package.Category));
                card.Controls.Add(mark);

                PictureBox packageIcon = new PictureBox();
                packageIcon.Location = new Point(6, 6);
                packageIcon.Size = new Size(40, 40);
                packageIcon.BackColor = Color.Transparent;
                packageIcon.SizeMode = PictureBoxSizeMode.Zoom;
                packageIcon.Image = SoftwareIconAssets.Load(package.Id);
                mark.Controls.Add(packageIcon);

                Label name = UiFactory.Label(package.Name, AppTheme.Subheading, AppTheme.Text);
                name.Location = new Point(80, 15);
                card.Controls.Add(name);
                Label publisher = UiFactory.Label(package.Publisher + "  •  " + package.Category, AppTheme.Small, AppTheme.Cyan);
                publisher.Location = new Point(81, 43);
                card.Controls.Add(publisher);
                Label description = UiFactory.Label(package.Description, AppTheme.Small, AppTheme.TextMuted);
                description.Location = new Point(18, 76);
                description.AutoSize = false;
                description.Size = new Size(430, 28);
                card.Controls.Add(description);

                CheckBox selected = new CheckBox();
                selected.AutoSize = false;
                selected.Size = new Size(28, 28);
                selected.Location = new Point(448, 17);
                selected.ForeColor = AppTheme.Text;
                selected.BackColor = Color.Transparent;
                bool checkedValue;
                selected.Checked = softwareSelection.TryGetValue(package.Id, out checkedValue) && checkedValue;
                selected.CheckedChanged += delegate
                {
                    softwareSelection[package.Id] = selected.Checked;
                    card.BorderColor = selected.Checked ? AppTheme.Accent : AppTheme.Border;
                    card.Invalidate();
                    UpdateSoftwareSelectionLabel();
                };
                card.Controls.Add(selected);
                card.BorderColor = selected.Checked ? AppTheme.Accent : AppTheme.Border;
                host.Controls.Add(card);
            }

            host.ResumeLayout();
            UpdateSoftwareSelectionLabel();
        }

        private void ApplySoftwareProfile(string profile)
        {
            softwareSelection.Clear();
            foreach (SoftwarePackageDefinition package in softwarePackages)
            {
                bool selected = profile == "essential"
                    ? package.Essential
                    : profile == "gaming"
                        ? package.Gaming
                        : package.Creator;
                if (selected) softwareSelection[package.Id] = true;
            }
            UpdateSoftwareSelectionLabel();
        }

        private void UpdateSoftwareSelectionLabel()
        {
            if (softwareSelectionLabel == null || softwareSelectionLabel.IsDisposed) return;
            int count = softwareSelection.Count(x => x.Value);
            softwareSelectionLabel.Text = count + " uygulama seçildi";
            softwareSelectionLabel.ForeColor = count > 0 ? AppTheme.Cyan : AppTheme.Text;
        }

        private void StartSelectedSoftwareInstall(
            Button installButton,
            ProgressBar progress,
            Label status)
        {
            List<SoftwarePackageDefinition> selected = softwarePackages
                .Where(x =>
                {
                    bool value;
                    return softwareSelection.TryGetValue(x.Id, out value) && value;
                })
                .ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Önce en az bir uygulama seçin.", "tercan.exe");
                return;
            }
            if (!WinGetTools.IsAvailable())
            {
                DialogResult openStore = MessageBox.Show(
                    "WinGet bulunamadı. Microsoft Store'da App Installer sayfası açılsın mı?",
                    "Windows Paket Yöneticisi gerekli",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);
                if (openStore == DialogResult.Yes) ProcessRunner.Open(WinGetTools.AppInstallerStoreUri);
                return;
            }

            DialogResult answer = MessageBox.Show(
                selected.Count + " uygulama sırayla indirilecek ve kurulacak:\n\n" +
                string.Join("\n", selected.Select(x => "• " + x.Name + " — " + x.Publisher)) +
                "\n\nDevam ederek WinGet kaynak koşullarını ve seçilen uygulamaların lisans sözleşmelerini kabul etmiş olursunuz. " +
                "Kurulum sırasında Tercan'ı kapatmayın.",
                "Uygulamaları indir ve kur",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            installationRunning = true;
            installButton.Enabled = false;
            progress.Visible = true;
            progress.Minimum = 0;
            progress.Maximum = selected.Count;
            progress.Value = 0;
            status.Text = "Kurulum hazırlanıyor…";
            status.ForeColor = AppTheme.Cyan;

            System.ComponentModel.BackgroundWorker worker = new System.ComponentModel.BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object sender, System.ComponentModel.DoWorkEventArgs e)
            {
                List<string> errors = new List<string>();
                for (int i = 0; i < selected.Count; i++)
                {
                    ProcessResult result;
                    try
                    {
                        result = WinGetTools.Install(selected[i]);
                    }
                    catch (Exception ex)
                    {
                        result = new ProcessResult { ExitCode = -1, Error = ex.Message, Output = string.Empty };
                    }
                    if (result.ExitCode != 0)
                    {
                        string detail = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                        errors.Add(selected[i].Name + ": " + Shorten(detail.Trim(), 180));
                    }
                    worker.ReportProgress(i + 1, new PackageInstallUpdate
                    {
                        Package = selected[i],
                        Result = result,
                        Index = i + 1,
                        Total = selected.Count
                    });
                }
                e.Result = errors;
            };
            worker.ProgressChanged += delegate(object sender, System.ComponentModel.ProgressChangedEventArgs e)
            {
                PackageInstallUpdate update = e.UserState as PackageInstallUpdate;
                progress.Value = Math.Max(0, Math.Min(progress.Maximum, e.ProgressPercentage));
                if (update != null)
                {
                    status.Text = update.Index + "/" + update.Total + " • " + update.Package.Name +
                                  (update.Result.ExitCode == 0 ? " tamamlandı" : " kurulamadı");
                    status.ForeColor = update.Result.ExitCode == 0 ? AppTheme.Green : AppTheme.Amber;
                }
            };
            worker.RunWorkerCompleted += delegate(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
            {
                installationRunning = false;
                installButton.Enabled = WinGetTools.IsAvailable();
                progress.Visible = false;
                List<string> errors = new List<string>();
                if (e.Error != null)
                {
                    errors.Add("Kurulum işlemi: " + e.Error.Message);
                }
                else
                {
                    errors = e.Result as List<string> ?? new List<string>();
                }
                if (errors.Count == 0)
                {
                    status.Text = "✓ Seçilen uygulamaların kurulumu tamamlandı";
                    status.ForeColor = AppTheme.Green;
                    softwareSelection.Clear();
                    MessageBox.Show(
                        "Seçilen uygulamaların kurulumu tamamlandı.",
                        "Tercan Uygulama Merkezi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    status.Text = "Bazı kurulumlar tamamlanamadı";
                    status.ForeColor = AppTheme.Amber;
                    MessageBox.Show(
                        "Bazı uygulamalar kurulamadı:\n\n" + string.Join("\n\n", errors),
                        "Kurulum özeti",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                UpdateSoftwareSelectionLabel();
            };
            worker.RunWorkerAsync();
        }

        private void ShowFocusModePage()
        {
            pageTitle.Text = "Oyun Odak Modu";
            pageDescription.Text = "Gereksiz uygulamaları geçici kapatın, oyuna öncelik verin ve işiniz bitince her şeyi geri getirin.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel hero = new SmoothPanel();
            hero.Size = new Size(1010, 168);
            hero.Margin = new Padding(0, 0, 0, 16);
            hero.BackColor = focusEngine.IsActive ? Color.FromArgb(20, 49, 43) : AppTheme.Surface;
            hero.BorderColor = focusEngine.IsActive ? AppTheme.Green : AppTheme.Border;
            flow.Controls.Add(hero);

            Label title = UiFactory.Label(
                focusEngine.IsActive ? "Oyun Odak Modu etkin" : "Kaynakları oyuna ayır",
                new Font("Segoe UI Semibold", 19f, FontStyle.Bold),
                AppTheme.Text);
            title.Location = new Point(24, 20);
            hero.Controls.Add(title);

            Label summary = UiFactory.Label(
                focusEngine.IsActive
                    ? lastFocusClosedCount + " süreç kapatıldı • yaklaşık " +
                      (lastFocusReleasedBytes / 1024d / 1024d).ToString("0") + " MB çalışma kümesi bırakıldı"
                    : "Seçtiğiniz arka plan uygulamalarını kapatır; güç planını geçici yükseltir ve seçtiğiniz oyuna Yüksek öncelik verir.",
                AppTheme.Body,
                focusEngine.IsActive ? AppTheme.Green : AppTheme.TextMuted);
            summary.Location = new Point(25, 62);
            summary.AutoSize = false;
            summary.Size = new Size(720, 48);
            hero.Controls.Add(summary);

            Label noPromise = UiFactory.Label(
                "FPS garantisi değildir; esas amaç RAM, CPU zamanı ve arka plan disk/ağ etkinliğini azaltmaktır.",
                AppTheme.Small,
                AppTheme.Amber);
            noPromise.Location = new Point(26, 125);
            hero.Controls.Add(noPromise);

            Button modeButton = UiFactory.Button(
                focusEngine.IsActive ? "Modu kapat ve geri yükle" : "Oyun modunu başlat",
                focusEngine.IsActive ? AppTheme.Red : AppTheme.Accent,
                Color.White);
            modeButton.AutoSize = false;
            modeButton.Size = new Size(205, 44);
            modeButton.Location = new Point(770, 48);
            hero.Controls.Add(modeButton);

            if (focusEngine.IsActive)
            {
                modeButton.Click += delegate
                {
                    FocusModeResult restored = focusEngine.Deactivate(true);
                    string note = restored.RestartedApplicationCount + " uygulama yeniden başlatıldı; güç planı ve oyun önceliği geri alındı.";
                    if (restored.Messages.Count > 0) note += "\n\n" + string.Join("\n", restored.Messages);
                    MessageBox.Show(note, "Oyun Odak Modu kapatıldı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lastFocusClosedCount = 0;
                    lastFocusReleasedBytes = 0;
                    RefreshCurrentPage();
                };

                SmoothPanel activeDetails = new SmoothPanel();
                activeDetails.Size = new Size(1010, 250);
                activeDetails.Margin = new Padding(0, 0, 0, 16);
                activeDetails.BackColor = AppTheme.Surface;
                flow.Controls.Add(activeDetails);
                Label activeTitle = UiFactory.Label("Geçici olarak kapatılanlar", AppTheme.Subheading, AppTheme.Text);
                activeTitle.Location = new Point(24, 20);
                activeDetails.Controls.Add(activeTitle);

                FocusModeSession activeSession = focusEngine.Session;
                string activeList = activeSession == null || activeSession.ClosedApplications.Count == 0
                    ? "Herhangi bir uygulama kapatılmadı; yalnızca güç/öncelik ayarı etkin olabilir."
                    : string.Join(
                        "\n",
                        activeSession.ClosedApplications.Select(x =>
                            "• " + x.Name + " — " + x.ClosedProcessCount + " süreç, yaklaşık " +
                            (x.ReleasedBytes / 1024d / 1024d).ToString("0") + " MB"));
                Label list = UiFactory.Label(activeList, AppTheme.Body, AppTheme.TextMuted);
                list.Location = new Point(26, 62);
                list.AutoSize = false;
                list.Size = new Size(930, 150);
                activeDetails.Controls.Add(list);
                return;
            }

            SmoothPanel setup = new SmoothPanel();
            setup.Size = new Size(1010, 500);
            setup.Margin = new Padding(0, 0, 0, 16);
            setup.BackColor = AppTheme.Surface;
            flow.Controls.Add(setup);

            Label setupTitle = UiFactory.Label("Kapatılabilecek çalışan uygulamalar", AppTheme.Subheading, AppTheme.Text);
            setupTitle.Location = new Point(24, 18);
            setup.Controls.Add(setupTitle);

            Label setupCopy = UiFactory.Label(
                "Yalnızca şu anda çalışan uygulamalar listelenir. Steam, Epic, Windows Gezgini, Defender ve Windows servisleri bu mod tarafından kapatılmaz.",
                AppTheme.Small,
                AppTheme.TextMuted);
            setupCopy.Location = new Point(25, 48);
            setup.Controls.Add(setupCopy);

            CheckedListBox running = new CheckedListBox();
            running.Location = new Point(24, 82);
            running.Size = new Size(620, 245);
            running.BackColor = AppTheme.SurfaceRaised;
            running.ForeColor = AppTheme.Text;
            running.BorderStyle = BorderStyle.FixedSingle;
            running.CheckOnClick = true;
            running.Font = AppTheme.Body;
            setup.Controls.Add(running);

            foreach (FocusProcessDefinition definition in focusDefinitions)
            {
                int processCount = FocusProcessCatalog.RunningCount(definition);
                if (processCount <= 0) continue;
                FocusDisplayItem item = new FocusDisplayItem
                {
                    Definition = definition,
                    ProcessCount = processCount,
                    WorkingSetBytes = FocusProcessCatalog.RunningWorkingSet(definition)
                };
                running.Items.Add(item, definition.SafeDefault);
            }

            Label detail = UiFactory.Label(
                running.Items.Count == 0
                    ? "Listelenen arka plan uygulamalarından hiçbiri şu anda çalışmıyor."
                    : "Bir uygulama seçerek etkisini ve uyarısını görün.",
                AppTheme.Body,
                running.Items.Count == 0 ? AppTheme.Green : AppTheme.TextMuted);
            detail.Location = new Point(675, 88);
            detail.AutoSize = false;
            detail.Size = new Size(295, 155);
            setup.Controls.Add(detail);
            running.SelectedIndexChanged += delegate
            {
                FocusDisplayItem selected = running.SelectedItem as FocusDisplayItem;
                if (selected != null)
                {
                    detail.Text = selected.Definition.Name + "\n\n" +
                                  selected.Definition.Description + "\n\nDikkat: " +
                                  selected.Definition.Warning;
                    detail.ForeColor = selected.Definition.SafeDefault ? AppTheme.TextMuted : AppTheme.Amber;
                }
            };

            Button recommended = UiFactory.Button("Güvenli önerileri seç", AppTheme.SurfaceRaised, AppTheme.Cyan);
            recommended.Location = new Point(24, 342);
            recommended.Click += delegate
            {
                for (int i = 0; i < running.Items.Count; i++)
                {
                    FocusDisplayItem item = running.Items[i] as FocusDisplayItem;
                    running.SetItemChecked(i, item != null && item.Definition.SafeDefault);
                }
            };
            setup.Controls.Add(recommended);

            Button clear = UiFactory.Button("Seçimi temizle", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            clear.Location = new Point(195, 342);
            clear.Click += delegate
            {
                for (int i = 0; i < running.Items.Count; i++) running.SetItemChecked(i, false);
            };
            setup.Controls.Add(clear);

            Label gameLabel = UiFactory.Label("Öncelik verilecek oyun veya uygulama", AppTheme.Small, AppTheme.TextMuted);
            gameLabel.Location = new Point(24, 397);
            setup.Controls.Add(gameLabel);
            ComboBox targetGame = new ComboBox();
            targetGame.Location = new Point(24, 420);
            targetGame.Size = new Size(360, 30);
            targetGame.DropDownStyle = ComboBoxStyle.DropDownList;
            targetGame.BackColor = AppTheme.SurfaceRaised;
            targetGame.ForeColor = AppTheme.Text;
            targetGame.Font = AppTheme.Body;
            foreach (ProcessChoice choice in GetForegroundProcessChoices())
            {
                targetGame.Items.Add(choice);
            }
            targetGame.SelectedIndex = 0;
            setup.Controls.Add(targetGame);

            CheckBox highPower = new CheckBox();
            highPower.Text = "Geçici Yüksek Performans güç planı";
            highPower.Checked = !systemInfo.IsLaptop;
            highPower.AutoSize = true;
            highPower.Location = new Point(425, 401);
            highPower.ForeColor = systemInfo.IsLaptop ? AppTheme.Amber : AppTheme.Text;
            highPower.BackColor = Color.Transparent;
            setup.Controls.Add(highPower);

            CheckBox highPriority = new CheckBox();
            highPriority.Text = "Seçilen oyuna Yüksek işlem önceliği";
            highPriority.Checked = true;
            highPriority.AutoSize = true;
            highPriority.Location = new Point(425, 431);
            highPriority.ForeColor = AppTheme.Text;
            highPriority.BackColor = Color.Transparent;
            setup.Controls.Add(highPriority);

            modeButton.Click += delegate
            {
                List<FocusProcessDefinition> selectedDefinitions = running.CheckedItems
                    .Cast<object>()
                    .OfType<FocusDisplayItem>()
                    .Select(x => x.Definition)
                    .ToList();
                ProcessChoice target = targetGame.SelectedItem as ProcessChoice;
                int targetId = target == null ? 0 : target.ProcessId;
                if (selectedDefinitions.Count == 0 && !highPower.Checked && (!highPriority.Checked || targetId == 0))
                {
                    MessageBox.Show("Önce en az bir geçici işlem seçin.", "Oyun Odak Modu");
                    return;
                }

                string applicationList = selectedDefinitions.Count == 0
                    ? "• Arka plan uygulaması kapatılmayacak"
                    : string.Join("\n", selectedDefinitions.Select(x => "• " + x.Name));
                DialogResult answer = MessageBox.Show(
                    "Aşağıdaki çalışan uygulamalar kapatılacak:\n\n" + applicationList +
                    "\n\nAçık belgeleri, formları ve mesajları önce kaydedin. Oyun modu kapatıldığında uygulamalar yeniden başlatılmaya çalışılır.\n\nDevam edilsin mi?",
                    "Oyun Odak Modunu başlat",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;

                FocusModeResult result = focusEngine.Activate(
                    selectedDefinitions,
                    targetId,
                    highPower.Checked,
                    highPriority.Checked && targetId > 0);
                lastFocusClosedCount = result.ClosedProcessCount;
                lastFocusReleasedBytes = result.ReleasedBytes;
                if (result.Messages.Count > 0)
                {
                    MessageBox.Show(
                        "Mod başlatıldı; bazı işlemler tamamlanamadı:\n\n" + string.Join("\n", result.Messages),
                        "Oyun Odak Modu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                RefreshCurrentPage();
            };

            SmoothPanel caution = new SmoothPanel();
            caution.Size = new Size(1010, 112);
            caution.Margin = new Padding(0, 0, 0, 24);
            caution.BackColor = Color.FromArgb(48, 35, 21);
            caution.BorderColor = AppTheme.Amber;
            flow.Controls.Add(caution);
            Label cautionText = UiFactory.Label(
                "Nasıl çalışır?\n" +
                "Uygulamaları kapatarak kaynak açar; bellek dosyalarını silmez, çekirdek zamanlayıcılarını değiştirmez ve Gerçek Zamanlı öncelik kullanmaz. " +
                "Dizüstü bilgisayarda Yüksek Performans güç planı daha fazla ısı ve pil tüketebilir.",
                AppTheme.Body,
                Color.FromArgb(250, 211, 145));
            cautionText.Location = new Point(22, 18);
            cautionText.AutoSize = false;
            cautionText.Size = new Size(950, 78);
            caution.Controls.Add(cautionText);
        }

        private List<ProcessChoice> GetForegroundProcessChoices()
        {
            List<ProcessChoice> result = new List<ProcessChoice>();
            result.Add(new ProcessChoice { ProcessId = 0, Name = "Oyun seçilmedi", WindowTitle = string.Empty });
            HashSet<string> excluded = new HashSet<string>(
                new[]
                {
                    "explorer", "dwm", "ApplicationFrameHost", "SearchHost", "ShellExperienceHost",
                    "TextInputHost", "StartMenuExperienceHost", "SystemSettings", "GameTuneOptimizer", "GameTuneUltimate", "tercan"
                },
                StringComparer.OrdinalIgnoreCase);

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id ||
                        process.MainWindowHandle == IntPtr.Zero ||
                        excluded.Contains(process.ProcessName))
                    {
                        continue;
                    }
                    result.Add(new ProcessChoice
                    {
                        ProcessId = process.Id,
                        Name = process.ProcessName,
                        WindowTitle = string.IsNullOrWhiteSpace(process.MainWindowTitle)
                            ? string.Empty
                            : Shorten(process.MainWindowTitle, 55)
                    });
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
            return result.OrderBy(x => x.ProcessId == 0 ? string.Empty : x.Name).ToList();
        }

        private static Color PackageColor(string category)
        {
            if (category == "Oyun") return AppTheme.Accent;
            if (category == "Medya" || category == "İçerik") return Color.FromArgb(180, 58, 115);
            if (category == "İnternet") return AppTheme.Cyan;
            if (category == "Araçlar") return AppTheme.Amber;
            return AppTheme.Green;
        }

        private void ShowAppsPage()
        {
            pageTitle.Text = "Uygulamalar";
            pageDescription.Text = "Unlost listesinden güvenli bir alt küme; her uygulamayı ayrı seçin ve ne kaybedeceğinizi görün.";

            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 8, 24, 24);
            root.BackColor = Color.Transparent;
            content.Controls.Add(root);

            SmoothPanel panel = new SmoothPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = AppTheme.Surface;
            panel.Padding = new Padding(24);
            root.Controls.Add(panel);

            Label title = UiFactory.Label("İsteğe bağlı Windows uygulamaları", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            panel.Controls.Add(title);

            Label copy = UiFactory.Label(
                "Tercan sistem uygulamalarını, Microsoft Store'u, Not Defteri'ni veya güvenlik bileşenlerini kaldırmaz. " +
                "Seçilen paketler yalnızca mevcut kullanıcı hesabından kaldırılır ve gerekirse Store'dan tekrar kurulabilir.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(25, 58);
            copy.MaximumSize = new Size(900, 47);
            copy.AutoSize = false;
            copy.Size = new Size(900, 47);
            panel.Controls.Add(copy);

            CheckedListBox list = new CheckedListBox();
            list.Location = new Point(25, 120);
            list.Size = new Size(670, 470);
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            list.BackColor = AppTheme.SurfaceRaised;
            list.ForeColor = AppTheme.Text;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.CheckOnClick = true;
            list.Font = AppTheme.Body;
            panel.Controls.Add(list);

            Label detail = UiFactory.Label("Bir uygulama seçerek açıklamasını görün.", AppTheme.Body, AppTheme.TextMuted);
            detail.Location = new Point(725, 125);
            detail.MaximumSize = new Size(245, 150);
            detail.AutoSize = false;
            detail.Size = new Size(245, 150);
            detail.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            panel.Controls.Add(detail);

            HashSet<string> installed;
            try
            {
                Cursor = Cursors.WaitCursor;
                installed = AppxTools.InstalledPackageNames();
            }
            catch (Exception ex)
            {
                installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                detail.Text = "Uygulama listesi okunamadı: " + ex.Message;
                detail.ForeColor = AppTheme.Red;
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            foreach (AppxDefinition app in AppxTools.Catalog())
            {
                if (installed.Contains(app.PackageName))
                {
                    list.Items.Add(app, false);
                }
            }
            list.DisplayMember = "DisplayName";
            if (list.Items.Count == 0)
            {
                detail.Text = "Listelenen isteğe bağlı uygulamalardan hiçbiri kurulu değil.";
                detail.ForeColor = AppTheme.Green;
            }
            list.SelectedIndexChanged += delegate
            {
                AppxDefinition selected = list.SelectedItem as AppxDefinition;
                if (selected != null)
                {
                    detail.Text = selected.DisplayName + "\n\n" + selected.Note + "\n\nPaket: " + selected.PackageName;
                    detail.ForeColor = AppTheme.TextMuted;
                }
            };

            Button safe = UiFactory.Button("Güvenli önerileri seç", AppTheme.SurfaceRaised, AppTheme.Cyan);
            safe.Location = new Point(25, 612);
            safe.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            safe.Click += delegate
            {
                for (int i = 0; i < list.Items.Count; i++)
                {
                    AppxDefinition app = list.Items[i] as AppxDefinition;
                    list.SetItemChecked(i, app != null && app.SafeSelection);
                }
            };
            panel.Controls.Add(safe);

            Button remove = UiFactory.Button("Seçilenleri kaldır", AppTheme.Red, Color.White);
            remove.Location = new Point(190, 612);
            remove.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            remove.Click += delegate { RemoveSelectedApps(list); };
            panel.Controls.Add(remove);

            Label warning = UiFactory.Label(
                "Toplu kaldırma FPS garantisi vermez. Kazanç çoğunlukla daha sade Başlat menüsü ve daha az arka plan uygulamasıdır.",
                AppTheme.Small,
                AppTheme.Amber);
            warning.Location = new Point(725, 300);
            warning.MaximumSize = new Size(245, 90);
            warning.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            panel.Controls.Add(warning);
        }

        private void RemoveSelectedApps(CheckedListBox list)
        {
            List<AppxDefinition> selected = list.CheckedItems.Cast<object>().OfType<AppxDefinition>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Önce en az bir uygulama seçin.", "tercan.exe");
                return;
            }
            DialogResult answer = MessageBox.Show(
                selected.Count + " uygulama mevcut kullanıcı hesabından kaldırılacak:\n\n" +
                string.Join("\n", selected.Select(x => "• " + x.DisplayName)) +
                "\n\nDaha sonra Microsoft Store'dan yeniden kurulabilir. Devam edilsin mi?",
                "Uygulamaları kaldır",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            Cursor = Cursors.WaitCursor;
            List<string> errors = new List<string>();
            try
            {
                foreach (AppxDefinition app in selected)
                {
                    try
                    {
                        ProcessResult result = AppxTools.RemoveForCurrentUser(app.PackageName);
                        if (result.ExitCode != 0) errors.Add(app.DisplayName + ": " + result.Error.Trim());
                        else Logger.Info("Uygulama kaldırıldı: " + app.DisplayName);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(app.DisplayName + ": " + ex.Message);
                    }
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }

            MessageBox.Show(
                errors.Count == 0 ? "Seçilen uygulamalar kaldırıldı." : "Bazı uygulamalar kaldırılamadı:\n\n" + string.Join("\n", errors),
                "tercan.exe",
                MessageBoxButtons.OK,
                errors.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            RefreshCurrentPage();
        }

        private void ShowRecoveryPage()
        {
            pageTitle.Text = "Geri Alma";
            pageDescription.Text = "Tercan'ın değiştirdiği ayarları tek tek veya topluca eski değerine döndürün.";

            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(24, 8, 24, 24);
            content.Controls.Add(root);

            SmoothPanel panel = new SmoothPanel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = AppTheme.Surface;
            root.Controls.Add(panel);

            Label title = UiFactory.Label("Tercan yedekleri", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            panel.Controls.Add(title);

            ListView list = new ListView();
            list.Location = new Point(24, 70);
            list.Size = new Size(930, 480);
            list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = true;
            list.BackColor = AppTheme.SurfaceRaised;
            list.ForeColor = AppTheme.Text;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Columns.Add("Ayar", 380);
            list.Columns.Add("Kategori", 150);
            list.Columns.Add("Yedek tarihi", 220);
            list.Columns.Add("Kimlik", 170);
            foreach (KeyValuePair<string, TweakBackup> item in backupStore.Document.Tweaks)
            {
                TweakDefinition tweak = tweaks.FirstOrDefault(x => x.Id == item.Key);
                ListViewItem row = new ListViewItem(tweak == null ? item.Key : tweak.Title);
                row.SubItems.Add(tweak == null ? "Eski eklenti" : tweak.Category);
                DateTime captured;
                row.SubItems.Add(DateTime.TryParse(item.Value.CapturedAt, out captured) ? captured.ToString("dd.MM.yyyy HH:mm") : item.Value.CapturedAt);
                row.SubItems.Add(item.Key);
                row.Tag = item.Key;
                list.Items.Add(row);
            }
            panel.Controls.Add(list);

            Label empty = UiFactory.Label(
                list.Items.Count == 0 ? "Henüz Tercan tarafından değiştirilmiş bir ayar yok." : list.Items.Count + " ayar için geri dönüş yedeği var.",
                AppTheme.Body,
                list.Items.Count == 0 ? AppTheme.TextMuted : AppTheme.Green);
            empty.Location = new Point(25, 567);
            empty.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            panel.Controls.Add(empty);

            Button selected = UiFactory.Button("Seçileni geri al", AppTheme.Accent, Color.White);
            selected.Location = new Point(25, 612);
            selected.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            selected.Click += delegate { RevertSelected(list); };
            panel.Controls.Add(selected);

            Button all = UiFactory.Button("Tümünü geri al", AppTheme.Red, Color.White);
            all.Location = new Point(175, 612);
            all.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            all.Click += delegate { RevertAll(); };
            panel.Controls.Add(all);

            Button restorePoint = UiFactory.Button("Geri yükleme noktası oluştur", AppTheme.SurfaceRaised, AppTheme.Text);
            restorePoint.Location = new Point(320, 612);
            restorePoint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            restorePoint.Click += delegate { CreateRestorePointNow(); };
            panel.Controls.Add(restorePoint);

            Button openFolder = UiFactory.Button("Yedek klasörünü aç", AppTheme.SurfaceRaised, AppTheme.Cyan);
            openFolder.Location = new Point(545, 612);
            openFolder.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            openFolder.Click += delegate { ProcessRunner.Open(Path.GetDirectoryName(AppPaths.BackupFile)); };
            panel.Controls.Add(openFolder);
        }

        private void RevertSelected(ListView list)
        {
            List<string> ids = list.SelectedItems.Cast<ListViewItem>().Select(x => Convert.ToString(x.Tag)).ToList();
            if (ids.Count == 0)
            {
                MessageBox.Show("Geri almak için en az bir ayar seçin.", "tercan.exe");
                return;
            }
            if (MessageBox.Show(ids.Count + " ayar eski değerine döndürülecek. Devam edilsin mi?", "Geri alma", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            RevertIds(ids);
        }

        private void RevertAll()
        {
            List<string> ids = backupStore.Document.Tweaks.Keys.ToList();
            if (ids.Count == 0) return;
            if (MessageBox.Show(
                "Tercan tarafından uygulanmış tüm ayarlar eski değerlerine döndürülecek. Devam edilsin mi?",
                "Tümünü geri al",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            RevertIds(ids);
        }

        private void RevertIds(List<string> ids)
        {
            List<string> errors = new List<string>();
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (string id in ids)
                {
                    TweakDefinition tweak = tweaks.FirstOrDefault(x => x.Id == id);
                    if (tweak == null)
                    {
                        errors.Add(id + ": eklenti tanımı artık mevcut değil.");
                        continue;
                    }
                    try { engine.Revert(tweak); }
                    catch (Exception ex) { errors.Add(tweak.Title + ": " + ex.Message); }
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }
            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Bazı ayarlar geri alınamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshCurrentPage();
        }

        private void CreateRestorePointNow()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                ProcessResult result = RestorePointTools.Create("Tercan manuel yedek");
                if (result.ExitCode == 0) MessageBox.Show("Geri yükleme noktası oluşturuldu.", "tercan.exe");
                else MessageBox.Show(result.Error, "Geri yükleme noktası oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Geri yükleme noktası oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ShowAboutPage()
        {
            pageTitle.Text = "Hakkında";
            pageDescription.Text = "Ölçülebilir, geri alınabilir ve kaynak gösteren Windows oyun optimizasyonu.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel about = new SmoothPanel();
            about.Width = 1010;
            about.Height = 244;
            about.BackColor = AppTheme.Surface;
            about.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(about);
            PictureBox aboutLogo = BrandAssets.CreateLogoBox(132, 132);
            aboutLogo.Location = new Point(24, 28);
            about.Controls.Add(aboutLogo);
            Label title = UiFactory.Label("tercan.exe 1.8.0", new Font("Segoe UI Semibold", 20f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(178, 25);
            about.Controls.Add(title);
            Label text = UiFactory.Label(
                "Windows 10 ve Windows 11 için oyun optimizasyonu, performans analizi, uygulama kurulumu, isteğe bağlı DPI bağlantı yönetimi ve güvenli bakım merkezidir. Hellzerg Optimizer'ın geniş araç yaklaşımından, " +
                "Unlost'un 2026 rehberindeki kullanıcı beklentilerinden ve Microsoft'un resmî Windows belgelerinden yararlanır; başka bir projenin kodunu kopyalamaz.",
                AppTheme.Body,
                AppTheme.TextMuted);
            text.Location = new Point(179, 71);
            text.MaximumSize = new Size(790, 82);
            text.AutoSize = false;
            text.Size = new Size(790, 82);
            about.Controls.Add(text);
            Label principle = UiFactory.Label(
                "Temel ilke: Önce ölç • Tek değişiklik uygula • Aynı sahnede karşılaştır • Gerekirse geri al",
                AppTheme.Subheading,
                AppTheme.Cyan);
            principle.Location = new Point(25, 190);
            about.Controls.Add(principle);

            SmoothPanel sources = new SmoothPanel();
            sources.Width = 1010;
            sources.Height = 484;
            sources.BackColor = AppTheme.Surface;
            sources.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(sources);
            Label sourcesTitle = UiFactory.Label("Doğrulanan ana kaynaklar", AppTheme.Subheading, AppTheme.Text);
            sourcesTitle.Location = new Point(24, 20);
            sources.Controls.Add(sourcesTitle);
            AddSourceLink(sources, 60, "Microsoft – Windows 11 ayar referansı", "https://learn.microsoft.com/en-us/windows/apps/develop/settings/settings-windows-11");
            AddSourceLink(sources, 98, "Microsoft – güç ve performans modları", "https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-power-slider");
            AddSourceLink(sources, 136, "Microsoft – MMAgent bellek sıkıştırması", "https://learn.microsoft.com/en-us/powershell/module/mmagent/enable-mmagent");
            AddSourceLink(sources, 174, "Wagnardsoft – resmî ISLC indirmesi", IslcIntegration.OfficialPage);
            AddSourceLink(sources, 212, "Hellzerg Optimizer – GitHub", "https://github.com/hellzerg/optimizer");
            AddSourceLink(sources, 250, "Unlost 2026 bilgisayar hızlandırma rehberi", "https://www.youtube.com/watch?v=uMDPDyRnsvo");
            AddSourceLink(sources, 288, "Microsoft – WinGet uygulama kurulum belgeleri", "https://learn.microsoft.com/en-us/windows/package-manager/winget/install");
            AddSourceLink(sources, 326, "Microsoft – WinGet paket manifest deposu", "https://github.com/microsoft/winget-pkgs");
            AddSourceLink(sources, 364, "Razer Cortex – Game Booster çalışma yaklaşımı", "https://www.razer.com/cortex/boost");
            AddSourceLink(sources, 402, "GoodbyeDPI-Turkey – resmî GitHub deposu", GoodbyeDpiIntegration.OfficialRepository);
            AddSourceLink(sources, 440, "ValdikSS GoodbyeDPI – ana proje", "https://github.com/ValdikSS/GoodbyeDPI");

            SmoothPanel exclusions = new SmoothPanel();
            exclusions.Width = 1010;
            exclusions.Height = 170;
            exclusions.BackColor = Color.FromArgb(33, 28, 35);
            exclusions.BorderColor = AppTheme.Amber;
            exclusions.Margin = new Padding(0, 0, 0, 24);
            flow.Controls.Add(exclusions);
            Label exclusionsTitle = UiFactory.Label("Bilerek otomatikleştirilmeyen ayarlar", AppTheme.Subheading, AppTheme.Amber);
            exclusionsTitle.Location = new Point(24, 20);
            exclusions.Controls.Add(exclusionsTitle);
            Label exclusionsText = UiFactory.Label(
                "• Microsoft Defender ve Windows Update'i kapatma\n" +
                "• HPET / useplatformclock / disabledynamictick BCD değişiklikleri\n" +
                "• BIOS voltajı, XMP/EXPO veya overclock\n" +
                "• Gerçek zamanlı işlem önceliği ve rastgele TCP kayıtları",
                AppTheme.Body,
                AppTheme.TextMuted);
            exclusionsText.Location = new Point(26, 58);
            exclusions.Controls.Add(exclusionsText);
        }

        private void AddSourceLink(Control parent, int top, string label, string url)
        {
            LinkLabel link = new LinkLabel();
            link.Text = label;
            link.Font = AppTheme.Body;
            link.LinkColor = AppTheme.Cyan;
            link.ActiveLinkColor = Color.White;
            link.AutoSize = true;
            link.Location = new Point(26, top);
            link.LinkClicked += delegate { ProcessRunner.Open(url); };
            parent.Controls.Add(link);
        }

        private void RefreshCurrentPage()
        {
            Navigate(currentPage, currentCategory);
            UpdateApplyBar();
        }

        private void StopPageTimers()
        {
            if (pageRevealTimer != null)
            {
                pageRevealTimer.Stop();
                pageRevealTimer.Dispose();
                pageRevealTimer = null;
            }
            if (memoryTimer != null)
            {
                memoryTimer.Stop();
                memoryTimer.Dispose();
                memoryTimer = null;
            }
        }

        private FlowLayoutPanel NewPageFlow()
        {
            FlowLayoutPanel flow = new ModernScrollFlowPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.Padding = new Padding(24, 8, 24, 24);
            flow.BackColor = Color.Transparent;
            return flow;
        }

        private int CalculateReadinessScore()
        {
            List<TweakDefinition> recommended = tweaks.Where(x => x.Recommended && x.Risk != RiskLevel.Experimental).ToList();
            if (recommended.Count == 0) return 0;
            int applied = recommended.Count(engine.IsApplied);
            return (int)Math.Round(applied * 100.0 / recommended.Count);
        }

        private decimal DefaultFreeThreshold()
        {
            ulong gb = systemInfo.TotalRamBytes / 1024UL / 1024UL / 1024UL;
            if (gb <= 8) return 1024;
            if (gb <= 16) return 2048;
            if (gb <= 32) return 4096;
            return 8192;
        }

        private void BuildTrayIcon()
        {
            trayIcon = new NotifyIcon();
            trayIcon.Icon = Icon;
            trayIcon.Text = "Tercan Bellek İzleyici";
            trayIcon.Visible = false;
            trayIcon.DoubleClick += delegate
            {
                RestoreFromTray();
            };
            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("Tercan'ı göster", delegate
            {
                RestoreFromTray();
            });
            menu.MenuItems.Add("Bellek izleyiciyi durdur", delegate
            {
                memoryCleanerActive = false;
                trayIcon.Visible = backgroundStartMode;
                UpdateMemoryMonitorSchedule();
                if (memoryStartButton != null && !memoryStartButton.IsDisposed)
                {
                    memoryStartButton.Text = "Arka planda başlat";
                    memoryStartButton.BackColor = AppTheme.Accent;
                }
            });
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Çıkış", delegate { memoryCleanerActive = false; Close(); });
            trayIcon.ContextMenu = menu;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized &&
                (memoryCleanerActive || backgroundStartMode) &&
                !previewMode)
            {
                Hide();
                trayIcon.Visible = true;
                trayIcon.ShowBalloonTip(
                    2000,
                    "tercan.exe",
                    memoryCleanerActive
                        ? "Bellek izleyici düşük yükte arka planda çalışıyor."
                        : "Tercan düşük yükte bildirim alanında bekliyor.",
                    ToolTipIcon.Info);
            }
        }

        private void RestoreFromTray()
        {
            try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal; } catch { }
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (quickGameModeBusy)
            {
                e.Cancel = true;
                MessageBox.Show(
                    "Oyun Kipi geçişi devam ediyor. İşlem tamamlandığında Tercan'ı kapatabilirsiniz.",
                    "Oyun Kipi hazırlanıyor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (oneClickBusy)
            {
                e.Cancel = true;
                MessageBox.Show(
                    "Tek tık bakım işlemi devam ediyor. İşlem tamamlandıktan sonra Tercan'ı kapatabilirsiniz.",
                    "Bakım devam ediyor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (installationRunning)
            {
                e.Cancel = true;
                MessageBox.Show(
                    "Uygulama kurulumu devam ediyor. Kurulum tamamlandıktan sonra Tercan'ı kapatabilirsiniz.",
                    "Kurulum devam ediyor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!previewMode && focusEngine.IsActive)
            {
                try
                {
                    focusEngine.Deactivate(true);
                }
                catch (Exception ex)
                {
                    Logger.Error("Kapanışta Oyun Odak Modu geri yüklenemedi", ex);
                }
            }

            StopPageTimers();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }

        private static string CategoryDescription(string category)
        {
            if (category == "Oyun") return "Oyun Modu, yakalama ve güç planı gibi doğrudan oyun deneyimiyle ilgili ayarlar.";
            if (category == "Arka Plan") return "Oyun sırasında kaynak tüketebilen Windows görevlerini dikkatle yönetin.";
            if (category == "Görünüm") return "Masaüstünü sadeleştirin; oyun içi grafik kalitesi değişmez.";
            if (category == "Gizlilik") return "Tanılama ve kişiselleştirme özelliklerini azaltın; FPS etkisi genellikle düşüktür.";
            if (category == "Ağ") return "Oyun sırasında gereksiz ağ trafiğini azaltabilecek güvenli seçenekler.";
            if (category == "Deneysel") return "Her sistemde farklı sonuç verebilen, yalnızca ölçerek kullanılması gereken ayarlar.";
            if (category == "Eklentiler") return "Modules klasöründeki doğrulanmış kayıt defteri eklentileri.";
            return "Tüm Tercan ayarları.";
        }

        private static string Shorten(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Bilinmiyor";
            value = value.Trim();
            return value.Length <= max ? value : value.Substring(0, max - 1) + "…";
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "Bilinmiyor";
            double gb = bytes / 1024d / 1024d / 1024d;
            return gb.ToString("0.#") + " GB";
        }

        private static Icon CreateAppIcon()
        {
            Icon brandIcon = BrandAssets.LoadAppIcon();
            if (brandIcon != null) return brandIcon;
            using (Bitmap bitmap = new Bitmap(64, 64))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (SolidBrush background = new SolidBrush(AppTheme.Accent))
                {
                    graphics.FillEllipse(background, 2, 2, 60, 60);
                }
                using (Font font = new Font("Segoe UI Semibold", 25f, FontStyle.Bold))
                using (SolidBrush foreground = new SolidBrush(Color.White))
                {
                    graphics.DrawString("T", font, foreground, new PointF(17, 10));
                }
                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
    }
}

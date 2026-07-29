using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace TercanOptimizer
{
    internal sealed partial class MainForm
    {
        private Label dpiPackageStatusLabel;
        private Label dpiEngineStatusLabel;
        private Label dpiProfileDescriptionLabel;
        private Label dpiStartupDetailLabel;
        private ProgressBar dpiDownloadProgress;
        private Button dpiDownloadButton;
        private Button dpiStartButton;
        private Button dpiStopButton;
        private ComboBox dpiProfileCombo;
        private ToggleSwitch dpiStartupToggle;
        private ToggleSwitch tercanStartupToggle;
        private DpiStatusOrb dpiStatusOrb;
        private bool dpiUiUpdating;
        private bool dpiDownloadRunning;

        private void ShowGoodbyeDpiPage()
        {
            pageTitle.Text = "Discord / DPI";
            pageDescription.Text = "GoodbyeDPI-Turkey motorunu doğrulanmış paket, Türkiye profili ve tek tık denetimlerle yönetin.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            GoodbyeDpiStatus status = ReadGoodbyeDpiStatusForUi();

            TercanHeroPanel hero = new TercanHeroPanel();
            hero.Width = 1010;
            hero.Height = 280;
            hero.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(hero);

            dpiStatusOrb = new DpiStatusOrb();
            dpiStatusOrb.Location = new Point(42, 49);
            dpiStatusOrb.Active = status.IsRunning;
            hero.Controls.Add(dpiStatusOrb);

            Label eyebrow = UiFactory.Label(
                "DOĞRULANMIŞ TÜRKİYE PROFİLİ",
                new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                AppTheme.Cyan);
            eyebrow.Location = new Point(250, 31);
            hero.Controls.Add(eyebrow);

            Label title = UiFactory.Label(
                "Discord ve DPI bağlantı paneli",
                new Font("Segoe UI Semibold", 23f, FontStyle.Bold),
                AppTheme.Text);
            title.Location = new Point(248, 61);
            hero.Controls.Add(title);

            Label copy = UiFactory.Label(
                "Tercan, cagritaskn/GoodbyeDPI-Turkey v" + GoodbyeDpiIntegration.Version +
                " paketini yalnızca resmî GitHub sürümünden indirir. ZIP ve çalıştırılabilir dosyalar SHA-256 ile doğrulanmadan motor başlatılmaz.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(251, 108);
            copy.Size = new Size(700, 52);
            copy.AutoSize = false;
            hero.Controls.Add(copy);

            dpiPackageStatusLabel = UiFactory.Label(string.Empty, AppTheme.Body, AppTheme.Text);
            dpiPackageStatusLabel.Location = new Point(251, 170);
            hero.Controls.Add(dpiPackageStatusLabel);

            dpiEngineStatusLabel = UiFactory.Label(string.Empty, AppTheme.Body, AppTheme.Text);
            dpiEngineStatusLabel.Location = new Point(251, 201);
            hero.Controls.Add(dpiEngineStatusLabel);

            FlowLayoutPanel heroBadges = new FlowLayoutPanel();
            heroBadges.Location = new Point(251, 233);
            heroBadges.Width = 700;
            heroBadges.Height = 30;
            heroBadges.WrapContents = false;
            heroBadges.BackColor = Color.Transparent;
            heroBadges.Controls.Add(UiFactory.Pill("VPN DEĞİL", AppTheme.Amber));
            heroBadges.Controls.Add(UiFactory.Pill("DEFENDER DIŞLAMASI YOK", AppTheme.Green));
            heroBadges.Controls.Add(UiFactory.Pill("İSTEĞE BAĞLI", AppTheme.Cyan));
            hero.Controls.Add(heroBadges);

            SmoothPanel control = new SmoothPanel();
            control.Width = 1010;
            control.Height = 300;
            control.Margin = new Padding(0, 0, 0, 16);
            control.BackColor = AppTheme.Surface;
            flow.Controls.Add(control);

            Label controlTitle = UiFactory.Label("Motor ve profil", AppTheme.Subheading, AppTheme.Text);
            controlTitle.Location = new Point(24, 19);
            control.Controls.Add(controlTitle);

            Label profileTitle = UiFactory.Label("Bağlantı profili", AppTheme.Small, AppTheme.TextMuted);
            profileTitle.Location = new Point(25, 61);
            control.Controls.Add(profileTitle);

            dpiProfileCombo = new ComboBox();
            dpiProfileCombo.Location = new Point(24, 84);
            dpiProfileCombo.Size = new Size(330, 32);
            dpiProfileCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            dpiProfileCombo.DrawMode = DrawMode.OwnerDrawFixed;
            dpiProfileCombo.ItemHeight = 27;
            dpiProfileCombo.BackColor = AppTheme.SurfaceRaised;
            dpiProfileCombo.ForeColor = AppTheme.Text;
            dpiProfileCombo.FlatStyle = FlatStyle.Flat;
            dpiProfileCombo.Font = AppTheme.Body;
            dpiProfileCombo.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                using (SolidBrush background = new SolidBrush(AppTheme.SurfaceRaised))
                {
                    e.Graphics.FillRectangle(background, e.Bounds);
                }
                GoodbyeDpiProfile item = (GoodbyeDpiProfile)dpiProfileCombo.Items[e.Index];
                TextRenderer.DrawText(
                    e.Graphics,
                    item.Name,
                    dpiProfileCombo.Font,
                    e.Bounds,
                    AppTheme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
            foreach (GoodbyeDpiProfile profile in GoodbyeDpiIntegration.Profiles())
            {
                dpiProfileCombo.Items.Add(profile);
            }

            string preferredProfile = GoodbyeDpiPreferenceStore.Load().ProfileId;
            for (int i = 0; i < dpiProfileCombo.Items.Count; i++)
            {
                GoodbyeDpiProfile candidate = (GoodbyeDpiProfile)dpiProfileCombo.Items[i];
                if (string.Equals(candidate.Id, preferredProfile, StringComparison.OrdinalIgnoreCase))
                {
                    dpiProfileCombo.SelectedIndex = i;
                    break;
                }
            }
            if (dpiProfileCombo.SelectedIndex < 0) dpiProfileCombo.SelectedIndex = 0;
            control.Controls.Add(dpiProfileCombo);

            dpiProfileDescriptionLabel = UiFactory.Label(string.Empty, AppTheme.Body, AppTheme.TextMuted);
            dpiProfileDescriptionLabel.Location = new Point(25, 130);
            dpiProfileDescriptionLabel.Size = new Size(930, 42);
            dpiProfileDescriptionLabel.AutoSize = false;
            control.Controls.Add(dpiProfileDescriptionLabel);

            dpiDownloadButton = UiFactory.Button(
                status.PackageVerified ? "Paketi yeniden doğrula" : "Resmî paketi indir",
                AppTheme.Accent,
                Color.White);
            dpiDownloadButton.Location = new Point(24, 188);
            dpiDownloadButton.Click += delegate { BeginGoodbyeDpiDownload(); };
            control.Controls.Add(dpiDownloadButton);

            dpiStartButton = UiFactory.Button("Türkiye profilini başlat", AppTheme.Green, Color.FromArgb(8, 30, 22));
            dpiStartButton.Location = new Point(214, 188);
            dpiStartButton.Click += delegate { StartGoodbyeDpi(); };
            control.Controls.Add(dpiStartButton);

            dpiStopButton = UiFactory.Button("Durdur", AppTheme.SurfaceRaised, AppTheme.Text);
            dpiStopButton.Location = new Point(425, 188);
            dpiStopButton.Click += delegate { StopGoodbyeDpi(); };
            control.Controls.Add(dpiStopButton);

            Button refresh = UiFactory.Button("Durumu yenile", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            refresh.Location = new Point(525, 188);
            refresh.Click += delegate { RefreshGoodbyeDpiUi(); };
            control.Controls.Add(refresh);

            dpiDownloadProgress = new ProgressBar();
            dpiDownloadProgress.Location = new Point(24, 246);
            dpiDownloadProgress.Size = new Size(930, 8);
            dpiDownloadProgress.Style = ProgressBarStyle.Continuous;
            dpiDownloadProgress.Visible = false;
            control.Controls.Add(dpiDownloadProgress);

            Label hash = UiFactory.Label(
                "Sabit sürüm: " + GoodbyeDpiIntegration.Version +
                "  •  ZIP SHA-256: " + GoodbyeDpiIntegration.OfficialZipSha256.Substring(0, 12) + "…",
                AppTheme.Small,
                AppTheme.TextMuted);
            hash.Location = new Point(25, 267);
            control.Controls.Add(hash);

            dpiProfileCombo.SelectedIndexChanged += delegate
            {
                GoodbyeDpiProfile selected = SelectedGoodbyeDpiProfile();
                GoodbyeDpiPreferenceStore.Save(selected.Id);
                UpdateGoodbyeDpiProfileDescription();
            };
            UpdateGoodbyeDpiProfileDescription();

            SmoothPanel startup = new SmoothPanel();
            startup.Width = 1010;
            startup.Height = 236;
            startup.Margin = new Padding(0, 0, 0, 16);
            startup.BackColor = AppTheme.Surface;
            flow.Controls.Add(startup);

            Label startupTitle = UiFactory.Label("Windows başlangıcı", AppTheme.Subheading, AppTheme.Text);
            startupTitle.Location = new Point(24, 19);
            startup.Controls.Add(startupTitle);
            Label startupCopy = UiFactory.Label(
                "İki ayar da varsayılan olarak kapalıdır. GoodbyeDPI hizmet olarak bağımsız çalışabilir; bu nedenle yalnız DPI için Tercan'ın açık kalması gerekmez.",
                AppTheme.Body,
                AppTheme.TextMuted);
            startupCopy.Location = new Point(25, 54);
            startupCopy.Size = new Size(930, 38);
            startupCopy.AutoSize = false;
            startup.Controls.Add(startupCopy);

            AddStartupToggleRow(
                startup,
                105,
                "GoodbyeDPI Windows ile başlasın",
                "Seçili Türkiye profilini otomatik başlayan Windows hizmeti olarak kurar.",
                out dpiStartupToggle);
            AddStartupToggleRow(
                startup,
                164,
                "tercan.exe bildirim alanında başlasın",
                "Arayüzü açmadan, tarama yapmadan ve zamanlayıcı çalıştırmadan düşük yükte bekler.",
                out tercanStartupToggle);

            dpiStartupDetailLabel = UiFactory.Label(string.Empty, AppTheme.Small, AppTheme.TextMuted);
            dpiStartupDetailLabel.Location = new Point(650, 118);
            startup.Controls.Add(dpiStartupDetailLabel);

            dpiStartupToggle.CheckedChanged += delegate { ChangeGoodbyeDpiStartup(); };
            tercanStartupToggle.CheckedChanged += delegate { ChangeTercanStartup(); };

            SmoothPanel warning = new SmoothPanel();
            warning.Width = 1010;
            warning.Height = 228;
            warning.Margin = new Padding(0, 0, 0, 24);
            warning.BackColor = Color.FromArgb(31, 27, 24);
            warning.BorderColor = AppTheme.Amber;
            flow.Controls.Add(warning);

            Label warningTitle = UiFactory.Label("Bilmeniz gerekenler", AppTheme.Subheading, AppTheme.Amber);
            warningTitle.Location = new Point(24, 18);
            warning.Controls.Add(warningTitle);
            Label warningText = UiFactory.Label(
                "• GoodbyeDPI bir VPN değildir; trafiği şifrelemez, IP adresinizi gizlemez ve FPS/internet hızı artışı vaat etmez.\n" +
                "• Ağ paketlerini WinDivert üzerinden işler. Bazı güvenlik yazılımları yanlış pozitif verebilir; Tercan Defender dışlaması eklemez.\n" +
                "• Profil sonucu ISS'ye göre değişebilir. Bağlantı sorunu olursa motoru durdurun.\n" +
                "• Resmî Türkiye deposu 29.07.2025 notunda bazı Discord/içerik sorunları için daha yeni SplitWire-Turkey aracını öneriyor.\n" +
                "• Kullanımın yerel mevzuata ve ağ hizmeti koşullarına uygunluğu kullanıcı sorumluluğundadır.",
                AppTheme.Body,
                AppTheme.TextMuted);
            warningText.Location = new Point(25, 53);
            warningText.Size = new Size(950, 112);
            warningText.AutoSize = false;
            warning.Controls.Add(warningText);

            AddSourceLink(warning, 178, "GoodbyeDPI-Turkey GitHub", GoodbyeDpiIntegration.OfficialRepository);
            AddSourceLink(warning, 202, "Resmî sürüm ve lisans", GoodbyeDpiIntegration.OfficialReleasePage);
            AddSourceLink(warning, 178, "Yeni SplitWire-Turkey", GoodbyeDpiIntegration.ReplacementRepository);
            Control splitWireLink = warning.Controls[warning.Controls.Count - 1];
            splitWireLink.Left = 500;

            RefreshGoodbyeDpiUi();
        }

        private void AddStartupToggleRow(
            Control parent,
            int top,
            string title,
            string description,
            out ToggleSwitch toggle)
        {
            toggle = new ToggleSwitch();
            toggle.Location = new Point(25, top + 2);
            parent.Controls.Add(toggle);
            Label titleLabel = UiFactory.Label(title, new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AppTheme.Text);
            titleLabel.Location = new Point(90, top);
            parent.Controls.Add(titleLabel);
            Label detail = UiFactory.Label(description, AppTheme.Small, AppTheme.TextMuted);
            detail.Location = new Point(91, top + 27);
            parent.Controls.Add(detail);
        }

        private GoodbyeDpiProfile SelectedGoodbyeDpiProfile()
        {
            return dpiProfileCombo == null
                ? GoodbyeDpiIntegration.Profiles()[0]
                : (dpiProfileCombo.SelectedItem as GoodbyeDpiProfile ?? GoodbyeDpiIntegration.Profiles()[0]);
        }

        private void UpdateGoodbyeDpiProfileDescription()
        {
            if (dpiProfileDescriptionLabel == null || dpiProfileDescriptionLabel.IsDisposed) return;
            GoodbyeDpiProfile profile = SelectedGoodbyeDpiProfile();
            dpiProfileDescriptionLabel.Text =
                (profile.Caution ? "Temkinli profil • " : "Önerilen profil • ") + profile.Description;
            dpiProfileDescriptionLabel.ForeColor = profile.Caution ? AppTheme.Amber : AppTheme.TextMuted;
        }

        private void RefreshGoodbyeDpiUi()
        {
            if (dpiPackageStatusLabel == null || dpiPackageStatusLabel.IsDisposed) return;
            GoodbyeDpiStatus status = ReadGoodbyeDpiStatusForUi();
            bool tercanStarts = previewMode ? false : TercanStartupManager.IsEnabled();

            dpiPackageStatusLabel.Text = status.PackageVerified
                ? "✓ Paket kurulu ve dosyaları doğrulandı"
                : "○ Paket indirilmedi";
            dpiPackageStatusLabel.ForeColor = status.PackageVerified ? AppTheme.Green : AppTheme.TextMuted;

            if (status.ExternalService)
            {
                dpiEngineStatusLabel.Text = "⚠ Haricî GoodbyeDPI hizmeti bulundu • Tercan değiştirmeyecek";
                dpiEngineStatusLabel.ForeColor = AppTheme.Amber;
            }
            else if (status.IsRunning)
            {
                dpiEngineStatusLabel.Text = status.StartsWithWindows
                    ? "✓ Motor çalışıyor • Windows başlangıcı açık"
                    : "✓ Motor çalışıyor • yalnız bu oturum";
                dpiEngineStatusLabel.ForeColor = AppTheme.Cyan;
            }
            else
            {
                dpiEngineStatusLabel.Text = "○ Motor kapalı";
                dpiEngineStatusLabel.ForeColor = AppTheme.TextMuted;
            }

            if (dpiStatusOrb != null && !dpiStatusOrb.IsDisposed) dpiStatusOrb.Active = status.IsRunning;
            if (dpiStartButton != null) dpiStartButton.Enabled = status.PackageVerified && !status.ExternalService && !dpiDownloadRunning;
            if (dpiStopButton != null) dpiStopButton.Enabled = status.IsRunning && !status.ExternalService;
            if (dpiDownloadButton != null) dpiDownloadButton.Enabled = !dpiDownloadRunning;

            dpiUiUpdating = true;
            try
            {
                dpiStartupToggle.Checked = status.StartsWithWindows;
                tercanStartupToggle.Checked = tercanStarts;
            }
            finally
            {
                dpiUiUpdating = false;
            }

            dpiStartupDetailLabel.Text = status.StartsWithWindows
                ? "DPI hizmeti: OTOMATİK"
                : "DPI hizmeti: KAPALI";
            dpiStartupDetailLabel.ForeColor = status.StartsWithWindows ? AppTheme.Green : AppTheme.TextMuted;
        }

        private GoodbyeDpiStatus ReadGoodbyeDpiStatusForUi()
        {
            if (previewMode)
            {
                return new GoodbyeDpiStatus();
            }
            return GoodbyeDpiIntegration.ReadStatus();
        }

        private void BeginGoodbyeDpiDownload()
        {
            if (dpiDownloadRunning) return;
            if (GoodbyeDpiIntegration.IsInstalledAndVerified())
            {
                MessageBox.Show(
                    "Kurulu GoodbyeDPI dosyalarının tamamı SHA-256 ile yeniden doğrulandı.",
                    "Paket doğrulandı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                RefreshGoodbyeDpiUi();
                return;
            }

            DialogResult answer = MessageBox.Show(
                "GoodbyeDPI-Turkey v" + GoodbyeDpiIntegration.Version +
                " yalnızca resmî GitHub sürümünden indirilecek. ZIP ve motor dosyaları SHA-256 ile denetlenecek; uyuşmayan dosya çalıştırılmadan silinecek.\n\nİndirmek istiyor musunuz?",
                "Resmî GoodbyeDPI-Turkey paketi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            try
            {
                if (File.Exists(GoodbyeDpiIntegration.PartialArchivePath))
                {
                    File.Delete(GoodbyeDpiIntegration.PartialArchivePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "İndirme hazırlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            dpiDownloadRunning = true;
            dpiDownloadButton.Enabled = false;
            dpiStartButton.Enabled = false;
            dpiDownloadProgress.Visible = true;
            dpiDownloadProgress.Value = 0;
            dpiPackageStatusLabel.Text = "Resmî GitHub sürümü indiriliyor…";
            dpiPackageStatusLabel.ForeColor = AppTheme.Cyan;

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            WebClient client = new WebClient();
            client.Headers.Add("User-Agent", "tercan.exe/1.8.0");
            client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e)
            {
                if (dpiDownloadProgress != null && !dpiDownloadProgress.IsDisposed)
                {
                    dpiDownloadProgress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                }
            };
            client.DownloadFileCompleted += delegate(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
            {
                client.Dispose();
                dpiDownloadRunning = false;
                if (dpiDownloadProgress != null && !dpiDownloadProgress.IsDisposed)
                {
                    dpiDownloadProgress.Visible = false;
                }

                if (e.Error != null || e.Cancelled)
                {
                    try { if (File.Exists(GoodbyeDpiIntegration.PartialArchivePath)) File.Delete(GoodbyeDpiIntegration.PartialArchivePath); } catch { }
                    MessageBox.Show(
                        e.Error == null ? "İndirme iptal edildi." : e.Error.Message,
                        "GoodbyeDPI indirilemedi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    RefreshGoodbyeDpiUi();
                    return;
                }

                try
                {
                    if (!GoodbyeDpiIntegration.IsArchiveVerified(GoodbyeDpiIntegration.PartialArchivePath))
                    {
                        File.Delete(GoodbyeDpiIntegration.PartialArchivePath);
                        throw new InvalidDataException(
                            "İndirilen ZIP paketinin SHA-256 özeti resmî sabit değerle eşleşmedi. Dosya silindi.");
                    }
                    if (File.Exists(GoodbyeDpiIntegration.ArchivePath))
                    {
                        File.Delete(GoodbyeDpiIntegration.ArchivePath);
                    }
                    File.Move(GoodbyeDpiIntegration.PartialArchivePath, GoodbyeDpiIntegration.ArchivePath);
                    GoodbyeDpiIntegration.InstallVerifiedArchive(GoodbyeDpiIntegration.ArchivePath);
                    MessageBox.Show(
                        "GoodbyeDPI-Turkey indirildi, ZIP ve motor dosyaları doğrulandı. Başlatma işlemi yalnız düğmeye bastığınızda yapılır.",
                        "Paket hazır",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error("GoodbyeDPI paketi kurulamadı", ex);
                    MessageBox.Show(ex.Message, "GoodbyeDPI doğrulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                RefreshGoodbyeDpiUi();
            };
            client.DownloadFileAsync(
                new Uri(GoodbyeDpiIntegration.OfficialDownloadUrl),
                GoodbyeDpiIntegration.PartialArchivePath);
        }

        private void StartGoodbyeDpi()
        {
            GoodbyeDpiProfile profile = SelectedGoodbyeDpiProfile();
            if (profile.Caution)
            {
                DialogResult answer = MessageBox.Show(
                    "Bu profil yalnız standart yöntem bağlantınızda çalışmıyorsa önerilir. Bazı sitelerde bağlantı sorunu oluşturabilir.\n\nDevam edilsin mi?",
                    "Temkinli profil",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            try
            {
                GoodbyeDpiIntegration.Start(profile);
                RefreshGoodbyeDpiUi();
            }
            catch (Exception ex)
            {
                Logger.Error("GoodbyeDPI başlatılamadı", ex);
                MessageBox.Show(ex.Message, "GoodbyeDPI başlatılamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopGoodbyeDpi()
        {
            try
            {
                GoodbyeDpiIntegration.Stop();
                RefreshGoodbyeDpiUi();
            }
            catch (Exception ex)
            {
                Logger.Error("GoodbyeDPI durdurulamadı", ex);
                MessageBox.Show(ex.Message, "GoodbyeDPI durdurulamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ChangeGoodbyeDpiStartup()
        {
            if (dpiUiUpdating || previewMode) return;
            bool requested = dpiStartupToggle.Checked;
            try
            {
                if (requested)
                {
                    if (!GoodbyeDpiIntegration.IsInstalledAndVerified())
                    {
                        throw new InvalidOperationException("Önce resmî GoodbyeDPI paketini indirip doğrulayın.");
                    }
                    DialogResult answer = MessageBox.Show(
                        "GoodbyeDPI seçili profille otomatik başlayan Windows hizmeti olarak kurulacak. Tercan kapalıyken de çalışır.\n\nEtkinleştirilsin mi?",
                        "Windows ile başlat",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (answer != DialogResult.Yes)
                    {
                        RefreshGoodbyeDpiUi();
                        return;
                    }
                    GoodbyeDpiIntegration.EnableStartup(SelectedGoodbyeDpiProfile());
                }
                else
                {
                    GoodbyeDpiIntegration.DisableStartup();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("GoodbyeDPI başlangıç ayarı değiştirilemedi", ex);
                MessageBox.Show(ex.Message, "Başlangıç ayarı değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshGoodbyeDpiUi();
        }

        private void ChangeTercanStartup()
        {
            if (dpiUiUpdating || previewMode) return;
            try
            {
                TercanStartupManager.SetEnabled(tercanStartupToggle.Checked);
            }
            catch (Exception ex)
            {
                Logger.Error("Tercan başlangıç ayarı değiştirilemedi", ex);
                MessageBox.Show(ex.Message, "Başlangıç ayarı değiştirilemedi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshGoodbyeDpiUi();
        }
    }
}

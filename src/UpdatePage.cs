using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace TercanOptimizer
{
    internal sealed partial class MainForm
    {
        private UpdateCheckResult latestUpdate;
        private bool updateCheckBusy;
        private bool updateInstallBusy;

        private void ShowUpdatePage()
        {
            pageTitle.Text = "Güncellemeler";
            pageDescription.Text = "Yeni sürümleri GitHub üzerinden doğrulayın ve tek tıkla kurun.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            TercanHeroPanel hero = new TercanHeroPanel();
            hero.Width = 1010;
            hero.Height = 176;
            hero.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(hero);

            Label eyebrow = UiFactory.Label(
                "GÜVENLİ GÜNCELLEME MERKEZİ",
                new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                AppTheme.Cyan);
            eyebrow.Location = new Point(28, 22);
            hero.Controls.Add(eyebrow);

            Label title = UiFactory.Label(
                latestUpdate != null && latestUpdate.IsUpdateAvailable
                    ? "Yeni sürüm hazır: " + latestUpdate.LatestVersion
                    : "tercan.exe " + TercanUpdateService.CurrentVersion,
                new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                AppTheme.Text);
            title.Location = new Point(26, 51);
            hero.Controls.Add(title);

            string statusText = updateCheckBusy
                ? "GitHub yayını ve bütünlük bilgisi denetleniyor…"
                : latestUpdate == null
                    ? "Açılışta sessiz denetim yapılır; kurulum yalnız sizin düğmeye basmanızla başlar."
                    : latestUpdate.Message;
            Label status = UiFactory.Label(
                statusText,
                AppTheme.Body,
                latestUpdate != null && latestUpdate.IsUpdateAvailable
                    ? AppTheme.Green
                    : AppTheme.TextMuted);
            status.Location = new Point(29, 96);
            status.Size = new Size(700, 42);
            status.AutoSize = false;
            hero.Controls.Add(status);

            PictureBox logo = BrandAssets.CreateLogoBox(92, 92);
            logo.Location = new Point(746, 34);
            hero.Controls.Add(logo);

            Button action = UiFactory.Button(
                latestUpdate != null && latestUpdate.IsUpdateAvailable
                    ? "Tek tıkla güncelle"
                    : "Şimdi denetle",
                latestUpdate != null && latestUpdate.IsUpdateAvailable
                    ? AppTheme.Green
                    : AppTheme.Accent,
                latestUpdate != null && latestUpdate.IsUpdateAvailable
                    ? Color.FromArgb(6, 28, 19)
                    : Color.White);
            action.Location = new Point(850, 66);
            action.Size = new Size(136, 38);
            action.Enabled = !updateCheckBusy && !updateInstallBusy;
            action.Click += delegate
            {
                if (latestUpdate != null && latestUpdate.IsUpdateAvailable)
                {
                    StartOneClickUpdate(latestUpdate.Manifest);
                }
                else
                {
                    BeginUpdateCheck(false, true);
                }
            };
            hero.Controls.Add(action);

            SmoothPanel protection = new SmoothPanel();
            protection.Width = 1010;
            protection.Height = 174;
            protection.Margin = new Padding(0, 0, 0, 16);
            protection.BackColor = AppTheme.Surface;
            protection.BorderColor = Color.FromArgb(78, AppTheme.Green);
            flow.Controls.Add(protection);

            Label protectionTitle = UiFactory.Label(
                "Güncelleme nasıl korunuyor?",
                AppTheme.Subheading,
                AppTheme.Text);
            protectionTitle.Location = new Point(24, 20);
            protection.Controls.Add(protectionTitle);

            Label protectionText = UiFactory.Label(
                "1  GitHub'ın son sürüm kaydı okunur.\n" +
                "2  update.json içindeki sürüm, indirme adresi ve SHA-256 doğrulanır.\n" +
                "3  Yalnız Tercan24/Can yayın adresindeki setup indirilir.\n" +
                "4  Hash eşleşirse kurulum başlatılır; eşleşmezse dosya silinir.",
                AppTheme.Body,
                AppTheme.TextMuted);
            protectionText.Location = new Point(26, 58);
            protectionText.Size = new Size(760, 100);
            protectionText.AutoSize = false;
            protection.Controls.Add(protectionText);

            Label badge = UiFactory.Pill("SHA-256 + HTTPS", AppTheme.Green);
            badge.Location = new Point(826, 26);
            protection.Controls.Add(badge);

            SmoothPanel release = new SmoothPanel();
            release.Width = 1010;
            release.Height = 126;
            release.Margin = new Padding(0, 0, 0, 24);
            release.BackColor = Color.FromArgb(18, 20, 31);
            flow.Controls.Add(release);
            Label releaseTitle = UiFactory.Label(
                "Yayın kanalı",
                AppTheme.Subheading,
                AppTheme.Text);
            releaseTitle.Location = new Point(24, 20);
            release.Controls.Add(releaseTitle);
            Label releaseText = UiFactory.Label(
                "GitHub • Tercan24/Can • Stable\n" +
                "Özel depolar son kullanıcı güncellemesi için erişim anahtarı ister. Anahtar uygulamaya gömülmez.",
                AppTheme.Body,
                AppTheme.TextMuted);
            releaseText.Location = new Point(26, 56);
            releaseText.Size = new Size(780, 54);
            releaseText.AutoSize = false;
            release.Controls.Add(releaseText);
            Button openRelease = UiFactory.Button("Yayınları aç", AppTheme.SurfaceRaised, AppTheme.Cyan);
            openRelease.Location = new Point(842, 58);
            openRelease.Click += delegate
            {
                ProcessRunner.Open("https://github.com/" + TercanUpdateService.Repository + "/releases");
            };
            release.Controls.Add(openRelease);
        }

        private void ScheduleAutomaticUpdateCheck()
        {
            if (previewMode) return;
            Timer timer = new Timer();
            timer.Interval = 5000;
            timer.Tick += delegate
            {
                timer.Stop();
                timer.Dispose();
                BeginUpdateCheck(true, false);
            };
            timer.Start();
        }

        private void BeginUpdateCheck(bool automatic, bool installWhenAvailable)
        {
            if (updateCheckBusy || updateInstallBusy) return;
            updateCheckBusy = true;
            if (!automatic && currentPage == "updates") RefreshCurrentPage();

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = TercanUpdateService.CheckLatest();
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                updateCheckBusy = false;
                if (IsDisposed) return;
                if (e.Error != null)
                {
                    if (!automatic)
                    {
                        MessageBox.Show(
                            e.Error.Message,
                            "Güncelleme denetlenemedi",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    if (currentPage == "updates") RefreshCurrentPage();
                    return;
                }

                latestUpdate = e.Result as UpdateCheckResult;
                if (installWhenAvailable && latestUpdate != null && latestUpdate.IsUpdateAvailable)
                {
                    StartOneClickUpdate(latestUpdate.Manifest);
                    return;
                }
                if (currentPage == "updates") RefreshCurrentPage();
            };
            worker.RunWorkerAsync();
        }

        private void StartOneClickUpdate(UpdateManifest manifest)
        {
            if (updateInstallBusy) return;
            DialogResult answer = MessageBox.Show(
                "tercan.exe " + manifest.Version + " indirilecek, SHA-256 ile doğrulanacak ve kurulacak.\n\n" +
                "Kurulum başladığında Tercan güvenli biçimde kapanır. Devam edilsin mi?",
                "Tek tıkla güncelle",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (answer != DialogResult.Yes) return;

            updateInstallBusy = true;
            Cursor = Cursors.WaitCursor;
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = TercanUpdateService.DownloadVerifiedSetup(manifest);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                Cursor = Cursors.Default;
                updateInstallBusy = false;
                if (e.Error != null)
                {
                    MessageBox.Show(
                        e.Error.Message,
                        "Güncelleme indirilemedi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    if (currentPage == "updates") RefreshCurrentPage();
                    return;
                }
                try
                {
                    TercanUpdateService.LaunchInstaller(Convert.ToString(e.Result));
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        ex.Message,
                        "Güncelleme başlatılamadı",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            };
            worker.RunWorkerAsync();
        }
    }
}

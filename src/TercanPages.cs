using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TercanOptimizer
{
    internal sealed partial class MainForm
    {
        private SystemScanReport lastSystemScan;
        private bool systemScanRunning;

        private void ShowTercanScannerPage()
        {
            if (previewMode && lastSystemScan == null)
            {
                lastSystemScan = SystemScanEngine.CreatePreview();
            }

            pageTitle.Text = "TERCAN.EXE";
            pageDescription.Text = "Windows 10/11 Oyun Optimizasyon ve Bilgisayar Performans Merkezi";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SystemScanReport report = lastSystemScan;
            TercanHeroPanel hero = new TercanHeroPanel();
            hero.Width = 1010;
            hero.Height = 310;
            hero.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(hero);

            AutomotiveGauge gauge = new AutomotiveGauge();
            gauge.Location = new Point(18, 28);
            gauge.Value = report == null ? 0 : report.ReadinessScore;
            gauge.TargetValue = report == null ? 0 : report.TargetScore;
            gauge.ImpactScore = report == null ? 0 : report.EstimatedImpactScore;
            gauge.ImpactLabel = report == null ? "TARAMA BEKLİYOR" : report.EstimatedImpactLabel;
            gauge.HasReading = report != null;
            hero.Controls.Add(gauge);

            Label eyebrow = UiFactory.Label("AKILLI PERFORMANS ANALİZİ", new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), AppTheme.Cyan);
            eyebrow.Location = new Point(410, 36);
            hero.Controls.Add(eyebrow);
            Label title = UiFactory.Label(
                report == null ? "Bilgisayarınızı oyuna hazırlayın" : ScanHeadline(report),
                new Font("Segoe UI Semibold", 22f, FontStyle.Bold),
                AppTheme.Text);
            title.Location = new Point(408, 67);
            title.MaximumSize = new Size(550, 70);
            hero.Controls.Add(title);
            Label copy = UiFactory.Label(
                report == null
                    ? "Tercan; Windows oyun ayarlarını, belleği, başlangıç programlarını, arka plan yükünü ve güvenli temizlik alanlarını inceler. Sonuçlara göre gerekli ayarları nedenleriyle önerir."
                    : report.EstimatedImpactDescription +
                      " Gösterge bir FPS yüzdesi değildir; Windows hazırlığı ve arka plan yükü için tahmini etki puanıdır.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(410, 121);
            copy.Size = new Size(545, 65);
            copy.AutoSize = false;
            hero.Controls.Add(copy);

            FlowLayoutPanel badges = new FlowLayoutPanel();
            badges.Location = new Point(410, 194);
            badges.Width = 520;
            badges.Height = 30;
            badges.WrapContents = false;
            badges.BackColor = Color.Transparent;
            badges.Controls.Add(UiFactory.Pill("SALT OKUNUR TARAMA", AppTheme.Green));
            badges.Controls.Add(UiFactory.Pill("ONAYSIZ DEĞİŞİKLİK YOK", AppTheme.Cyan));
            badges.Controls.Add(UiFactory.Pill("GERİ ALINABİLİR", AppTheme.Amber));
            hero.Controls.Add(badges);

            Button scan = UiFactory.Button(
                report == null ? "Bilgisayarı şimdi tara" : "Yeniden tara",
                AppTheme.Accent,
                Color.White);
            scan.Location = new Point(410, 244);
            scan.Enabled = !systemScanRunning;
            hero.Controls.Add(scan);
            Label scanStatus = UiFactory.Label(
                systemScanRunning
                    ? "Sistem analiz ediliyor…"
                    : report == null
                        ? "Tarama yalnızca mevcut durumu okur; ayar değiştirmez."
                        : "Son tarama: " + report.ScannedAt.ToString("HH:mm:ss") + " • Hedef işareti, öneriler uygulandıktan sonraki hazırlık seviyesidir.",
                AppTheme.Small,
                systemScanRunning ? AppTheme.Cyan : AppTheme.TextMuted);
            scanStatus.Location = new Point(595, 256);
            scanStatus.MaximumSize = new Size(355, 42);
            hero.Controls.Add(scanStatus);
            scan.Click += delegate { BeginSystemScan(scan, scanStatus); };

            if (report == null && !previewMode && !backgroundStartMode && !systemScanRunning)
            {
                Timer automaticScan = new Timer();
                automaticScan.Interval = 700;
                automaticScan.Tick += delegate
                {
                    automaticScan.Stop();
                    automaticScan.Dispose();
                    if (currentPage == "scanner" && lastSystemScan == null && !systemScanRunning)
                    {
                        BeginSystemScan(scan, scanStatus);
                    }
                };
                automaticScan.Start();
            }

            if (report == null)
            {
                ShowScanScope(flow);
                return;
            }

            FlowLayoutPanel metrics = new FlowLayoutPanel();
            metrics.Width = 1010;
            metrics.Height = 132;
            metrics.WrapContents = false;
            metrics.Margin = new Padding(0, 0, 0, 16);
            metrics.BackColor = Color.Transparent;
            metrics.Controls.Add(new MetricCard(
                "Önerilen ayar",
                report.RecommendedTweakIds.Count.ToString(),
                report.RecommendedTweakIds.Count == 0 ? "Temel ayarlar hazır" : "Uygulamadan önce incelenir",
                AppTheme.Accent));
            metrics.Controls.Add(new MetricCard(
                "Arka plan yükü",
                SafeCleanupEngine.FormatBytes(report.ActiveBackgroundBytes),
                report.ActiveBackgroundGroups + " kullanıcı uygulama grubu",
                AppTheme.Cyan));
            metrics.Controls.Add(new MetricCard(
                "Kullanılabilir RAM",
                report.AvailableMemoryMb > 0 ? report.AvailableMemoryMb + " MB" : "Bilinmiyor",
                report.TotalMemoryMb > 0 ? report.TotalMemoryMb + " MB toplam" : "Bellek okunamadı",
                AppTheme.Green));
            metrics.Controls.Add(new MetricCard(
                "Temizlik adayı",
                SafeCleanupEngine.FormatBytes(report.CleanupBytes),
                report.CleanupFiles + " geçici dosya",
                AppTheme.Amber));
            flow.Controls.Add(metrics);

            ShowScanFindings(flow, report);

            SmoothPanel disclaimer = new SmoothPanel();
            disclaimer.Width = 1010;
            disclaimer.Height = 105;
            disclaimer.Margin = new Padding(0, 0, 0, 24);
            disclaimer.BackColor = Color.FromArgb(31, 27, 24);
            disclaimer.BorderColor = AppTheme.Amber;
            flow.Controls.Add(disclaimer);
            Label disclaimerTitle = UiFactory.Label("Göstergeyi doğru okuyun", AppTheme.Subheading, AppTheme.Amber);
            disclaimerTitle.Location = new Point(22, 17);
            disclaimer.Controls.Add(disclaimerTitle);
            Label disclaimerText = UiFactory.Label(
                "Puan doğrudan FPS artışı değildir. İşlemci, ekran kartı, oyun motoru ve sıcaklık sınırları gerçek sonucu belirler. " +
                "Tercan'ın etki puanı; eksik oyun ayarları, bellek baskısı ve arka plan yüküne göre göreli bir tahmindir. Sonucu aynı oyun sahnesinde ölçün.",
                AppTheme.Body,
                AppTheme.TextMuted);
            disclaimerText.Location = new Point(23, 51);
            disclaimerText.Size = new Size(950, 40);
            disclaimerText.AutoSize = false;
            disclaimer.Controls.Add(disclaimerText);
        }

        private void ShowScanScope(FlowLayoutPanel flow)
        {
            SmoothPanel scope = new SmoothPanel();
            scope.Width = 1010;
            scope.Height = 240;
            scope.Margin = new Padding(0, 0, 0, 16);
            scope.BackColor = AppTheme.Surface;
            flow.Controls.Add(scope);
            Label title = UiFactory.Label("Tarama hangi alanları kontrol eder?", AppTheme.Subheading, AppTheme.Text);
            title.Location = new Point(24, 20);
            scope.Controls.Add(title);

            AddScopeColumn(scope, 24, "01", "Oyun ve Windows ayarları",
                "Oyun Modu, yakalama, güç planı, görünüm ve güvenli ağ ayarlarının mevcut durumu.");
            AddScopeColumn(scope, 348, "02", "Bellek ve arka plan",
                "Kullanılabilir RAM, standby durumu ve oyun sırasında kapatılabilecek kullanıcı uygulamaları.");
            AddScopeColumn(scope, 672, "03", "Başlangıç ve disk",
                "Otomatik açılan kullanıcı programları ile güvenli geçici dosya ve önbellek alanları.");

            Label safe = UiFactory.Label(
                "Tarama sırasında kayıt defteri yazılmaz, uygulama kapatılmaz, dosya silinmez ve ağ ayarı değiştirilmez.",
                new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                AppTheme.Green);
            safe.Location = new Point(25, 194);
            scope.Controls.Add(safe);
        }

        private void AddScopeColumn(Control parent, int left, string number, string title, string description)
        {
            Label index = UiFactory.Label(number, new Font("Segoe UI Semibold", 20f, FontStyle.Bold), AppTheme.Accent);
            index.Location = new Point(left, 61);
            parent.Controls.Add(index);
            Label heading = UiFactory.Label(title, new Font("Segoe UI Semibold", 11f, FontStyle.Bold), AppTheme.Text);
            heading.Location = new Point(left + 48, 64);
            parent.Controls.Add(heading);
            Label copy = UiFactory.Label(description, AppTheme.Body, AppTheme.TextMuted);
            copy.Location = new Point(left + 48, 94);
            copy.Size = new Size(250, 70);
            copy.AutoSize = false;
            parent.Controls.Add(copy);
        }

        private void ShowScanFindings(FlowLayoutPanel flow, SystemScanReport report)
        {
            List<SystemScanFinding> findings = report.Findings.Take(7).ToList();
            SmoothPanel panel = new SmoothPanel();
            panel.Width = 1010;
            panel.Height = 150 + Math.Max(1, findings.Count) * 78;
            panel.Margin = new Padding(0, 0, 0, 16);
            panel.BackColor = AppTheme.Surface;
            flow.Controls.Add(panel);
            Label title = UiFactory.Label("Tercan'ın önerileri", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 19);
            panel.Controls.Add(title);
            Label subtitle = UiFactory.Label(
                findings.Count == 0
                    ? "Temel oyun ve sistem ayarlarında önemli bir eksik bulunmadı."
                    : report.Findings.Count + " bulgu arasından en önemli " + findings.Count + " sonuç gösteriliyor.",
                AppTheme.Body,
                AppTheme.TextMuted);
            subtitle.Location = new Point(25, 54);
            panel.Controls.Add(subtitle);

            int top = 88;
            foreach (SystemScanFinding finding in findings)
            {
                Panel row = new Panel();
                row.Location = new Point(24, top);
                row.Size = new Size(960, 68);
                row.BackColor = AppTheme.SurfaceRaised;
                panel.Controls.Add(row);
                Panel marker = new Panel();
                marker.Dock = DockStyle.Left;
                marker.Width = 5;
                marker.BackColor = SeverityColor(finding.Severity);
                row.Controls.Add(marker);
                Label category = UiFactory.Pill(finding.Category.ToUpperInvariant(), SeverityColor(finding.Severity));
                category.Location = new Point(16, 11);
                row.Controls.Add(category);
                Label findingTitle = UiFactory.Label(finding.Title, new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AppTheme.Text);
                findingTitle.Location = new Point(150, 12);
                findingTitle.MaximumSize = new Size(370, 22);
                row.Controls.Add(findingTitle);
                Label detail = UiFactory.Label(finding.Detail, AppTheme.Small, AppTheme.TextMuted);
                detail.Location = new Point(150, 38);
                detail.Size = new Size(765, 22);
                detail.AutoSize = false;
                detail.AutoEllipsis = true;
                row.Controls.Add(detail);
                if (!string.IsNullOrWhiteSpace(finding.TweakId))
                {
                    Label ready = UiFactory.Pill("AYAR HAZIRLANABİLİR", AppTheme.Cyan);
                    ready.Location = new Point(765, 10);
                    row.Controls.Add(ready);
                }
                top += 78;
            }

            Button prepare = UiFactory.Button("Önerilen ayarları gözden geçir", AppTheme.Accent, Color.White);
            prepare.Location = new Point(24, panel.Height - 48);
            prepare.Enabled = report.RecommendedTweakIds.Count > 0;
            prepare.Click += delegate { StageScanRecommendations(report); };
            panel.Controls.Add(prepare);
            Button focus = UiFactory.Button("Oyun Odak Modu", AppTheme.SurfaceRaised, AppTheme.Text);
            focus.Location = new Point(270, panel.Height - 48);
            focus.Click += delegate { Navigate("focus", null); };
            panel.Controls.Add(focus);
            Button cleanup = UiFactory.Button("Temizlik sonuçlarını aç", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            cleanup.Location = new Point(415, panel.Height - 48);
            cleanup.Click += delegate { Navigate("cleanup", null); };
            panel.Controls.Add(cleanup);
        }

        private void BeginSystemScan(Button scanButton, Label status)
        {
            if (systemScanRunning) return;
            systemScanRunning = true;
            scanButton.Enabled = false;
            scanButton.Text = "Sistem taranıyor…";
            status.Text = "Windows ayarları, bellek, arka plan, başlangıç ve geçici dosyalar okunuyor…";
            status.ForeColor = AppTheme.Cyan;
            Cursor = Cursors.WaitCursor;

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                e.Result = SystemScanEngine.Scan(tweaks, engine, systemInfo);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                systemScanRunning = false;
                Cursor = Cursors.Default;
                if (e.Error != null)
                {
                    scanButton.Enabled = true;
                    scanButton.Text = "Taramayı yeniden dene";
                    status.Text = "Tarama tamamlanamadı: " + e.Error.Message;
                    status.ForeColor = AppTheme.Amber;
                    Logger.Error("Akıllı tarama tamamlanamadı", e.Error);
                    return;
                }
                lastSystemScan = (SystemScanReport)e.Result;
                Navigate("scanner", null);
            };
            worker.RunWorkerAsync();
        }

        private void StageScanRecommendations(SystemScanReport report)
        {
            int staged = 0;
            foreach (string id in report.RecommendedTweakIds)
            {
                TweakDefinition tweak = tweaks.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (tweak == null || engine.IsApplied(tweak)) continue;
                pending[tweak.Id] = true;
                staged++;
            }
            UpdateApplyBar();
            if (staged == 0)
            {
                MessageBox.Show("Hazırlanacak yeni bir önerilen ayar yok.", "tercan.exe");
                return;
            }
            MessageBox.Show(
                staged + " önerilen ayar hazırlandı. Şimdi her ayarın açıklamasını ve tahmini etki seviyesini inceleyin; alttaki düğmeden toplu onay ekranını açabilirsiniz.",
                "Öneriler hazır",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            Navigate("tweaks", null);
        }

        private static Color SeverityColor(ScanSeverity severity)
        {
            if (severity == ScanSeverity.Important) return AppTheme.Red;
            if (severity == ScanSeverity.Recommended) return AppTheme.Amber;
            return AppTheme.Cyan;
        }

        private static string ScanHeadline(SystemScanReport report)
        {
            if (report.ReadinessScore >= 85) return "Sisteminiz oyuna hazır görünüyor";
            if (report.ReadinessScore >= 65) return "Birkaç ayarla daha dengeli performans";
            return "Arka plan yükünü azaltma fırsatı var";
        }
    }
}

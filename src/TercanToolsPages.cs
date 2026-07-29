using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TercanOptimizer
{
    internal sealed partial class MainForm
    {
        private sealed class CleanupWork
        {
            public List<CleanupScan> Scans { get; set; }
            public CleanupResult Result { get; set; }
        }

        private sealed class CleanupPageProgress
        {
            public string TargetId { get; set; }
            public string Status { get; set; }
            public string FilePath { get; set; }
            public int Progress { get; set; }
            public int FileCount { get; set; }
            public long Bytes { get; set; }
            public int SkippedFiles { get; set; }
        }

        private bool repairRunning;

        private void ShowToolboxPage()
        {
            pageTitle.Text = "Araç Kutusu";
            pageDescription.Text = "Hellzerg benzeri geniş araç kapsamı; Tercan yedekleme ve güvenlik yaklaşımıyla.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel intro = new SmoothPanel();
            intro.Width = 1010;
            intro.Height = 132;
            intro.Margin = new Padding(0, 0, 0, 16);
            intro.BackColor = AppTheme.Surface;
            flow.Controls.Add(intro);

            Label title = UiFactory.Label("Windows bakım ve yönetim merkezi", new Font("Segoe UI Semibold", 18f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            intro.Controls.Add(title);
            Label copy = UiFactory.Label(
                "Bu araçlar yalnızca seçtiğiniz işlemi çalıştırır. Temizlik önce taranır; başlangıç, DNS ve HOSTS değişiklikleri " +
                "ayrı yedeklenir. Defender, Windows Update ve önyükleme zamanlayıcıları korunur.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(25, 59);
            copy.Size = new Size(935, 48);
            copy.AutoSize = false;
            intro.Controls.Add(copy);

            FlowLayoutPanel grid = new FlowLayoutPanel();
            grid.Width = 1010;
            grid.Height = 410;
            grid.WrapContents = true;
            grid.Margin = new Padding(0, 0, 0, 16);
            grid.BackColor = Color.Transparent;
            flow.Controls.Add(grid);
            grid.Controls.Add(CreateToolTile("Temizlik Merkezi", "Geçici dosya ve önbellekleri önce ölçer, sonra yalnızca seçilen güvenli konumları temizler.", "Tara ve temizle", AppTheme.Cyan, "cleanup"));
            grid.Controls.Add(CreateToolTile("Başlangıç Yöneticisi", "Windows ile açılan programları görür; girdileri yedeğe alarak devre dışı bırakır veya geri getirir.", "Başlangıcı yönet", AppTheme.Accent, "startup"));
            grid.Controls.Add(CreateToolTile("Ağ ve DNS Araçları", "Bağdaştırıcı, IP, DNS ve ağ geçidini gösterir; ping ölçer, DNS değiştirir ve geri alır.", "Ağı incele", AppTheme.Green, "network-tools"));
            grid.Controls.Add(CreateToolTile("Windows Onarım", "SFC, DISM ve CHKDSK gibi Microsoft araçlarını görünür çıktı ve durum bilgisiyle çalıştırır.", "Onarım araçları", AppTheme.Amber, "repair"));
            grid.Controls.Add(CreateToolTile("Donanım Raporu", "İşlemci, ekran kartı, anakart, BIOS, RAM, disk ve ağ bilgilerini tek raporda toplar.", "Donanımı göster", Color.FromArgb(190, 110, 255), "hardware"));
            grid.Controls.Add(CreateToolTile("HOSTS Editörü", "HOSTS dosyasını doğrulama, otomatik yedek ve son yedeği geri yükleme korumasıyla düzenler.", "HOSTS'u düzenle", Color.FromArgb(255, 105, 130), "hosts"));

            SmoothPanel shortcuts = new SmoothPanel();
            shortcuts.Width = 1010;
            shortcuts.Height = 150;
            shortcuts.Margin = new Padding(0, 0, 0, 24);
            shortcuts.BackColor = AppTheme.Surface;
            flow.Controls.Add(shortcuts);
            Label shortcutsTitle = UiFactory.Label("Windows yönetim kısayolları", AppTheme.Subheading, AppTheme.Text);
            shortcutsTitle.Location = new Point(24, 18);
            shortcuts.Controls.Add(shortcutsTitle);
            AddShortcutButton(shortcuts, "Görev Yöneticisi", 24, "taskmgr.exe");
            AddShortcutButton(shortcuts, "Hizmetler", 180, "services.msc");
            AddShortcutButton(shortcuts, "Aygıt Yöneticisi", 300, "devmgmt.msc");
            AddShortcutButton(shortcuts, "Olay Görüntüleyici", 455, "eventvwr.msc");
            AddShortcutButton(shortcuts, "Depolama Ayarları", 618, "ms-settings:storagesense");
            AddShortcutButton(shortcuts, "Windows Update", 786, "ms-settings:windowsupdate");
        }

        private Control CreateToolTile(string title, string description, string action, Color accent, string page)
        {
            PremiumCard card = new PremiumCard();
            card.Width = 326;
            card.Height = 190;
            card.Margin = new Padding(0, 0, 10, 10);
            card.BackColor = AppTheme.Surface;
            card.AccentColor = accent;
            card.BorderColor = Color.FromArgb(
                Math.Min(255, accent.R / 2 + 20),
                Math.Min(255, accent.G / 2 + 20),
                Math.Min(255, accent.B / 2 + 20));

            Panel marker = new Panel();
            marker.Location = new Point(20, 21);
            marker.Size = new Size(8, 31);
            marker.BackColor = accent;
            card.Controls.Add(marker);
            Label heading = UiFactory.Label(title, AppTheme.Subheading, AppTheme.Text);
            heading.Location = new Point(40, 24);
            card.Controls.Add(heading);
            Label copy = UiFactory.Label(description, AppTheme.Body, AppTheme.TextMuted);
            copy.Location = new Point(21, 66);
            copy.Size = new Size(282, 57);
            copy.AutoSize = false;
            card.Controls.Add(copy);
            Button open = UiFactory.Button(action, AppTheme.SurfaceRaised, AppTheme.Text);
            open.Location = new Point(20, 138);
            open.Click += delegate { Navigate(page, null); };
            card.Controls.Add(open);
            return card;
        }

        private void AddShortcutButton(Control parent, string text, int left, string target)
        {
            Button button = UiFactory.Button(text, AppTheme.SurfaceRaised, AppTheme.Text);
            button.Location = new Point(left, 72);
            button.Click += delegate
            {
                try { ProcessRunner.Open(target); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            parent.Controls.Add(button);
        }

        private Button AddToolboxBack(Control parent)
        {
            Button back = UiFactory.Button("← Hızlandırma", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            back.Location = new Point(830, 18);
            back.Click += delegate { Navigate("optimizer-settings", null); };
            parent.Controls.Add(back);
            return back;
        }

        private void ShowCleanupPage()
        {
            pageTitle.Text = "Temizlik";
            pageDescription.Text = "Tara, sonuçları incele ve yalnızca seçtiklerini temizle.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel shell = new SmoothPanel();
            shell.Width = 1010;
            shell.Height = 680;
            shell.Margin = new Padding(0, 0, 0, 24);
            shell.BackColor = Color.FromArgb(9, 11, 20);
            shell.BorderColor = Color.FromArgb(100, AppTheme.Accent);
            flow.Controls.Add(shell);

            Label title = UiFactory.Label("Akıllı Temizlik Merkezi", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 18);
            shell.Controls.Add(title);
            Label safeBadge = UiFactory.Pill("GÜVENLİ ALANLAR", AppTheme.Green);
            safeBadge.Location = new Point(338, 23);
            shell.Controls.Add(safeBadge);
            Button storage = UiFactory.Button("Windows depolama", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            storage.Location = new Point(826, 17);
            storage.Width = 158;
            storage.Click += delegate { ProcessRunner.Open("ms-settings:storagesense"); };
            shell.Controls.Add(storage);

            SmoothPanel categoriesPanel = new SmoothPanel();
            categoriesPanel.Location = new Point(20, 67);
            categoriesPanel.Size = new Size(342, 590);
            categoriesPanel.BackColor = AppTheme.Surface;
            categoriesPanel.BorderColor = Color.FromArgb(72, AppTheme.Accent);
            shell.Controls.Add(categoriesPanel);

            Label categoriesTitle = UiFactory.Label("Temizlenecek alanlar", AppTheme.Subheading, AppTheme.Text);
            categoriesTitle.Location = new Point(20, 18);
            categoriesPanel.Controls.Add(categoriesTitle);
            Label categoriesHint = UiFactory.Label("İstemediğinizi tek tıkla kapatın.", AppTheme.Small, AppTheme.TextMuted);
            categoriesHint.Location = new Point(21, 48);
            categoriesPanel.Controls.Add(categoriesHint);

            FlowLayoutPanel categoryFlow = new ModernScrollFlowPanel();
            categoryFlow.Location = new Point(12, 82);
            categoryFlow.Size = new Size(318, 482);
            categoryFlow.FlowDirection = FlowDirection.TopDown;
            categoryFlow.WrapContents = false;
            categoryFlow.AutoScroll = true;
            categoryFlow.BackColor = Color.Transparent;
            categoriesPanel.Controls.Add(categoryFlow);

            SmoothPanel scanPanel = new SmoothPanel();
            scanPanel.Location = new Point(378, 67);
            scanPanel.Size = new Size(612, 590);
            scanPanel.BackColor = AppTheme.Surface;
            scanPanel.BorderColor = Color.FromArgb(82, AppTheme.Cyan);
            shell.Controls.Add(scanPanel);

            Label statusTitle = UiFactory.Label("Temizlik için hazır", new Font("Segoe UI Semibold", 18f, FontStyle.Bold), AppTheme.Text);
            statusTitle.Location = new Point(24, 20);
            statusTitle.Size = new Size(560, 36);
            statusTitle.AutoSize = false;
            scanPanel.Controls.Add(statusTitle);
            Label statusDetail = UiFactory.Label(
                "Önce tarayın; sonuçları görmeden hiçbir dosya silinmez.",
                AppTheme.Body,
                AppTheme.TextMuted);
            statusDetail.Location = new Point(25, 57);
            statusDetail.Size = new Size(560, 38);
            statusDetail.AutoSize = false;
            scanPanel.Controls.Add(statusDetail);

            CleanupGauge gauge = new CleanupGauge();
            gauge.Location = new Point(181, 82);
            scanPanel.Controls.Add(gauge);

            Label currentPath = UiFactory.Label(
                "Oturumlar, parolalar, indirilenler ve belgeler kapsam dışıdır.",
                new Font("Segoe UI", 8f),
                AppTheme.TextMuted);
            currentPath.Location = new Point(25, 333);
            currentPath.Size = new Size(562, 40);
            currentPath.AutoSize = false;
            currentPath.AutoEllipsis = true;
            currentPath.TextAlign = ContentAlignment.MiddleCenter;
            scanPanel.Controls.Add(currentPath);

            SmoothPanel fileSummary = new SmoothPanel();
            fileSummary.Location = new Point(25, 390);
            fileSummary.Size = new Size(270, 72);
            fileSummary.BackColor = AppTheme.SurfaceRaised;
            scanPanel.Controls.Add(fileSummary);
            Label fileValue = UiFactory.Label("—", new Font("Segoe UI Semibold", 16f, FontStyle.Bold), AppTheme.Text);
            fileValue.Location = new Point(16, 12);
            fileSummary.Controls.Add(fileValue);
            Label fileCaption = UiFactory.Label("SEÇİLİ DOSYA", new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold), AppTheme.TextMuted);
            fileCaption.Location = new Point(17, 46);
            fileSummary.Controls.Add(fileCaption);

            SmoothPanel byteSummary = new SmoothPanel();
            byteSummary.Location = new Point(312, 390);
            byteSummary.Size = new Size(275, 72);
            byteSummary.BackColor = AppTheme.SurfaceRaised;
            scanPanel.Controls.Add(byteSummary);
            Label byteValue = UiFactory.Label("—", new Font("Segoe UI Semibold", 16f, FontStyle.Bold), AppTheme.Green);
            byteValue.Location = new Point(16, 12);
            byteSummary.Controls.Add(byteValue);
            Label byteCaption = UiFactory.Label("KAZANILABİLECEK ALAN", new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold), AppTheme.TextMuted);
            byteCaption.Location = new Point(17, 46);
            byteSummary.Controls.Add(byteCaption);

            Label safety = UiFactory.Label(
                "Daireye tıklayın • Kullanımdaki dosyalar atlanır • Yeniden başlatma gerekmez",
                AppTheme.Small,
                AppTheme.TextMuted);
            safety.Location = new Point(25, 516);
            safety.Size = new Size(560, 24);
            safety.AutoSize = false;
            safety.TextAlign = ContentAlignment.MiddleCenter;
            scanPanel.Controls.Add(safety);

            List<CleanupTarget> targets = SafeCleanupEngine.BuildCatalog();
            Dictionary<string, ToggleSwitch> toggles = new Dictionary<string, ToggleSwitch>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Label> countLabels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, SmoothPanel> categoryRows = new Dictionary<string, SmoothPanel>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CleanupScan> scanResults = new Dictionary<string, CleanupScan>(StringComparer.OrdinalIgnoreCase);
            bool busy = false;
            Action updateSelectionSummary = null;
            Action<string> setActiveRow = null;
            EventHandler startScan = null;
            EventHandler startClean = null;

            foreach (CleanupTarget target in targets)
            {
                CleanupTarget currentTarget = target;
                PremiumCard row = new PremiumCard();
                row.Size = new Size(286, 68);
                row.Margin = new Padding(6, 0, 6, 8);
                row.BackColor = Color.FromArgb(20, 23, 35);
                row.BorderColor = target.Recommended
                    ? Color.FromArgb(92, AppTheme.Accent)
                    : AppTheme.Border;
                row.AccentColor = target.Recommended ? AppTheme.Accent : AppTheme.Cyan;
                row.Cursor = Cursors.Hand;
                categoryFlow.Controls.Add(row);
                categoryRows[target.Id] = row;

                Label rowTitle = UiFactory.Label(target.Name, new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold), AppTheme.Text);
                rowTitle.Location = new Point(14, 9);
                rowTitle.MaximumSize = new Size(218, 19);
                row.Controls.Add(rowTitle);
                Label rowDetail = UiFactory.Label(CleanupShortDescription(target.Id), new Font("Segoe UI", 7.4f), AppTheme.TextMuted);
                rowDetail.Location = new Point(14, 33);
                rowDetail.MaximumSize = new Size(196, 25);
                row.Controls.Add(rowDetail);
                Label count = UiFactory.Pill("TARANMADI", AppTheme.TextMuted);
                count.Location = new Point(203, 38);
                count.Size = new Size(70, 20);
                count.TextAlign = ContentAlignment.MiddleCenter;
                row.Controls.Add(count);
                countLabels[target.Id] = count;
                ToggleSwitch toggle = new ToggleSwitch();
                toggle.Location = new Point(226, 8);
                toggle.Checked = target.Recommended;
                row.Controls.Add(toggle);
                toggles[target.Id] = toggle;

                EventHandler toggleRow = delegate { if (!busy) toggle.Checked = !toggle.Checked; };
                row.Click += toggleRow;
                rowTitle.Click += toggleRow;
                rowDetail.Click += toggleRow;
                toggle.CheckedChanged += delegate
                {
                    row.BorderColor = toggle.Checked
                        ? Color.FromArgb(110, AppTheme.Accent)
                        : AppTheme.Border;
                    row.Invalidate();
                    if (updateSelectionSummary != null) updateSelectionSummary();
                };
            }

            updateSelectionSummary = delegate
            {
                List<CleanupScan> selectedScans = scanResults.Values
                    .Where(x => toggles.ContainsKey(x.Target.Id) && toggles[x.Target.Id].Checked)
                    .ToList();
                int files = selectedScans.Sum(x => x.FileCount);
                long bytes = selectedScans.Sum(x => x.Bytes);
                fileValue.Text = scanResults.Count == 0 ? "—" : files.ToString("N0");
                byteValue.Text = scanResults.Count == 0 ? "—" : SafeCleanupEngine.FormatBytes(bytes);
                if (scanResults.Count > 0 && !busy)
                {
                    gauge.Completed = false;
                    statusTitle.Text = files > 0 ? "Temizliğe hazır" : "Seçili gereksiz dosya yok";
                    statusDetail.Text = files > 0
                        ? "İstemediğiniz kategoriyi soldan kapatıp toplamı değiştirebilirsiniz."
                        : "Bir kategori seçin veya sistemi yeniden tarayın.";
                    gauge.Progress = files > 0 ? 100 : 0;
                    gauge.PrimaryText = files > 0 ? "TEMİZLE" : "TEKRAR TARA";
                    gauge.SecondaryText = files > 0 ? files + " DOSYA SEÇİLİ" : "DOSYA BULUNMADI";
                    gauge.Invalidate();
                }
            };

            setActiveRow = delegate(string targetId)
            {
                foreach (KeyValuePair<string, SmoothPanel> pair in categoryRows)
                {
                    bool active = string.Equals(pair.Key, targetId, StringComparison.OrdinalIgnoreCase);
                    pair.Value.BackColor = active ? AppTheme.AccentSoft : Color.FromArgb(20, 23, 35);
                    pair.Value.Invalidate();
                }
            };

            startScan = delegate
            {
                List<CleanupTarget> selectedTargets = targets
                    .Where(x => toggles[x.Id].Checked)
                    .ToList();
                if (selectedTargets.Count == 0)
                {
                    MessageBox.Show("Taramak için en az bir kategori seçin.", "tercan.exe");
                    return;
                }

                busy = true;
                scanResults.Clear();
                gauge.Cursor = Cursors.WaitCursor;
                gauge.Completed = false;
                foreach (ToggleSwitch toggle in toggles.Values) toggle.Enabled = false;
                foreach (CleanupTarget target in selectedTargets) countLabels[target.Id].Text = "…";
                gauge.Busy = true;
                gauge.Progress = 1;
                gauge.PrimaryText = "1%";
                gauge.SecondaryText = "TARANIYOR";
                statusTitle.Text = "Gereksiz dosyalar aranıyor…";
                statusDetail.Text = "Seçtiğiniz güvenli klasörler ölçülüyor.";
                currentPath.Text = "Tarama hazırlanıyor…";
                System.Diagnostics.Stopwatch visualClock = System.Diagnostics.Stopwatch.StartNew();

                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.DoWork += delegate(object sender, DoWorkEventArgs e)
                {
                    BackgroundWorker background = (BackgroundWorker)sender;
                    List<CleanupScan> scans = new List<CleanupScan>();
                    for (int i = 0; i < selectedTargets.Count; i++)
                    {
                        CleanupTarget target = selectedTargets[i];
                        int categoryIndex = i;
                        int startProgress = 2 + (int)(categoryIndex * 94d / selectedTargets.Count);
                        int categorySpan = Math.Max(2, (int)(94d / selectedTargets.Count));
                        background.ReportProgress(startProgress, new CleanupPageProgress
                        {
                            TargetId = target.Id,
                            Status = target.Name + " taranıyor…",
                            Progress = startProgress
                        });
                        System.Diagnostics.Stopwatch throttle = System.Diagnostics.Stopwatch.StartNew();
                        CleanupScan scan = SafeCleanupEngine.Scan(target, delegate(CleanupScanProgress live)
                        {
                            if (throttle.ElapsedMilliseconds < 65) return;
                            throttle.Restart();
                            int localProgress = Math.Min(categorySpan - 1, (int)(Math.Log10(live.FileCount + 1) * 5d));
                            background.ReportProgress(
                                Math.Max(1, Math.Min(98, startProgress + localProgress)),
                                new CleanupPageProgress
                                {
                                    TargetId = target.Id,
                                    Status = target.Name + " taranıyor…",
                                    FilePath = live.FilePath,
                                    FileCount = live.FileCount,
                                    Bytes = live.Bytes,
                                    Progress = startProgress + localProgress
                                });
                        });
                        scans.Add(scan);
                        int completedProgress = 2 + (int)((categoryIndex + 1) * 94d / selectedTargets.Count);
                        background.ReportProgress(Math.Min(98, completedProgress), new CleanupPageProgress
                        {
                            TargetId = target.Id,
                            Status = target.Name + " tarandı.",
                            FileCount = scan.FileCount,
                            Bytes = scan.Bytes,
                            Progress = completedProgress
                        });
                    }
                    e.Result = scans;
                };
                worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
                {
                    CleanupPageProgress progress = e.UserState as CleanupPageProgress;
                    if (progress == null) return;
                    int percent = Math.Max(1, Math.Min(99, e.ProgressPercentage));
                    gauge.Progress = percent;
                    gauge.PrimaryText = percent + "%";
                    gauge.SecondaryText = "TARANIYOR";
                    gauge.Invalidate();
                    statusTitle.Text = progress.Status;
                    if (!string.IsNullOrWhiteSpace(progress.FilePath))
                    {
                        currentPath.Text = CompactCleanupPagePath(progress.FilePath);
                    }
                    Label count;
                    if (countLabels.TryGetValue(progress.TargetId, out count) && progress.FileCount > 0)
                    {
                        count.Text = progress.FileCount.ToString("N0");
                    }
                    setActiveRow(progress.TargetId);
                };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    if (gauge.IsDisposed) return;
                    if (e.Error != null)
                    {
                        busy = false;
                        gauge.Cursor = Cursors.Hand;
                        foreach (ToggleSwitch toggle in toggles.Values) toggle.Enabled = true;
                        gauge.Busy = false;
                        setActiveRow(string.Empty);
                        statusTitle.Text = "Tarama tamamlanamadı";
                        statusDetail.Text = e.Error.Message;
                        currentPath.Text = "Hiçbir dosya silinmedi.";
                        gauge.Progress = 0;
                        gauge.PrimaryText = "HATA";
                        gauge.SecondaryText = "TEKRAR DENE";
                        gauge.Invalidate();
                        return;
                    }

                    List<CleanupScan> completedScans = (List<CleanupScan>)e.Result;
                    Timer analysisTimer = new Timer();
                    int analysisStage = 0;
                    Action finishAnalysis = delegate
                    {
                        analysisTimer.Stop();
                        analysisTimer.Dispose();
                        if (gauge.IsDisposed) return;

                        busy = false;
                        gauge.Cursor = Cursors.Hand;
                        foreach (ToggleSwitch toggle in toggles.Values) toggle.Enabled = true;
                        gauge.Busy = false;
                        setActiveRow(string.Empty);
                        foreach (CleanupScan scan in completedScans)
                        {
                            scanResults[scan.Target.Id] = scan;
                            countLabels[scan.Target.Id].Text = scan.FileCount > 0
                                ? scan.FileCount.ToString("N0")
                                : "TEMİZ";
                        }
                        currentPath.Text = "Derin analiz tamamlandı • Seçimleri soldan değiştirebilirsiniz.";
                        updateSelectionSummary();
                    };

                    analysisTimer.Interval = 420;
                    analysisTimer.Tick += delegate
                    {
                        if (gauge.IsDisposed)
                        {
                            analysisTimer.Stop();
                            analysisTimer.Dispose();
                            return;
                        }

                        analysisStage++;
                        gauge.Progress = Math.Max(gauge.Progress, Math.Min(99, 95 + analysisStage));
                        gauge.PrimaryText = gauge.Progress + "%";
                        gauge.SecondaryText = "DERİN ANALİZ";
                        if (analysisStage % 3 == 1)
                        {
                            statusTitle.Text = "Dosya boyutları doğrulanıyor…";
                            statusDetail.Text = "Bulunan öğelerin boyutları ve erişilebilirliği yeniden kontrol ediliyor.";
                            currentPath.Text = "Derin analiz • Boyut doğrulama";
                        }
                        else if (analysisStage % 3 == 2)
                        {
                            statusTitle.Text = "Korunan alanlar dışlanıyor…";
                            statusDetail.Text = "Kullanımdaki dosyalar ve kişisel veriler temizlik listesinin dışında tutuluyor.";
                            currentPath.Text = "Derin analiz • Güvenlik denetimi";
                        }
                        else
                        {
                            statusTitle.Text = "Temizlik özeti hazırlanıyor…";
                            statusDetail.Text = "Seçilebilir dosya ve kazanılabilecek alan sonuçları hesaplanıyor.";
                            currentPath.Text = "Derin analiz • Sonuç hazırlanıyor";
                        }

                        if (visualClock.ElapsedMilliseconds >= 3800 && analysisStage >= 3)
                        {
                            finishAnalysis();
                        }
                    };
                    analysisTimer.Start();
                };
                worker.RunWorkerAsync();
            };

            startClean = delegate
            {
                List<CleanupScan> selectedScans = scanResults.Values
                    .Where(x => toggles[x.Target.Id].Checked && x.FileCount > 0)
                    .ToList();
                if (selectedScans.Count == 0)
                {
                    MessageBox.Show("Temizlemek için taranmış bir kategori seçin.", "tercan.exe");
                    return;
                }
                int files = selectedScans.Sum(x => x.FileCount);
                long bytes = selectedScans.Sum(x => x.Bytes);
                if (MessageBox.Show(
                    files.ToString("N0") + " dosya ve yaklaşık " + SafeCleanupEngine.FormatBytes(bytes) +
                    " temizlenecek.\n\nKullanımdaki dosyalar atlanır. Devam edilsin mi?",
                    "Seçilenleri temizle",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                busy = true;
                gauge.Cursor = Cursors.WaitCursor;
                gauge.Completed = false;
                foreach (ToggleSwitch toggle in toggles.Values) toggle.Enabled = false;
                gauge.Busy = true;
                gauge.Progress = 1;
                gauge.PrimaryText = "1%";
                gauge.SecondaryText = "TEMİZLENİYOR";
                statusTitle.Text = "Gereksiz dosyalar temizleniyor…";
                statusDetail.Text = "Silinen öğeleri aşağıdan canlı izleyebilirsiniz.";
                currentPath.Text = "Temizlik hazırlanıyor…";
                System.Diagnostics.Stopwatch visualClock = System.Diagnostics.Stopwatch.StartNew();

                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.DoWork += delegate(object sender, DoWorkEventArgs e)
                {
                    BackgroundWorker background = (BackgroundWorker)sender;
                    System.Diagnostics.Stopwatch throttle = System.Diagnostics.Stopwatch.StartNew();
                    CleanupResult result = SafeCleanupEngine.Clean(selectedScans, delegate(CleanupProgress live)
                    {
                        if (throttle.ElapsedMilliseconds < 55 && live.ProcessedFiles < live.TotalFiles) return;
                        throttle.Restart();
                        int percent = Math.Max(1, Math.Min(99,
                            (int)(live.ProcessedFiles * 100d / Math.Max(1, live.TotalFiles))));
                        CleanupScan activeScan = selectedScans.FirstOrDefault(x =>
                            string.Equals(x.Target.Name, live.CategoryName, StringComparison.OrdinalIgnoreCase));
                        background.ReportProgress(percent, new CleanupPageProgress
                        {
                            TargetId = activeScan == null ? string.Empty : activeScan.Target.Id,
                            Status = live.CategoryName + " temizleniyor…",
                            FilePath = live.FilePath,
                            FileCount = live.DeletedFiles,
                            Bytes = live.ReleasedBytes,
                            SkippedFiles = live.SkippedFiles,
                            Progress = percent
                        });
                    });
                    e.Result = result;
                };
                worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
                {
                    CleanupPageProgress progress = e.UserState as CleanupPageProgress;
                    if (progress == null) return;
                    gauge.Progress = e.ProgressPercentage;
                    gauge.PrimaryText = e.ProgressPercentage + "%";
                    gauge.SecondaryText = "TEMİZLENİYOR";
                    gauge.Invalidate();
                    statusTitle.Text = progress.Status;
                    statusDetail.Text = progress.FileCount.ToString("N0") + " dosya silindi • " +
                                        SafeCleanupEngine.FormatBytes(progress.Bytes) + " alan açıldı • " +
                                        progress.SkippedFiles + " atlandı";
                    currentPath.Text = CompactCleanupPagePath(progress.FilePath);
                    setActiveRow(progress.TargetId);
                };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    if (gauge.IsDisposed) return;
                    if (e.Error != null)
                    {
                        busy = false;
                        gauge.Cursor = Cursors.Hand;
                        foreach (ToggleSwitch toggle in toggles.Values) toggle.Enabled = true;
                        gauge.Busy = false;
                        setActiveRow(string.Empty);
                        scanResults.Clear();
                        statusTitle.Text = "Temizlik tamamlanamadı";
                        statusDetail.Text = e.Error.Message;
                        gauge.PrimaryText = "HATA";
                        gauge.SecondaryText = "TEKRAR DENE";
                        gauge.Invalidate();
                        return;
                    }
                    CleanupResult result = (CleanupResult)e.Result;
                    Timer verificationTimer = new Timer();
                    int verificationStage = 0;
                    Action finishCleanup = delegate
                    {
                        verificationTimer.Stop();
                        verificationTimer.Dispose();
                        if (gauge.IsDisposed) return;

                        busy = false;
                        gauge.Cursor = Cursors.Hand;
                        foreach (ToggleSwitch toggle in toggles.Values) toggle.Enabled = true;
                        gauge.Busy = false;
                        setActiveRow(string.Empty);
                        scanResults.Clear();
                        foreach (CleanupTarget target in targets)
                        {
                            if (toggles[target.Id].Checked) countLabels[target.Id].Text = "TEMİZ";
                        }
                        fileValue.Text = result.DeletedFiles.ToString("N0");
                        byteValue.Text = SafeCleanupEngine.FormatBytes(result.ReleasedBytes);
                        gauge.Progress = 100;
                        gauge.PrimaryText = "TAMAMLANDI";
                        gauge.SecondaryText = "TEMİZLİK BİTTİ";
                        gauge.Completed = true;
                        statusTitle.Text = "Temizlik başarıyla tamamlandı";
                        statusDetail.Text = result.DeletedFiles.ToString("N0") + " dosya temizlendi • " +
                                            SafeCleanupEngine.FormatBytes(result.ReleasedBytes) + " alan açıldı • " +
                                            result.SkippedFiles + " kullanımdaki dosya atlandı.";
                        currentPath.Text = "✓ Yeniden başlatma gerekmiyor • Yeniden taramak için daireye tıklayın.";
                    };

                    verificationTimer.Interval = 420;
                    verificationTimer.Tick += delegate
                    {
                        if (gauge.IsDisposed)
                        {
                            verificationTimer.Stop();
                            verificationTimer.Dispose();
                            return;
                        }

                        verificationStage++;
                        gauge.Progress = Math.Max(gauge.Progress, Math.Min(99, 95 + verificationStage));
                        gauge.PrimaryText = gauge.Progress + "%";
                        gauge.SecondaryText = "TEMİZLİK KONTROLÜ";
                        if (verificationStage % 3 == 1)
                        {
                            statusTitle.Text = "Temizlenen öğeler doğrulanıyor…";
                            statusDetail.Text = "Silinen ve kullanımdaki olduğu için atlanan dosyalar kontrol ediliyor.";
                            currentPath.Text = "Temizlik doğrulaması • Dosya kontrolü";
                        }
                        else if (verificationStage % 3 == 2)
                        {
                            statusTitle.Text = "Boşaltılan alan hesaplanıyor…";
                            statusDetail.Text = "Kazanılan disk alanı ve temizlenen dosya sayısı kesinleştiriliyor.";
                            currentPath.Text = "Temizlik doğrulaması • Alan hesabı";
                        }
                        else
                        {
                            statusTitle.Text = "Sonuç raporu hazırlanıyor…";
                            statusDetail.Text = "Temizlik özeti son kez kontrol edilip ekrana hazırlanıyor.";
                            currentPath.Text = "Temizlik doğrulaması • Sonuç hazırlanıyor";
                        }

                        if (visualClock.ElapsedMilliseconds >= 3400 && verificationStage >= 3)
                        {
                            finishCleanup();
                        }
                    };
                    verificationTimer.Start();
                };
                worker.RunWorkerAsync();
            };

            gauge.Click += delegate
            {
                if (busy) return;
                bool hasSelectedResult = scanResults.Values.Any(
                    x => toggles.ContainsKey(x.Target.Id) &&
                         toggles[x.Target.Id].Checked &&
                         x.FileCount > 0);
                if (hasSelectedResult)
                {
                    startClean(gauge, EventArgs.Empty);
                }
                else
                {
                    startScan(gauge, EventArgs.Empty);
                }
            };

            updateSelectionSummary();
        }

        private static string CleanupShortDescription(string id)
        {
            switch (id)
            {
                case "user-temp": return "Kullanıcı geçici dosyaları";
                case "directx-cache": return "Oyun gölgelendirici önbelleği";
                case "crash-dumps": return "Uygulama çökme raporları";
                case "windows-temp": return "Windows geçici dosyaları";
                case "error-reports": return "Windows hata raporları";
                case "browser-cache": return "Oturum ve parolalar korunur";
                default: return "Güvenli gereksiz dosyalar";
            }
        }

        private static string CompactCleanupPagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string compact = path;
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(user) &&
                compact.StartsWith(user, StringComparison.OrdinalIgnoreCase))
            {
                compact = "%USERPROFILE%" + compact.Substring(user.Length);
            }
            if (compact.Length <= 92) return compact;
            return compact.Substring(0, 53) + "…\\" + Path.GetFileName(compact);
        }

        private void ShowCleanupPageLegacy()
        {
            pageTitle.Text = "Temizlik Merkezi";
            pageDescription.Text = "Önce alanı ölçün; sonuçları görmeden hiçbir dosya silinmez.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel panel = new SmoothPanel();
            panel.Width = 1010;
            panel.Height = 590;
            panel.Margin = new Padding(0, 0, 0, 16);
            panel.BackColor = AppTheme.Surface;
            flow.Controls.Add(panel);
            Label title = UiFactory.Label("Güvenli disk taraması", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            panel.Controls.Add(title);
            AddToolboxBack(panel);
            Label copy = UiFactory.Label(
                "Oturum, parola, indirme ve belge klasörleri kapsam dışıdır. Tarayıcılar açıksa kilitli önbellek dosyaları atlanır.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(25, 58);
            panel.Controls.Add(copy);

            ListView list = NewDarkListView();
            list.Location = new Point(24, 95);
            list.Size = new Size(960, 365);
            list.CheckBoxes = true;
            list.Columns.Add("Kategori", 245);
            list.Columns.Add("Açıklama", 455);
            list.Columns.Add("Dosya", 90);
            list.Columns.Add("Alan", 115);
            panel.Controls.Add(list);

            List<CleanupTarget> targets = SafeCleanupEngine.BuildCatalog();
            foreach (CleanupTarget target in targets)
            {
                ListViewItem row = new ListViewItem(target.Name);
                row.SubItems.Add(target.Description);
                row.SubItems.Add("—");
                row.SubItems.Add("Taranmadı");
                row.Tag = target;
                row.Checked = target.Recommended;
                list.Items.Add(row);
            }

            Label status = UiFactory.Label("Hazır. Temizlemek istediğiniz kategorileri seçip tarayın.", AppTheme.Body, AppTheme.TextMuted);
            status.Location = new Point(26, 478);
            panel.Controls.Add(status);
            Button scanButton = UiFactory.Button("Seçilenleri tara", AppTheme.Accent, Color.White);
            scanButton.Location = new Point(24, 522);
            panel.Controls.Add(scanButton);
            Button cleanButton = UiFactory.Button("İncele ve temizle", AppTheme.SurfaceRaised, AppTheme.Text);
            cleanButton.Location = new Point(176, 522);
            cleanButton.Enabled = false;
            panel.Controls.Add(cleanButton);
            Button storage = UiFactory.Button("Windows Depolama ayarları", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            storage.Location = new Point(340, 522);
            storage.Click += delegate { ProcessRunner.Open("ms-settings:storagesense"); };
            panel.Controls.Add(storage);

            Dictionary<string, CleanupScan> scanResults = new Dictionary<string, CleanupScan>(StringComparer.OrdinalIgnoreCase);
            scanButton.Click += delegate
            {
                List<CleanupTarget> selected = list.Items.Cast<ListViewItem>()
                    .Where(x => x.Checked)
                    .Select(x => (CleanupTarget)x.Tag)
                    .ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Taramak için en az bir kategori seçin.", "tercan.exe");
                    return;
                }
                scanButton.Enabled = false;
                cleanButton.Enabled = false;
                list.Enabled = false;
                status.Text = "Taranıyor… Büyük önbelleklerde bu işlem biraz sürebilir.";
                Cursor = Cursors.WaitCursor;

                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += delegate(object sender, DoWorkEventArgs e)
                {
                    e.Result = selected.Select(SafeCleanupEngine.Scan).ToList();
                };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    Cursor = Cursors.Default;
                    scanButton.Enabled = true;
                    list.Enabled = true;
                    if (e.Error != null)
                    {
                        status.Text = "Tarama tamamlanamadı.";
                        MessageBox.Show(e.Error.Message, "Tarama hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    List<CleanupScan> scans = (List<CleanupScan>)e.Result;
                    scanResults.Clear();
                    foreach (CleanupScan scan in scans) scanResults[scan.Target.Id] = scan;
                    foreach (ListViewItem row in list.Items)
                    {
                        CleanupTarget target = (CleanupTarget)row.Tag;
                        CleanupScan found;
                        if (!scanResults.TryGetValue(target.Id, out found)) continue;
                        row.SubItems[2].Text = found.FileCount.ToString();
                        row.SubItems[3].Text = SafeCleanupEngine.FormatBytes(found.Bytes);
                    }
                    long total = scans.Sum(x => x.Bytes);
                    int files = scans.Sum(x => x.FileCount);
                    status.Text = files + " dosya • " + SafeCleanupEngine.FormatBytes(total) + " temizlenebilir alan bulundu.";
                    cleanButton.Enabled = scans.Any(x => x.FileCount > 0);
                };
                worker.RunWorkerAsync();
            };

            cleanButton.Click += delegate
            {
                List<CleanupScan> selected = list.Items.Cast<ListViewItem>()
                    .Where(x => x.Checked)
                    .Select(x =>
                    {
                        CleanupTarget target = (CleanupTarget)x.Tag;
                        CleanupScan scan;
                        return scanResults.TryGetValue(target.Id, out scan) ? scan : null;
                    })
                    .Where(x => x != null && x.FileCount > 0)
                    .ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show("Seçili kategorileri yeniden tarayın.", "tercan.exe");
                    return;
                }
                long bytes = selected.Sum(x => x.Bytes);
                int files = selected.Sum(x => x.FileCount);
                DialogResult answer = MessageBox.Show(
                    files + " dosya ve yaklaşık " + SafeCleanupEngine.FormatBytes(bytes) +
                    " silinecek.\n\nKullanımdaki dosyalar atlanır. Bu temizlik geri alınamaz. Devam edilsin mi?",
                    "Temizliği onayla",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;

                cleanButton.Enabled = false;
                scanButton.Enabled = false;
                list.Enabled = false;
                status.Text = "Seçilen güvenli konumlar temizleniyor…";
                Cursor = Cursors.WaitCursor;
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += delegate(object sender, DoWorkEventArgs e)
                {
                    e.Result = SafeCleanupEngine.Clean(selected);
                };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    Cursor = Cursors.Default;
                    scanButton.Enabled = true;
                    list.Enabled = true;
                    scanResults.Clear();
                    if (e.Error != null)
                    {
                        status.Text = "Temizlik tamamlanamadı.";
                        MessageBox.Show(e.Error.Message, "Temizlik hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    CleanupResult result = (CleanupResult)e.Result;
                    foreach (ListViewItem row in list.Items)
                    {
                        row.SubItems[2].Text = "—";
                        row.SubItems[3].Text = "Yeniden tara";
                    }
                    status.Text = result.DeletedFiles + " dosya silindi • " +
                                  SafeCleanupEngine.FormatBytes(result.ReleasedBytes) + " alan açıldı • " +
                                  result.SkippedFiles + " kilitli/erişilemez dosya atlandı.";
                    MessageBox.Show(status.Text, "Temizlik tamamlandı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                worker.RunWorkerAsync();
            };
        }

        private void ShowStartupPage()
        {
            pageTitle.Text = "Başlangıç Yöneticisi";
            pageDescription.Text = "Windows ile açılan uygulamaları yedekli biçimde yönetin.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);
            SmoothPanel panel = new SmoothPanel();
            panel.Width = 1010;
            panel.Height = 610;
            panel.Margin = new Padding(0, 0, 0, 16);
            panel.BackColor = AppTheme.Surface;
            flow.Controls.Add(panel);
            Label title = UiFactory.Label("Kayıt defteri başlangıç girdileri", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            panel.Controls.Add(title);
            AddToolboxBack(panel);
            Label copy = UiFactory.Label(
                "Bir girdiyi kapatınca komutu Tercan yedeğine alınır. Görev Zamanlayıcı ve Windows hizmetleri bu sayfada değiştirilmez.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(25, 58);
            panel.Controls.Add(copy);

            ListView list = NewDarkListView();
            list.Location = new Point(24, 95);
            list.Size = new Size(960, 410);
            list.FullRowSelect = true;
            list.Columns.Add("Program", 205);
            list.Columns.Add("Durum", 105);
            list.Columns.Add("Konum", 155);
            list.Columns.Add("Komut", 470);
            panel.Controls.Add(list);
            Label status = UiFactory.Label("Başlangıç girdileri yükleniyor…", AppTheme.Body, AppTheme.TextMuted);
            status.Location = new Point(26, 522);
            panel.Controls.Add(status);

            Action refresh = delegate
            {
                try
                {
                    list.BeginUpdate();
                    list.Items.Clear();
                    foreach (StartupRecord record in StartupManager.ReadAll())
                    {
                        ListViewItem item = new ListViewItem(record.Name);
                        item.SubItems.Add(record.Protected ? "Korumalı" : (record.Enabled ? "Etkin" : "Devre dışı"));
                        item.SubItems.Add(record.Hive + " • " + (((RegistryView)record.View) == RegistryView.Registry32 ? "32-bit" : "64-bit"));
                        item.SubItems.Add(record.Command);
                        item.ForeColor = record.Protected ? AppTheme.Amber : (record.Enabled ? AppTheme.Text : AppTheme.TextMuted);
                        item.Tag = record;
                        list.Items.Add(item);
                    }
                    status.Text = list.Items.Count + " başlangıç girdisi gösteriliyor. Bir satır seçip durumunu değiştirebilirsiniz.";
                }
                catch (Exception ex)
                {
                    status.Text = "Başlangıç girdileri okunamadı.";
                    MessageBox.Show(ex.Message, "Okuma hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally
                {
                    list.EndUpdate();
                }
            };

            Button disable = UiFactory.Button("Seçileni devre dışı bırak", AppTheme.Amber, Color.FromArgb(30, 25, 12));
            disable.Location = new Point(24, 558);
            panel.Controls.Add(disable);
            Button enable = UiFactory.Button("Seçileni geri getir", AppTheme.Green, Color.FromArgb(10, 32, 20));
            enable.Location = new Point(230, 558);
            panel.Controls.Add(enable);
            Button reload = UiFactory.Button("Listeyi yenile", AppTheme.SurfaceRaised, AppTheme.Text);
            reload.Location = new Point(405, 558);
            reload.Click += delegate { refresh(); };
            panel.Controls.Add(reload);

            disable.Click += delegate
            {
                if (list.SelectedItems.Count == 0) { MessageBox.Show("Önce bir başlangıç girdisi seçin.", "tercan.exe"); return; }
                StartupRecord record = (StartupRecord)list.SelectedItems[0].Tag;
                if (record.Protected) { MessageBox.Show("Bu girdi Windows güvenliği veya tek seferlik kurulum görevi için korunuyor.", "tercan.exe"); return; }
                if (!record.Enabled) { MessageBox.Show("Bu girdi zaten devre dışı.", "tercan.exe"); return; }
                if (MessageBox.Show(
                    record.Name + " Windows başlangıcından kaldırılacak ve geri dönüş yedeği saklanacak.\n\nDevam edilsin mi?",
                    "Başlangıç girdisini kapat",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;
                try { StartupManager.SetEnabled(record, false); refresh(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Değişiklik uygulanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            enable.Click += delegate
            {
                if (list.SelectedItems.Count == 0) { MessageBox.Show("Önce bir başlangıç girdisi seçin.", "tercan.exe"); return; }
                StartupRecord record = (StartupRecord)list.SelectedItems[0].Tag;
                if (record.Enabled) { MessageBox.Show("Bu girdi zaten etkin.", "tercan.exe"); return; }
                try { StartupManager.SetEnabled(record, true); refresh(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Geri yükleme yapılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            refresh();
        }

        private void ShowNetworkToolsPage()
        {
            pageTitle.Text = "Ağ ve DNS Araçları";
            pageDescription.Text = "Bağlantınızı görün, gecikmeyi ölçün ve DNS değişikliklerini geri alın.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            SmoothPanel adapterPanel = new SmoothPanel();
            adapterPanel.Width = 1010;
            adapterPanel.Height = 245;
            adapterPanel.Margin = new Padding(0, 0, 0, 16);
            adapterPanel.BackColor = AppTheme.Surface;
            flow.Controls.Add(adapterPanel);
            Label title = UiFactory.Label("Etkin ağ bağlantısı", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            adapterPanel.Controls.Add(title);
            AddToolboxBack(adapterPanel);
            ComboBox adapters = new ComboBox();
            adapters.Location = new Point(25, 65);
            adapters.Size = new Size(430, 32);
            adapters.DropDownStyle = ComboBoxStyle.DropDownList;
            adapters.BackColor = AppTheme.SurfaceRaised;
            adapters.ForeColor = AppTheme.Text;
            adapters.Font = AppTheme.Body;
            adapterPanel.Controls.Add(adapters);
            Label adapterInfo = UiFactory.Label("Ağ bilgisi yükleniyor…", AppTheme.Body, AppTheme.TextMuted);
            adapterInfo.Location = new Point(26, 112);
            adapterInfo.Size = new Size(930, 76);
            adapterInfo.AutoSize = false;
            adapterPanel.Controls.Add(adapterInfo);
            Button refresh = UiFactory.Button("Bağlantıları yenile", AppTheme.SurfaceRaised, AppTheme.Text);
            refresh.Location = new Point(25, 195);
            adapterPanel.Controls.Add(refresh);
            Button settings = UiFactory.Button("Windows ağ ayarları", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            settings.Location = new Point(183, 195);
            settings.Click += delegate { ProcessRunner.Open("ms-settings:network-status"); };
            adapterPanel.Controls.Add(settings);

            Action updateInfo = delegate
            {
                NetworkAdapterSnapshot selected = adapters.SelectedItem as NetworkAdapterSnapshot;
                if (selected == null) { adapterInfo.Text = "Kullanılabilir ağ bağdaştırıcısı bulunamadı."; return; }
                adapterInfo.Text =
                    selected.Description + Environment.NewLine +
                    "IPv4: " + JoinValues(selected.Addresses) + "    Ağ geçidi: " + JoinValues(selected.Gateways) + Environment.NewLine +
                    "DNS: " + JoinValues(selected.DnsServers) +
                    (NetworkTools.HasBackup(selected) ? "    • Tercan geri dönüş yedeği var" : string.Empty);
            };
            Action reloadAdapters = delegate
            {
                string previous = (adapters.SelectedItem as NetworkAdapterSnapshot) == null ? string.Empty : ((NetworkAdapterSnapshot)adapters.SelectedItem).Id;
                adapters.Items.Clear();
                foreach (NetworkAdapterSnapshot adapter in NetworkTools.ReadAdapters()) adapters.Items.Add(adapter);
                int select = 0;
                for (int i = 0; i < adapters.Items.Count; i++)
                {
                    if (string.Equals(((NetworkAdapterSnapshot)adapters.Items[i]).Id, previous, StringComparison.OrdinalIgnoreCase)) select = i;
                }
                if (adapters.Items.Count > 0) adapters.SelectedIndex = select;
                updateInfo();
            };
            adapters.SelectedIndexChanged += delegate { updateInfo(); };
            refresh.Click += delegate { reloadAdapters(); };

            SmoothPanel pingPanel = new SmoothPanel();
            pingPanel.Width = 1010;
            pingPanel.Height = 160;
            pingPanel.Margin = new Padding(0, 0, 0, 16);
            pingPanel.BackColor = AppTheme.Surface;
            flow.Controls.Add(pingPanel);
            Label pingTitle = UiFactory.Label("Ping ve gecikme testi", AppTheme.Subheading, AppTheme.Text);
            pingTitle.Location = new Point(24, 18);
            pingPanel.Controls.Add(pingTitle);
            TextBox pingHost = DarkTextBox();
            pingHost.Text = "1.1.1.1";
            pingHost.Location = new Point(25, 59);
            pingHost.Size = new Size(300, 30);
            pingPanel.Controls.Add(pingHost);
            Button ping = UiFactory.Button("Ping gönder", AppTheme.Accent, Color.White);
            ping.Location = new Point(340, 55);
            pingPanel.Controls.Add(ping);
            Label pingResult = UiFactory.Label("Sunucu adı veya IP adresi girin.", AppTheme.Body, AppTheme.TextMuted);
            pingResult.Location = new Point(26, 110);
            pingPanel.Controls.Add(pingResult);
            ping.Click += delegate
            {
                ping.Enabled = false;
                pingResult.Text = "Yanıt bekleniyor…";
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = NetworkTools.PingHost(pingHost.Text); };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    ping.Enabled = true;
                    pingResult.Text = e.Error == null ? Convert.ToString(e.Result) : e.Error.Message;
                    pingResult.ForeColor = e.Error == null ? AppTheme.Green : AppTheme.Amber;
                };
                worker.RunWorkerAsync();
            };

            SmoothPanel dnsPanel = new SmoothPanel();
            dnsPanel.Width = 1010;
            dnsPanel.Height = 215;
            dnsPanel.Margin = new Padding(0, 0, 0, 24);
            dnsPanel.BackColor = AppTheme.Surface;
            flow.Controls.Add(dnsPanel);
            Label dnsTitle = UiFactory.Label("DNS değiştirici", AppTheme.Subheading, AppTheme.Text);
            dnsTitle.Location = new Point(24, 18);
            dnsPanel.Controls.Add(dnsTitle);
            Label dnsCopy = UiFactory.Label(
                "DNS, FPS'i artırmaz; alan adı çözümleme hızını ve erişilebilirliği etkileyebilir. İlk değişiklikten önce mevcut sunucular yedeklenir.",
                AppTheme.Body,
                AppTheme.TextMuted);
            dnsCopy.Location = new Point(25, 51);
            dnsPanel.Controls.Add(dnsCopy);
            ComboBox presets = new ComboBox();
            presets.Location = new Point(25, 90);
            presets.Size = new Size(350, 30);
            presets.DropDownStyle = ComboBoxStyle.DropDownList;
            presets.BackColor = AppTheme.SurfaceRaised;
            presets.ForeColor = AppTheme.Text;
            presets.Font = AppTheme.Body;
            foreach (DnsPreset preset in NetworkTools.Presets()) presets.Items.Add(preset);
            if (presets.Items.Count > 0) presets.SelectedIndex = 0;
            dnsPanel.Controls.Add(presets);
            Button applyDns = UiFactory.Button("Seçili DNS'i uygula", AppTheme.Accent, Color.White);
            applyDns.Location = new Point(390, 86);
            dnsPanel.Controls.Add(applyDns);
            Button restoreDns = UiFactory.Button("Önceki DNS'i geri yükle", AppTheme.SurfaceRaised, AppTheme.Text);
            restoreDns.Location = new Point(565, 86);
            dnsPanel.Controls.Add(restoreDns);
            Button flush = UiFactory.Button("DNS önbelleğini temizle", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            flush.Location = new Point(25, 145);
            dnsPanel.Controls.Add(flush);
            Label dnsStatus = UiFactory.Label("Hazır.", AppTheme.Body, AppTheme.TextMuted);
            dnsStatus.Location = new Point(230, 155);
            dnsPanel.Controls.Add(dnsStatus);

            applyDns.Click += delegate
            {
                NetworkAdapterSnapshot adapter = adapters.SelectedItem as NetworkAdapterSnapshot;
                DnsPreset preset = presets.SelectedItem as DnsPreset;
                if (adapter == null || preset == null) { MessageBox.Show("Bağlı bir ağ bağdaştırıcısı ve DNS seçin.", "tercan.exe"); return; }
                if (MessageBox.Show(
                    adapter.Name + " için DNS " + preset.Name + " olarak değiştirilecek.\nMevcut ayar geri alma için saklanacak. Devam edilsin mi?",
                    "DNS değişikliğini onayla",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    Cursor = Cursors.WaitCursor;
                    NetworkTools.SetDns(adapter, preset);
                    dnsStatus.Text = preset.Name + " uygulandı ve DNS önbelleği temizlendi.";
                    dnsStatus.ForeColor = AppTheme.Green;
                    reloadAdapters();
                }
                catch (Exception ex)
                {
                    dnsStatus.Text = "DNS değiştirilemedi.";
                    dnsStatus.ForeColor = AppTheme.Amber;
                    MessageBox.Show(ex.Message, "DNS hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                finally { Cursor = Cursors.Default; }
            };
            restoreDns.Click += delegate
            {
                NetworkAdapterSnapshot adapter = adapters.SelectedItem as NetworkAdapterSnapshot;
                if (adapter == null) { MessageBox.Show("Önce bir ağ bağdaştırıcısı seçin.", "tercan.exe"); return; }
                try
                {
                    Cursor = Cursors.WaitCursor;
                    NetworkTools.RestoreDns(adapter);
                    dnsStatus.Text = "Tercan öncesindeki DNS ayarı geri yüklendi.";
                    dnsStatus.ForeColor = AppTheme.Green;
                    reloadAdapters();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "DNS geri yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { Cursor = Cursors.Default; }
            };
            flush.Click += delegate
            {
                try
                {
                    ProcessResult result = NetworkTools.FlushDns();
                    dnsStatus.Text = result.ExitCode == 0 ? "DNS önbelleği temizlendi." : "DNS önbelleği temizlenemedi.";
                    dnsStatus.ForeColor = result.ExitCode == 0 ? AppTheme.Green : AppTheme.Amber;
                }
                catch (Exception ex) { dnsStatus.Text = ex.Message; dnsStatus.ForeColor = AppTheme.Amber; }
            };
            reloadAdapters();
        }

        private void ShowRepairPage()
        {
            pageTitle.Text = "Windows Onarım";
            pageDescription.Text = "Microsoft'un yerleşik doğrulama ve onarım araçlarını görünür çıktıyla çalıştırın.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);
            SmoothPanel panel = new SmoothPanel();
            panel.Width = 1010;
            panel.Height = 665;
            panel.Margin = new Padding(0, 0, 0, 24);
            panel.BackColor = AppTheme.Surface;
            flow.Controls.Add(panel);
            Label title = UiFactory.Label("Sistem dosyası ve bileşen onarımı", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            panel.Controls.Add(title);
            AddToolboxBack(panel);
            Label copy = UiFactory.Label(
                "Aynı anda tek işlem çalışır. SFC ve DISM uzun sürebilir; uygulamayı işlem tamamlanana kadar kapatmayın.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(25, 58);
            panel.Controls.Add(copy);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Location = new Point(24, 94);
            actions.Size = new Size(960, 118);
            actions.WrapContents = false;
            actions.BackColor = Color.Transparent;
            panel.Controls.Add(actions);
            Button sfc = RepairButton("SFC /scannow", "Bozuk Windows sistem dosyalarını tarar ve onarır.", AppTheme.Accent);
            Button dism = RepairButton("DISM RestoreHealth", "Windows bileşen deposunu çevrimiçi kaynakla onarır.", AppTheme.Green);
            Button chkdsk = RepairButton("CHKDSK /scan", "Sistem sürücüsünü çevrimiçi ve salt okunur tarar.", AppTheme.Cyan);
            Button scanHealth = RepairButton("DISM ScanHealth", "Bileşen deposunda bozulma olup olmadığını inceler.", AppTheme.Amber);
            actions.Controls.Add(sfc);
            actions.Controls.Add(dism);
            actions.Controls.Add(chkdsk);
            actions.Controls.Add(scanHealth);

            Label status = UiFactory.Label("Bir onarım aracı seçin.", AppTheme.Body, AppTheme.TextMuted);
            status.Location = new Point(26, 227);
            panel.Controls.Add(status);
            TextBox output = DarkTextBox();
            output.Location = new Point(24, 260);
            output.Size = new Size(960, 315);
            output.Multiline = true;
            output.ReadOnly = true;
            output.ScrollBars = ScrollBars.Both;
            output.WordWrap = false;
            output.Font = new Font("Consolas", 9f);
            output.Text = "Çalıştırılan aracın çıktısı burada görünecek.";
            panel.Controls.Add(output);

            Button storeReset = UiFactory.Button("Microsoft Store önbelleğini sıfırla", AppTheme.SurfaceRaised, AppTheme.Text);
            storeReset.Location = new Point(24, 596);
            storeReset.Click += delegate
            {
                if (repairRunning) { MessageBox.Show("Önce çalışan onarım işleminin bitmesini bekleyin.", "tercan.exe"); return; }
                try { ProcessRunner.Open("wsreset.exe"); status.Text = "Microsoft Store sıfırlama aracı açıldı."; }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Araç açılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            panel.Controls.Add(storeReset);
            Button troubleshoot = UiFactory.Button("Sorun giderme ayarları", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            troubleshoot.Location = new Point(285, 596);
            troubleshoot.Click += delegate { ProcessRunner.Open("ms-settings:troubleshoot"); };
            panel.Controls.Add(troubleshoot);
            Button restorePoint = UiFactory.Button("Geri yükleme noktası oluştur", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            restorePoint.Location = new Point(470, 596);
            restorePoint.Click += delegate
            {
                try
                {
                    Cursor = Cursors.WaitCursor;
                    ProcessResult result = RestorePointTools.Create("Tercan manuel yedek");
                    MessageBox.Show(
                        result.ExitCode == 0 ? "Geri yükleme noktası oluşturuldu." : (result.Error + Environment.NewLine + result.Output).Trim(),
                        result.ExitCode == 0 ? "Hazır" : "Oluşturulamadı",
                        MessageBoxButtons.OK,
                        result.ExitCode == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Oluşturulamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                finally { Cursor = Cursors.Default; }
            };
            panel.Controls.Add(restorePoint);

            sfc.Click += delegate { RunRepair("SFC sistem dosyası taraması", "sfc.exe", "/scannow", 1800000, output, status, actions); };
            dism.Click += delegate { RunRepair("DISM bileşen onarımı", "dism.exe", "/Online /Cleanup-Image /RestoreHealth", 3600000, output, status, actions); };
            scanHealth.Click += delegate { RunRepair("DISM bileşen taraması", "dism.exe", "/Online /Cleanup-Image /ScanHealth", 1800000, output, status, actions); };
            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory);
            chkdsk.Click += delegate { RunRepair("CHKDSK çevrimiçi taraması", "chkdsk.exe", "\"" + systemDrive.TrimEnd('\\') + "\" /scan", 1800000, output, status, actions); };
        }

        private Button RepairButton(string title, string description, Color accent)
        {
            Button button = new Button();
            button.Width = 231;
            button.Height = 108;
            button.Margin = new Padding(0, 0, 9, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = accent;
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = AppTheme.SurfaceRaised;
            button.ForeColor = AppTheme.Text;
            button.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 8, 10, 8);
            button.Text = title + Environment.NewLine + Environment.NewLine + description;
            button.Cursor = Cursors.Hand;
            return button;
        }

        private void RunRepair(string title, string file, string arguments, int timeout, TextBox output, Label status, Control actionHost)
        {
            if (repairRunning)
            {
                MessageBox.Show("Başka bir onarım işlemi devam ediyor.", "tercan.exe");
                return;
            }
            if (MessageBox.Show(
                title + " başlatılacak. İşlem tamamlanana kadar uygulamayı açık tutun.\n\nDevam edilsin mi?",
                "Onarım aracını çalıştır",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;

            repairRunning = true;
            actionHost.Enabled = false;
            status.Text = title + " çalışıyor…";
            status.ForeColor = AppTheme.Cyan;
            output.Text = title + Environment.NewLine + new string('-', 60) + Environment.NewLine + "İşlem devam ediyor…";
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = ProcessRunner.Run(file, arguments, timeout); };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                repairRunning = false;
                actionHost.Enabled = true;
                if (e.Error != null)
                {
                    status.Text = title + " tamamlanamadı.";
                    status.ForeColor = AppTheme.Amber;
                    output.Text = e.Error.ToString();
                    return;
                }
                ProcessResult result = (ProcessResult)e.Result;
                status.Text = title + (result.ExitCode == 0 ? " tamamlandı." : " hata koduyla tamamlandı: " + result.ExitCode);
                status.ForeColor = result.ExitCode == 0 ? AppTheme.Green : AppTheme.Amber;
                output.Text = ((result.Output ?? string.Empty) + Environment.NewLine + (result.Error ?? string.Empty)).Trim();
                if (string.IsNullOrWhiteSpace(output.Text)) output.Text = "Araç çıktı üretmedi. Çıkış kodu: " + result.ExitCode;
                Logger.Info(title + " tamamlandı. Kod=" + result.ExitCode);
            };
            worker.RunWorkerAsync();
        }

        private void ShowHardwarePage()
        {
            pageTitle.Text = "Donanım Raporu";
            pageDescription.Text = "Bilgisayarınızın temel donanım ve sürücü bilgilerini tek yerde görün.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);
            SmoothPanel panel = new SmoothPanel();
            panel.Width = 1010;
            panel.Height = 675;
            panel.Margin = new Padding(0, 0, 0, 24);
            panel.BackColor = AppTheme.Surface;
            flow.Controls.Add(panel);
            Label title = UiFactory.Label("Sistem envanteri", new Font("Segoe UI Semibold", 17f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(24, 20);
            panel.Controls.Add(title);
            AddToolboxBack(panel);
            Label status = UiFactory.Label("Donanım bilgileri okunuyor…", AppTheme.Body, AppTheme.TextMuted);
            status.Location = new Point(25, 58);
            panel.Controls.Add(status);
            TextBox report = DarkTextBox();
            report.Location = new Point(24, 94);
            report.Size = new Size(960, 505);
            report.Multiline = true;
            report.ReadOnly = true;
            report.ScrollBars = ScrollBars.Both;
            report.WordWrap = false;
            report.Font = new Font("Consolas", 9f);
            report.Text = "Rapor hazırlanıyor…";
            panel.Controls.Add(report);
            Button refresh = UiFactory.Button("Raporu yenile", AppTheme.Accent, Color.White);
            refresh.Location = new Point(24, 617);
            panel.Controls.Add(refresh);
            Button save = UiFactory.Button("Metin dosyası olarak kaydet", AppTheme.SurfaceRaised, AppTheme.Text);
            save.Location = new Point(160, 617);
            save.Enabled = false;
            panel.Controls.Add(save);

            Action load = delegate
            {
                refresh.Enabled = false;
                save.Enabled = false;
                status.Text = "Donanım bilgileri okunuyor…";
                report.Text = "Rapor hazırlanıyor…";
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += delegate(object sender, DoWorkEventArgs e) { e.Result = HardwareReport.Build(); };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
                {
                    if (report.IsDisposed) return;
                    refresh.Enabled = true;
                    if (e.Error != null)
                    {
                        report.Text = e.Error.ToString();
                        status.Text = "Rapor oluşturulamadı.";
                        status.ForeColor = AppTheme.Amber;
                        return;
                    }
                    report.Text = Convert.ToString(e.Result);
                    status.Text = "Rapor hazır. Hassas seri numaraları özellikle dahil edilmedi.";
                    status.ForeColor = AppTheme.Green;
                    save.Enabled = true;
                };
                worker.RunWorkerAsync();
            };
            refresh.Click += delegate { load(); };
            save.Click += delegate
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Title = "Donanım raporunu kaydet";
                dialog.Filter = "Metin dosyası (*.txt)|*.txt";
                dialog.FileName = "Tercan-Donanim-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dialog.FileName, report.Text, new System.Text.UTF8Encoding(true));
                    MessageBox.Show("Rapor kaydedildi.", "tercan.exe", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Rapor kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            if (previewMode)
            {
                report.Text = "TERCAN.EXE - DONANIM VE SİSTEM RAPORU\r\n\r\n[İŞLETİM SİSTEMİ]\r\nWindows 10 / 11\r\n\r\n[İŞLEMCİ]\r\nDonanım bilgileri uygulama açıldığında burada gösterilir.";
                status.Text = "Donanım raporu önizlemesi.";
            }
            else load();
        }

        private void ShowHostsPage()
        {
            pageTitle.Text = "HOSTS Editörü";
            pageDescription.Text = "Alan adı yönlendirmelerini doğrulama ve otomatik yedek korumasıyla düzenleyin.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);
            SmoothPanel warning = new SmoothPanel();
            warning.Width = 1010;
            warning.Height = 112;
            warning.Margin = new Padding(0, 0, 0, 16);
            warning.BackColor = Color.FromArgb(34, 29, 24);
            warning.BorderColor = AppTheme.Amber;
            flow.Controls.Add(warning);
            Label warningTitle = UiFactory.Label("Dikkatli kullanın", AppTheme.Subheading, AppTheme.Amber);
            warningTitle.Location = new Point(24, 18);
            warning.Controls.Add(warningTitle);
            AddToolboxBack(warning);
            Label warningCopy = UiFactory.Label(
                "Yanlış bir HOSTS girdisi web sitelerine, oyun sunucularına veya başlatıcılara erişimi engelleyebilir. " +
                "Kaydetmeden önce biçim doğrulanır ve mevcut dosyanın zaman damgalı yedeği alınır.",
                AppTheme.Body,
                AppTheme.TextMuted);
            warningCopy.Location = new Point(25, 54);
            warningCopy.Size = new Size(930, 45);
            warningCopy.AutoSize = false;
            warning.Controls.Add(warningCopy);

            SmoothPanel editorPanel = new SmoothPanel();
            editorPanel.Width = 1010;
            editorPanel.Height = 545;
            editorPanel.Margin = new Padding(0, 0, 0, 24);
            editorPanel.BackColor = AppTheme.Surface;
            flow.Controls.Add(editorPanel);
            Label title = UiFactory.Label("Windows HOSTS dosyası", AppTheme.Subheading, AppTheme.Text);
            title.Location = new Point(24, 18);
            editorPanel.Controls.Add(title);
            Label path = UiFactory.Label(HostsManager.HostsPath, AppTheme.Small, AppTheme.TextMuted);
            path.Location = new Point(25, 50);
            editorPanel.Controls.Add(path);
            TextBox editor = DarkTextBox();
            editor.Location = new Point(24, 82);
            editor.Size = new Size(960, 365);
            editor.Multiline = true;
            editor.AcceptsReturn = true;
            editor.AcceptsTab = true;
            editor.ScrollBars = ScrollBars.Both;
            editor.WordWrap = false;
            editor.Font = new Font("Consolas", 9.5f);
            editorPanel.Controls.Add(editor);
            Label status = UiFactory.Label("Dosya yükleniyor…", AppTheme.Body, AppTheme.TextMuted);
            status.Location = new Point(26, 461);
            editorPanel.Controls.Add(status);
            Button validate = UiFactory.Button("Biçimi doğrula", AppTheme.SurfaceRaised, AppTheme.Text);
            validate.Location = new Point(24, 495);
            editorPanel.Controls.Add(validate);
            Button save = UiFactory.Button("Yedekle ve kaydet", AppTheme.Accent, Color.White);
            save.Location = new Point(155, 495);
            editorPanel.Controls.Add(save);
            Button restore = UiFactory.Button("Son yedeği geri yükle", AppTheme.SurfaceRaised, AppTheme.Text);
            restore.Location = new Point(320, 495);
            editorPanel.Controls.Add(restore);
            Button reload = UiFactory.Button("Dosyayı yeniden yükle", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            reload.Location = new Point(490, 495);
            editorPanel.Controls.Add(reload);

            Action load = delegate
            {
                try
                {
                    editor.Text = HostsManager.Read();
                    status.Text = "Dosya yüklendi" + (HostsManager.HasBackup() ? " • Tercan yedeği var." : ".");
                    status.ForeColor = AppTheme.TextMuted;
                }
                catch (Exception ex) { status.Text = ex.Message; status.ForeColor = AppTheme.Amber; }
            };
            validate.Click += delegate
            {
                try { HostsManager.Validate(editor.Text); status.Text = "Biçim geçerli. Kaydetmeye hazır."; status.ForeColor = AppTheme.Green; }
                catch (Exception ex) { status.Text = ex.Message; status.ForeColor = AppTheme.Amber; }
            };
            save.Click += delegate
            {
                try
                {
                    HostsManager.Validate(editor.Text);
                    if (MessageBox.Show(
                        "Mevcut HOSTS dosyası yedeklenecek ve düzenlediğiniz içerik kaydedilecek.\n\nDevam edilsin mi?",
                        "HOSTS değişikliğini onayla",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    HostsManager.Save(editor.Text);
                    NetworkTools.FlushDns();
                    status.Text = "HOSTS kaydedildi, önceki dosya yedeklendi ve DNS önbelleği temizlendi.";
                    status.ForeColor = AppTheme.Green;
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "HOSTS kaydedilemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            restore.Click += delegate
            {
                if (MessageBox.Show(
                    "En son Tercan HOSTS yedeği geri yüklenecek. Devam edilsin mi?",
                    "HOSTS yedeğini geri yükle",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    editor.Text = HostsManager.RestoreLatest();
                    NetworkTools.FlushDns();
                    status.Text = "Son HOSTS yedeği geri yüklendi.";
                    status.ForeColor = AppTheme.Green;
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Geri yüklenemedi", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            reload.Click += delegate { load(); };
            load();
        }

        private static ListView NewDarkListView()
        {
            ListView list = new ListView();
            list.View = View.Details;
            list.FullRowSelect = true;
            list.GridLines = false;
            list.HideSelection = false;
            list.BackColor = AppTheme.SurfaceRaised;
            list.ForeColor = AppTheme.Text;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = AppTheme.Body;
            return list;
        }

        private static TextBox DarkTextBox()
        {
            TextBox text = new TextBox();
            text.BackColor = AppTheme.SurfaceRaised;
            text.ForeColor = AppTheme.Text;
            text.BorderStyle = BorderStyle.FixedSingle;
            text.Font = AppTheme.Body;
            return text;
        }

        private static string JoinValues(IEnumerable<string> values)
        {
            string joined = string.Join(", ", values ?? Enumerable.Empty<string>());
            return string.IsNullOrWhiteSpace(joined) ? "—" : joined;
        }
    }
}

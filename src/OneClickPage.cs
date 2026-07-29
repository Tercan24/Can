using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace TercanOptimizer
{
    internal sealed class OneClickScanReport
    {
        public DateTime ScannedAt { get; set; }
        public int ReadinessScore { get; set; }
        public int PerformanceIssues { get; set; }
        public int PrivacyIssues { get; set; }
        public int RepairBlocks { get; set; }
        public List<CleanupScan> CleanupScans { get; set; }

        public OneClickScanReport()
        {
            CleanupScans = new List<CleanupScan>();
        }

        public long CleanupBytes
        {
            get { return CleanupScans.Sum(x => x.Bytes); }
        }

        public int CleanupFiles
        {
            get { return CleanupScans.Sum(x => x.FileCount); }
        }
    }

    internal sealed class OneClickExecutionResult
    {
        public int AppliedTweaks { get; set; }
        public int RepairedPolicies { get; set; }
        public int DeletedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public long ReleasedBytes { get; set; }
        public bool NetworkRefreshed { get; set; }
        public bool RestartRequired { get; set; }
        public List<string> Errors { get; set; }

        public OneClickExecutionResult()
        {
            Errors = new List<string>();
        }
    }

    internal sealed class OneClickReviewItem
    {
        public string Id { get; set; }
        public string Group { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public Color Accent { get; set; }
        public int FileCount { get; set; }
        public long Bytes { get; set; }
    }

    internal sealed class OneClickProgressUpdate
    {
        public string StageKey { get; set; }
        public string Status { get; set; }
        public string Detail { get; set; }
        public int DeletedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public long ReleasedBytes { get; set; }
    }

    internal sealed partial class MainForm
    {
        private const string OneClickPerformance = "performance";
        private const string OneClickPrivacy = "privacy";
        private const string OneClickCleanup = "cleanup";
        private const string OneClickNetwork = "network";
        private const string OneClickRepair = "repair";

        private Dictionary<string, bool> oneClickSelection;
        private HashSet<string> oneClickActionSelection;
        private OneClickScanReport oneClickReport;
        private OneClickExecutionResult oneClickLastResult;
        private bool oneClickBusy;
        private bool oneClickUiUpdating;
        private OptimizerOrbButton oneClickOrb;
        private Label oneClickStatusLabel;
        private ProgressBar oneClickProgress;
        private Label oneClickActivityDetail;
        private Label oneClickActivityStats;
        private FlowLayoutPanel oneClickActivityLog;
        private Dictionary<string, Label> oneClickStageLabels;
        private bool optimizerDetailsVisible;

        private void ShowOneClickOptimizerPage()
        {
            EnsureOneClickSelection();
            pageTitle.Text = "Ana Sayfa";
            pageDescription.Text = "Tara, seçimini yap ve yalnızca istediklerini uygula.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            TercanHeroPanel hero = new TercanHeroPanel();
            hero.Width = 1010;
            hero.Height = oneClickBusy ? 400 : 338;
            hero.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(hero);

            Label eyebrow = UiFactory.Label(
                "TERCAN AKILLI BAKIM MERKEZİ",
                new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                AppTheme.Cyan);
            eyebrow.Location = new Point(34, 34);
            hero.Controls.Add(eyebrow);

            Label title = UiFactory.Label(
                oneClickBusy
                    ? (oneClickReport == null ? "Bilgisayarınız taranıyor" : "Seçtiğiniz işlemler uygulanıyor")
                    : oneClickLastResult == null
                        ? "Tek tıkla temizle ve hızlandır"
                        : "İşlemler tamamlandı",
                new Font("Segoe UI Semibold", 24f, FontStyle.Bold),
                AppTheme.Text);
            title.Location = new Point(32, 66);
            title.MaximumSize = new Size(610, 70);
            hero.Controls.Add(title);

            Label copy = UiFactory.Label(
                oneClickBusy
                    ? "Yapılan işlemi ve temizlenen öğeleri anlık olarak izleyebilirsiniz."
                    : "Güvenli ayarlar, geçici dosyalar ve Windows onarımı. Son karar her zaman sizde.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(35, 119);
            copy.Size = new Size(590, 42);
            copy.AutoSize = false;
            hero.Controls.Add(copy);

            oneClickStatusLabel = UiFactory.Label(CurrentOneClickStatus(), AppTheme.Body, AppTheme.Text);
            oneClickStatusLabel.Location = new Point(35, oneClickBusy ? 168 : 229);
            oneClickStatusLabel.Size = new Size(590, 45);
            oneClickStatusLabel.AutoSize = false;
            hero.Controls.Add(oneClickStatusLabel);

            oneClickProgress = new ProgressBar();
            oneClickProgress.Location = new Point(35, oneClickBusy ? 211 : 290);
            oneClickProgress.Size = new Size(575, 8);
            oneClickProgress.Style = ProgressBarStyle.Continuous;
            oneClickProgress.Visible = oneClickBusy;
            hero.Controls.Add(oneClickProgress);

            oneClickOrb = new OptimizerOrbButton();
            oneClickOrb.Location = new Point(700, 32);
            oneClickOrb.State = oneClickBusy
                ? (oneClickReport == null ? OptimizerOrbState.Scanning : OptimizerOrbState.Optimizing)
                : oneClickLastResult != null
                    ? OptimizerOrbState.Complete
                    : oneClickReport == null
                        ? OptimizerOrbState.Scan
                        : OptimizerOrbState.Optimize;
            oneClickOrb.OrbClick += delegate
            {
                if (oneClickLastResult != null)
                {
                    oneClickLastResult = null;
                    oneClickReport = null;
                    BeginOneClickScan();
                }
                else if (oneClickReport == null)
                {
                    BeginOneClickScan();
                }
                else
                {
                    BeginOneClickOptimization();
                }
            };
            hero.Controls.Add(oneClickOrb);

            if (oneClickBusy)
            {
                BuildOneClickBusyContent(hero, flow);
                return;
            }

            FlowLayoutPanel badges = new FlowLayoutPanel();
            badges.Location = new Point(34, 177);
            badges.Size = new Size(620, 32);
            badges.WrapContents = false;
            badges.BackColor = Color.Transparent;
            badges.Controls.Add(UiFactory.Pill("ÖNCE TARA", AppTheme.Cyan));
            badges.Controls.Add(UiFactory.Pill("TEK TEK SEÇ", AppTheme.Accent));
            badges.Controls.Add(UiFactory.Pill("GERİ ALINABİLİR", AppTheme.Green));
            hero.Controls.Add(badges);

            SmoothPanel modules = new SmoothPanel();
            modules.Width = 1010;
            modules.Height = oneClickReport == null ? 258 : 132;
            modules.Margin = new Padding(0, 0, 0, 16);
            modules.BackColor = AppTheme.Surface;
            flow.Controls.Add(modules);

            Label modulesTitle = UiFactory.Label("Bakım modülleri", AppTheme.Subheading, AppTheme.Text);
            modulesTitle.Location = new Point(24, 18);
            modules.Controls.Add(modulesTitle);
            Label modulesCopy = UiFactory.Label(
                oneClickReport == null
                    ? "Taramaya dahil etmek istediklerinizi açık bırakın."
                    : "Tarama tamamlandı. Aşağıdan istemediğiniz işlemleri kapatabilirsiniz.",
                AppTheme.Small,
                AppTheme.TextMuted);
            modulesCopy.Location = new Point(25, 50);
            modules.Controls.Add(modulesCopy);

            if (oneClickReport == null)
            {
                ToggleSwitch allToggle = new ToggleSwitch();
                allToggle.Location = new Point(906, 20);
                allToggle.Checked = oneClickSelection.All(x => x.Value);
                modules.Controls.Add(allToggle);
                Label allLabel = UiFactory.Label("Tümünü seç", AppTheme.Small, AppTheme.TextMuted);
                allLabel.Location = new Point(823, 25);
                modules.Controls.Add(allLabel);

                AddOneClickModuleCard(
                    modules, 24, 84, OneClickPerformance, "PERFORMANS", "Oyun ve Windows ayarları", "⚡", AppTheme.Accent);
                AddOneClickModuleCard(
                    modules, 344, 84, OneClickPrivacy, "GİZLİLİK", "İzleme ve öneriler", "◉", AppTheme.Cyan);
                AddOneClickModuleCard(
                    modules, 664, 84, OneClickCleanup, "GEREKSİZ DOSYALAR", "Önbellek ve geçici dosyalar", "✦", AppTheme.Green);
                AddOneClickModuleCard(
                    modules, 184, 166, OneClickNetwork, "AĞ YENİLEME", "DNS ve ağ önbelleği", "⌁", AppTheme.Cyan);
                AddOneClickModuleCard(
                    modules, 504, 166, OneClickRepair, "SİSTEM ARAÇLARI", "Windows araçlarını geri aç", "✚", AppTheme.Amber);

                allToggle.CheckedChanged += delegate
                {
                    if (oneClickUiUpdating) return;
                    oneClickUiUpdating = true;
                    foreach (string key in oneClickSelection.Keys.ToList())
                    {
                        oneClickSelection[key] = allToggle.Checked;
                    }
                    oneClickUiUpdating = false;
                    oneClickReport = null;
                    oneClickLastResult = null;
                    oneClickActionSelection = null;
                    Navigate("scanner", null);
                };
            }
            else
            {
                FlowLayoutPanel selectedModules = new FlowLayoutPanel();
                selectedModules.Location = new Point(24, 82);
                selectedModules.Size = new Size(770, 32);
                selectedModules.WrapContents = false;
                selectedModules.BackColor = Color.Transparent;
                selectedModules.Controls.Add(UiFactory.Pill("PERFORMANS", oneClickSelection[OneClickPerformance] ? AppTheme.Accent : AppTheme.TextMuted));
                selectedModules.Controls.Add(UiFactory.Pill("GİZLİLİK", oneClickSelection[OneClickPrivacy] ? AppTheme.Cyan : AppTheme.TextMuted));
                selectedModules.Controls.Add(UiFactory.Pill("DOSYALAR", oneClickSelection[OneClickCleanup] ? AppTheme.Green : AppTheme.TextMuted));
                selectedModules.Controls.Add(UiFactory.Pill("AĞ", oneClickSelection[OneClickNetwork] ? AppTheme.Cyan : AppTheme.TextMuted));
                selectedModules.Controls.Add(UiFactory.Pill("ONARIM", oneClickSelection[OneClickRepair] ? AppTheme.Amber : AppTheme.TextMuted));
                modules.Controls.Add(selectedModules);
                Button changeSelection = UiFactory.Button("Baştan tara", AppTheme.SurfaceRaised, AppTheme.TextMuted);
                changeSelection.Location = new Point(830, 78);
                changeSelection.Click += delegate
                {
                    oneClickReport = null;
                    oneClickLastResult = null;
                    oneClickActionSelection = null;
                    Navigate("scanner", null);
                };
                modules.Controls.Add(changeSelection);
            }

            if (oneClickReport != null && oneClickLastResult == null)
            {
                BuildOneClickReview(flow);
            }
            else
            {
                BuildOneClickResults(flow);
            }
        }

        private void BuildOneClickBusyContent(Control hero, FlowLayoutPanel flow)
        {
            oneClickActivityDetail = UiFactory.Label(
                "Hazırlanıyor…",
                new Font("Segoe UI", 8.5f),
                AppTheme.TextMuted);
            oneClickActivityDetail.Location = new Point(35, 231);
            oneClickActivityDetail.Size = new Size(575, 38);
            oneClickActivityDetail.AutoSize = false;
            oneClickActivityDetail.AutoEllipsis = true;
            hero.Controls.Add(oneClickActivityDetail);

            oneClickActivityStats = UiFactory.Label(
                "İşlem bilgileri hazırlanıyor",
                new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                AppTheme.Cyan);
            oneClickActivityStats.Location = new Point(35, 274);
            oneClickActivityStats.Size = new Size(575, 24);
            oneClickActivityStats.AutoSize = false;
            hero.Controls.Add(oneClickActivityStats);

            oneClickStageLabels = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
            string[] keys = { "settings", "privacy", "cleanup", "network", "repair" };
            string[] names = { "SİSTEM", "GİZLİLİK", "DOSYALAR", "AĞ", "ONARIM" };
            FlowLayoutPanel stages = new FlowLayoutPanel();
            stages.Location = new Point(34, 324);
            stages.Size = new Size(610, 38);
            stages.WrapContents = false;
            stages.BackColor = Color.Transparent;
            for (int i = 0; i < keys.Length; i++)
            {
                Label stage = UiFactory.Pill(names[i], AppTheme.TextMuted);
                stage.Margin = new Padding(0, 0, 8, 0);
                oneClickStageLabels[keys[i]] = stage;
                stages.Controls.Add(stage);
            }
            hero.Controls.Add(stages);

            SmoothPanel activity = new SmoothPanel();
            activity.Width = 1010;
            activity.Height = 190;
            activity.Margin = new Padding(0, 0, 0, 24);
            activity.BackColor = AppTheme.Surface;
            flow.Controls.Add(activity);
            Label activityTitle = UiFactory.Label("Canlı işlem akışı", AppTheme.Subheading, AppTheme.Text);
            activityTitle.Location = new Point(24, 18);
            activity.Controls.Add(activityTitle);
            Label activityHint = UiFactory.Label(
                "Tercan yalnızca seçtiğiniz güvenli alanlarda çalışıyor.",
                AppTheme.Small,
                AppTheme.TextMuted);
            activityHint.Location = new Point(25, 48);
            activity.Controls.Add(activityHint);

            oneClickActivityLog = new ModernScrollFlowPanel();
            oneClickActivityLog.Location = new Point(24, 78);
            oneClickActivityLog.Size = new Size(960, 88);
            oneClickActivityLog.FlowDirection = FlowDirection.TopDown;
            oneClickActivityLog.WrapContents = false;
            oneClickActivityLog.AutoScroll = true;
            oneClickActivityLog.BackColor = AppTheme.SurfaceRaised;
            activity.Controls.Add(oneClickActivityLog);
            AddOneClickActivityLine("İşlem başlatıldı", AppTheme.Cyan);
        }

        private void AddOneClickActivityLine(string text, Color color)
        {
            if (oneClickActivityLog == null || oneClickActivityLog.IsDisposed || string.IsNullOrWhiteSpace(text)) return;
            Label line = UiFactory.Label(
                DateTime.Now.ToString("HH:mm:ss") + "   " + text,
                new Font("Segoe UI", 8.3f),
                color);
            line.AutoSize = false;
            line.Size = new Size(914, 22);
            line.Margin = new Padding(12, 7, 0, 0);
            line.AutoEllipsis = true;
            oneClickActivityLog.Controls.Add(line);
            while (oneClickActivityLog.Controls.Count > 12)
            {
                Control first = oneClickActivityLog.Controls[0];
                oneClickActivityLog.Controls.RemoveAt(0);
                first.Dispose();
            }
            oneClickActivityLog.ScrollControlIntoView(line);
        }

        private List<OneClickReviewItem> BuildOneClickReviewItems()
        {
            List<OneClickReviewItem> items = new List<OneClickReviewItem>();
            if (oneClickReport == null) return items;

            if (oneClickSelection[OneClickPerformance])
            {
                foreach (TweakDefinition tweak in OneClickTweaks(OneClickPerformance).Where(x => !engine.IsApplied(x)))
                {
                    items.Add(new OneClickReviewItem
                    {
                        Id = "tweak:" + tweak.Id,
                        Group = "PERFORMANS",
                        Title = tweak.Title,
                        Detail = "Güvenli sistem ve oyun ayarı",
                        Accent = AppTheme.Accent
                    });
                }
            }

            if (oneClickSelection[OneClickPrivacy])
            {
                foreach (TweakDefinition tweak in OneClickTweaks(OneClickPrivacy).Where(x => !engine.IsApplied(x)))
                {
                    items.Add(new OneClickReviewItem
                    {
                        Id = "tweak:" + tweak.Id,
                        Group = "GİZLİLİK",
                        Title = tweak.Title,
                        Detail = "İzleme ve öneri ayarı",
                        Accent = AppTheme.Cyan
                    });
                }
            }

            if (oneClickSelection[OneClickCleanup])
            {
                foreach (CleanupScan scan in oneClickReport.CleanupScans.Where(x => x.FileCount > 0))
                {
                    items.Add(new OneClickReviewItem
                    {
                        Id = "cleanup:" + scan.Target.Id,
                        Group = "TEMİZLİK",
                        Title = scan.Target.Name,
                        Detail = scan.FileCount + " dosya • " + SafeCleanupEngine.FormatBytes(scan.Bytes),
                        FileCount = scan.FileCount,
                        Bytes = scan.Bytes,
                        Accent = AppTheme.Green
                    });
                }
            }

            if (oneClickSelection[OneClickNetwork])
            {
                items.Add(new OneClickReviewItem
                {
                    Id = "network:flushdns",
                    Group = "AĞ",
                    Title = "DNS önbelleğini yenile",
                    Detail = "Eski DNS kayıtlarını güvenle temizler",
                    Accent = AppTheme.Cyan
                });
            }

            if (oneClickSelection[OneClickRepair] && oneClickReport.RepairBlocks > 0)
            {
                TweakDefinition repair = tweaks.FirstOrDefault(x => x.Id == "repair.common_tools");
                if (repair != null)
                {
                    items.Add(new OneClickReviewItem
                    {
                        Id = "tweak:" + repair.Id,
                        Group = "ONARIM",
                        Title = "Windows araçlarına erişimi onar",
                        Detail = oneClickReport.RepairBlocks + " erişim engeli bulundu",
                        Accent = AppTheme.Amber
                    });
                }
            }
            return items;
        }

        private void EnsureOneClickActionSelection(List<OneClickReviewItem> items)
        {
            if (oneClickActionSelection != null) return;
            oneClickActionSelection = new HashSet<string>(
                items.Select(x => x.Id),
                StringComparer.OrdinalIgnoreCase);
        }

        private void BuildOneClickReview(FlowLayoutPanel flow)
        {
            List<OneClickReviewItem> items = BuildOneClickReviewItems();
            EnsureOneClickActionSelection(items);

            SmoothPanel review = new SmoothPanel();
            review.Width = 1010;
            review.Height = 570;
            review.Margin = new Padding(0, 0, 0, 24);
            review.BackColor = AppTheme.Surface;
            flow.Controls.Add(review);

            Label title = UiFactory.Label("Uygulanacak işlemleri seçin", AppTheme.Subheading, AppTheme.Text);
            title.Location = new Point(24, 18);
            review.Controls.Add(title);
            Label hint = UiFactory.Label(
                "İstemediğiniz düzeltmeyi kapatın. Tercan yalnızca açık bıraktıklarınızı uygular.",
                AppTheme.Small,
                AppTheme.TextMuted);
            hint.Location = new Point(25, 50);
            review.Controls.Add(hint);

            Button selectAll = UiFactory.Button("Tümünü seç", AppTheme.SurfaceRaised, AppTheme.Cyan);
            selectAll.Location = new Point(782, 20);
            selectAll.Width = 92;
            review.Controls.Add(selectAll);
            Button selectNone = UiFactory.Button("Seçimi kaldır", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            selectNone.Location = new Point(882, 20);
            selectNone.Width = 104;
            review.Controls.Add(selectNone);

            SmoothPanel listPanel = new SmoothPanel();
            listPanel.Location = new Point(24, 82);
            listPanel.Size = new Size(620, 462);
            listPanel.BackColor = AppTheme.SurfaceRaised;
            listPanel.BorderColor = Color.FromArgb(90, AppTheme.Accent);
            review.Controls.Add(listPanel);

            Label listTitle = UiFactory.Label("BULUNANLAR", new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), AppTheme.Cyan);
            listTitle.Location = new Point(18, 16);
            listPanel.Controls.Add(listTitle);

            FlowLayoutPanel itemFlow = new ModernScrollFlowPanel();
            itemFlow.Location = new Point(12, 47);
            itemFlow.Size = new Size(596, 402);
            itemFlow.FlowDirection = FlowDirection.TopDown;
            itemFlow.WrapContents = false;
            itemFlow.AutoScroll = true;
            itemFlow.BackColor = Color.Transparent;
            listPanel.Controls.Add(itemFlow);

            SmoothPanel summary = new SmoothPanel();
            summary.Location = new Point(660, 82);
            summary.Size = new Size(326, 462);
            summary.BackColor = Color.FromArgb(14, 16, 27);
            summary.BorderColor = Color.FromArgb(100, AppTheme.Cyan);
            review.Controls.Add(summary);
            Label summaryEyebrow = UiFactory.Label("SEÇİM ÖZETİ", new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold), AppTheme.Accent);
            summaryEyebrow.Location = new Point(22, 22);
            summary.Controls.Add(summaryEyebrow);
            Label selectedCount = UiFactory.Label("0 işlem", new Font("Segoe UI Semibold", 25f, FontStyle.Bold), AppTheme.Text);
            selectedCount.Location = new Point(20, 60);
            summary.Controls.Add(selectedCount);
            Label selectedDetail = UiFactory.Label("", AppTheme.Body, AppTheme.TextMuted);
            selectedDetail.Location = new Point(23, 116);
            selectedDetail.Size = new Size(280, 78);
            selectedDetail.AutoSize = false;
            summary.Controls.Add(selectedDetail);
            Panel summaryLine = new Panel();
            summaryLine.Location = new Point(22, 211);
            summaryLine.Size = new Size(280, 1);
            summaryLine.BackColor = AppTheme.Border;
            summary.Controls.Add(summaryLine);
            Label safety = UiFactory.Label(
                "✓ Seçimler uygulanmadan önce yedeklenir\n\n✓ Kullanımdaki dosyalar atlanır\n\n✓ Yeniden başlatma yalnızca gerekiyorsa sorulur",
                AppTheme.Small,
                AppTheme.TextMuted);
            safety.Location = new Point(23, 238);
            safety.Size = new Size(278, 114);
            safety.AutoSize = false;
            summary.Controls.Add(safety);
            Label actionHint = UiFactory.Label(
                "Hazırsanız üstteki OPTİMİZE ET düğmesine basın.",
                new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                AppTheme.Cyan);
            actionHint.Location = new Point(23, 382);
            actionHint.Size = new Size(278, 48);
            actionHint.AutoSize = false;
            summary.Controls.Add(actionHint);

            Action updateSummary = delegate
            {
                List<OneClickReviewItem> selected = items
                    .Where(x => oneClickActionSelection.Contains(x.Id))
                    .ToList();
                selectedCount.Text = selected.Count + " işlem";
                selectedDetail.Text =
                    selected.Sum(x => x.FileCount) + " dosya temizlenecek\n" +
                    SafeCleanupEngine.FormatBytes(selected.Sum(x => x.Bytes)) + " alan açılabilir\n" +
                    selected.Count(x => x.Id.StartsWith("tweak:", StringComparison.OrdinalIgnoreCase)) + " Windows ayarı";
                selectedCount.ForeColor = selected.Count > 0 ? AppTheme.Text : AppTheme.TextMuted;
                actionHint.Text = selected.Count > 0
                    ? "Hazırsanız üstteki OPTİMİZE ET düğmesine basın."
                    : "Devam etmek için en az bir işlem seçin.";
                actionHint.ForeColor = selected.Count > 0 ? AppTheme.Cyan : AppTheme.Amber;
            };

            if (items.Count == 0)
            {
                Label empty = UiFactory.Label(
                    "✓ Seçtiğiniz bölümlerde uygulanması gereken bir işlem bulunamadı.",
                    AppTheme.Body,
                    AppTheme.Green);
                empty.Size = new Size(540, 50);
                empty.Margin = new Padding(16, 20, 0, 0);
                itemFlow.Controls.Add(empty);
            }

            foreach (OneClickReviewItem item in items)
            {
                PremiumCard row = new PremiumCard();
                row.Size = new Size(560, 64);
                row.Margin = new Padding(8, 0, 8, 8);
                row.BackColor = Color.FromArgb(20, 23, 35);
                row.BorderColor = oneClickActionSelection.Contains(item.Id)
                    ? Color.FromArgb(125, item.Accent)
                    : AppTheme.Border;
                row.AccentColor = item.Accent;
                row.Cursor = Cursors.Hand;
                itemFlow.Controls.Add(row);

                Panel marker = new Panel();
                marker.Location = new Point(0, 0);
                marker.Size = new Size(4, 64);
                marker.BackColor = item.Accent;
                row.Controls.Add(marker);
                Label group = UiFactory.Label(item.Group, new Font("Segoe UI Semibold", 7.4f, FontStyle.Bold), item.Accent);
                group.Location = new Point(17, 8);
                row.Controls.Add(group);
                Label rowTitle = UiFactory.Label(item.Title, new Font("Segoe UI Semibold", 9f, FontStyle.Bold), AppTheme.Text);
                rowTitle.Location = new Point(17, 25);
                rowTitle.MaximumSize = new Size(430, 20);
                row.Controls.Add(rowTitle);
                Label detail = UiFactory.Label(item.Detail, new Font("Segoe UI", 7.6f), AppTheme.TextMuted);
                detail.Location = new Point(17, 45);
                detail.MaximumSize = new Size(450, 18);
                row.Controls.Add(detail);

                ToggleSwitch toggle = new ToggleSwitch();
                toggle.Location = new Point(500, 20);
                toggle.Checked = oneClickActionSelection.Contains(item.Id);
                row.Controls.Add(toggle);

                EventHandler toggleRow = delegate
                {
                    toggle.Checked = !toggle.Checked;
                };
                row.Click += toggleRow;
                group.Click += toggleRow;
                rowTitle.Click += toggleRow;
                detail.Click += toggleRow;
                toggle.CheckedChanged += delegate
                {
                    if (toggle.Checked) oneClickActionSelection.Add(item.Id);
                    else oneClickActionSelection.Remove(item.Id);
                    row.BorderColor = toggle.Checked ? Color.FromArgb(125, item.Accent) : AppTheme.Border;
                    row.Invalidate();
                    updateSummary();
                };
            }

            selectAll.Click += delegate
            {
                oneClickActionSelection = new HashSet<string>(items.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
                Navigate("scanner", null);
            };
            selectNone.Click += delegate
            {
                oneClickActionSelection.Clear();
                Navigate("scanner", null);
            };
            updateSummary();
        }

        private void EnsureOneClickSelection()
        {
            if (oneClickSelection != null) return;
            oneClickSelection = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                { OneClickPerformance, true },
                { OneClickPrivacy, true },
                { OneClickCleanup, true },
                { OneClickNetwork, true },
                { OneClickRepair, true }
            };
        }

        private void AddOneClickModuleCard(
            Control parent,
            int left,
            int top,
            string key,
            string title,
            string description,
            string glyph,
            Color accent)
        {
            PremiumCard card = new PremiumCard();
            card.Location = new Point(left, top);
            card.Size = new Size(304, 70);
            card.BackColor = AppTheme.SurfaceRaised;
            card.BorderColor = oneClickSelection[key] ? Color.FromArgb(130, accent) : AppTheme.Border;
            card.AccentColor = accent;
            parent.Controls.Add(card);

            Label icon = UiFactory.Label(glyph, new Font("Segoe UI Symbol", 16f, FontStyle.Bold), accent);
            icon.Location = new Point(16, 20);
            card.Controls.Add(icon);
            Label heading = UiFactory.Label(title, new Font("Segoe UI Semibold", 9f, FontStyle.Bold), AppTheme.Text);
            heading.Location = new Point(52, 13);
            card.Controls.Add(heading);
            Label detail = UiFactory.Label(description, new Font("Segoe UI", 7.7f), AppTheme.TextMuted);
            detail.Location = new Point(53, 37);
            detail.MaximumSize = new Size(192, 28);
            card.Controls.Add(detail);

            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Location = new Point(246, 21);
            toggle.Checked = oneClickSelection[key];
            card.Controls.Add(toggle);
            toggle.CheckedChanged += delegate
            {
                if (oneClickUiUpdating) return;
                oneClickSelection[key] = toggle.Checked;
                oneClickReport = null;
                oneClickLastResult = null;
                oneClickActionSelection = null;
                Navigate("scanner", null);
            };
        }

        private string CurrentOneClickStatus()
        {
            if (oneClickBusy) return "İşlem sürüyor… Pencereyi kapatmayın.";
            if (oneClickLastResult != null)
            {
                if (oneClickLastResult.Errors.Count > 0)
                {
                    return "⚠ Bitti; bazı adımlar uygulanamadı.";
                }
                return oneClickLastResult.RestartRequired
                    ? "✓ Bitti. Seçtiğiniz bir ayar yeniden başlatma gerektiriyor."
                    : "✓ Bitti. Yeniden başlatma gerekmiyor.";
            }
            if (oneClickReport != null)
            {
                int total = oneClickReport.PerformanceIssues + oneClickReport.PrivacyIssues +
                            oneClickReport.RepairBlocks + oneClickReport.CleanupFiles;
                return total == 0
                    ? "✓ Sistem hazır görünüyor."
                    : "Tarama bitti • İstemediğiniz işlemleri kapatıp OPTİMİZE ET'e basın.";
            }
            return "Hazır • TARA düğmesine basın.";
        }

        private void BuildOneClickResults(FlowLayoutPanel flow)
        {
            if (oneClickReport == null) return;

            SmoothPanel results = new SmoothPanel();
            results.Width = 1010;
            results.Margin = new Padding(0, 0, 0, 24);
            results.BackColor = AppTheme.Surface;
            flow.Controls.Add(results);

            results.Height = 526;
            Label resultsTitle = UiFactory.Label(
                oneClickLastResult == null ? "Tarama sonucu" : "Uygulama sonucu",
                AppTheme.Subheading,
                AppTheme.Text);
            resultsTitle.Location = new Point(24, 18);
            results.Controls.Add(resultsTitle);
            Label scanned = UiFactory.Label(
                "Son tarama: " + oneClickReport.ScannedAt.ToString("HH:mm:ss") +
                " • Hazırlık puanı: " + oneClickReport.ReadinessScore,
                AppTheme.Small,
                AppTheme.TextMuted);
            scanned.Location = new Point(25, 50);
            results.Controls.Add(scanned);

            AddOneClickResultRow(
                results,
                80,
                "Performans ayarları",
                oneClickLastResult == null
                    ? oneClickReport.PerformanceIssues + " güvenli ayar önerisi"
                    : oneClickLastResult.AppliedTweaks + " ayar uygulandı",
                OneClickPerformance,
                AppTheme.Accent);
            AddOneClickResultRow(
                results,
                144,
                "Gizlilik ve öneriler",
                oneClickLastResult == null
                    ? oneClickReport.PrivacyIssues + " gizlilik ayarı önerisi"
                    : "Seçili gizlilik ayarları yedekli biçimde işlendi",
                OneClickPrivacy,
                AppTheme.Cyan);

            Panel cleanupDetail = new Panel();
            cleanupDetail.Location = new Point(24, 208);
            cleanupDetail.Size = new Size(960, 132);
            cleanupDetail.BackColor = AppTheme.SurfaceRaised;
            results.Controls.Add(cleanupDetail);
            Panel cleanupAccent = new Panel();
            cleanupAccent.Dock = DockStyle.Left;
            cleanupAccent.Width = 5;
            cleanupAccent.BackColor = AppTheme.Green;
            cleanupDetail.Controls.Add(cleanupAccent);
            Label cleanupTitle = UiFactory.Label("Gereksiz dosya ayrıntısı", new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AppTheme.Text);
            cleanupTitle.Location = new Point(18, 12);
            cleanupDetail.Controls.Add(cleanupTitle);
            Label cleanupTotal = UiFactory.Label(
                oneClickLastResult == null
                    ? oneClickReport.CleanupFiles + " dosya • " + SafeCleanupEngine.FormatBytes(oneClickReport.CleanupBytes)
                    : oneClickLastResult.DeletedFiles + " dosya silindi • " + SafeCleanupEngine.FormatBytes(oneClickLastResult.ReleasedBytes) + " alan açıldı",
                AppTheme.Body,
                oneClickSelection[OneClickCleanup] ? AppTheme.Green : AppTheme.TextMuted);
            cleanupTotal.Location = new Point(690, 13);
            cleanupDetail.Controls.Add(cleanupTotal);
            int itemIndex = 0;
            foreach (CleanupScan scan in oneClickReport.CleanupScans.Take(6))
            {
                int column = itemIndex % 2;
                int row = itemIndex / 2;
                Label item = UiFactory.Label(
                    "• " + scan.Target.Name + "  " + SafeCleanupEngine.FormatBytes(scan.Bytes),
                    AppTheme.Small,
                    AppTheme.TextMuted);
                item.Location = new Point(20 + column * 465, 48 + row * 25);
                cleanupDetail.Controls.Add(item);
                itemIndex++;
            }

            AddOneClickResultRow(
                results,
                354,
                "Ağ yenileme",
                oneClickLastResult == null
                    ? "DNS önbelleği ve güvenli ağ ayarı hazır"
                    : (oneClickLastResult.NetworkRefreshed ? "DNS önbelleği yenilendi" : "Ağ modülü çalıştırılmadı"),
                OneClickNetwork,
                Color.FromArgb(112, 151, 255));
            AddOneClickResultRow(
                results,
                418,
                "Sistem araçları onarımı",
                oneClickLastResult == null
                    ? oneClickReport.RepairBlocks + " erişim engeli bulundu"
                    : oneClickLastResult.RepairedPolicies + " erişim engeli onarıldı",
                OneClickRepair,
                AppTheme.Amber);

            if (oneClickLastResult != null)
            {
                Label restart = UiFactory.Label(
                    oneClickLastResult.RestartRequired
                        ? "İŞLEMLER BİTTİ • Seçtiğiniz bir Windows ayarını etkinleştirmek için yeniden başlatma gerekiyor."
                        : "İŞLEMLER BİTTİ • Yeniden başlatma gerekmiyor.",
                    new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
                    oneClickLastResult.RestartRequired ? AppTheme.Amber : AppTheme.Green);
                restart.Location = new Point(25, 492);
                results.Controls.Add(restart);
            }
        }

        private void AddOneClickResultRow(
            Control parent,
            int top,
            string title,
            string detail,
            string selectionKey,
            Color accent)
        {
            Panel row = new Panel();
            row.Location = new Point(24, top);
            row.Size = new Size(960, 52);
            row.BackColor = AppTheme.SurfaceRaised;
            parent.Controls.Add(row);
            Panel marker = new Panel();
            marker.Dock = DockStyle.Left;
            marker.Width = 5;
            marker.BackColor = oneClickSelection[selectionKey] ? accent : Color.FromArgb(70, 76, 90);
            row.Controls.Add(marker);
            Label titleLabel = UiFactory.Label(title, new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold), AppTheme.Text);
            titleLabel.Location = new Point(18, 8);
            row.Controls.Add(titleLabel);
            Label detailLabel = UiFactory.Label(detail, AppTheme.Small, AppTheme.TextMuted);
            detailLabel.Location = new Point(18, 29);
            row.Controls.Add(detailLabel);
            Label state = UiFactory.Pill(
                oneClickSelection[selectionKey] ? "SEÇİLİ" : "ATLANDI",
                oneClickSelection[selectionKey] ? accent : AppTheme.TextMuted);
            state.Location = new Point(860, 13);
            row.Controls.Add(state);
        }

        private void BeginOneClickScan()
        {
            if (oneClickBusy || !oneClickSelection.Any(x => x.Value))
            {
                if (!oneClickSelection.Any(x => x.Value))
                {
                    MessageBox.Show("Taramak için en az bir bakım modülü seçin.", "tercan.exe");
                }
                return;
            }

            oneClickBusy = true;
            oneClickReport = null;
            oneClickLastResult = null;
            oneClickActionSelection = null;
            SetNavigationEnabled(false);
            Navigate("scanner", null);
            oneClickOrb.State = OptimizerOrbState.Scanning;
            oneClickOrb.Progress = 0;
            oneClickProgress.Visible = true;
            oneClickProgress.Value = 0;
            oneClickStatusLabel.Text = "Windows ayarları okunuyor…";

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker background = (BackgroundWorker)sender;
                OneClickScanReport report = new OneClickScanReport();
                report.ScannedAt = DateTime.Now;
                background.ReportProgress(10, new OneClickProgressUpdate
                {
                    StageKey = "settings",
                    Status = "Performans ve Windows ayarları inceleniyor…",
                    Detail = "Güvenli sistem ayarları mevcut durumla karşılaştırılıyor."
                });

                SystemScanReport systemReport = SystemScanEngine.Scan(tweaks, engine, systemInfo);
                report.ReadinessScore = systemReport.ReadinessScore;
                report.PerformanceIssues = OneClickTweaks(OneClickPerformance).Count(x => !engine.IsApplied(x));
                background.ReportProgress(32, new OneClickProgressUpdate
                {
                    StageKey = "privacy",
                    Status = "Gizlilik ayarları inceleniyor…",
                    Detail = "İzleme, öneri ve arka plan tercihleri denetleniyor."
                });
                report.PrivacyIssues = OneClickTweaks(OneClickPrivacy).Count(x => !engine.IsApplied(x));

                background.ReportProgress(48, new OneClickProgressUpdate
                {
                    StageKey = "cleanup",
                    Status = "Güvenli geçici dosya konumları ölçülüyor…",
                    Detail = "Kullanımdaki dosyalara dokunulmadan yalnızca boyut hesaplanıyor."
                });
                if (oneClickSelection[OneClickCleanup])
                {
                    List<CleanupTarget> targets = SafeCleanupEngine.BuildCatalog()
                        .Where(x => x.Recommended)
                        .ToList();
                    for (int i = 0; i < targets.Count; i++)
                    {
                        report.CleanupScans.Add(SafeCleanupEngine.Scan(targets[i]));
                        background.ReportProgress(
                            48 + (int)((i + 1) * 28d / Math.Max(1, targets.Count)),
                            new OneClickProgressUpdate
                            {
                                StageKey = "cleanup",
                                Status = targets[i].Name + " tarandı…",
                                Detail = report.CleanupScans.Last().FileCount + " dosya • " +
                                         SafeCleanupEngine.FormatBytes(report.CleanupScans.Last().Bytes)
                            });
                    }
                }

                background.ReportProgress(84, new OneClickProgressUpdate
                {
                    StageKey = "repair",
                    Status = "Windows araç erişimleri denetleniyor…",
                    Detail = "Görev Yöneticisi, Komut İstemi ve sistem araçları kontrol ediliyor."
                });
                report.RepairBlocks = CountRepairPolicyBlocks();
                background.ReportProgress(100, new OneClickProgressUpdate
                {
                    StageKey = "repair",
                    Status = "Tarama tamamlandı.",
                    Detail = "Sonuçlar seçim ekranına hazırlanıyor."
                });
                e.Result = report;
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                UpdateOneClickProgress(e.ProgressPercentage, e.UserState);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                oneClickBusy = false;
                SetNavigationEnabled(true);
                if (e.Error != null)
                {
                    Logger.Error("Tek tık tarama tamamlanamadı", e.Error);
                    MessageBox.Show(e.Error.Message, "Tarama tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Navigate("scanner", null);
                    return;
                }
                oneClickReport = (OneClickScanReport)e.Result;
                oneClickActionSelection = null;
                Navigate("scanner", null);
            };
            worker.RunWorkerAsync();
        }

        private void BeginOneClickOptimization()
        {
            if (oneClickBusy || oneClickReport == null) return;
            List<OneClickReviewItem> reviewItems = BuildOneClickReviewItems();
            EnsureOneClickActionSelection(reviewItems);
            if (!reviewItems.Any(x => oneClickActionSelection.Contains(x.Id)))
            {
                MessageBox.Show(
                    "Devam etmek için en az bir işlem seçin.",
                    "tercan.exe",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            oneClickBusy = true;
            SetNavigationEnabled(false);
            Navigate("scanner", null);
            oneClickOrb.State = OptimizerOrbState.Optimizing;
            oneClickOrb.Progress = 0;
            oneClickProgress.Visible = true;
            oneClickProgress.Value = 0;
            oneClickStatusLabel.Text = "Geri alma yedeği hazırlanıyor…";

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                BackgroundWorker background = (BackgroundWorker)sender;
                OneClickExecutionResult result = new OneClickExecutionResult();
                HashSet<string> selectedActionIds = new HashSet<string>(
                    oneClickActionSelection,
                    StringComparer.OrdinalIgnoreCase);
                List<TweakDefinition> selectedTweaks = tweaks
                    .Where(x => selectedActionIds.Contains("tweak:" + x.Id))
                    .Where(x => !engine.IsApplied(x))
                    .ToList();
                List<CleanupScan> selectedCleanupScans = oneClickReport.CleanupScans
                    .Where(x => selectedActionIds.Contains("cleanup:" + x.Target.Id))
                    .ToList();
                bool refreshNetwork = selectedActionIds.Contains("network:flushdns");

                if (selectedTweaks.Count > 0)
                {
                    try
                    {
                        background.ReportProgress(5, new OneClickProgressUpdate
                        {
                            StageKey = "settings",
                            Status = "Geri alma yedeği hazırlanıyor…",
                            Detail = "Seçtiğiniz Windows ayarları güvenli biçimde yedekleniyor."
                        });
                        ProcessResult restore = RestorePointTools.Create("Tercan tek tık bakım");
                        if (restore.ExitCode != 0)
                        {
                            Logger.Info("Tek tık bakım geri yükleme noktası oluşturulamadı; kayıt bazlı yedekler devam ediyor.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Tek tık bakım geri yükleme noktası oluşturulamadı", ex);
                    }
                }

                int totalSteps = Math.Max(1, selectedTweaks.Count +
                    selectedCleanupScans.Count +
                    (refreshNetwork ? 1 : 0));
                int completed = 0;
                foreach (TweakDefinition tweak in selectedTweaks)
                {
                    try
                    {
                        engine.Apply(tweak);
                        result.AppliedTweaks++;
                        if (tweak.Id == "repair.common_tools")
                        {
                            result.RepairedPolicies = oneClickReport.RepairBlocks;
                        }
                        result.RestartRequired = result.RestartRequired || tweak.RequiresRestart;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add(tweak.Title + ": " + ex.Message);
                        Logger.Error("Tek tık ayarı uygulanamadı: " + tweak.Title, ex);
                    }
                    completed++;
                    background.ReportProgress(
                        10 + (int)(completed * 72d / totalSteps),
                        new OneClickProgressUpdate
                        {
                            StageKey = tweak.Category == "Gizlilik"
                                ? "privacy"
                                : tweak.Id == "repair.common_tools" ? "repair" : "settings",
                            Status = tweak.Title + " işlendi…",
                            Detail = "Seçtiğiniz güvenli Windows ayarı uygulandı."
                        });
                }

                if (selectedCleanupScans.Count > 0)
                {
                    try
                    {
                        Stopwatch updateThrottle = Stopwatch.StartNew();
                        CleanupResult cleanup = SafeCleanupEngine.Clean(
                            selectedCleanupScans,
                            delegate(CleanupProgress live)
                            {
                                if (updateThrottle.ElapsedMilliseconds < 70 &&
                                    live.ProcessedFiles < live.TotalFiles)
                                {
                                    return;
                                }
                                updateThrottle.Restart();
                                double cleanupFraction = live.ProcessedFiles / (double)Math.Max(1, live.TotalFiles);
                                int liveProgress = 10 + (int)((completed + cleanupFraction * selectedCleanupScans.Count) * 72d / totalSteps);
                                background.ReportProgress(
                                    Math.Max(10, Math.Min(88, liveProgress)),
                                    new OneClickProgressUpdate
                                    {
                                        StageKey = "cleanup",
                                        Status = live.CategoryName + " temizleniyor…",
                                        Detail = CompactOneClickPath(live.FilePath),
                                        DeletedFiles = live.DeletedFiles,
                                        SkippedFiles = live.SkippedFiles,
                                        ReleasedBytes = live.ReleasedBytes
                                    });
                            });
                        result.DeletedFiles = cleanup.DeletedFiles;
                        result.SkippedFiles = cleanup.SkippedFiles;
                        result.ReleasedBytes = cleanup.ReleasedBytes;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add("Gereksiz dosyalar: " + ex.Message);
                        Logger.Error("Tek tık temizliği tamamlanamadı", ex);
                    }
                    completed += selectedCleanupScans.Count;
                    background.ReportProgress(
                        10 + (int)(completed * 72d / totalSteps),
                        new OneClickProgressUpdate
                        {
                            StageKey = "cleanup",
                            Status = "Gereksiz dosya temizliği tamamlandı.",
                            Detail = result.DeletedFiles + " dosya silindi • " +
                                     SafeCleanupEngine.FormatBytes(result.ReleasedBytes) + " alan açıldı",
                            DeletedFiles = result.DeletedFiles,
                            SkippedFiles = result.SkippedFiles,
                            ReleasedBytes = result.ReleasedBytes
                        });
                }

                if (refreshNetwork)
                {
                    background.ReportProgress(90, new OneClickProgressUpdate
                    {
                        StageKey = "network",
                        Status = "DNS önbelleği yenileniyor…",
                        Detail = "Eski DNS kayıtları temizleniyor."
                    });
                    try
                    {
                        ProcessResult flush = ProcessRunner.Run("ipconfig.exe", "/flushdns", 15000);
                        result.NetworkRefreshed = flush.ExitCode == 0;
                        if (!result.NetworkRefreshed)
                        {
                            result.Errors.Add("DNS önbelleği yenilenemedi.");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add("Ağ yenileme: " + ex.Message);
                    }
                    completed++;
                }

                background.ReportProgress(100, new OneClickProgressUpdate
                {
                    StageKey = refreshNetwork ? "network" : selectedCleanupScans.Count > 0 ? "cleanup" : "settings",
                    Status = "Seçilen bakım işlemleri tamamlandı.",
                    Detail = result.DeletedFiles + " dosya • " + result.AppliedTweaks + " ayar işlendi",
                    DeletedFiles = result.DeletedFiles,
                    SkippedFiles = result.SkippedFiles,
                    ReleasedBytes = result.ReleasedBytes
                });
                e.Result = result;
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                UpdateOneClickProgress(e.ProgressPercentage, e.UserState);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                oneClickBusy = false;
                SetNavigationEnabled(true);
                if (e.Error != null)
                {
                    Logger.Error("Tek tık optimizasyon tamamlanamadı", e.Error);
                    MessageBox.Show(e.Error.Message, "Optimizasyon tamamlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Navigate("scanner", null);
                    return;
                }
                oneClickLastResult = (OneClickExecutionResult)e.Result;
                lastSystemScan = null;
                Navigate("scanner", null);
                if (oneClickLastResult.RestartRequired)
                {
                    ShowRestartRecommendationDialog(
                        oneClickLastResult.AppliedTweaks + " ayar ve " +
                        oneClickLastResult.DeletedFiles + " dosya işlendi.",
                        oneClickLastResult.Errors.Count > 0);
                }
            };
            worker.RunWorkerAsync();
        }

        private void UpdateOneClickProgress(int progress, object userState)
        {
            OneClickProgressUpdate update = userState as OneClickProgressUpdate;
            string status = update != null ? update.Status : Convert.ToString(userState);
            if (oneClickOrb != null && !oneClickOrb.IsDisposed)
            {
                oneClickOrb.Progress = progress;
            }
            if (oneClickProgress != null && !oneClickProgress.IsDisposed)
            {
                oneClickProgress.Value = Math.Max(0, Math.Min(100, progress));
            }
            if (oneClickStatusLabel != null && !oneClickStatusLabel.IsDisposed)
            {
                oneClickStatusLabel.Text = status;
            }
            if (update == null) return;

            if (oneClickActivityDetail != null && !oneClickActivityDetail.IsDisposed)
            {
                oneClickActivityDetail.Text = string.IsNullOrWhiteSpace(update.Detail)
                    ? "İşlem sürüyor…"
                    : update.Detail;
            }
            if (oneClickActivityStats != null && !oneClickActivityStats.IsDisposed)
            {
                bool hasCleanupStats = update.DeletedFiles > 0 ||
                                       update.SkippedFiles > 0 ||
                                       update.ReleasedBytes > 0;
                oneClickActivityStats.Text = hasCleanupStats
                    ? update.DeletedFiles + " dosya temizlendi • " +
                      SafeCleanupEngine.FormatBytes(update.ReleasedBytes) + " alan açıldı • " +
                      update.SkippedFiles + " dosya atlandı"
                    : progress + "% tamamlandı";
            }
            UpdateOneClickStage(update.StageKey);
            if (!string.IsNullOrWhiteSpace(update.Detail))
            {
                AddOneClickActivityLine(
                    update.Status + "  " + update.Detail,
                    update.StageKey == "cleanup" ? AppTheme.Green : AppTheme.TextMuted);
            }
        }

        private void UpdateOneClickStage(string activeStage)
        {
            if (oneClickStageLabels == null || string.IsNullOrWhiteSpace(activeStage)) return;
            string[] order = { "settings", "privacy", "cleanup", "network", "repair" };
            int activeIndex = Array.IndexOf(order, activeStage);
            for (int i = 0; i < order.Length; i++)
            {
                Label label;
                if (!oneClickStageLabels.TryGetValue(order[i], out label) || label.IsDisposed) continue;
                label.ForeColor = i < activeIndex
                    ? AppTheme.Green
                    : i == activeIndex ? AppTheme.Cyan : AppTheme.TextMuted;
                label.BackColor = i == activeIndex ? AppTheme.AccentSoft : AppTheme.SurfaceRaised;
            }
        }

        private static string CompactOneClickPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string compact = path;
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(user) &&
                compact.StartsWith(user, StringComparison.OrdinalIgnoreCase))
            {
                compact = "%USERPROFILE%" + compact.Substring(user.Length);
            }
            if (compact.Length <= 88) return compact;
            string fileName = Path.GetFileName(compact);
            return compact.Substring(0, 48) + "…\\" + fileName;
        }

        private List<TweakDefinition> OneClickTweaks(string group)
        {
            IEnumerable<TweakDefinition> selected = tweaks.Where(x =>
                x.Recommended &&
                x.Risk == RiskLevel.Safe &&
                !x.Id.StartsWith("custom.", StringComparison.OrdinalIgnoreCase) &&
                x.Id != "repair.common_tools");

            if (group == OneClickPrivacy)
            {
                return selected.Where(x =>
                    x.Category == "Gizlilik").ToList();
            }

            return selected.Where(x =>
                x.Category == "Oyun" ||
                x.Category == "Görünüm" ||
                x.Category == "Arka Plan" ||
                x.Category == "Ağ" ||
                x.Category == "Sistem" ||
                x.Category == "Windows 11").ToList();
        }

        private int CountRepairPolicyBlocks()
        {
            try
            {
                TweakDefinition repair = tweaks.FirstOrDefault(x => x.Id == "repair.common_tools");
                if (repair == null) return 0;
                return repair.RegistryChanges.Count(RegistryTools.Exists);
            }
            catch
            {
                return 0;
            }
        }

        private void SetNavigationEnabled(bool enabled)
        {
            foreach (Button button in navigationButtons)
            {
                button.Enabled = enabled;
            }
            if (applyButton != null) applyButton.Enabled = enabled;
        }

        internal void PrepareOneClickPreview()
        {
            EnsureOneClickSelection();
            OneClickScanReport report = new OneClickScanReport
            {
                ScannedAt = DateTime.Now,
                ReadinessScore = 72,
                PerformanceIssues = 7,
                PrivacyIssues = 3,
                RepairBlocks = 2
            };
            int index = 0;
            foreach (CleanupTarget target in SafeCleanupEngine.BuildCatalog().Where(x => x.Recommended))
            {
                report.CleanupScans.Add(new CleanupScan
                {
                    Target = target,
                    FileCount = 70 + index * 41,
                    Bytes = (24L + index * 83L) * 1024L * 1024L
                });
                index++;
            }
            oneClickReport = report;
            oneClickLastResult = null;
            oneClickActionSelection = null;
        }

        internal void PrepareOneClickBusyPreview()
        {
            PrepareOneClickPreview();
            oneClickBusy = true;
        }

        internal void FillOneClickBusyPreview()
        {
            UpdateOneClickProgress(54, new OneClickProgressUpdate
            {
                StageKey = "cleanup",
                Status = "DirectX gölgelendirici önbelleği temizleniyor…",
                Detail = @"%USERPROFILE%\AppData\Local\D3DSCache\7d942f0c.bin",
                DeletedFiles = 899,
                SkippedFiles = 2,
                ReleasedBytes = 1572864000L
            });
        }

        private void ShowOptimizerSettingsPage()
        {
            pageTitle.Text = "Hızlandırma";
            pageDescription.Text = "Bilgisayarınızı yavaşlatan alanları tek merkezden yönetin.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            int startupCount = SafeStartupOptimizationCount();
            int backgroundCount = SafeRunningBoostProcessCount();
            bool attentionNeeded = startupCount > 0 || backgroundCount > 0;

            TercanHeroPanel hero = new TercanHeroPanel();
            hero.Width = 1010;
            hero.Height = 146;
            hero.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(hero);
            Label eyebrow = UiFactory.Label(
                "TERCAN HIZLANDIRMA MERKEZİ",
                new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                AppTheme.Cyan);
            eyebrow.Location = new Point(28, 22);
            hero.Controls.Add(eyebrow);
            Label title = UiFactory.Label(
                attentionNeeded ? "PC'niz daha yüksek verimliliğe hazır." : "PC'niz dengeli görünüyor.",
                new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                AppTheme.Text);
            title.Location = new Point(26, 51);
            hero.Controls.Add(title);
            Label summary = UiFactory.Label(
                startupCount + " etkin başlangıç öğesi • " +
                backgroundCount + " yönetilebilir arka plan süreci • " +
                (memoryCleanerActive ? "RAM izleyici açık" : "RAM izleyici kapalı"),
                AppTheme.Body,
                attentionNeeded ? AppTheme.Amber : AppTheme.Green);
            summary.Location = new Point(29, 91);
            summary.Size = new Size(720, 30);
            summary.AutoSize = false;
            hero.Controls.Add(summary);
            PictureBox heroLogo = BrandAssets.CreateLogoBox(82, 82);
            heroLogo.Location = new Point(698, 28);
            hero.Controls.Add(heroLogo);
            Label safety = UiFactory.Pill("GÜVENLİ + GERİ ALINABİLİR", AppTheme.Green);
            safety.Location = new Point(792, 29);
            hero.Controls.Add(safety);
            Label system = UiFactory.Label("Windows 10 / 11", AppTheme.Small, AppTheme.TextMuted);
            system.Location = new Point(845, 72);
            hero.Controls.Add(system);

            FlowLayoutPanel cards = new FlowLayoutPanel();
            cards.Width = 1010;
            cards.Height = 316;
            cards.WrapContents = true;
            cards.Margin = new Padding(0, 0, 0, 16);
            cards.BackColor = Color.Transparent;
            flow.Controls.Add(cards);

            cards.Controls.Add(CreateGameModeAccelerationCard());

            cards.Controls.Add(CreateAccelerationCard(
                "➤",
                "Başlangıç Eniyileyici",
                startupCount == 0 ? "TEMİZ" : startupCount + " ETKİN ÖĞE",
                startupCount == 0
                    ? "Tercan'ın okuyabildiği başlangıç girdilerinde gereksiz yük görünmüyor."
                    : "Windows ile açılan uygulamaları yedekli biçimde kapatın veya geri getirin.",
                "Eniyile",
                startupCount == 0 ? AppTheme.Green : AppTheme.Amber,
                delegate { Navigate("startup", null); }));

            cards.Controls.Add(CreateAccelerationCard(
                "◷",
                "Gerçek Zamanlı İnce Ayar",
                memoryCleanerActive ? "RAM İZLEYİCİ AÇIK" : "RAM İZLEYİCİ KAPALI",
                "Boş bellek azalıp standby önbelleği yükseldiğinde eşik tabanlı bakım yapar; normal önbelleğe dokunmaz.",
                memoryCleanerActive ? "Ayarlar" : "Kur",
                memoryCleanerActive ? AppTheme.Green : AppTheme.Cyan,
                delegate { Navigate("memory", null); }));

            cards.Controls.Add(CreateAccelerationCard(
                "♙",
                "Uygulama Temizleyici",
                "KORUMALI KALDIRMA",
                "İsteğe bağlı Windows uygulamalarını tek tek seçin; güvenlik ve sistem bileşenleri korunur.",
                "Yönet",
                AppTheme.Accent,
                delegate { Navigate("apps", null); }));

            if (focusEngine.IsActive)
            {
                FocusModeSession activeSession = focusEngine.Session;
                SmoothPanel sessionPanel = new SmoothPanel();
                sessionPanel.Width = 1010;
                sessionPanel.Height = 112;
                sessionPanel.Margin = new Padding(0, 0, 0, 16);
                sessionPanel.BackColor = Color.FromArgb(17, 27, 31);
                sessionPanel.BorderColor = Color.FromArgb(95, AppTheme.Green);
                flow.Controls.Add(sessionPanel);

                Label sessionTitle = UiFactory.Label(
                    "OYUN KİPİ OTURUMU • YAPILANLAR",
                    new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                    AppTheme.Green);
                sessionTitle.Location = new Point(22, 16);
                sessionPanel.Controls.Add(sessionTitle);

                string serviceNames = activeSession == null || activeSession.StoppedServices == null ||
                    activeSession.StoppedServices.Count == 0
                    ? "Bu bilgisayarda durdurulması gereken güvenli servis bulunmadı."
                    : string.Join(
                        "  •  ",
                        activeSession.StoppedServices.Select(x => x.DisplayName));
                Label serviceList = UiFactory.Label(serviceNames, AppTheme.Small, AppTheme.Text);
                serviceList.Location = new Point(22, 43);
                serviceList.Size = new Size(965, 34);
                serviceList.AutoSize = false;
                sessionPanel.Controls.Add(serviceList);

                Label protectedNote = UiFactory.Label(
                    "Kritik sistem, ağ, ses, güvenlik ve güncelleme servisleri korundu. Kapatınca tüm geçici değişiklikler geri alınır.",
                    AppTheme.Small,
                    AppTheme.TextMuted);
                protectedNote.Location = new Point(22, 82);
                sessionPanel.Controls.Add(protectedNote);
            }

            SmoothPanel quick = new SmoothPanel();
            quick.Width = 1010;
            quick.Height = 146;
            quick.Margin = new Padding(0, 0, 0, 16);
            quick.BackColor = AppTheme.Surface;
            quick.BorderColor = Color.FromArgb(78, AppTheme.Accent);
            flow.Controls.Add(quick);
            Label quickTitle = UiFactory.Label("Hızlı güvenli profiller", AppTheme.Subheading, AppTheme.Text);
            quickTitle.Location = new Point(22, 18);
            quick.Controls.Add(quickTitle);
            Label quickCopy = UiFactory.Label(
                "Profil yalnızca önerilen güvenli ayarları seçer. Değişiklikler alttaki Uygula düğmesine basılmadan yapılmaz.",
                AppTheme.Small,
                AppTheme.TextMuted);
            quickCopy.Location = new Point(23, 49);
            quick.Controls.Add(quickCopy);
            Button recommended = UiFactory.Button("Önerilen profil", AppTheme.Accent, Color.White);
            recommended.Location = new Point(22, 91);
            recommended.Click += delegate { StageCompactProfile(false); };
            quick.Controls.Add(recommended);
            Button gaming = UiFactory.Button("Oyun profili", AppTheme.Green, Color.FromArgb(7, 28, 20));
            gaming.Location = new Point(168, 91);
            gaming.Click += delegate { StageCompactProfile(true); };
            quick.Controls.Add(gaming);
            Button clear = UiFactory.Button("Seçimi temizle", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            clear.Location = new Point(290, 91);
            clear.Click += delegate
            {
                pending.Clear();
                UpdateApplyBar();
                Navigate("optimizer-settings", null);
            };
            quick.Controls.Add(clear);
            Button details = UiFactory.Button(
                optimizerDetailsVisible ? "Ayrıntıları gizle" : "Ayrıntılı ayarlar",
                AppTheme.SurfaceRaised,
                optimizerDetailsVisible ? AppTheme.Amber : AppTheme.Cyan);
            details.Location = new Point(830, 91);
            details.Width = 154;
            details.Click += delegate
            {
                optimizerDetailsVisible = !optimizerDetailsVisible;
                Navigate("optimizer-settings", null);
            };
            quick.Controls.Add(details);

            if (optimizerDetailsVisible)
            {
                BuildOptimizerDetailSection(flow);
            }
        }

        private SmoothPanel CreateAccelerationCard(
            string glyph,
            string title,
            string status,
            string description,
            string actionText,
            Color accent,
            EventHandler action)
        {
            PremiumCard card = new PremiumCard();
            card.Size = new Size(495, 146);
            card.Margin = new Padding(0, 0, 10, 12);
            card.BackColor = AppTheme.Surface;
            card.BorderColor = Color.FromArgb(72, accent);
            card.AccentColor = accent;

            Label icon = UiFactory.Label(glyph, new Font("Segoe UI Symbol", 24f, FontStyle.Regular), accent);
            icon.Location = new Point(20, 27);
            icon.Size = new Size(42, 46);
            icon.AutoSize = false;
            icon.TextAlign = ContentAlignment.MiddleCenter;
            card.Controls.Add(icon);
            Label heading = UiFactory.Label(title, new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold), AppTheme.Text);
            heading.Location = new Point(78, 18);
            card.Controls.Add(heading);
            Label state = UiFactory.Label(status, new Font("Segoe UI Semibold", 8f, FontStyle.Bold), accent);
            state.Location = new Point(78, 44);
            card.Controls.Add(state);
            Label copy = UiFactory.Label(description, new Font("Segoe UI", 8.2f), AppTheme.TextMuted);
            copy.Location = new Point(78, 68);
            copy.Size = new Size(385, 42);
            copy.AutoSize = false;
            card.Controls.Add(copy);
            Button open = UiFactory.Button(actionText, AppTheme.SurfaceRaised, accent);
            open.Location = new Point(349, 108);
            open.Size = new Size(124, 30);
            open.Click += action;
            card.Controls.Add(open);
            return card;
        }

        private SmoothPanel CreateGameModeAccelerationCard()
        {
            Color accent = focusEngine.IsActive ? AppTheme.Green : AppTheme.Amber;
            PremiumCard card = new PremiumCard();
            card.Size = new Size(495, 146);
            card.Margin = new Padding(0, 0, 10, 12);
            card.BackColor = AppTheme.Surface;
            card.BorderColor = Color.FromArgb(78, accent);
            card.AccentColor = accent;

            GameBoostGauge gauge = new GameBoostGauge();
            gauge.Location = new Point(12, 25);
            gauge.Active = focusEngine.IsActive;
            card.Controls.Add(gauge);

            Label heading = UiFactory.Label(
                "Oyun Kipi",
                new Font("Segoe UI Semibold", 10.2f, FontStyle.Bold),
                AppTheme.Text);
            heading.Location = new Point(87, 18);
            card.Controls.Add(heading);

            string statusText;
            if (quickGameModeBusy)
            {
                statusText = focusEngine.IsActive ? "GERİ ALINIYOR…" : "OYUN İÇİN HAZIRLANIYOR…";
                gauge.BeginTransition(!focusEngine.IsActive);
            }
            else if (focusEngine.IsActive)
            {
                statusText = "AÇIK • " + lastFocusStoppedServiceCount + " SERVİS • " +
                    lastFocusClosedCount + " SÜREÇ";
            }
            else
            {
                statusText = "KAPALI";
            }
            Label state = UiFactory.Label(
                statusText,
                new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                quickGameModeBusy ? AppTheme.Cyan : accent);
            state.Location = new Point(87, 44);
            state.Size = new Size(370, 20);
            state.AutoSize = false;
            card.Controls.Add(state);

            string description = focusEngine.IsActive
                ? lastFocusStoppedServiceCount + " güvenli servis geçici durdu • " +
                  lastFocusAppliedSettingCount + " oyun ayarı değişti • yaklaşık " +
                  (lastFocusReleasedBytes / 1024d / 1024d).ToString("0") + " MB uygulama belleği bırakıldı."
                : "Gereksiz servisleri ve güvenli arka plan uygulamalarını geçici durdurur; Oyun Modu, kayıt ve güç ayarlarını düzenler.";
            Label copy = UiFactory.Label(description, new Font("Segoe UI", 8.2f), AppTheme.TextMuted);
            copy.Location = new Point(87, 68);
            copy.Size = new Size(374, 42);
            copy.AutoSize = false;
            card.Controls.Add(copy);

            Button action = UiFactory.Button(
                focusEngine.IsActive ? "Kapat ve geri al" : "Tek tıkla aç",
                AppTheme.SurfaceRaised,
                accent);
            action.Location = new Point(337, 108);
            action.Size = new Size(136, 30);
            action.Enabled = !quickGameModeBusy;
            action.Click += delegate { ToggleQuickGameMode(gauge, action, state, copy); };
            card.Controls.Add(action);
            return card;
        }

        private int SafeStartupOptimizationCount()
        {
            try
            {
                return StartupManager.ReadAll().Count(x => x.Enabled && !x.Protected);
            }
            catch
            {
                return 0;
            }
        }

        private int SafeRunningBoostProcessCount()
        {
            int total = 0;
            foreach (FocusProcessDefinition definition in focusDefinitions)
            {
                try { total += FocusProcessCatalog.RunningCount(definition); }
                catch { }
            }
            return total;
        }

        private void ToggleQuickGameMode(
            GameBoostGauge gauge,
            Button actionButton,
            Label stateLabel,
            Label descriptionLabel)
        {
            if (quickGameModeBusy) return;
            bool turningOn = !focusEngine.IsActive;
            quickGameModeBusy = true;
            actionButton.Enabled = false;
            stateLabel.Text = turningOn ? "OYUN İÇİN HAZIRLANIYOR…" : "GERİ ALINIYOR…";
            stateLabel.ForeColor = AppTheme.Cyan;
            descriptionLabel.Text = turningOn
                ? "Servisler ve arka plan uygulamaları denetleniyor; oyun ayarları güvenli biçimde uygulanıyor."
                : "Durdurulan servisler, güç planı, kayıt ayarları ve uygulamalar eski durumuna getiriliyor.";
            gauge.BeginTransition(turningOn);

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                if (!turningOn)
                {
                    e.Result = focusEngine.Deactivate(true);
                    return;
                }

                List<FocusProcessDefinition> safeRunning = new List<FocusProcessDefinition>();
                foreach (FocusProcessDefinition definition in focusDefinitions.Where(x => x.SafeDefault))
                {
                    try
                    {
                        if (FocusProcessCatalog.RunningCount(definition) > 0)
                        {
                            safeRunning.Add(definition);
                        }
                    }
                    catch
                    {
                    }
                }

                e.Result = focusEngine.Activate(
                    safeRunning,
                    0,
                    true,
                    false,
                    true);
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                quickGameModeBusy = false;
                if (e.Error != null)
                {
                    if (gauge != null && !gauge.IsDisposed)
                    {
                        gauge.CompleteTransition(focusEngine.IsActive);
                    }
                    MessageBox.Show(
                        e.Error.Message,
                        "Oyun Kipi değiştirilemedi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    RefreshCurrentPage();
                    return;
                }

                FocusModeResult result = e.Result as FocusModeResult ?? new FocusModeResult();
                if (turningOn)
                {
                    lastFocusClosedCount = result.ClosedProcessCount;
                    lastFocusReleasedBytes = result.ReleasedBytes;
                    lastFocusStoppedServiceCount = result.StoppedServiceCount;
                    lastFocusAppliedSettingCount = result.AppliedGameSettingCount;
                }
                else
                {
                    lastFocusClosedCount = 0;
                    lastFocusReleasedBytes = 0;
                    lastFocusStoppedServiceCount = 0;
                    lastFocusAppliedSettingCount = 0;
                }

                if (gauge != null && !gauge.IsDisposed)
                {
                    gauge.CompleteTransition(focusEngine.IsActive);
                }
                if (result.Messages.Count > 0)
                {
                    MessageBox.Show(
                        (turningOn
                            ? "Oyun Kipi açıldı; bazı adımlar uygulanamadı:\n\n"
                            : "Oyun Kipi kapatıldı; bazı öğeler geri getirilemedi:\n\n") +
                        string.Join("\n", result.Messages),
                        "Oyun Kipi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                RefreshCurrentPage();
            };
            worker.RunWorkerAsync();
        }

        private void OpenOptionalDriverUpdates()
        {
            try
            {
                ProcessRunner.Open("ms-settings:windowsupdate-optionalupdates");
            }
            catch
            {
                ProcessRunner.Open("ms-settings:windowsupdate");
            }
        }

        private void BuildOptimizerDetailSection(FlowLayoutPanel flow)
        {
            SmoothPanel protectedPanel = new SmoothPanel();
            protectedPanel.Width = 1010;
            protectedPanel.Height = 86;
            protectedPanel.Margin = new Padding(0, 0, 0, 16);
            protectedPanel.BackColor = Color.FromArgb(22, 21, 31);
            protectedPanel.BorderColor = AppTheme.Amber;
            flow.Controls.Add(protectedPanel);
            Label protectedText = UiFactory.Label(
                "KORUNANLAR  •  Defender, Firewall, Windows Update, Sistem Geri Yükleme ve SMB2",
                new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                AppTheme.Amber);
            protectedText.Location = new Point(22, 18);
            protectedPanel.Controls.Add(protectedText);
            Label protectedDetail = UiFactory.Label(
                "Güvenlik ve kurtarma özellikleri kapatılmaz.",
                AppTheme.Small,
                AppTheme.TextMuted);
            protectedDetail.Location = new Point(23, 49);
            protectedPanel.Controls.Add(protectedDetail);

            TableLayoutPanel groups = new TableLayoutPanel();
            groups.Width = 1010;
            groups.Height = 970;
            groups.Margin = new Padding(0, 0, 0, 24);
            groups.ColumnCount = 2;
            groups.RowCount = 2;
            groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            groups.RowStyles.Add(new RowStyle(SizeType.Absolute, 560f));
            groups.RowStyles.Add(new RowStyle(SizeType.Absolute, 400f));
            groups.BackColor = Color.Transparent;
            flow.Controls.Add(groups);

            groups.Controls.Add(BuildCompactTweakGroup(
                "Sistem ve performans",
                new[]
                {
                    "gaming.game_mode", "gaming.disable_capture", "gaming.high_performance_power",
                    "visual.effects", "system.long_paths", "system.error_reporting", "custom.menu-delay"
                },
                486,
                540), 0, 0);
            groups.Controls.Add(BuildCompactTweakGroup(
                "Windows 11",
                new[]
                {
                    "visual.widgets", "visual.search", "windows11.taskbar_chat", "windows11.copilot",
                    "windows11.compact_explorer", "windows11.classic_context", "windows11.edge_sidebar",
                    "windows11.cloud_clipboard"
                },
                486,
                540), 1, 0);
            groups.Controls.Add(BuildCompactTweakGroup(
                "Gizlilik ve arka plan",
                new[]
                {
                    "background.suggestions", "background.activity_history", "background.advertising_id",
                    "privacy.tailored_experiences", "background.telemetry_service"
                },
                486,
                382), 0, 1);
            groups.Controls.Add(BuildCompactTweakGroup(
                "Windows araçlarını onar",
                new[] { "repair.common_tools" },
                486,
                382), 1, 1);
        }

        private void ShowOptimizerSettingsPageLegacy()
        {
            pageTitle.Text = "Optimizasyon";
            pageDescription.Text = "Ayarları seçin; Tercan yedekleyip uygulasın.";

            FlowLayoutPanel flow = NewPageFlow();
            content.Controls.Add(flow);

            TercanHeroPanel hero = new TercanHeroPanel();
            hero.Width = 1010;
            hero.Height = 166;
            hero.Margin = new Padding(0, 0, 0, 16);
            flow.Controls.Add(hero);
            Label title = UiFactory.Label("Ayarları seç", new Font("Segoe UI Semibold", 21f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(26, 22);
            hero.Controls.Add(title);
            Label copy = UiFactory.Label(
                "Anahtarları açın veya hazır profil seçin. Onay vermeden hiçbir şey uygulanmaz.",
                AppTheme.Body,
                AppTheme.TextMuted);
            copy.Location = new Point(28, 61);
            hero.Controls.Add(copy);

            Button recommended = UiFactory.Button("Önerilen güvenli profil", AppTheme.Accent, Color.White);
            recommended.Location = new Point(28, 106);
            recommended.Click += delegate { StageCompactProfile(false); };
            hero.Controls.Add(recommended);
            Button gaming = UiFactory.Button("Oyun profili", AppTheme.Green, Color.FromArgb(7, 28, 20));
            gaming.Location = new Point(228, 106);
            gaming.Click += delegate { StageCompactProfile(true); };
            hero.Controls.Add(gaming);
            Button clear = UiFactory.Button("Seçimi temizle", AppTheme.SurfaceRaised, AppTheme.TextMuted);
            clear.Location = new Point(348, 106);
            clear.Click += delegate
            {
                pending.Clear();
                UpdateApplyBar();
                Navigate("optimizer-settings", null);
            };
            hero.Controls.Add(clear);

            SmoothPanel protectedPanel = new SmoothPanel();
            protectedPanel.Width = 1010;
            protectedPanel.Height = 86;
            protectedPanel.Margin = new Padding(0, 0, 0, 16);
            protectedPanel.BackColor = Color.FromArgb(31, 27, 24);
            protectedPanel.BorderColor = AppTheme.Amber;
            flow.Controls.Add(protectedPanel);
            Label protectedText = UiFactory.Label(
                "KORUNANLAR  •  Defender, Firewall, Windows Update, Sistem Geri Yükleme ve SMB2",
                new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
                AppTheme.Amber);
            protectedText.Location = new Point(22, 18);
            protectedPanel.Controls.Add(protectedText);
            Label protectedDetail = UiFactory.Label(
                "Güvenlik ve kurtarma özellikleri kapatılmaz.",
                AppTheme.Small,
                AppTheme.TextMuted);
            protectedDetail.Location = new Point(23, 49);
            protectedPanel.Controls.Add(protectedDetail);

            TableLayoutPanel groups = new TableLayoutPanel();
            groups.Width = 1010;
            groups.Height = 970;
            groups.Margin = new Padding(0, 0, 0, 24);
            groups.ColumnCount = 2;
            groups.RowCount = 2;
            groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            groups.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            groups.RowStyles.Add(new RowStyle(SizeType.Absolute, 560f));
            groups.RowStyles.Add(new RowStyle(SizeType.Absolute, 400f));
            groups.BackColor = Color.Transparent;
            flow.Controls.Add(groups);

            groups.Controls.Add(BuildCompactTweakGroup(
                "Sistem ve performans",
                new[]
                {
                    "gaming.game_mode", "gaming.disable_capture", "gaming.high_performance_power",
                    "visual.effects", "system.long_paths", "system.error_reporting", "custom.menu-delay"
                },
                486,
                540), 0, 0);
            groups.Controls.Add(BuildCompactTweakGroup(
                "Windows 11",
                new[]
                {
                    "visual.widgets", "visual.search", "windows11.taskbar_chat", "windows11.copilot",
                    "windows11.compact_explorer", "windows11.classic_context", "windows11.edge_sidebar",
                    "windows11.cloud_clipboard"
                },
                486,
                540), 1, 0);
            groups.Controls.Add(BuildCompactTweakGroup(
                "Gizlilik ve arka plan",
                new[]
                {
                    "background.suggestions", "background.activity_history", "background.advertising_id",
                    "privacy.tailored_experiences", "background.telemetry_service"
                },
                486,
                382), 0, 1);
            groups.Controls.Add(BuildCompactTweakGroup(
                "Windows araçlarını onar",
                new[] { "repair.common_tools" },
                486,
                382), 1, 1);
        }

        private SmoothPanel BuildCompactTweakGroup(string title, string[] ids, int width, int height)
        {
            SmoothPanel panel = new SmoothPanel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(0, 0, 12, 12);
            panel.BackColor = AppTheme.Surface;
            Label heading = UiFactory.Label(title, AppTheme.Subheading, AppTheme.Text);
            heading.Location = new Point(20, 18);
            panel.Controls.Add(heading);
            int top = 58;
            foreach (string id in ids)
            {
                TweakDefinition tweak = tweaks.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
                if (tweak == null) continue;
                AddCompactTweakRow(panel, tweak, top);
                top += 58;
            }
            return panel;
        }

        private void AddCompactTweakRow(Control parent, TweakDefinition tweak, int top)
        {
            Panel row = new Panel();
            row.Location = new Point(18, top);
            row.Size = new Size(448, 50);
            row.BackColor = AppTheme.SurfaceRaised;
            parent.Controls.Add(row);
            Label title = UiFactory.Label(tweak.Title, new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold), AppTheme.Text);
            title.Location = new Point(13, 7);
            title.MaximumSize = new Size(340, 19);
            row.Controls.Add(title);
            Label detail = UiFactory.Label(
                UiFactory.RiskText(tweak.Risk) + " • " + UiFactory.ImpactText(tweak.Impact),
                new Font("Segoe UI", 7.4f),
                UiFactory.RiskColor(tweak.Risk));
            detail.Location = new Point(14, 29);
            row.Controls.Add(detail);

            bool applied = engine.IsApplied(tweak);
            bool desired;
            bool shown = pending.TryGetValue(tweak.Id, out desired) ? desired : applied;
            ToggleSwitch toggle = new ToggleSwitch();
            toggle.Location = new Point(382, 12);
            toggle.Checked = shown;
            row.Controls.Add(toggle);
            toggle.CheckedChanged += delegate
            {
                TweakRequestedChanged(tweak, toggle.Checked);
            };
        }

        private void StageCompactProfile(bool gamingOnly)
        {
            IEnumerable<TweakDefinition> profile = tweaks.Where(x =>
                x.Risk == RiskLevel.Safe &&
                x.Recommended &&
                !x.Id.StartsWith("custom.", StringComparison.OrdinalIgnoreCase) &&
                x.Id != "repair.common_tools");
            if (gamingOnly)
            {
                profile = profile.Where(x =>
                    x.Category == "Oyun" ||
                    x.Category == "Görünüm" ||
                    x.Category == "Ağ" ||
                    x.Category == "Sistem" ||
                    x.Category == "Windows 11");
            }
            foreach (TweakDefinition tweak in profile)
            {
                if (!engine.IsApplied(tweak)) pending[tweak.Id] = true;
            }
            UpdateApplyBar();
            Navigate("optimizer-settings", null);
        }

        private void ShowRestartRecommendationDialog(string summary, bool partial)
        {
            using (Form dialog = new Form())
            {
                dialog.Text = "Tercan • İşlem tamamlandı";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ClientSize = new Size(560, 310);
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.BackColor = AppTheme.Window;
                dialog.ForeColor = AppTheme.Text;
                dialog.Font = AppTheme.Body;
                dialog.Icon = Icon;

                Label badge = UiFactory.Pill(partial ? "KISMEN TAMAMLANDI" : "BAŞARIYLA TAMAMLANDI", partial ? AppTheme.Amber : AppTheme.Green);
                badge.Location = new Point(28, 25);
                dialog.Controls.Add(badge);
                Label title = UiFactory.Label("Bilgisayarı yeniden başlatın", new Font("Segoe UI Semibold", 19f, FontStyle.Bold), AppTheme.Text);
                title.Location = new Point(27, 67);
                dialog.Controls.Add(title);
                Label detail = UiFactory.Label(
                    summary + "\n\nDeğişikliklerin tamamının etkinleşmesi ve Windows bileşenlerinin temiz başlaması için bilgisayarınızı yeniden başlatmanız önerilir.",
                    AppTheme.Body,
                    AppTheme.TextMuted);
                detail.Location = new Point(29, 112);
                detail.Size = new Size(500, 80);
                detail.AutoSize = false;
                dialog.Controls.Add(detail);
                Label warning = UiFactory.Label(
                    "Şimdi seçerseniz Windows 15 saniye sonra yeniden başlar. Açık belgelerinizi kaydedin.",
                    AppTheme.Small,
                    AppTheme.Amber);
                warning.Location = new Point(29, 205);
                dialog.Controls.Add(warning);

                Button later = UiFactory.Button("Daha sonra", AppTheme.SurfaceRaised, AppTheme.Text);
                later.Location = new Point(288, 248);
                later.Click += delegate { dialog.DialogResult = DialogResult.No; dialog.Close(); };
                dialog.Controls.Add(later);
                Button restart = UiFactory.Button("Şimdi yeniden başlat", AppTheme.Accent, Color.White);
                restart.Location = new Point(388, 248);
                restart.Click += delegate { dialog.DialogResult = DialogResult.Yes; dialog.Close(); };
                dialog.Controls.Add(restart);
                dialog.AcceptButton = restart;
                dialog.CancelButton = later;

                if (dialog.ShowDialog(this) == DialogResult.Yes)
                {
                    try
                    {
                        ProcessResult result = ProcessRunner.Run(
                            "shutdown.exe",
                            "/r /t 15 /c \"Tercan optimizasyon ayarlarını etkinleştiriyor.\"",
                            10000);
                        if (result.ExitCode != 0)
                        {
                            throw new InvalidOperationException((result.Error + " " + result.Output).Trim());
                        }
                        MessageBox.Show(
                            "Windows 15 saniye sonra yeniden başlayacak. Vazgeçerseniz Başlat > Çalıştır bölümüne shutdown /a yazabilirsiniz.",
                            "Yeniden başlatma planlandı",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Yeniden başlatma planlanamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }
    }
}

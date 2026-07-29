using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TercanOptimizer
{
    internal enum ScanSeverity
    {
        Info,
        Recommended,
        Important
    }

    internal sealed class SystemScanFinding
    {
        public string Category { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public string Recommendation { get; set; }
        public string TweakId { get; set; }
        public ScanSeverity Severity { get; set; }
        public int Weight { get; set; }
    }

    internal sealed class SystemScanReport
    {
        public DateTime ScannedAt { get; set; }
        public int ReadinessScore { get; set; }
        public int TargetScore { get; set; }
        public int EstimatedImpactScore { get; set; }
        public string EstimatedImpactLabel { get; set; }
        public string EstimatedImpactDescription { get; set; }
        public int EnabledStartupCount { get; set; }
        public int ActiveBackgroundGroups { get; set; }
        public long ActiveBackgroundBytes { get; set; }
        public long CleanupBytes { get; set; }
        public int CleanupFiles { get; set; }
        public long TotalMemoryMb { get; set; }
        public long AvailableMemoryMb { get; set; }
        public List<SystemScanFinding> Findings { get; set; }
        public List<string> RecommendedTweakIds { get; set; }

        public SystemScanReport()
        {
            Findings = new List<SystemScanFinding>();
            RecommendedTweakIds = new List<string>();
        }
    }

    internal static class SystemScanEngine
    {
        public static SystemScanReport Scan(
            IEnumerable<TweakDefinition> tweakDefinitions,
            TweakEngine tweakEngine,
            SystemInfoSnapshot systemInfo)
        {
            if (tweakDefinitions == null) throw new ArgumentNullException("tweakDefinitions");
            if (tweakEngine == null) throw new ArgumentNullException("tweakEngine");
            if (systemInfo == null) throw new ArgumentNullException("systemInfo");

            SystemScanReport report = new SystemScanReport();
            report.ScannedAt = DateTime.Now;
            int penalty = 0;
            int impact = 0;

            List<TweakDefinition> candidates = tweakDefinitions
                .Where(x => x.Recommended && x.Risk != RiskLevel.Experimental)
                .Where(x => !(systemInfo.IsLaptop &&
                              string.Equals(x.Id, "gaming.high_performance_power", StringComparison.OrdinalIgnoreCase)))
                .Where(x => !tweakEngine.IsApplied(x))
                .ToList();

            foreach (TweakDefinition tweak in candidates)
            {
                int weight = ImpactWeight(tweak.Impact);
                penalty += weight;
                impact += weight * (tweak.Impact == ImpactLevel.High ? 2 : 1);
                report.RecommendedTweakIds.Add(tweak.Id);
                report.Findings.Add(new SystemScanFinding
                {
                    Category = tweak.Category,
                    Title = tweak.Title,
                    Detail = tweak.Summary,
                    Recommendation = "Tercan bu ayarı inceleme listesine ekleyebilir. Değişiklik uygulanmadan önce ayrıntısı ve geri alma yolu gösterilir.",
                    TweakId = tweak.Id,
                    Severity = tweak.Impact == ImpactLevel.High ? ScanSeverity.Important : ScanSeverity.Recommended,
                    Weight = weight
                });
            }

            MemorySnapshot memory = SystemProbe.ReadMemory();
            report.TotalMemoryMb = memory.TotalMb;
            report.AvailableMemoryMb = memory.AvailableMb;
            if (memory.TotalMb > 0)
            {
                double availableRatio = memory.AvailableMb / (double)memory.TotalMb;
                if (availableRatio < 0.15d)
                {
                    penalty += 12;
                    impact += 18;
                    report.Findings.Add(new SystemScanFinding
                    {
                        Category = "Bellek",
                        Title = "Kullanılabilir bellek çok düşük",
                        Detail = "Toplam belleğin yalnızca %" + Math.Round(availableRatio * 100d) + " kadarı kullanılabilir durumda.",
                        Recommendation = "Oyun Odak Modu ile seçtiğiniz arka plan uygulamalarını kapatın; bellek temizlemeyi yalnızca eşik aşıldığında kullanın.",
                        Severity = ScanSeverity.Important,
                        Weight = 12
                    });
                }
                else if (availableRatio < 0.27d)
                {
                    penalty += 6;
                    impact += 9;
                    report.Findings.Add(new SystemScanFinding
                    {
                        Category = "Bellek",
                        Title = "Bellek yükü orta seviyede",
                        Detail = "Kullanılabilir fiziksel bellek %" + Math.Round(availableRatio * 100d) + " seviyesinde.",
                        Recommendation = "Oyundan önce tarayıcı, senkronizasyon ve medya uygulamalarını Oyun Odak Modu üzerinden gözden geçirin.",
                        Severity = ScanSeverity.Recommended,
                        Weight = 6
                    });
                }
            }

            ReadBackgroundLoad(report);
            if (report.ActiveBackgroundGroups >= 2 && report.ActiveBackgroundBytes >= 300L * 1024L * 1024L)
            {
                int backgroundPenalty = Math.Min(12, 4 + report.ActiveBackgroundGroups * 2);
                penalty += backgroundPenalty;
                impact += Math.Min(22, backgroundPenalty * 2);
                report.Findings.Add(new SystemScanFinding
                {
                    Category = "Oyun Odak Modu",
                    Title = report.ActiveBackgroundGroups + " kapatılabilir uygulama grubu çalışıyor",
                    Detail = "Tespit edilen kullanıcı uygulamaları yaklaşık " +
                             SafeCleanupEngine.FormatBytes(report.ActiveBackgroundBytes) + " fiziksel bellek kullanıyor.",
                    Recommendation = "Oyun sırasında ihtiyacınız olmayan uygulamaları Oyun Odak Modu listesinden seçin. Tercan sistem ve oyun başlatıcılarını korur.",
                    Severity = report.ActiveBackgroundBytes >= 1024L * 1024L * 1024L
                        ? ScanSeverity.Important
                        : ScanSeverity.Recommended,
                    Weight = backgroundPenalty
                });
            }

            try
            {
                List<StartupRecord> startup = StartupManager.ReadAll();
                report.EnabledStartupCount = startup.Count(x => x.Enabled && !x.Protected);
                if (report.EnabledStartupCount > 6)
                {
                    int startupPenalty = Math.Min(10, (report.EnabledStartupCount - 5) * 2);
                    penalty += startupPenalty;
                    impact += Math.Min(12, startupPenalty);
                    report.Findings.Add(new SystemScanFinding
                    {
                        Category = "Başlangıç",
                        Title = report.EnabledStartupCount + " kullanıcı başlangıç girdisi etkin",
                        Detail = "Çok sayıda otomatik başlayan uygulama açılış süresini ve oyun öncesi arka plan yükünü artırabilir.",
                        Recommendation = "Başlangıç Yöneticisi'ni açın ve yalnızca tanıdığınız, oyun sırasında gerekmeyen uygulamaları kapatın.",
                        Severity = ScanSeverity.Recommended,
                        Weight = startupPenalty
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Akıllı taramada başlangıç girdileri okunamadı", ex);
            }

            try
            {
                foreach (CleanupTarget target in SafeCleanupEngine.BuildCatalog().Where(x => x.Recommended))
                {
                    CleanupScan scan = SafeCleanupEngine.Scan(target);
                    report.CleanupBytes += scan.Bytes;
                    report.CleanupFiles += scan.FileCount;
                }
                if (report.CleanupBytes >= 500L * 1024L * 1024L)
                {
                    int cleanupPenalty = report.CleanupBytes >= 2L * 1024L * 1024L * 1024L ? 5 : 3;
                    penalty += cleanupPenalty;
                    report.Findings.Add(new SystemScanFinding
                    {
                        Category = "Temizlik",
                        Title = SafeCleanupEngine.FormatBytes(report.CleanupBytes) + " güvenli temizlik adayı",
                        Detail = report.CleanupFiles + " geçici dosya ve yeniden oluşturulabilir önbellek dosyası bulundu.",
                        Recommendation = "Temizlik Merkezi'nde sonuçları yeniden tarayın ve yalnızca istediğiniz kategorileri onaylayın. Disk temizliği doğrudan FPS artışı değildir.",
                        Severity = ScanSeverity.Info,
                        Weight = cleanupPenalty
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Akıllı taramada geçici dosyalar ölçülemedi", ex);
            }

            if (systemInfo.IsLaptop &&
                !string.Equals(systemInfo.PowerPlan, "Dengeli", StringComparison.CurrentCultureIgnoreCase))
            {
                report.Findings.Add(new SystemScanFinding
                {
                    Category = "Güç ve sıcaklık",
                    Title = "Dizüstü bilgisayarda sıcaklığı izleyin",
                    Detail = "Yüksek güç modu daha fazla ısı ve fan sesi oluşturabilir; ısıl sınıra ulaşılırsa performans düşebilir.",
                    Recommendation = "Yüksek performans profilini yalnızca adaptör takılıyken ve sıcaklıkları izleyerek kullanın.",
                    Severity = ScanSeverity.Info,
                    Weight = 0
                });
            }

            penalty = Math.Min(72, penalty);
            report.ReadinessScore = Math.Max(28, 100 - penalty);
            report.TargetScore = Math.Min(100, report.ReadinessScore + Math.Min(50, candidates.Sum(x => ImpactWeight(x.Impact))));
            report.EstimatedImpactScore = Math.Max(0, Math.Min(100, impact));
            ApplyImpactLabel(report);
            report.Findings = report.Findings
                .OrderByDescending(x => x.Severity)
                .ThenByDescending(x => x.Weight)
                .ThenBy(x => x.Category)
                .ToList();
            Logger.Info("Akıllı sistem taraması tamamlandı. Hazırlık=" + report.ReadinessScore +
                        ", TahminiEtki=" + report.EstimatedImpactScore +
                        ", Öneri=" + report.RecommendedTweakIds.Count);
            return report;
        }

        public static SystemScanReport CreatePreview()
        {
            SystemScanReport report = new SystemScanReport
            {
                ScannedAt = DateTime.Now,
                ReadinessScore = 61,
                TargetScore = 89,
                EstimatedImpactScore = 48,
                EstimatedImpactLabel = "ORTA",
                EstimatedImpactDescription = "Arka plan yükünü ve kare süresi dalgalanmasını azaltma potansiyeli var.",
                EnabledStartupCount = 8,
                ActiveBackgroundGroups = 4,
                ActiveBackgroundBytes = 1260L * 1024L * 1024L,
                CleanupBytes = 1840L * 1024L * 1024L,
                CleanupFiles = 3260,
                TotalMemoryMb = 16384,
                AvailableMemoryMb = 4280
            };
            report.RecommendedTweakIds.AddRange(new[]
            {
                "gaming.game_mode", "gaming.disable_capture", "network.delivery_optimization"
            });
            report.Findings.Add(new SystemScanFinding
            {
                Category = "Oyun",
                Title = "Windows Oyun Modu etkin değil",
                Detail = "Windows oyun çalışırken arka plan etkinliğini önceliklendirmiyor.",
                Recommendation = "Tercan bu ayarı inceleme listesine ekleyebilir.",
                TweakId = "gaming.game_mode",
                Severity = ScanSeverity.Important,
                Weight = 8
            });
            report.Findings.Add(new SystemScanFinding
            {
                Category = "Oyun Odak Modu",
                Title = "4 kapatılabilir uygulama grubu çalışıyor",
                Detail = "Tarayıcı ve eşitleme uygulamaları yaklaşık 1,2 GB bellek kullanıyor.",
                Recommendation = "Oyun sırasında gerekmediklerini seçerek geçici kapatın.",
                Severity = ScanSeverity.Recommended,
                Weight = 7
            });
            report.Findings.Add(new SystemScanFinding
            {
                Category = "Temizlik",
                Title = "1,8 GB güvenli temizlik adayı",
                Detail = "Geçici dosya ve yeniden oluşturulabilir önbellekler bulundu.",
                Recommendation = "Temizlik Merkezi'nde kategorileri inceleyin.",
                Severity = ScanSeverity.Info,
                Weight = 3
            });
            return report;
        }

        private static int ImpactWeight(ImpactLevel impact)
        {
            if (impact == ImpactLevel.High) return 12;
            if (impact == ImpactLevel.Medium) return 7;
            return 3;
        }

        private static void ReadBackgroundLoad(SystemScanReport report)
        {
            HashSet<int> counted = new HashSet<int>();
            foreach (FocusProcessDefinition definition in FocusProcessCatalog.Build())
            {
                bool groupActive = false;
                foreach (string processName in definition.ProcessNames)
                {
                    foreach (Process process in Process.GetProcessesByName(
                        System.IO.Path.GetFileNameWithoutExtension(processName)))
                    {
                        try
                        {
                            if (process.Id == Process.GetCurrentProcess().Id || !counted.Add(process.Id)) continue;
                            report.ActiveBackgroundBytes += Math.Max(0, process.WorkingSet64);
                            groupActive = true;
                        }
                        catch
                        {
                        }
                        finally
                        {
                            process.Dispose();
                        }
                    }
                }
                if (groupActive) report.ActiveBackgroundGroups++;
            }
        }

        private static void ApplyImpactLabel(SystemScanReport report)
        {
            if (report.EstimatedImpactScore >= 62)
            {
                report.EstimatedImpactLabel = "YÜKSEK";
                report.EstimatedImpactDescription = "Yoğun arka plan yükü veya eksik temel oyun ayarları bulundu. Kazanç sistem ve oyuna göre belirgin olabilir.";
            }
            else if (report.EstimatedImpactScore >= 28)
            {
                report.EstimatedImpactLabel = "ORTA";
                report.EstimatedImpactDescription = "Arka plan yükünü ve kare süresi dalgalanmasını azaltma potansiyeli var.";
            }
            else
            {
                report.EstimatedImpactLabel = "DÜŞÜK";
                report.EstimatedImpactDescription = "Sistem temel olarak hazır. Öneriler çoğunlukla küçük arka plan ve kullanım iyileştirmeleridir.";
            }
        }
    }
}

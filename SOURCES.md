# Araştırma ve kaynak notları

tercan.exe arayüzü ve kaynak kodu özgündür. Hellzerg Optimizer'ın özellik listesi ürün kapsamı için referans alınmış, GPL-3.0 lisanslı kaynak kodu kopyalanmamıştır.

## Ana referanslar

- Homarr Labs Dashboard Icons, uygulama logoları ve Apache-2.0 lisansı:  
  https://github.com/homarr-labs/dashboard-icons
- Simple Icons v16, marka SVG'leri ve CC0-1.0 lisansı:  
  https://github.com/simple-icons/simple-icons
- Microsoft PowerToys resmî uygulama simgesi:  
  https://github.com/microsoft/PowerToys
- WinRAR ve Everything resmî site simgeleri:  
  https://www.win-rar.com/  
  https://www.voidtools.com/
- Microsoft, Windows 11 ayar URI'leri ve Oyun Modu/Game DVR:  
  https://learn.microsoft.com/en-us/windows/apps/develop/settings/settings-windows-11
- Microsoft, Windows güç ve performans modları:  
  https://learn.microsoft.com/en-us/windows-hardware/customize/desktop/customize-power-slider
- Microsoft, MMAgent bellek sıkıştırması:  
  https://learn.microsoft.com/en-us/powershell/module/mmagent/enable-mmagent
- Microsoft, sistem geri yükleme noktası (`Checkpoint-Computer`):  
  https://learn.microsoft.com/tr-tr/powershell/module/microsoft.powershell.management/checkpoint-computer
- Microsoft, modern gelen kutusu/Store uygulamalarını kaldırma konusunda destek sınırları:  
  https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/modern-inbox-store-apps-troubleshooting-guidance
- Wagnardsoft, resmî ISLC v1.0.4.6 sayfası ve indirmeleri:  
  https://www.wagnardsoft.com/forums/viewtopic.php?t=1256
- Wagnardsoft, ISLC komut satırı seçenekleri:  
  https://www.wagnardsoft.com/forums/viewtopic.php?t=3918
- Hellzerg Optimizer GitHub deposu (modüler kullanım yaklaşımı için ürün referansı):  
  https://github.com/hellzerg/optimizer
- GoodbyeDPI-Turkey resmî GitHub deposu:  
  https://github.com/cagritaskn/GoodbyeDPI-Turkey
- GoodbyeDPI-Turkey v0.2.3rc3 resmî sürüm paketi:  
  https://github.com/cagritaskn/GoodbyeDPI-Turkey/releases/tag/release-0.2.3rc3-turkey
- GoodbyeDPI-Turkey Apache-2.0 lisansı:  
  https://github.com/cagritaskn/GoodbyeDPI-Turkey/blob/master/LICENSE
- GoodbyeDPI ana projesi ve WinDivert çalışma açıklaması:  
  https://github.com/ValdikSS/GoodbyeDPI
- GoodbyeDPI-Turkey deposunun önerdiği yeni SplitWire-Turkey projesi:  
  https://github.com/cagritaskn/SplitWire-Turkey
- Unlost, 2026 bilgisayar hızlandırma ve oyun FPS ayarları videosu:  
  https://www.youtube.com/watch?v=uMDPDyRnsvo
- Microsoft, WinGet ile uygulama kurma ve yönetme:  
  https://learn.microsoft.com/en-us/windows/package-manager/winget/
- Microsoft, WinGet `install` seçenekleri ve lisans kabulü:  
  https://learn.microsoft.com/en-us/windows/package-manager/winget/install
- Microsoft, WinGet topluluk paket manifest deposu:  
  https://github.com/microsoft/winget-pkgs
- Microsoft, App Installer/WinGet kurulumu:  
  https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/troubleshoot-apps-start-failure-use-windows-package-manager
- Razer Cortex, Game Booster ürün yaklaşımı:  
  https://www.razer.com/cortex
- IObit Advanced SystemCare 19, Hızlandırma Merkezi işlev kapsamı:
  https://www.iobit.com/en/advancedsystemcarefree.php
- Microsoft, Windows performansını iyileştirme önerileri:
  https://support.microsoft.com/en-us/windows/tips-to-improve-pc-performance-in-windows-b3b3ef5b-5953-fb6a-2528-4bbed82fba96
- Microsoft, Windows başlangıç uygulamalarını yönetme:
  https://support.microsoft.com/tr-TR/Windows/Experience/Startup-Boot/configure-startup-applications-in-windows
- Microsoft, Windows Update ile önerilen ve isteğe bağlı sürücü güncellemeleri:
  https://support.microsoft.com/en-us/windows/hardware/drivers/automatically-get-recommended-and-updated-hardware-drivers
- Microsoft, Windows arka plan uygulaması izinleri:
  https://support.microsoft.com/en-us/windows/experience/performance-optimization/manage-background-activity-for-apps-in-windows
- Microsoft, System File Checker kullanımı:
  https://support.microsoft.com/windows/using-system-file-checker-in-windows-365e0031-36b1-6031-f804-8fd86e0ef4ca
- Microsoft, DISM işletim sistemi paket servis seçenekleri:
  https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/what-is-dism
- Microsoft, `netsh dnsclient` ve ağ komutları:
  https://learn.microsoft.com/en-us/windows-server/networking/technologies/netsh/netsh-contexts
- Microsoft, Windows HOSTS dosyası:
  https://learn.microsoft.com/en-us/troubleshoot/windows-server/networking/cannot-modify-hosts-lmhosts-files

## Tercan güvenlik sınırları

Temizlik motoru yalnızca uygulama içinde tanımlanmış geçici dosya ve önbellek köklerini kabul eder, NTFS yeniden ayrıştırma noktalarını izlemez ve kullanımda olan dosyaları atlar. Başlangıç girdileri silinmeden önce komut ve kayıt defteri görünümü saklanır. DNS ilk kez değiştirilirken mevcut ayar yedeklenir. HOSTS her kayıttan önce zaman damgalı olarak kopyalanır.

Hellzerg Optimizer'da bulunan Defender/Windows Update kapatma, BCD/HPET, sistem değişkeni yolu ve dosya kilidi sonlandırma gibi yüksek riskli özellikler Tercan'a otomatik işlem olarak eklenmemiştir.

## Uygulama Merkezi güvenliği

Tercan, kullanıcı tarafından girilen paket adlarını çalıştırmaz. Uygulama kataloğundaki sabit WinGet kimliklerini `--exact` ve `--source winget` seçenekleriyle kurar. Güvenlik özeti denetimini atlayan `--ignore-security-hash` seçeneği kullanılmaz.

## ISLC bütünlük bilgisi

- Sürüm: `1.0.4.6`
- Portable indirme: `https://download.wagnardsoft.com/ISLC/ISLC%20v1.0.4.6.exe`
- Yayımlanan SHA-256: `606DCBA965AF417D97486B125723BBC5CCE92F830C7791DEF06B0C542A10DF50`

Tercan, dosya özeti eşleşmezse indirilen dosyayı siler ve çalıştırmaz.

## GoodbyeDPI-Turkey bütünlük bilgisi

- Sürüm: `0.2.3rc3-turkey`
- Resmî paket: `https://github.com/cagritaskn/GoodbyeDPI-Turkey/releases/download/release-0.2.3rc3-turkey/goodbyedpi-0.2.3rc3-turkey.zip`
- Sabit ZIP SHA-256: `B1F93B2E9434D93C5321275C4A3D0A87F3B822C552ECEABDBEB1610C879E1863`
- Lisans: Apache License 2.0

Paket Tercan dağıtımına gömülmez. Kullanıcı DPI sayfasındaki indirme düğmesine bastığında resmî GitHub sürümünden alınır. ZIP özeti doğrulandıktan sonra arşiv, klasör dışına yazmayı engelleyen güvenli çıkarma işlemiyle açılır ve motor, DLL ve sürücü dosyalarının ayrı SHA-256 değerleri tekrar denetlenir.

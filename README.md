# tercan.exe 1.8.0

Sürüm 1.8.0; hızlı kurulum sağlayan tek dosyalık `tercan-setup.exe`, GitHub Release tabanlı SHA-256 doğrulamalı uygulama içi güncelleme merkezi ve her kaynak yüklemesinde kurulum paketlerini otomatik hazırlayan GitHub Actions akışını ekler.

`tercan.exe`, Windows 10 ve Windows 11 için oyun optimizasyonu, bilgisayar performans analizi, yeni PC uygulama kurulumu ve güvenli sistem bakımı araçlarını tek arayüzde birleştirir.

## Tek Tık Bakım

Ana sayfa modern bakım merkezi biçiminde yeniden tasarlanmıştır:

- Yalnız gerektiğinde dönen ve nabız efekti gösteren büyük `TARA / OPTİMİZE ET` düğmesi
- Performans, gizlilik, gereksiz dosyalar, ağ yenileme ve sistem araçları için ayrı seçim anahtarları
- Salt okunur ilk tarama ve tarama tamamlandıktan sonra ayrı uygulama adımı
- Bulunan performans/gizlilik önerilerinin sayısı
- TEMP, DirectX gölgelendirici önbelleği, çökme dökümleri ve Windows TEMP için kategori bazlı dosya/alan dökümü
- Görev Yöneticisi, Komut İstemi, Denetim Masası, Çalıştır, sağ tık menüsü, Dosya Seçenekleri ve Kayıt Defteri erişim engeli denetimi
- Güvenli gereksiz dosya temizliği ve DNS önbelleği yenileme
- İşlem tamamlandığında ayrıntılı sonuç ve Windows'u yeniden başlatma önerisi
- Kullanıcı açıkça seçerse 15 saniyelik yeniden başlatma planı

Tek tık önerilen profil; yalnız `GÜVENLİ` sınıfındaki ayarları uygular. Yüksek performans güç planı, hata raporlaması, bellek sıkıştırma ve benzeri temkinli/deneysel ayarlar otomatik uygulanmaz.

## Kompakt optimizasyon ayarları

Ekran görüntülerindeki anahtar düzeninden esinlenen yeni sayfa:

- Sistem ve performans
- Windows 11 görev çubuğu, Copilot, kompakt Explorer, klasik sağ tık ve Edge kenar çubuğu
- Gizlilik ve arka plan
- Windows araçlarını geri açma
- Önerilen güvenli profil ve oyun profili
- Her anahtar için risk ve tahmini etki etiketi
- Değişiklikleri uygulamadan önce bekleten ve toplu inceleme sunan akış

Defender, SmartScreen, Güvenlik Duvarı, Windows Update, Sistem Geri Yükleme ve SMB2 kapatma seçenekleri performans profiline eklenmez.

## Eski Akıllı tarama altyapısı

Tek Tık Bakım taraması mevcut Akıllı Tarama motorunu kullanır. Tarama hiçbir ayarı değiştirmeden şu alanları okur:

- Windows Oyun Modu, oyun yakalama, güç planı, görünüm ve güvenli ağ ayarları
- Toplam ve kullanılabilir fiziksel bellek
- Oyun sırasında kapatılabilecek kullanıcı uygulamaları ve yaklaşık bellek yükleri
- HKCU/HKLM 32-bit ve 64-bit başlangıç girdileri
- Kullanıcı TEMP, Windows TEMP, DirectX önbelleği ve çökme dökümleri

Tarama tamamlandığında:

- Otomobil hız göstergesine benzeyen **Performans Hazırlığı** göstergesi
- Önerilerden sonraki olası hazırlık seviyesini gösteren hedef işareti
- Düşük, orta veya yüksek **tahmini etki puanı**
- Bulunan sorunların gerekçeleri ve yapılması önerilen işlemler
- Güvenli ayarları uygulamadan önce toplu inceleme listesine hazırlama

Gösterge doğrudan FPS yüzdesi değildir. İşlemci, ekran kartı, oyun motoru, sıcaklık ve oyun içi ayarlar gerçek performansı belirler. Tercan puanı; Windows hazırlığı, eksik temel ayarlar, bellek baskısı ve arka plan yükü üzerinden göreli bir etki tahmini verir.

## Oyun ve performans araçları

- Oyun Modu, arka plan oyun kaydı, güç planı ve HAGS denetimleri
- Arka plan servisleri, görünüm, gizlilik ve ağ seçenekleri
- Güvenli, temkinli ve deneysel risk sınıfları
- Her değişiklikten önce mevcut değeri yedekleme
- Tek tek veya toplu geri alma
- Seçilen kullanıcı uygulamalarını geçici kapatan Oyun Odak Modu
- Geçici yüksek performans güç planı ve oyun süreci için yüksek öncelik
- Beklenmedik kapanma sonrasında oyun modu oturumunu kurtarma
- Eşik tabanlı dahili standby bellek izleyici ve doğrulanmış ISLC desteği

## Sistem Araç Kutusu

- Güvenli konum beyaz listeli Temizlik Merkezi
- Yedekli Başlangıç Yöneticisi
- Ping, DNS önbelleği temizleme, DNS profilleri ve DNS geri alma
- SFC, DISM ve çevrimiçi CHKDSK onarım paneli
- İşlemci, GPU, anakart, BIOS, RAM, disk ve ağ raporu
- Biçim doğrulamalı ve otomatik yedekli HOSTS editörü
- Görev Yöneticisi, Hizmetler, Aygıt Yöneticisi ve Windows ayar kısayolları

## Discord / DPI paneli

- `cagritaskn/GoodbyeDPI-Turkey` v0.2.3rc3 için isteğe bağlı resmî GitHub indirmesi
- İndirmeden sonra sabit ZIP SHA-256 değeri ve yedi kritik motor/WinDivert dosyası için ayrı bütünlük denetimi
- Türkiye Standart ve temkinli SuperOnline Alternatif 4 profilleri
- Tek tıkla başlatma, durdurma ve durum yenileme
- Seçili profili Windows ile otomatik başlatan, kullanıcı tarafından açılıp kapatılabilen hizmet
- Tercan'ı tarama ve arka plan zamanlayıcısı çalıştırmadan bildirim alanında başlatma seçeneği
- Tercan dışında kurulmuş aynı adlı hizmeti değiştirmeyen sahiplik kontrolü
- Microsoft Defender dışlaması eklemeyen güvenli sınır

GoodbyeDPI bir VPN değildir; trafiği şifrelemez, IP adresini gizlemez ve FPS veya internet hızı artışı vaat etmez. Profil sonuçları ISS'ye göre değişebilir. Türkiye deposu 29.07.2025 tarihli notunda bazı Discord ve içerik sorunları için daha yeni `SplitWire-Turkey` aracını önermektedir; bağlantı sorunu yaşanırsa motor durdurulmalıdır.

## Modern arayüz ve düşük arka plan yükü

- Tarama sonucunda yumuşak hareket eden otomobil göstergesi
- DPI sayfası açık ve motor çalışır durumdayken etkin olan düşük kare hızlı bağlantı animasyonu
- Yalnız kullanıcı anahtarı değiştirirken çalışan kısa geçiş animasyonları
- Bellek sayfası kapalı ve temizleyici devre dışıyken tamamen duran izleme zamanlayıcısı
- Bellek temizleyici arka plandayken 10 saniyelik düşük sıklıklı örnekleme
- Windows başlangıcında otomatik Akıllı Tarama yapmayan ve düşük işlem önceliğinde bekleyen bildirim alanı modu
- Aynı anda ikinci Tercan örneğini engelleyen tek örnek koruması ve beklenmeyen hata günlüğü

## Uygulama kurulum merkezi

- WinRAR, 7-Zip, Chrome, Steam, Discord, Epic Games ve diğer yaygın uygulamalar
- Yeni PC, oyuncu ve yayıncı hazır seçim profilleri
- Sabit WinGet paket kimliği ve tam eşleşme
- Güvenlik özeti denetimini atlamayan sıralı kurulum

## Güvenlik sınırları

Tercan:

- Tarama sırasında kayıt defteri yazmaz, dosya silmez veya uygulama kapatmaz.
- Microsoft Defender'ı veya Windows Update'i kapatmaz.
- GoodbyeDPI için Microsoft Defender dışlaması eklemez.
- HPET, `useplatformclock`, `disabledynamictick` ve benzeri BCD değişiklikleri uygulamaz.
- BIOS, voltaj, XMP/EXPO veya hız aşırtma ayarlarına dokunmaz.
- Gerçek zamanlı işlem önceliği kullanmaz.
- Belge, İndirilenler, Masaüstü, tarayıcı oturumu veya parola verisini temizlemez.
- Windows güvenlik bildirimi ve RunOnce kurulum girdilerini başlangıç yöneticisinde korur.

Uygulama verileri ve geri alma yedekleri `%ProgramData%\Tercan` altında saklanır.

## Hızlı kullanım

1. `tercan.exe` dosyasını açın ve Windows yönetici iznini kabul edin.
2. Tek Tık Bakım modüllerini seçin ve büyük **TARA** düğmesine basın.
3. Tarama sonucu ve gereksiz dosya ayrıntılarını inceleyin.
4. Büyük **OPTİMİZE ET** düğmesiyle seçili güvenli işlemleri uygulayın.
5. İşlem bittiğinde açık belgelerinizi kaydedip yeniden başlatma seçiminizi yapın.
6. Daha ayrıntılı seçim için **Optimizasyon** sayfasındaki anahtarları kullanın.
7. Sonucu aynı oyun, aynı sahne ve aynı grafik ayarlarıyla karşılaştırın.

## Kaynaktan derleme

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Build.ps1 -Test -RenderPreviews
```

Derlenen uygulama `dist\tercan.exe` olarak oluşturulur.

## Açık kaynak referansı

Hellzerg Optimizer'ın özellik kapsamı ürün araştırması için referans alınmıştır. GPL-3.0 lisanslı kaynak kodu kopyalanmamış; uygulama mevcut özgün kod tabanından Tercan adıyla geliştirilmiştir.

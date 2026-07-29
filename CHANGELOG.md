# Değişiklik günlüğü

## tercan.exe 1.8.0 — 2026-07-30

- Tek dosyalık `tercan-setup.exe` hızlı kurulum programı eklendi.
- Uygulama Program Files altına kurulur; Başlat menüsü, isteğe bağlı masaüstü kısayolu ve Windows kaldırma kaydı oluşturulur.
- Kurulum programı aynı zamanda güvenli güncelleme ve kaldırma yardımcısı olarak çalışır.
- Sol menüye GitHub Release tabanlı Güncellemeler sayfası eklendi.
- Uygulama açılışta sessiz güncelleme denetimi yapar; kurulum yalnız kullanıcı onayıyla başlar.
- İndirilen setup adresi Tercan24/Can GitHub yayın alanıyla sınırlandırılır ve SHA-256 eşleşmeden çalıştırılmaz.
- Kaynaklar `main` dalına yüklendiğinde uygulama, setup, taşınabilir paket, kaynak paket ve `update.json` dosyasını otomatik yayınlayan GitHub Actions akışı eklendi.
- Sonraki değişiklikleri tek komutla kaydedip GitHub'a gönderen `Publish-GitHub.ps1` eklendi.

## tercan.exe 1.7.8 — 2026-07-30

- Hızlandırma sayfasındaki Oyun Kipi artık yalnızca güç planını değiştirmekle kalmaz; çalışan güvenli Windows servislerini geçici olarak durdurur.
- Windows Oyun Modu etkinleştirilir, arka plan oyun kaydı kapatılır ve değişen değerler oturum sonunda eski durumuna getirilir.
- OneDrive, Google Drive, Widget'lar ve Telefon Bağlantısı gibi güvenli varsayılan arka plan uygulamaları çalışıyorsa geçici olarak kapatılır.
- Servis başlangıç türleri değiştirilmez; yalnızca o anda çalışan ve çalışan bağımlısı bulunmayan izinli servisler durdurulur.
- RPC, ağ, ses, Defender, Güvenlik Duvarı, Windows Update ve diğer kritik servisler koruma listesinde tutulur.
- Oyun Kipi kartına soldan sağa hareket eden otomotiv tipi ibre animasyonu eklendi.
- Kartta durdurulan servis/süreç, değişen oyun ayarı ve bırakılan yaklaşık uygulama belleği gösterilir.
- Etkin oturum sırasında durdurulan servislerin adları ve geri alma bilgisi ayrı bir sonuç şeridinde görüntülenir.
- Oyun Kipi kapanırken servisler, güç planı, oyun/kayıt ayarları ve kapatılan uygulamalar geri yüklenir.

## tercan.exe 1.7.7 — 2026-07-30

- Uygulama Merkezi'ndeki harf kutuları kaldırıldı.
- WinRAR, 7-Zip, Chrome, Firefox, Brave, VLC, Spotify, Notepad++, Everything ve PowerToys gerçek logolarıyla gösterilir.
- Steam, Epic Games, Discord, EA, Ubisoft, GOG, OBS, Audacity, ShareX ve qBittorrent gerçek logolarıyla gösterilir.
- 20 logo şeffaf arka planlı ve eşit boyutlu PNG olarak uygulamaya gömüldü.
- Kartlara koyu temaya uyumlu, yuvarlatılmış logo alanları eklendi.
- Logo yükleme yerel önbellekle yapılır; Uygulamalar sayfası açılırken ağ isteği oluşturulmaz.

## tercan.exe 1.7.6 — 2026-07-30

- Sol menüdeki Optimizasyon adı Hızlandırma olarak değiştirildi.
- Ayrı Oyun Modu ve Araçlar menüleri kaldırılarak arayüz sadeleştirildi.
- Oyun Kipi, Hızlandırma sayfasından güvenli varsayılanlarla tek tıkla açılıp kapatılabilir hale getirildi.
- Oyun Kipi açıldığında kapatılan süreç ve bırakılan yaklaşık çalışma kümesi kart üzerinde gösterilir.
- Hızlandırma sayfasındaki gereksiz sürücü ve ek araç kartları kaldırıldı; başlangıç, RAM ve uygulama yönetimi korundu.
- Ana Sayfa'nın altındaki boş “Sonuçlar burada görünecek” alanı kaldırıldı; gerçek tarama sonucu yine işlem sonrasında gösterilir.
- İç yönetim sayfalarındaki geri düğmesi artık kaldırılan Araç Kutusu yerine Hızlandırma sayfasına döner.

## tercan.exe 1.7.5 — 2026-07-30

- Fare tekerleği kullanılırken görünen ince mor kaydırma göstergesi tamamen kaldırıldı.
- Yerel yatay ve dikey scrollbar pencere stilleri kontrol oluşturulurken temizlenir.
- Fare tekerleği ve kaydırma mesajlarında sistem çubuğu çizimden önce, hemen sonra ve sonraki arayüz turunda gizlenir.
- Kaydırma çalışmaya devam ederken ekranda hiçbir çubuk veya konum göstergesi görünmez.

## tercan.exe 1.7.4 — 2026-07-30

- Sekme değişimlerinde yeniden oluşabilen beyaz Windows kaydırma çubuğu sorunu kalıcı olarak giderildi.
- Geçici gizleme davranışı yerine özel kaydırılabilir panel, akış paneli ve ayrıntı paneli kontrolleri eklendi.
- İçerik uzunsa sağ kenarda yalnızca 3 piksellik mor premium konum göstergesi çizilir.
- Ana sayfa, Temizlik, Optimizasyon, Uygulamalar, Araçlar, Geri Alma ve iç listeler aynı kontrol altyapısına geçirildi.

## tercan.exe 1.7.3 — 2026-07-30

- Windows'un açık renkli yerel dikey ve yatay kaydırma çubukları uygulama arayüzünden gizlendi.
- Ana sayfalar, sol menü, temizlik kategorileri, optimizasyon listeleri ve uygulama listeleri aynı davranışa bağlandı.
- Kaydırma çubuğu görünmese de fare tekerleği ve dokunmatik yüzeyle kaydırma korunur.
- Koyu arka planın sağ kenarda kesintisiz görünmesi sağlandı.

## tercan.exe 1.7.2 — 2026-07-29

- Hızlı biten temizlik taramalarına yaklaşık 3,8 saniyelik minimum, akıcı analiz sunumu eklendi.
- Tarama sonunda dosya boyutu doğrulama, korunan alanları dışlama ve sonuç hazırlama aşamaları gösterilir.
- Temizlik sonucu yaklaşık 3,4 saniyeden önce kapanmaz; dosya kontrolü, alan hesabı ve rapor hazırlama aşamaları görünür.
- Tamamlanan temizlik yeşil halka, onay işareti, `TAMAMLANDI` başlığı ve net sonuç özetiyle gösterilir.
- Minimum süre yalnız arayüz sunumunu düzenler; gerçek dosya işlemi arka planda güvenli hızında devam eder.

## tercan.exe 1.7.1 — 2026-07-29

- Temizlik Merkezi'ndeki ayrı tarama ve temizleme düğmeleri kaldırıldı.
- Büyük daire ilk tıkta tarama yapar; sonuç bulunduğunda aynı daire `TEMİZLE` durumuna geçer.
- Tarama ve temizlik ilerlemesi aynı daire içinde yüzde ve dönen halka animasyonuyla gösterilir.
- `TEMİZLE` durumu yeşil ışıkla ayrıldı; işlem tamamlanınca daire `TEKRAR TARA` durumuna döner.
- Daireye hover, basma ve klavye erişimi eklendi.

## tercan.exe 1.7.0 — 2026-07-29

- Ana içerik alanına düşük yoğunluklu, yavaş hareket eden mor–mavi ortam ışığı ve çizgi dokusu eklendi.
- Ana sayfa, Hızlandırma, Temizlik ve Araçlar kartlarına yumuşak hover parıltısı eklendi.
- Düğmelere etkileşim sırasında ışık geçişi, kenar vurgusu ve tıklama dalgası eklendi.
- Sayfa başlıklarına kısa yükselme/renk geçişi ve üst kısma premium ışık süpürmesi eklendi.
- Marka logosuna yalnız görünürken çalışan düşük maliyetli nefes parıltısı eklendi.
- Animasyon zamanlayıcıları pencere küçültüldüğünde veya geçiş tamamlandığında duracak biçimde sınırlandı.

## tercan.exe 1.6.0 — 2026-07-29

- Tercan için kalkan, hız göstergesi ve T harfini birleştiren özgün mor–mavi marka görseli tasarlandı.
- Logo uygulama dosyasına gömüldü; harici görsel dosyası gerektirmez.
- Windows uygulama simgesi, görev çubuğu ve bildirim alanı simgesi yenilendi.
- Sol üst marka alanı, Hızlandırma Merkezi ve Hakkında sayfası yeni görsel kimlikle güncellendi.
- Uygulama açılışına kısa, düşük maliyetli ve animasyonlu Tercan karşılama ekranı eklendi.

## tercan.exe 1.5.0 — 2026-07-29

- Optimizasyon sayfası kart tabanlı modern Hızlandırma Merkezi olarak yenilendi.
- Turbo Hızlandırma kartı geri alınabilir Oyun Odak Modu'na bağlandı.
- Başlangıç Eniyileyici kartı etkin başlangıç öğelerini sayar ve yedekli yönetim ekranını açar.
- Sürücü ve Donanım kartı, üçüncü taraf sürücü indirmek yerine Windows Update İsteğe Bağlı Güncellemeler'i açar.
- Uygulama Temizleyici, RAM izleyici ve bakım araçları tek merkezde birleştirildi.
- Ayrıntılı Windows ayarları varsayılan olarak gizlendi; isteyen kullanıcı tek düğmeyle açabilir.
- Microsoft ve Advanced SystemCare 19 resmî özellik kaynakları araştırma notlarına eklendi.

## tercan.exe 1.4.1 — 2026-07-29

- Ayrı Temizlik sayfası referanstaki iki sütunlu düzene dönüştürüldü.
- Temizlik kategorileri soldan tek tıkla açılıp kapatılabilir.
- Sağ bölümde dairesel tarama göstergesi, canlı dosya yolu ve seçili toplam gösterilir.
- Tarama sonrasında kategori seçimi değiştiğinde dosya ve alan toplamı anında güncellenir.
- Temizlik sırasında silinen dosya, açılan alan ve atlanan dosyalar canlı gösterilir.
- Temizlik tamamlandığında gereksiz yeniden başlatma uyarısı gösterilmez.

## tercan.exe 1.4.0 — 2026-07-29

- Tarama sonrası bulunan düzeltmeler ve temizlik kategorileri tek tek açılıp kapatılabilir.
- Seçim özeti; uygulanacak işlem, dosya ve kazanılabilecek alan miktarını gösterir.
- Temizlik sırasında kategori, dosya yolu, temizlenen dosya ve açılan alan canlı gösterilir.
- Sadece gerçekten gerektiren bir ayar uygulandığında yeniden başlatma önerilir.
- ParsMazi esintili mor–mavi renkler, cam yüzeyler ve hafif animasyonlarla ana bakım ekranı yenilendi.

## tercan.exe 1.3.0 — 2026-07-29

- Sol menü yalnız Ana Sayfa, Optimizasyon, Oyun Modu, Temizlik, Uygulamalar, Discord / DPI, Araçlar ve Geri Alma bölümlerine indirildi
- Tekrarlanan Oyun, Arka Plan, Görünüm, Gizlilik, Ağ ve deneysel kategori bağlantıları menüden kaldırıldı
- Ana sayfa ve optimizasyon açıklamaları kısa, doğrudan ifadelerle yenilendi
- Menü seçimlerine ve butonlara yalnız etkileşim sırasında çalışan yumuşak geçişler eklendi
- Üst kartlara hafif hareketli çizgi ve parıltı efekti eklendi
- Sayfa açılışına kısa vurgu geçişi eklendi
- Sayfa değiştirilirken eski animasyon ve kontroller güvenli biçimde kapatılacak şekilde yaşam döngüsü iyileştirildi

## tercan.exe 1.2.0 — 2026-07-29

- Modern, animasyonlu büyük `TARA / OPTİMİZE ET` bakım merkezi
- Performans, gizlilik, gereksiz dosyalar, ağ ve sistem onarımı için ayrı modül anahtarları
- Salt okunur tarama ile tek tık uygulama işleminin ayrılması
- Tarama sonrasında modül ve kategori bazlı ayrıntılı sonuç ekranı
- TEMP, DirectX önbelleği, çökme dökümleri ve Windows TEMP için dosya/alan dökümü
- 28 yedekli optimizasyon ve onarım ayarına genişletilen katalog
- Windows 11 widget, arama, sohbet, Copilot, kompakt Explorer, klasik sağ tık ve Edge kenar çubuğu seçenekleri
- Uzun dosya yolu desteği ve kişiselleştirilmiş deneyim denetimi
- Görev Yöneticisi, CMD, Denetim Masası, Çalıştır, sağ tık, Dosya Seçenekleri ve Regedit erişim onarımı
- Ekran görüntülerindeki yoğun anahtar düzenini modern kartlar halinde sunan Optimizasyon sayfası
- Önerilen güvenli profil ve oyun profili
- İşlem sonunda özel yeniden başlatma ekranı; kullanıcı seçerse 15 saniyelik yeniden başlatma
- Defender, SmartScreen, Güvenlik Duvarı, Windows Update, Sistem Geri Yükleme ve SMB2 için koruma sınırı

## tercan.exe 1.1.0 — 2026-07-29

- Modern Discord / DPI yönetim sayfası
- GoodbyeDPI-Turkey v0.2.3rc3 için resmî GitHub indirmesi ve sabit ZIP SHA-256 denetimi
- 32/64 bit motor, WinDivert DLL ve sürücüler için ikinci katman dosya bütünlüğü doğrulaması
- Türkiye Standart ve SuperOnline Alternatif 4 profilleri
- Tek tıkla başlatma, durdurma ve durum yenileme
- GoodbyeDPI için isteğe bağlı otomatik Windows hizmeti
- Tercan için Akıllı Tarama yapmadan düşük öncelikte açılan isteğe bağlı bildirim alanı başlangıç görevi
- Haricî GoodbyeDPI hizmetine dokunmayan sahiplik koruması
- Defender dışlaması eklemeyen açık güvenlik sınırı
- Yalnız hareket sırasında çalışan animasyonlu anahtarlar ve hız göstergesi
- Yalnız DPI sayfası açıkken çalışan düşük kare hızlı durum animasyonu
- Kullanılmadığında tamamen duran bellek izleme zamanlayıcısı; arka planda 10 saniyelik örnekleme
- Tek örnek uygulama kilidi ve genel hata günlüğü

## tercan.exe 1.0.0 — 2026-07-29

- Uygulama, proje, veri klasörü ve çalıştırılabilir dosya Tercan olarak yeniden markalandı
- Açılış sayfasına salt okunur Akıllı Sistem Taraması eklendi
- Windows ayarı, bellek, arka plan, başlangıç ve güvenli geçici dosya analizi
- Otomobil hız göstergesi biçiminde Performans Hazırlığı ve hedef puanı
- Tahmini etki puanının FPS yüzdesi olmadığını açıklayan ölçüm yaklaşımı
- Bulguların önem, gerekçe ve önerilen işlemle gösterilmesi
- Güvenli önerileri mevcut toplu inceleme akışına hazırlama
- Tercan turuncusu, neon camgöbeği ve yarış kokpiti esintili yeni tema
- T harfli uygulama ve kenar çubuğu simgesi

## 2.0.0 — 2026-07-29

- GameTune Optimizer 1.1'den tamamen bağımsız GameTune Ultimate ürünü
- Ayrı `%ProgramData%\GameTuneUltimate` veri, günlük ve geri alma alanı
- Güvenli konum beyaz listeli, önce tarayan Temizlik Merkezi
- 32/64-bit HKCU ve HKLM girdileri için yedekli Başlangıç Yöneticisi
- Ağ bağdaştırıcı bilgisi, ping, DNS temizleme ve geri alınabilir DNS profilleri
- SFC, DISM ve çevrimiçi CHKDSK için çıktı gösteren Windows Onarım sayfası
- Hassas seri numaralarını dışarıda bırakan donanım ve sistem raporu
- Doğrulama, otomatik yedek ve geri yükleme korumalı HOSTS editörü
- Görev Yöneticisi, Hizmetler, Aygıt Yöneticisi, Olay Görüntüleyici ve Windows ayar kısayolları
- Hellzerg Optimizer kapsamından esinlenen özgün Araç Kutusu arayüzü
- Defender/Windows Update kapatma ve BCD/HPET işlemlerini dışarıda tutan güvenlik sınırı

## 1.1.0 — 2026-07-29

- 20 uygulamalı kategorili Uygulama Kur merkezi
- WinRAR, Steam, Discord, Epic Games Launcher ve yeni bilgisayar araçları
- Yeni PC, oyuncu ve yayıncı seçim profilleri
- WinGet kullanılabilirlik denetimi ve Microsoft App Installer yönlendirmesi
- Sıralı, sessiz ve durum gösteren toplu kurulum
- Razer Cortex yaklaşımından esinlenen geri alınabilir Oyun Odak Modu
- Çalışan uygulamalara göre RAM görünümü ve seçmeli kapatma
- Geçici Yüksek Performans güç planı ve oyun için Yüksek işlem önceliği
- Oyun modu kapanınca uygulama, güç planı ve öncelik geri yükleme
- Çökme sonrası yarım kalan oyun modu oturumunu otomatik kurtarma

## 1.0.0 — 2026-07-29

- Windows 10/11 oyun optimizasyon paneli
- 17 yerleşik ayar ve örnek JSON eklentisi
- Güvenli, temkinli ve deneysel risk sınıfları
- İşlem öncesi kayıt defteri/servis/güç planı yedekleme
- Tek tek ve toplu geri alma
- Dahili standby bellek izleyici
- Resmî ISLC indirme, SHA-256 doğrulama ve komut satırıyla otomatik eşik ayarı
- Seçmeli Windows uygulaması kaldırma paneli
- Öz-test ve arayüz önizleme sistemi

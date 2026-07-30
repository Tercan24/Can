# Değişiklik günlüğü

## tercan.exe 1.8.0 — 2026-07-30

- Tek dosyalık `tercan-setup.exe` hızlı kurulum programı eklendi.
- Uygulama Program Files altına kurulur; Başlat menüsü, isteğe bağlı masaüstü kısayolu ve Windows kaldırma kaydı oluşturulur.
- Kurulum programı güvenli güncelleme ve kaldırma yardımcısı olarak da çalışır.
- Sol menüye GitHub Release tabanlı Güncellemeler sayfası eklendi.
- Uygulama açılışta sessiz güncelleme denetimi yapar; kurulum yalnızca kullanıcı onayıyla başlar.
- İndirilen kurulum dosyası Tercan24/Can GitHub yayın alanıyla sınırlandırılır ve SHA-256 değeri eşleşmeden çalıştırılmaz.
- `main` dalına gönderilen değişiklikler; uygulama, kurulum, taşınabilir paket, kaynak paket ve `update.json` dosyasını otomatik olarak yayınlar.
- Sonraki değişiklikleri tek komutla kaydedip GitHub’a gönderen `Publish-GitHub.ps1` eklendi.

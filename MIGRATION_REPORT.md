# APEX Racing V1.12 ke Unity 6.3 LTS + URP

**Keputusan produksi: pertahankan rilis Godot.** Proyek uji Unity layak dipakai untuk membandingkan renderer dan mengukur biaya port. Pergantian mesin penuh masih memerlukan bukti performa pada ponsel dan kesetaraan gameplay yang bisa dijalankan.

## Baseline dan alasan

| Area | Hasil audit |
|---|---|
| Mesin | Arsip V1.12 memakai Godot 4.7 Compatibility, GodotPhysics3D dan GDScript. V1.13 tersedia terpisah. |
| Data portabel | Forest Ridge dan Marina memakai GLB. Forest Ridge juga membawa segitiga collision, koordinat lintasan, pagar dan penempatan tanaman. Enam model kendaraan punya GLB dengan buffer dan tekstur tertanam. |
| Biaya port | Fisika CharacterBody3D, pose suspensi, gameplay karakter dan VRM lokal, pintu garasi, AI balap, mixer audio, mode kamera lanjutan, dan simpanan Godot perlu adapter atau implementasi Unity. Shader Godot perlu ditulis dalam HLSL atau Shader Graph. |
| Android lama | Preset Godot lanskap ARM64 dan ARMv7, API minimum 24, ID `id.apexracing.waterfront.v11`. Paket rilis menggunakan sertifikat Godot APEX yang sudah ada. |
| Android pilot | ID `id.apexracing.waterfront.urp.lab` menjaga pilot dapat dipasang berdampingan. Konfigurasi build memakai ARM64 IL2CPP, OpenGL ES 3, lanskap, API minimum 24 dan debug signing. |
| Perangkat target | Xiaomi Indonesia mencantumkan UNISOC T7250, Mali-G57, 4 GB RAM dan Android 15 Go untuk POCO C71. Performa URP belum diukur di perangkat tersebut. |

## Isi source Unity

- **Data dan rendering.** Memakai dua lintasan, enam mobil, sepuluh mesh LOD tanaman dan lingkungan Racing Hub asli. Scanning aspal didekode ke normal dan smoothness untuk URP Lit. Shader sungai dan air terjun, matahari dengan bayangan, dan kabut pegunungan tersedia di kode.
- **Gameplay uji.** Free Drive dan Time Trial dengan 28 checkpoint berarah. Kontrol keyboard dan multi-touch, kamera chase dan kap, pemulihan ke lintasan, jeda, pilihan mobil dan map, serta rekaman waktu lokal pilot disertakan.
- **Anggaran grafis.** Resolusi render 80%, satu cascade bayangan, tanpa buffer depth atau opaque texture yang tidak dibutuhkan, culling petak, GPU instancing dan dua tingkat detail tanaman. Target 30 FPS adalah batas frame yang dikonfigurasi.
- **Batas kesetaraan.** Source memakai pengendali mobil kinematik Unity untuk uji awal. Input Unity, fisika dan GPU perlu pengujian langsung. Interaksi karakter/VRM, pintu garasi, AI Race, audio, kelanjutan balap dan save Godot belum dipindahkan. Petak visual diseleksi menurut posisi kamera, sementara pemuatan berlangsung per lintasan.

## Bukti yang dijalankan

| Pemeriksaan | Hasil |
|---|---|
| Blender 4.3.2 | Membuka semua sembilan GLB asli. Forest menghasilkan 101 mesh dan 119.763 poligon, Marina 75 mesh dan 97.483 poligon. |
| .NET 8 / Roslyn C# 9 | 23 asersi lulus. Parser membaca tujuh file C# Unity dan compiler menjalankan dua kelas inti yang tidak bergantung pada Unity. |
| Integritas aset | 19 hash sumber cocok. Arsip source dan APK Godot V1.12 cocok dengan SHA-256 sebelum pekerjaan. |
| Koordinat kendaraan dan lintasan | 216 sampel dari tiga posisi lateral di Forest Ridge mendapat penopang dari data collision yang direfleksikan sesuai konversi glTFast. Selisih maksimum 0,013 m. |
| Aturan putaran | Loop data Forest 2.299,73 m dan Marina 1.199,09 m. Kedua loop lolos uji 28 gerbang, penolakan gerbang lompat, arah salah, melintas di luar lebar dan reset. |
| Unity Editor dan APK | Unity Editor dan Android Build Support tidak terpasang. Sesi Play, import glTFast di Unity, kompilasi shader URP, kompatibilitas IL2CPP, dan APK Unity belum dijalankan. |
| Profil perangkat | Ponsel tidak terhubung. FPS, frame time, suhu, RAM dan kontrol fisik belum diukur. |

## Syarat untuk mengganti rilis Godot

1. Validasi import Unity dan build Android dengan ID pilot. Ulangi drive loop, enam mobil, kedua map, semua checkpoint, collision start dan jembatan.
2. Bandingkan build Godot dan Unity di Poco C71 yang sama, setelan setara, lintasan dan mobil yang sama. Ukur frame-time median dan p95, RAM dan kestabilan termal pada sesi 15 menit. Target awal 30 FPS atau 33,3 ms per frame.
3. Pastikan kesetaraan karakter, masuk/keluar mobil, garasi, AI, kamera, audio, race resume, dan migrasi simpanan. Verifikasi tanda tangan lama, APK update-in-place dan data cadangan sebelum mempertimbangkan ID produksi.

## Build Automation

Source untuk build lewat browser HP menyertakan scene bootstrap terserialisasi, Build Settings dan metode pre-export untuk worker Unity. Alur ini terpisah dari build lokal lewat menu Editor. Uji integrasi pada layanan cloud dan hasil APK masih terbuka.

Lihat [panduan dari HP](BUILD_FROM_PHONE.md).

## Rujukan

- [Unity 6 LTS](https://unity.com/releases/unity-6/support)
- [Dokumentasi URP 17.3](https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4017.3/changelog/CHANGELOG.html)
- [glTFast Editor Import](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Documentation~/ImportEditor.md)
- [Dependensi Android Unity 6.3](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/getting-started/sdksetup/supported-dependency-versions)
- [Spesifikasi POCO C71 Indonesia](https://www.mi.co.id/id/product/poco-c71/specs/)

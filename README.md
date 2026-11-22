# APEX Racing — Unity 6.3 URP Lab

Proyek uji untuk port dari APEX Racing Godot V1.12. Branch lokal `migration/unity6-urp` dibuat di repositori tersendiri. `GODOT_BASELINE.sha256` mencatat dua arsip Godot V1.12 yang dijadikan baseline.

## Menjalankan di Unity

Gunakan Unity **6000.3.0f1**, URP **17.3.0**, dan glTFast **6.12.1**. Buka folder proyek dan tunggu Package Manager menyelesaikan dependensi. Jalankan **Tools → APEX → Prepare URP Migration Lab**. Perintah ini mengatur URP, mengimpor sembilan GLB dari `SourceMeshes/`, membuat material, dan membuka scene `Assets/APEX/Scenes/ForestRidgeLab.unity`. Lalu gunakan Play. GLB disimpan di luar folder Assets saat dikirim supaya glTFast memilih material URP saat import pertama.

Kontrol desktop memakai WASD atau panah, Spasi untuk rem tangan, C untuk kamera, R untuk pemulihan, dan Esc untuk menu. Pemilihan mobil, lintasan, mode, dan detail vegetasi ada di menu jeda. Tombol sentuh memakai safe area perangkat.

## Build pilot Android

Pasang Android Build Support, SDK, NDK dan OpenJDK untuk editor tersebut. Gunakan **Tools → APEX → Build Lab APK**, atau jalankan batch build dari folder proyek:

```bash
Unity -batchmode -quit -projectPath "$PWD" -buildTarget Android \
  -executeMethod ApexUrpLab.Editor.LabBuild.BuildAndroid -logFile -
```

Lokasi keluaran yang dikonfigurasi adalah `Builds/APEX-URP-Lab.apk`. Android ID pilot memakai `id.apexracing.waterfront.urp.lab`. Build ini memerlukan editor Unity dengan lisensi yang valid. Lingkungan yang membuat source ini belum menjalankan Unity atau menghasilkan APK.

## Uji yang bisa diulang tanpa Unity

```bash
dotnet run --project Verification -- .
blender -b -t 3 --python-exit-code 1 --python Tools/blender_import_check.py
```

Python 3 dan Pillow hanya diperlukan untuk membuat ulang data dari arsip baseline dengan `python Tools/stage_assets.py path/ke/APEX-RACING-V1.12-Source.zip`. Asset siap impor sudah disertakan.

Lihat [laporan migrasi](MIGRATION_REPORT.md) dan [kredit aset](ASSET-CREDITS.txt). Penggunaan dan redistribusi kendaraan mengikuti lisensi masing-masing aset.

## Build dari HP

Gunakan [panduan Unity Build Automation](BUILD_FROM_PHONE.md) untuk menjalankan build di server Unity melalui browser HP. Source berisi scene dan metode pre-export untuk worker cloud. Build cloud belum dijalankan.

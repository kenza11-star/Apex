# Build APK APEX Unity dari HP

Panduan ini memakai Unity Build Automation. HP dipakai untuk menyiapkan proyek dan mengunduh APK. Unity Editor dijalankan oleh server Unity. Gunakan proyek cloud terpisah agar rilis Godot tetap bisa dimainkan.

1. Ekstrak ZIP. Masukkan **isi folder proyek** ke repositori privat. Pertahankan struktur `Assets`, `Packages`, `ProjectSettings`, dan `SourceMeshes`. Dashboard mengambil file dari repositori, jadi ia memerlukan isi proyek yang sudah diekstrak. Jika mengunggah melalui Git di Android, buat commit dari folder yang berisi empat direktori tersebut.
2. Buka [Unity Dashboard](https://cloud.unity.com/) dari browser HP. Buat proyek Unity Cloud, lalu buka **DevOps → Build Automation** dan hubungkan repositori privat tadi. Cek kuota dan biaya yang ditampilkan sebelum memilih paket atau memulai build.
3. Buat konfigurasi **Android** dan pilih branch yang berisi source. Aktifkan **Auto detect Unity version** dari `ProjectSettings/ProjectVersion.txt`, atau pilih **6000.3.0f1**. Gunakan project path yang berisi `Assets`. Biarkan **Build app bundles (.aab) instead of an APK** nonaktif.
4. Di **Advanced settings → Script hooks → Pre-export method**, masukkan `ApexUrpLab.Editor.ApexCloudBuild.PreExport`. Metode ini memasang URP pada proyek, mengimpor model dan menyiapkan material sebelum Unity membangun player.
5. Gunakan scene dari **Build Settings** proyek. Jika dashboard meminta path scene secara eksplisit, masukkan `Assets/APEX/Scenes/ForestRidgeLab.unity`.
6. Untuk tes pribadi, pilih **Auto-generated debug keystore** jika tersedia. ID aplikasi pilot adalah `id.apexracing.waterfront.urp.lab`.
7. Jalankan build manual. Setelah sukses, buka **Build History → menu ⋯ → Download .ZIP file**. Ekstrak hasil build itu dan instal file **.apk** di HP.

Paket ini sudah disiapkan untuk konfigurasi tersebut. Import Unity, kompilasi Android, dan layanan cloud masih perlu diuji pada worker Unity. APK pilot akan memiliki identitas aplikasi terpisah dari APK Godot.

## Dokumentasi

- [Menyiapkan Unity DevOps](https://docs.unity.com/en-us/devops/get-started-end-users)
- [Konfigurasi build Android](https://docs.unity.com/en-us/build-automation/basic-build-configuration/overview)
- [Metode pre-export](https://docs.unity.com/en-us/build-automation/advanced-build-configuration/run-custom-scripts-during-the-build-process)
- [Tanda tangan Android](https://docs.unity.com/en-us/build-automation/sign-build-artifacts/sign-an-android-application)

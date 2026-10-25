# Rolling Sky But RTX ON — Scene Gameplay (Unity 6, URP)

Tahap 1: scene gameplay. Level dibaca dari `Level.txt` lama atau dari `level.json` (format baru), jadi level editor yang sudah ada tetap bisa dipakai.

## Cara pasang
1. Unity Hub > **New project** > editor Unity 6 (6000.0 atau 6000.3 LTS) > template **Universal 3D** (URP).
   Template **High Definition 3D** (HDRP) atau **3D (Built-In)** tidak bisa: script builder butuh URP dan akan error `UniversalRenderPipelineAsset could not be found`.
2. Ekstrak `RTXOn.zip`. Folder `Scripts`, `Editor`, `Shaders`, `Levels` boleh ditaruh di mana saja di dalam `Assets/`, asal **tidak dobel** (script kembar = error).
3. Menu atas **RTX ON > Build Gameplay Scene**. Scene `Assets/RTXOn/Scenes/Gameplay.unity` terbuka.
4. Play. Klik / tap untuk mulai, drag mouse/jari untuk belok (di editor bisa juga A/D atau panah).
   **Stik PS5 / PS4 / Xbox** (colok USB atau Bluetooth, langsung jalan lewat Input System): belok dengan **stik kiri** (bawaan) dan D-pad; di **Settings > Steer With** bisa diganti ke **LT RT** atau **LB RB** (hanya satu yang aktif, D-pad selalu jalan). Tombol bawaan, bisa diganti semua di **Settings > Controller Buttons**: **X / Square = geser tepat 1 lajur ke kiri, B / Circle = 1 lajur ke kanan**, **A / Cross ditahan = Accelerator** (belok lebih cepat), **Y / Triangle = ganti kecepatan belok Slow / Normal / Fast** (setelah bola pecah Y = ulang dari awal), **Options / Menu = Pause**, **View / Create = kembali**, R3 = hitbox. Cross / A juga mulai dan coba lagi. Keyboard: A / D atau panah kiri / kanan belok, **Spasi ditahan = Accelerator**. Stik bergetar saat bola pecah. Bola bisa bergerak 7 lajur (5 lajur lintasan + 1 lajur kosong di tiap sisi), field **Lateral Limit** = 3.
   **Pause** (tombol II kiri atas, P, Esc / Back saat bola jalan, atau Options), desain baru 2026-10-24 (PDF halaman 7): tulisan gambar **PAUSED**, crown level (emas = sudah diambil, abu-abu = belum), progress bar dengan persen, gem, tombol **RESTART** (gambar `Restart`), dan baris bawah **Levels** (kembali ke daftar level; juga kalau scene Gameplay di-Play langsung, lewat scene MainMenu), **Resume** (besar, warna Accent), **Settings** (Musik, Efek Suara, Sensitivity, Smooth, Kecepatan Controller) dan **X** (ke Home menu, dari sana bisa keluar game; saat Play Test = kembali ke editor; tanpa scene MainMenu di build = keluar game). Back / Esc menutup Settings dulu, lalu Resume. Setting disimpan (PlayerPrefs, `GameSettings.cs`).
   **HUD** (PDF halaman 8): tombol Pause kiri atas, progress bar hijau dengan persen di atas tengah, crown lalu gem di bawah tombol Pause (baris crown hilang kalau level tidak punya crown). Warna ikut **Settings > Menu Color**. Portrait dan landscape punya ukuran canvas masing-masing (seperti Main Menu).

## Update paket
Timpa file lama di tempat yang sama, lalu **RTX ON > Build Gameplay Scene**. Scene dibuat ulang (kalau ada perubahan yang belum disimpan, Unity tanya dulu). Level File, Theme Changes, theme, material dan model kamu tetap.
Kalau mau material, langit dan preset Day/Sunset/Night dibuat ulang dari nol: **RTX ON > Reset Look** (slot model, props dan level tetap).

## Tema berganti (siang → sore → malam)
Saat bola sampai baris tertentu, dunia berganti preset: matahari **bergerak sendiri** ke posisi baru, langit, kabut, cahaya ambient, glow neon, bloom ikut berubah pelan-pelan selama beberapa baris. Malam: bulan, bintang berkelip, garis neon menyala lebih kuat.

- Preset ada di `MassifTheme` > **Environments**: `Day`, `Sunset`, `Night`. Bisa diubah atau ditambah (nama bebas). **Edit langsung saat Play**: ubah warna/angka environment di tema yang dipakai level (Track, atau `"look"` level itu) sambil Play, dan langsung terlihat di Game view (Game > Environment Director > **Live Edit**, hanya di editor). Perubahan saat Play tetap tersimpan di aset tema setelah Play dihentikan.
- **HDRI per preset** (misalnya dari Poly Haven): import `.hdr`, set **Texture Shape = Cube** dan Apply. Lalu di tiap preset Environments isi **Sky HDRI**, dengan **Hdri Exposure**, **Hdri Rotation** (putar supaya mataharinya searah cahaya Sun), dan **Hdri Tint**. Day, Sunset, dan Night boleh punya HDRI sendiri-sendiri; saat tema berganti langitnya memudar dari HDRI satu ke yang berikutnya. **Hdri Lighting** on = cahaya ambient diambil dari HDRI. Preset tanpa HDRI tetap memakai langit gradien. Skybox Material di jendela Lighting tidak perlu diganti, karena game memakai langit RTX ON sendiri. Samakan juga **Fog Color** tiap preset dengan warna cakrawala HDRI-nya.
- Kapan berganti:
  - `level.json`: bagian `"themes"` (lihat di bawah).
  - `Level.txt`: objek **Game** > **Theme Changes**.
- Level 1 bawaan: Day di awal, **Sunset di baris 218**, **Night di baris 373**, masing-masing berganti selama 40 baris.
- Semua berdasarkan jarak bola, jadi sunset selalu jatuh di bagian musik yang sama.
- **Matahari dan bulan** bisa diputar ke arah masing-masing kapan saja: Theme trigger > **Sun angle / Moon angle** (lihat `LEVEL-EDITOR.md`), di level.json `"turnSun": true, "sun": { "x": 20, "y": 180 }` dan `"turnMoon"`, `"moon"` di dalam `"themes"`.

## Main Menu
**RTX ON > Build Main Menu Scene** membuat scene `MainMenu` dan menaruhnya paling atas di Scene List.

**Menu baru (update 2026-10-24, belum dites di Unity)**, dibuat dari desain Adrian (`rolling-sky-ui-unity.md` + `uicoba.pdf`). Semua dibuat lewat kode (uGUI, tanpa TextMeshPro dan DOTween; animasinya memakai `MenuTween.cs`).
- **Canvas**: HP tegak memakai 1080 x 1920 (ikut lebar). HP miring memakai 1920 x 1080 (ikut tinggi) dengan tata letaknya sendiri. Saat HP diputar, menu dibangun ulang di layar yang sama.
- **Home**: Background1 yang bergerak pelan, titik cahaya naik, logo yang masuk memantul lalu melayang, tulisan "REMASTERED", dan "KETUK UNTUK MAIN" yang berdenyut. Ketuk di mana saja untuk membuka Level Official. Di PC ada X kecil di pojok kanan atas untuk keluar.
- **Level Official / Level Custom**: kartu level (gambar, nama, bintang kesulitan, mahkota, gem terbaik, persen terbaik, dan bar progres). Kartu masuk satu per satu. HP tegak menampilkan 1 kolom, landscape 2 kolom. X di bar judul kembali ke Home. Di Level Custom ada tombol **+** untuk membuka Level Editor.
- **Detail**: ketuk kartu, gambarnya terbang ke banner besar. Isinya mahkota yang muncul satu per satu, persen dan gem yang menghitung naik, serta Tema / Rute / Musik di kanan bawah. **Ketuk banner untuk main** (tidak ada tombol Play). Gear membuka Settings, Kuas = Edit (khusus level custom). Di landscape banner ada di kiri dan info di kanan. **Latar Detail ikut tema level** (background tema official, atau gambar Background custom theme, kalau kosong pakai tema dasarnya); level tanpa tema atau dengan Background = No memakai Background1. Jalankan **RTX ON > Build Main Menu Scene** sekali supaya build juga tahu background tiap tema (di Unity Editor otomatis).
- **Navbar**: Home, Level Custom, Skin ("segera hadir"), Level Official, Settings. Indikatornya bergerak seperti jeli, ikon aktif berganti ke versi Filled. Navbar turun dan hilang di layar Detail.
- **Settings** (panel dari bawah): Audio, Display & Grafik, Sensitivity, Controller, Tombol Controller, Keyboard (PC), **Warna Menu** (Original, RTX Green, Sunset Sky, Teal Coral; bawaan RTX Green), dan **Bahasa** (Indonesia / English). Kontrolnya berupa toggle ON/OFF, pilihan bersegmen (sampai 3 pilihan) atau < pilihan > (lebih dari 3), dan slider bulat.
- **Rekor**: persen, gem, dan mahkota terbaik setiap level dari Main Menu disimpan di PlayerPrefs (`LevelProgress.cs`). Play test tidak dihitung.
- **Field komponen Main Menu**: **Home Background** (Build Main Menu Scene mengisi Background1), **Subtitle** (REMASTERED), **Menu Font** dan **Number Font** (boleh kosong; Selawik adalah pengganti gratis Segoe UI).
- **Tulisan gambar** (`Assets/UI/Resources/MenuText/`, putih dengan latar transparan): `Start` (ketuk untuk main), `LevelOfficial`, `CustomLevel`, `NewEditor`, `Settings`, `Remastered`, `Paused` (layar Pause), `Restart` (tombol Restart di Pause). Tambahkan `_EN` di akhir nama untuk versi bahasa Inggris. Kalau gambar tidak ada, menu memakai teks biasa. Lisensi font Kirsty hanya mengizinkan kata yang sudah jadi gambar, bukan huruf satu per satu.
- Tidak dibuat karena belum ada di game: Anti-aliasing, After Effect, Water Surface, dan Audio Delay Calibration. Kunci level (level terkunci) juga belum ada; semua level terbuka.
- Keyboard dan stik: Enter / A untuk memilih, Esc / B untuk kembali, panah atau D-pad untuk pindah kartu, Tab / LB / RB untuk pindah layar navbar.

Setting yang disimpan (`GameSettings.cs`; **RESET SEMUA** mengembalikan semua ke bawaan):
  - **Music** dan **Sound FX** 0-100 (dikali Sfx Volume / Music Volume di Game Audio).
  - **Menu Music**: lagu Main Menu yang diputar terus: **Massif** (bawaan), Sky, Forest, Desert, Volcano (Music1-5) atau Off. Ikut volume Music, masuk pelan dan memudar saat level atau editor dibuka. Build Main Menu Scene mengisi field **World Music**; di editor yang kosong dicari sendiri dari nama Music1-5.
  - **Camera**: **Normal** (kamera kejar RTX ON, bawaan), **Classic / High / Far** (kamera game asli, sama dengan preset camera trigger). Camera trigger di level tetap mengambil alih.
  - **Sensitivity**: angka, bawaan **400** (= x1), 100 sampai 1200. **Smooth**: On (bawaan, bola meluncur halus) / Off (langsung).
  - **Auto Move** (PC): Off (bawaan) / On. On = bola ikut gerak mouse tanpa tahan klik; kursor disembunyikan selama main dan muncul lagi saat Pause, pecah atau selesai.
  - **Steer With**: Stick (bawaan) / LT RT / LB RB.
  - **Controller Speed** (x0.25-x3) dan **Accelerator** (x1.0-x3.0, bawaan x2.0): selama A / Cross atau Spasi ditahan, belok kiri-kanan sekian kali lebih cepat. Kecepatan maju tidak berubah, jadi tetap pas dengan musik.
  - **Tombol Controller**: klik baris (atau pilih lalu A), lalu tekan tombol stik mana saja untuk aksi itu (Esc = batal). Kalau tombol itu sudah dipakai aksi lain, keduanya bertukar.
  - **Android**: Main Menu ikut posisi HP (portrait atau landscape), **Gameplay selalu portrait**, Level Editor selalu landscape. Kamera game: **Field Of View 60** di semua layar.
  - Esc di game atau selesai level kembali langsung ke daftar level.
Jalankan Build Main Menu Scene lagi setelah menambah level atau gambar banner.

Di Level Editor: **Load** membuka daftar level dengan banner (Level 1-5 lalu level buatanmu) dan kartu **NEW +** untuk membuat level baru (`Custom1.json`, dibuat saat Save). Tombol merah **Exit** (atau Esc) kembali ke Main Menu. Detail di LEVEL-EDITOR.md, bagian "Load: pilih level, NEW +".

## level.json
`Levels/Level1.json` … `Level5.json` sudah jadi (hasil konversi `Level.txt`). Pakai: objek **Game** > **Level File** = `Level1.json`.
Konversi ulang setelah edit di level editor: pilih `Level.txt` > **RTX ON > Convert Level.txt to level.json**. Yang diganti hanya `rows`; `name`, `music`, `speed`, `themes` dan `props` yang sudah kamu tulis tetap.

```json
{
  "name": "Massif",
  "team": "Cheetah Mobile",
  "author": "Adrian (Dude)",
  "difficulty": 5,
  "music": "Music1",
  "speed": 5.981,
  "speedEnd": 0,
  "speedRampRows": 0,
  "themes": [
    { "row": 0,   "preset": "Day",    "rows": 0 },
    { "row": 218, "preset": "Sunset", "rows": 40 },
    { "row": 373, "preset": "Night",  "rows": 40 }
  ],
  "props": [
    { "row": 30, "x": -4.5, "model": "monas", "rotY": 0, "scale": 1 }
  ],
  "rows": [
    "0,1,1,1,0",
    "1,1,1,1,1"
  ]
}
```
- `rows`: **baris pertama = awal level** (kebalikan Tiled). 5 angka per baris, ID tile sama seperti `PANDUAN-LEVEL.md`.
- `objects` dan `collect` (dari level editor baru): layer object (rintangan) dan collect (gem, crown, powerup), formatnya sama dengan `rows` dan hanya ditulis kalau ada isinya. Level lama dengan ID gabungan (mis. 14, 54) tetap bisa dibuka: otomatis dipecah ke tiga layer.
- `speed` baris/detik. `speedEnd` + `speedRampRows` = makin cepat (World 3–5 asli begitu). 0 = konstan.
- `music` = nama lagu: `Music1`…`Music5`, clip di folder `CustomMusic`, atau file lagu di folder Music (lihat Audio).
- `bpm` hanya untuk file lama: dibaca sebagai **speed = BPM / 15** (90 BPM = 6). Sejak 2026-10-22 editor tidak memakai BPM lagi; Save hanya menulis `speed` (maks **20**, `speedEnd` juga maks 20).
- `checkpoints` = baris checkpoint, mis. `[120, 260, 400]`. Kosong = tanpa checkpoint. **Crown terpisah**: tile **150** di `rows` (3 per level).
- `team`, `author`, `difficulty` (1-5, setengah boleh: 3.5; bawaan 5 = Very Hard), `banner`, `gems`, `crowns` = hanya tampil di Level List (gems/crowns kosong = dihitung). `invincible: true` = rintangan tidak membunuh bola.
- `props` = dekorasi tanpa collider di samping/atas lintasan. Lajur x = -2..2, samping lintasan ±3.5 ke luar.
- `parts` = setting PowerUp (160-168) dan tile animasi (170-177, 194-199) per sel, dari jendela ARGS-SETTING di editor, mis. `{ "row": 7, "lane": 2, "id": 165, "distance": 30, "multiple": 2, "musicOffset": true }`. Tanpa entri = nilai bawaan. Daftar lengkap di LEVEL-EDITOR.md, bagian "Parts".

## Model sendiri (FBX Indonesia, tekstur EnemyTexture)
Bisa. Model yang di-UV ke atlas EnemyTexture tinggal pakai satu material:
1. Taruh FBX dan `IndoensiaEnemy.png` di `Assets/`. Build Gameplay Scene otomatis membuat `Generated/M_ModelAtlas` dari tekstur yang namanya mengandung "Enemy" dan mengisinya ke `MassifTheme` > **Model Material**. Semua model rintangan dan props memakai material itu. Kalau ada beberapa tekstur "Enemy", yang namanya mengandung "Indo" dipilih duluan. Kalau FBX kamu sudah punya material sendiri: matikan centang **Auto Model Material**, lalu kosongkan **Model Material**.
2. Tekstur atlas: Inspector > **Filter Mode: Point** (atau Bilinear) dan **Generate Mip Maps: off**, supaya warna kotak-kotak di atlas tidak bocor ke sebelahnya dari jauh.
3. Rintangan: seret FBX ke slot di `MassifTheme`: Riser, Crusher, Floater, Binary, Pillar, Roller, Tree, Palm, Gem, Star, Floor, Fragile, Jump (Block = cadangan untuk semua blok). **Fit Models** menyesuaikan ukuran dan posisi otomatis, collider dan gameplay tetap sama. **Models Face Ball** (aktif) memutar model 180° supaya depan model dari Blender (tampilan Front, -Y) menghadap bola; matikan kalau modelmu sudah menghadap -Z.
   Contoh: GateIndoDown → Crusher, KerisGetout → Riser, Tiang1 → Pillar, Layang → Floater, Angklung → Binary, Ondel-Ondel → Tree.
   **Laser Model** dan **Spotlight Model**: isi dengan `16_enemyLaser` dan `17_enemySpotlight` (Assets/MESH/GameObject). Model asli dipasang di ukuran aslinya dan bergerak seperti game asli: laser membuka kelopak dan bergetar dari 15 baris, menembak di 5 baris lalu mundur; spotlight berdiri di ubinnya dan sinarnya berayun ±30° ke langit (menyala di 15 baris). Model lain (bukan asli) di-fit ke ukuran ubin.
   **Floater** dianimasikan seperti asli: 10 sampai 3 baris di depan bola dia naik dari lantai setinggi **2 blok** (pusatnya 2.2, setinggi puncak lompatan) sambil berbalik setengah putaran; model asli `07_enemyFloater` juga membuka keempat durinya dengan pantulan 2 sampai 1 baris di depan bola. Hitbox-nya 1 blok di sekeliling Floater dan ikut naik (`TrackBuilder.cs`: `FloaterHeight` = tinggi, `FloaterHitbox` = ukuran hitbox). Model asli otomatis diberdirikan (X 90); kalau terbalik, isi Model Adjust Rotation X 270.
   **Roller** dengan model asli `03_enemyRoller` berdiri seperti roda dengan mukanya menghadap bola, dan menggelinding (berputar di porosnya) waktu geser kiri-kanan.
4. **Daftar Models** (di tiap tema) menggantikan tiga daftar lama (Model Adjusts, Model Animations, Props). Satu baris per model; isinya yang lama dipindah otomatis saat tema dibuka. Tiap baris:
   - **Model**: FBX/prefab-nya.
   - **Prop**: centang = dekorasi tanpa collider, bisa dipakai di `"props"` level.json dan dipilih sebagai model Custom Obstacle (tab Props di Level Editor dihapus 2026-10-23).
   - **Adjust**: Rotation, Scale, Offset, Code Motion, Material, Keep Own Material (lihat poin 7).
   - **Animation**: **Clip** dari FBX terisi sendiri kalau FBX-nya punya animasi. **Clip kosong = tidak ada animasi**: berarti FBX-nya belum membawa clip (lihat di bawah). **Enabled** mati = tidak dimainkan. **Loop** (main terus, Speed 1 = seperti di Blender) atau **Approach** (main sekali saat bola mendekat: mulai Start Rows baris sebelum bola sampai, selesai setelah Play Rows baris). `ModelAnimator` memutarnya tanpa Animator Controller.
   Supaya FBX membawa clip: di Blender export FBX dengan **Bake Animation** aktif (dan All Actions kalau ada beberapa action). Di Unity pilih FBX-nya, tab **Rig** = Generic, tab **Animation** = **Import Animation** aktif, Apply. Clip-nya kelihatan sebagai segitiga kecil di dalam FBX (klik panah FBX di Project). **RTX ON > Find Model Animations** mengisi Clip yang masih kosong untuk semua model sekaligus.
5. Dekorasi (Monas, Komidi, FlagBendera, GateIndo, ...): pilih FBX-nya di Project > **RTX ON > Add Selected Models As Props** (atau tambah baris di Models dan centang Prop), lalu buat Custom Obstacle dengan model itu (Collision OFF) di Level Editor (atau tulis manual di `"props"` level.json; nama = nama file tanpa .fbx).
6. Tema lain (Sky, Forest, Desert, Volcano, Indonesia...): **RTX ON > New Theme From Current**, beri nama misalnya `IndonesiaTheme`. Tema baru adalah salinan tema sekarang dengan material sendiri, jadi warna Floor, Fragile dan lainnya bisa diubah tanpa mengubah tema lain. Isi slot modelnya (Riser Model dan lain-lain) dan atur **Environments** (langit, matahari, Fog Color, Fog Density). Lalu di level.json tulis `"look": "IndonesiaTheme"` (atau cukup `"Indonesia"`). Level tanpa `"look"` memakai tema Track seperti biasa. Daftar tema ada di Game > **Looks** dan diisi otomatis.
7. **Adjust** (di baris Models tiap tema): isi **Model**, buka **Adjust**, lalu **Rotation** (derajat, dipakai sebelum Fit Models; misalnya X -90 untuk membangunkan model yang tidur), **Scale** (1 = pas ruangnya) dan **Offset** (geser, dalam ubin). Berlaku hanya untuk tema itu, jadi model yang sama bisa diputar beda di tiap tema. Model asli Rolling Sky yang berwarna magenta memakai shader lama: isi **Model Material** di tema (rintangan), atau pilih materialnya lalu **Edit > Rendering > Materials > Convert Selected Built-in Materials to URP**. Model lama yang jadi ungu/salah warna saat Model Material diisi: isi **Material** di Adjust baris model itu dengan material URP Lit bertekstur enemy lama, atau centang **Keep Own Material**.
   **Code Motion**: centang = kode game yang menggerakkan model (naik, menghantam, berputar), cocok untuk model lama tanpa animasi. Tidak dicentang = model diam di ubinnya dan hanya animasi Blender-nya yang main (atur di Animation baris model itu, biasanya Approach); bahayanya tetap mengikuti waktu rintangan aslinya.

## Tile Type (lantai asli: X, T, L, I, D, O)

Lantai bisa memakai mesh lantai asli Rolling Sky, sama seperti game aslinya. Tiap ubin dipilih dari 15 mesh (`01_X` sampai `15_O`) menurut tetangganya: X = tersambung semua, T = satu sisi terbuka, L = dua sisi di pojok, I = dua sisi berseberangan, D = tiga sisi, O = sendirian. Angka di belakangnya = jumlah pojok yang terbuka. Mesh-nya diputar supaya dindingnya menghadap sisi yang terbuka. Fragile hanya tersambung dengan kelompoknya sendiri, slider hanya dengan slider di barisnya, jump pad dan tepi segmen memutus sambungan.

**Sisi ubin terpisah (khusus tampilan RTX)**: tema punya slot **Floor Side**. Isi dengan material lain (misalnya kayu gelap), maka atas ubin memakai **Floor** dan sisinya memakai **Floor Side**. Kalau kosong, sisi ikut Floor seperti sebelumnya. UV sisi: U 0..1 sepanjang ubin, V 0..1 dari atas ke bawah. Karena sisinya tipis, turunkan **Tiling Y** material sisi (misalnya 0,2) supaya teksturnya tidak gepeng. Ikut berganti saat look berganti dan menyala neon di malam hari kalau materialnya punya Emission.

1. Taruh folder `GameObject` (01_X .. 15_O, `07_jump`, `01_moveArrow`, `02_moveAutoArrow`) dan tekstur aslinya (`General1`, `Fragile1`, `FragileActive1`, `Mover1`, `MoverAuto1`, `Enemy1`, dan angka lainnya) di mana saja dalam `Assets`.
2. **Tile Set** = angka tekstur tema: **1 Massif, 2 Sky, 3 Forest, 4 Morning, 5 Volcano, 7 Desert, 9 Illusion** (General<n>, Fragile<n>, FragileActive<n>, Mover<n>, MoverAuto<n>, Enemy<n>). Kalau masih 1, menu ini mengisinya otomatis dari nama tema (nama mengandung "Sky" = 2, "Forest" = 3, dan seterusnya; "Volanco" juga dianggap Volcano). Angka yang kamu pilih sendiri (2 ke atas) tidak diubah. Midground ikut berganti ke set barunya.
3. **RTX ON > Hook Up Tile Types**. Menu ini mengisi slot Tile Type di semua tema dan membuat 5 material per tema di folder `<Tema> Tiles`: `M_TileGeneral`, `M_TileFragile`, `M_TileFragileActive`, `M_TileMover`, `M_TileMoverAuto`. Model Material ikut diganti ke `Enemy<angka>` (`M_TileEnemy`), kecuali tema yang memakai atlas sendiri (misalnya Indo). Slot model musuh laser yang masih kosong juga diisi model aslinya: **Pillar Model** `18_enemyPillar`, **Pillar Laser Model** `19_enemyPillarLaser` (tile 105), **Laser Model** `16_enemyLaser`, **Spotlight Model** `17_enemySpotlight`.
4. **Model Look** dan **Texture Look** di tiap tema (update 2026-10-23, dipilih terpisah, bisa dicampur; Tile Look lama otomatis jadi Texture Look):
   - **Model Classic** (bawaan): model asli: lantai 01_X..15_O, jump pad `07_jump`, panah mover asli, dan Pillar / Laser / Block Model biasa.
   - **Model RTX**: model 3D dari grup **RTX Models**: Floor, Fragile, Fragile Checker (kosong = Fragile), Jump, Mover (kosong = Floor), Mover Auto (kosong = Mover, lalu Floor), Pillar RTX, Laser RTX, Block RTX. Slot kosong = slab bawaan RTX ON (atau tile asli kalau Texture Classic) / model biasa. Kalau Pillar RTX atau Laser RTX diisi, pillar laser (105) jadi pillar itu dengan turret laser di atasnya. Panah mover di atas model RTX = panah datar menyala.
   - **Texture RTX** (bawaan): dicat material RTX tema (`Floor`, `Fragile`, `Mover`, `Mover Auto`, `Jump`, `Mover Arrow`): mengkilap, alur neon, memantulkan cahaya. Di tile asli memakai UV per kotak.
   - **Texture Classic**: dicat atlas asli (`M_Tile…`) seperti game lama, termasuk Fragile Active.
   - Model RTX dicat dengan material lantai sesuai Texture Look. Mau warna model sendiri: buka Adjust baris model itu di Models, centang **Keep Own Material**, atau isi **Material**.
   - Level Editor > Theme > **Model** dan **Texture**: Default (milik tema) / RTX / Classic, per level (`"themeSettings"`: `modelLook`, `textureLook`).
5. **Glow Classic**: Emission tiap material M_Tile = teksturnya sendiri dikali Emission Color (awal abu 0.35). Naikkan atau ganti warnanya di material itu. Neon Boost environment ikut mengalikan.

Slot RTX yang masih kosong (Floor, Fragile, Jump, Mover, Mover Auto, Mover Arrow) diisi menu ini dengan material buatan Build Gameplay Scene (`M_Floor`, `M_Fragile`, …). Mau warna lain per tema (misalnya Volcano merah)? Duplikat `M_Floor` (Ctrl+D), ganti Base Color dan Emission Color, lalu taruh di slot Floor tema itu. Kalau slot tetap kosong, kotak itu memakai atlas Classic. Menu ini juga menyalakan **Read/Write** pada mesh lantai asli, karena look RTX membuat UV baru per kotak saat game jalan (perlu untuk build).

Ganti Tile Set lalu jalankan menu lagi untuk menukar teksturnya; warna dan glow yang sudah kamu atur tetap. Di look Classic: Fragile berubah ke **Fragile Active** saat kelompoknya disentuh, jump pad asli menyala (state02) saat bola melompat, dan panah mover asli gelap (state1) lalu menyala (state2) saat platform bergerak. Kosongkan **Tile X** di tema untuk kembali ke lantai bawaan RTX ON. Level editor (tampilan 3D) juga memakai Tile Type.

## Glow musuh malam (T_EnemyGlow)
**Hook Up Tile Types** membuat `T_EnemyGlow<set>.png` di folder "<Tema> Tiles" dari atlas Enemy: hanya bagian terang berwarna (cincin dan garis neon) yang dipakai, sisanya hitam. Glow map ini dipasang sebagai Emission Map di **M_TileEnemy** (Emission HDR 2.5), terpisah dari warnanya, seperti T_FragileGlow. Siang mati, sore mulai nyala, malam penuh. Mau atur sendiri bagian yang menyala? Edit PNG itu di Photoshop (hitam = mati, warna = nyala); file yang sudah ada tidak ditimpa. Atlas sendiri (Indo…) tidak disentuh, tapi kalau materialnya punya Emission Map, glow-nya ikut hanya malam.

## Material Finish, Portal, Pillar dan Block
**Portal** memakai dua material: bagian bulat (`portals`) pakai Model Material (`M_TileEnemy`, ikut glow malam), sedangkan bingkai luar (`portalOuter`: Middle, Outer, Bracket L/R) pakai material lantai (**Floor** `M_Floor` di look RTX, **Tile General** `M_TileGeneral` di Classic). Block tembok Finish (`anim` di 03_finish, block1-42) pakai **Tile Fragile Active** (`M_TileFragileActive`, atlas FragileActive<Tile Set>); rangka/pillar-nya (`HighscorePillars`) juga pakai material lantai (`M_Floor` di RTX, `M_TileGeneral` di Classic). Kalau Adjust model diisi Material, seluruh model pakai material itu. Original 03_finish dan 04_portal yang root-nya tanpa putaran otomatis didirikan (X 90), jadi Finish tidak terbalik lagi. 03_finish juga digeser 0,5 ke atas dan 0,5 ke belakang, sama seperti posisi tembok finish di game asli.

### Animasi Finish dan Portal (port dari game asli)
- **Portal akhir** (`PortalRig`): 289 potongan `PortalGeo` membentuk 9 cincin isi 32. Tiap cincin naik turun bergelombang sekali per detik (PortalAnimator, state Active), jadi terlihat seperti terowongan berdenyut. `portalStatic` disembunyikan dan `portalInner` (dial, khusus portal awal) juga disembunyikan. Model tanpa PortalGeo memutar `portalActiveStatic`.
- **Finish** (`FinishWall`): 42 `block` menunggu dalam tampilan state2. Saat bola menyentuh tembok, semua block berganti ke state1 (menyala), lalu melesat ke depan 5/detik sambil menyebar menjauhi bola. Block naik satu per satu: yang paling dekat bola duluan, baris atas sedikit lebih awal. Setelah 4 detik semuanya hilang (GoalWall asli). Kalau lanjut dari checkpoint sebelum finish, temboknya kembali utuh.
- Keduanya dipasang otomatis saat Play. Model custom tanpa bagian-bagian itu tidak diberi animasi.

### Start Portal (Level Settings)
Centang **Start Portal** di Level Settings (dulu "Portal, Coming Soon"), disimpan sebagai `"startPortal": true` di json level.
- **ON:** level mulai di atas portal (Portal Model tema, 04_portal) yang tidur di lantai, ukuran 0,75, tanpa Bracket L/R. Portalnya menyala dan bergelombang sambil menunggu tap. Saat tap pertama, bola langsung lompat keluar (4 baris, tinggi 1,6; atur di GameManager > Start Portal Jump Rows / Height), lalu portalnya padam.
- **OFF:** mulai biasa, tanpa portal dan tanpa lompat.
- Hanya untuk start dari awal level. Test dari Start Pos dan lanjut dari checkpoint tidak memakai portal.
Model **Finish**, **Pillar**, **Pillar Laser** dan **Block** memakai material lantai, bukan Model Material (`M_TileEnemy`): **Floor** (`M_Floor`) di look RTX, **Tile General** (`M_TileGeneral`) di look Classic. Kalau salah satunya kosong, yang lain dipakai. Bagian laser di blok `19_enemyPillarLaser` (yang aslinya memakai `5_Enemy`) memakai Model Material, jadi tekstur Enemy, sedangkan bagian pillar-nya tekstur General. Sinar merah pillar laser tetap Beam Red. Mau material lain untuk satu model: isi **Material** di Adjust baris model itu.

## Background per tema
Tiap tema punya slot **Background** (tekstur, misalnya `Background1`…`Background8` dari `Assets/MESH/Texture2D`), plus **Background Tint** (warna/terang) dan **Background Y** (geser naik/turun). Gambar ditempel di kamera di ujung pandangan, setinggi layar tanpa ditarik (update 2026-10-24): portrait penuh atas-bawah (kiri-kanan gambar terpotong), landscape menyisakan kiri dan kanan yang menampilkan langit. Hanya langit yang tertutup gambar; lintasan, fog dan rintangan tetap di depan. Kalau level ganti look, background ikut memudar ke gambar look berikutnya. Slot kosong = langit dari Environments seperti biasa. **RTX ON > Hook Up Tile Types** mengisi slot kosong dengan `Background<Tile Set>` (Massif 1, Volcano 5, dan seterusnya). 3D view di level editor juga menampilkannya.

Beam lampu sorot dan laser (shader `RTXOn/Beam`) memudar kalau dekat kamera (Near Fade Start 1.5 dan End 5 di material), jadi beam yang lewat di depan kamera tidak lagi membuat layar kedip putih.

Sinar laser (turret dan pillar laser) lebarnya 0.75 ubin, untuk tampilan maupun area mematikannya (`TrackBuilder.LaserWidth`), jadi bola yang sedikit keluar dari tengah lajur tetap kena.
Tips: samakan **Fog Color** di Environment dengan warna bawah gambar, supaya ujung lintasan menyatu dengan background.

## Jalur diagonal (collider lantai 1,25)
Collider tiap tile lantai sekarang **1,25 x 1,25** (Track Builder > **Floor Collider Size**, 1 = pas satu tile). Tile yang hanya bersentuhan di sudut (jalur diagonal, misalnya Slide Left 19 bertangga) jadi saling menempel, jadi bola bisa lewat diagonal kalau kamu belok tepat waktu. Lubang satu tile tetap menjatuhkan bola. Jump pad tetap 1 x 1 supaya bola yang lewat di sampingnya tidak ikut terlempar.

## Midground dan Mid Fog per tema
Seperti game asli (MidgroundManager): tiap baris dapat satu balok besar di kiri atau kanan lintasan (atau kosong), muncul jauh di bawah lalu **naik** saat kamera mendekat, sampai puncaknya **Midground Height** di bawah lintasan. Model dari slot **Midground Models** tema (`midground1a`, `2a`, `3L/3R`, `4L/4R` dari `Assets/MESH/GameObject`). Teksturnya dari **Tile Fragile** (`M_TileFragile`, atlas Fragile yang dipakai midground asli), tanpa cahaya, dan memudar ke warna **Mid Fog Color** antara **Mid Fog Start** dan **Mid Fog End** (jarak dari kamera; asli kira-kira 17 dan 30, warna cyan). Mid fog ini terpisah dari fog scene. **Midground Material** diisi = material itu dipakai apa adanya (tanpa mid fog). Pengaturan lain: **Midground Tint**, **Rise** (seberapa dalam mulai naik), **Min X / Max X** (jarak ke samping), **Blank** (0 = tiap baris ada balok, makin besar makin jarang). Tiap baris memakai look baris itu, jadi ganti look = balok look baru. **RTX ON > Hook Up Tile Types** mengisi daftar yang kosong dengan midground Tile Set-nya beserta angka aslinya (Massif: 5 / 15 / 0..12 / 4). Balok tetap sama tiap restart dan checkpoint. Belum tampil di 3D view editor, hanya saat Play.

## Warna bola per tema
**Ball Color** di tema (dekat slot Ball): warna bola selama look itu aktif, memudar bersama ganti look. Alpha 0 = warna material Ball sendiri (bawaan). Pecahan bola saat mati ikut warnanya. Karakter pengganti bola tidak diwarnai.

## Binary: Mini, Slasher, Hammer
Slot di tema sekarang berurutan **Binary Mini Model** (`06_enemyBinaryMini`), **Binary Slasher Model** (`05_enemySlasher`, dulu namanya Binary Model; isinya tetap terbawa) dan **Binary Hammer Model** (`04_enemyHammer`).
Tile 61-64/73-76 = Mini, 65-68/77-80 = Slasher (tidak acak lagi), **190-193 = Hammer** (190 kiri, 191 kanan, 192/193 offset). Slot yang kosong memakai model Binary lain sebagai cadangan.

## Efek layar (Screen FX)
Trigger **Screen FX** di tab Triggers Level Editor: Glitch, Grayscale, Negative, Chromatic, Scan Lines, Hue Shift. Digambar oleh **Screen Filter Feature** (Renderer Feature URP) dengan shader `Shaders/Resources/RTXOnScreenFilters.shader`. Feature itu otomatis ditambahkan ke semua renderer URP (PC_Renderer dll) saat script selesai compile; kalau tidak, jalankan **RTX ON > Hook Up Screen Filters**, atau manual: pilih asset renderer > Add Renderer Feature > Screen Filter Feature. Efek hanya kena kamera game (dan 3D view editor saat preview), menu dan UI tetap bersih.

## Fragile tembus pandang
Material Unity defaultnya **Opaque**, jadi alpha di tekstur diabaikan. Jalankan **RTX ON > Fragile See-Through**: semua material fragile di semua tema (Fragile, Fragile 1/2, Checker, Row, Column, Tile Fragile, Tile Fragile Active) jadi **Surface Type: Transparent**. Bagian tembusnya dari alpha tekstur dikali alpha **Base Color**; tekstur tanpa alpha tetap padat, turunkan alpha Base Color-nya. **RTX ON > Fragile Solid** mengembalikan ke Opaque. Hook Up Tile Types otomatis membuat Tile Fragile / Fragile Active transparan kalau teksturnya punya alpha. Bisa juga manual: pilih materialnya, Surface Type = Transparent.

## Kaca realistis (M_FragileRTX)
Menu **RTX ON > Make Fragile Glass (M_FragileRTX)** membuat 3 material kaca yang sama di `Assets/RTXOn/Materials` (shader `RTXOn/Glass`) dan mengisi slotnya di semua tema:
- **M_FragileRTX** untuk Fragile, Fragile 1/2, Row, Column: garis menyala hanya di keliling section, jadi section yang jatuh bareng kelihatan satu lempeng.
- **M_FragileRTXChecker** untuk Checker: tile Checker tidak pernah gabung, jadi tiap tile = 1 blok dengan garis sendiri.
- **M_FragileRTXSide** di slot baru **Fragile Side** (Track Theme): sisi (tebal) semua tile fragile, Full dan Checker. Kosong = sisi pakai material fragile-nya.
Menu juga menyalakan **Opaque Texture** di URP asset (wajib, tanpa itu kaca abu-abu). Kelihatan di Tile Look = RTX. Setelan material yang sudah ada tidak ditimpa (hanya Section Outline dinyalakan). Reset Look mengembalikan fragile lama. Emission Map (retakan) digambar per tile; kosongkan kalau mau lempengnya polos.
Isi material: Tint (warna kaca), Thickness (makin tebal makin pekat), Smoothness, Index Of Refraction (kaca 1.5, berlian 2.4), Refraction Strength (seberapa bengkok), Rainbow Edges (pelangi tipis di tepi), Frost (kaca buram/sandblast), Reflection Strength, Sun Highlight, Shadow On Glass, Normal Map (riak/goresan), Side Colour (sisi tebal kehijauan), Section Outline (garis keliling section), Line Colour/Width/Softness, Side Glow, Emission Map (retakan menyala malam).

## Shadows per tema
Di Inspector tiap Theme ada centang **Shadows** (bawah Tile Look). Nyala = matahari/bulan bikin bayangan realtime (gaya RTX ON). Mati = rata seperti Rolling Sky asli yang tidak punya shadow. SSAO (bayangan halus di sudut) tetap jalan. Berlaku untuk tema yang sedang dilewati bola, jadi kalau level ganti look di tengah jalan, shadow ikut berganti. 3D view di level editor juga ikut.

## Karakter pengganti bola (Idle, Run, Jump, Crash)

Bola bisa diganti karakter yang sudah di-rig (Humanoid), misalnya CuteBeard. Bola tetap yang mengatur semuanya (setir, lompat, kena musuh); karakternya hanya tampilan.

1. **Animasi dari Mixamo**: pilih animasi lalu Download **FBX for Unity, Without Skin**. Untuk Run, centang **In Place**. Beri nama file yang jelas: `Idle.fbx`, `Run.fbx`, `Jump.fbx`, `Crash.fbx` (atau `Death.fbx`). Taruh semuanya di satu folder, misalnya `Assets/MESH/CharacterAnim`.
2. Pilih folder itu di Project, lalu **RTX ON > Build Character Animator**. Menu ini:
   - mengubah FBX animasi jadi Humanoid, Idle dan Run diulang (Loop Time),
   - membuat `CharacterAnimator.controller` di folder itu: Idle, Run saat bola jalan, Jump tiap lompat, kembali Run saat mendarat, Crash saat mati,
   - menambah komponen **Ball Character** di objek Ball (scene Gameplay yang terbuka) dan memasang controller-nya.
3. Di **Ball > Ball Character**, isi **Model** dengan FBX karakter (misalnya Meshy_AI_Character_output). Atur **Height** (tinggi dalam ukuran tile, awal 0.9), **Yaw** kalau menghadap salah, dan **Offset**. Simpan scene.

Parameter Animator (kalau mau membuat controller sendiri): `Speed` (float, baris/detik, 0 sebelum tap), `Grounded` (bool), `Jump` (trigger), `Crash` (trigger). Saat mati, bola tidak pecah; karakternya memainkan Crash. Power-up Invisible ikut menyembunyikan karakter. Build Gameplay Scene menyimpan pengaturan Ball Character.

## Lantai bergerak (Mover Sentuh dan Auto Mover)
Tile 37–48 dan 57–60 sekarang bergerak seperti di game asli. Level 2–5 memakainya.
- **Mover Sentuh** (37 atas, 38 bawah, 39 kiri, 40 kanan): lantai **ungu** dengan panah putih. Saat bola menyentuh panahnya, seluruh platform tempat panah itu berada bergeser 1 kotak ke arah panah, lalu memantul sedikit.
- **Auto Mover** (41–48, 57–60): lantai **hijau**. Bergeser sendiri saat platform lain yang bergerak berhenti di sebelahnya. Bisa berantai, tiap mata rantai 0,7 baris lebih lambat. 45–48 bergerak sendirian.
- Platform = panah + semua lantai biasa yang menempel (atas, bawah, kiri, kanan). Lubang, jump, lantai rapuh, lantai geser, tile Edge (69–108) dan penanda segmen (109–112) membatasinya.
- Kotak tujuan bisa dipijak begitu platform mulai bergeser; kotak yang ditinggalkan jadi lubang setelah geser selesai.
- Warna: **RTX ON > Build Gameplay Scene** membuat `M_Mover` (ungu), `M_MoverAuto` (hijau) dan `M_MoverArrow` (panah) lalu mengisinya ke `MassifTheme`. Sebelum itu, game memakai warna cadangan.

## Level 1-5 dan level baru (update 2026-10-20)
- Menu **RTX ON > Apply Level 1-5 Settings** menulis setting ini ke `Level1.json` ... `Level5.json` di project-mu. Hanya header yang diganti; tile, object, collect, tema, kamera, checkpoint dan parts tetap.

| Level | Nama | Musik | Speed | Gems | Bintang |
|---|---|---|---|---|---|
| 1 | Massif | Massif | 7.5 | 20 | 1.3 |
| 2 | Sky | Sky | 8 | 20 | 2 |
| 3 | Forest | Forest | 8.5 | 20 | 3 |
| 4 | Dessert | Dessert | 9 | 20 | 3.3 |
| 5 | Volcano | Volcano | 10 | 20 | 4.2 |

  Speed dibuat konstan (End Speed dan Speed Max Point dikosongkan). Bintang sekarang bisa per 0,1.
- Nama musik **Massif / Sky / Forest / Desert (Dessert) / Volcano**: kalau tidak ada lagu dengan nama persis itu (Custom Music atau folder Music), dipakai **Music1 ... Music5**.
- **NEW +** (Custom Level): speed 7, musik Massif, target 10 gems, 5 bintang, 120 baris siap digambar.
- Preset kamera (Camera trigger): **Default** 0 / 4.5 / -1.46, **High** 0 / 5.84 / -2.85, **Far** 0 / 5.95 / -1.78, semua Rotation X 40, Offset 0, Curve -0.9.

## Theme di Level Editor (update 2026-10-22)
Tombol **Theme** di Level Editor membuka Theme Settings (theme level dengan tab Official / Fanmade / Custom, Offset, Gradient, Roller, Block, Mover Edge, Midground, preview 3D) dan Custom Theme (tekstur Classic atau material RTX ON, Glow Tile dan Glow Enemy terpisah, ColorSets dengan Environment dan Use Fog). File baru: `.themert` (theme), `.rsrtp` (object pack), `.levelrsrt` (level lengkap). Material RTX ON untuk custom theme taruh di `Assets/Resources/RTXOnMaterials`. TrackTheme juga punya **Background Gradient** dan **Mover Edge** di Inspector. Detail di LEVEL-EDITOR.md. Belum dites di Unity.

## Level Editor: Edit, Copy, Collection, Trigger dan Trigger Object (update 2026-10-22, belum dites di Unity)
- **BPM dihapus**: hanya **Speed** (kotak + slider 1-20). File lama yang punya `bpm` tetap terbaca (speed = BPM / 15) dan disimpan sebagai `speed`.
- **Edit** (tombol bawah palet, atau E): klik sel = jendela detail sel (Tile, Object, Collect + semua nilai ARGS). **ARGS** mengubah sel itu saja, **Pick** jadi kuas, **Copy cell**.
- **Copy** (atau C): klik sel = disalin (tile + object + collect + ARGS) dan langsung terpilih; tiap klik menggambar sel yang sama.
- **Bintang** di kanan kotak SELECTED = favorit; tab **Collection** berisi semua favorit dari tab mana pun.
- Baru: **157 Custom move tile**, **158 Custom move block**, **159 CustomTriggerTiles**, **169 Trigger Object**, dan 7 tile animasi (145-149, 178, 179). Detail di LEVEL-EDITOR.md. Model sendiri untuk 169: TrackTheme > **Trigger Object Model** + **Trigger Object Clip** (kosong = klip dari Models). Custom Obstacle (200-999) juga bisa diberi **Group** lewat Edit (E) > ARGS, jadi diam sampai trigger. Model dengan klip Blender sekarang punya collision yang ikut animasinya (satu kotak per bagian mesh; F1 menampilkannya).

## Autosave (update 2026-10-22)
Level Editor menyimpan perubahan yang belum di-Save ke `Application.persistentDataPath/Autosave/` setiap 15 detik, dan juga saat editor berhenti (Exit, Test, Play dihentikan, atau Unity mengompilasi ulang script saat Play). Saat level itu dibuka lagi, perubahannya kembali otomatis, dengan pesan "Restored your unsaved changes". **Save** menyimpannya ke file. Untuk membuangnya, buka level itu sekali lagi dari Load. Exit tanpa Save (tekan dua kali) juga menghapus autosave.
SpriteMaker juga menyimpan semua warna, lapisan dan palette ke `persistentDataPath/SpriteMaker/Autosave.json` (format project), lalu memuatnya lagi saat dibuka.
Saran Unity: **Edit > Preferences > General > Script Changes While Playing = Recompile After Finished Playing**, supaya script yang berubah saat Play tidak merusak sesi Play yang sedang berjalan.

## SpriteMaker (update 2026-10-22)
Di jendela Custom Theme, baris tekstur General, Fragile, FragileActive, Mover, MoverAuto, Enemy dan Glow-nya punya tombol **Make...** yang membuka **SpriteMaker**: atlas Rolling Sky dari 387 mask putih (`assets.zip`), diwarnai per lapisan, bawaan Massif/Default, lalu **Use** memasangnya ke Custom Theme atau **Save PNG**. Bisa membuat **glow map** juga. Mask ada di `Assets/SpriteMaker/Resources/SpriteMaker` sebagai `.bytes` (PNG yang diganti akhirannya) supaya ikut build dengan kecil. Menu baru: **RTX ON > SpriteMaker > Update Mask List**. Panduan: `SpriteMaker/README.md`. Belum dites di Unity.

Update 2026-10-22 (2): pemilih warna jadi seperti Photoshop (kotak S/B + batang Hue, H S B / R G B / Hex yang bisa diketik, **Eyedropper** dari layar, **Recent**). Warnanya **realtime**: langsung terpakai saat digeser, Cancel/Esc mengembalikan warna awal. Ini berlaku di semua jendela yang memakai tombol Edit warna. SpriteMaker punya tombol **3D**: `LevelTexturePreview.json` (di `SpriteMaker/Resources/SpriteMaker`) digambar dengan atlas yang sedang dibuat. Belum dites di Unity.

## Grafis, Pause, Complete, Gem dan Build PC / Android (update 2026-10-23, belum dites di Unity)

- **Settings > Graphics > Quality**: PC punya **Low, Medium, High, Ultra, RTX** (bawaan RTX). Android punya **Low, Medium, High, Ultra, Ultra+Shadow** (bawaan High, tanpa RTX).
  - Low: tanpa bayangan, tanpa bloom, tanpa pantulan bola, tanpa anti-aliasing.
  - Medium: bloom dan FXAA, pantulan bola kecil yang jarang diperbarui.
  - High: bayangan keras, SMAA, dan SSAO (khusus PC).
  - Ultra / RTX: bayangan halus, pantulan bola setiap frame, dan bloom kualitas tinggi.
  - Di game hasil build, Low dan Medium juga menurunkan render scale. Di editor Unity, aset URP tidak diubah supaya project aman.
- **Settings > Graphics > Bloom**: AUTO (ikut Quality, menyala dari Medium), ON, atau OFF. **Bloom Strength** 0-200% (100% = sesuai environment).
- **Settings > Graphics > Upscaler** (khusus PC): OFF, **FSR** (AMD FSR 1) atau **STP** (upscaler temporal Unity), dengan **Upscale Mode** Quality 77%, Balanced 67%, Performance 50%. Game merender lebih sedikit piksel lalu memperbesarnya, jadi FPS lebih tinggi. DLSS tidak ada karena URP tidak menyediakannya. Hanya terlihat di game hasil build (aset URP tidak diubah di editor).
- **Android Ultra+Shadow**: seperti Ultra, ditambah bayangan keras yang hanya sampai sekitar **10 baris** di depan bola (1 cascade, shadow map 1024). Kalau di HP bayangannya tetap tidak muncul, buka URP Asset yang dipakai Android (Project Settings > Quality, level Android) dan nyalakan **Lighting > Main Light > Cast Shadows**, supaya shader bayangan ikut masuk build.
- **Android**: selain Ultra+Shadow tidak ada bayangan, dan Dynamic Loading selalu ON dengan maksimal **50** baris (juga di Level Editor). Editor Unity dengan build target Android ikut memakai aturan ini, jadi bisa dicoba di sana.
- **Pause**: tombol II (kiri atas), P, atau Options. Setelah **RESUME** ada hitungan **3-2-1** sebelum bola jalan lagi. Pause lagi saat hitungan = kembali ke menu Pause.
- **Complete**: ada satu ikon crown untuk setiap crown di level. Crown yang didapat berwarna emas, yang tidak didapat kosong (0 crown = semua kosong). Level tanpa crown tidak menampilkan baris crown. Gem tampil dengan ikon dan jumlahnya (0 gem = ikon kosong).
- **Gem asli**: jalankan **RTX ON > Hook Up Tile Types** sekali. Slot Gem Model dan Star Model yang masih kosong di setiap tema akan diisi **01_pickup** dan **02_star** (MESH/GameObject). Gem dan star memakai **M_Fragile asli** (slot baru **Pickup Material** di tema, diisi otomatis oleh Hook Up Tile Types), bukan kaca M_FragileRTX. Kalau slot itu kosong, yang dipakai material gem / star.
- **Musik Main Menu**: kalau slot World Music di Main Menu kosong, lagu diambil dari **StreamingAssets/Music** (Massif, Sky, Forest, Dessert/Desert, Volanco/Volcano, atau Music1..5). Setiap build menulis `StreamingAssets/Music/index.txt` (daftar lagu) secara otomatis, karena di Android folder itu ada di dalam APK dan tidak bisa dibaca isinya. Bisa juga lewat menu **RTX ON > Update Music List**.
- **Custom Theme di build** (2026-10-24): setiap build menyalin semua `.themert` di `Levels/Themes` ke `Assets/RTXOn/Resources/RTXOnThemes` (sebagai `.bytes`), jadi Android punya semua theme (Illusion, ForestTheme, SkyTheme2, IndonesiaBest, dan yang baru). Bisa juga lewat menu **RTX ON > Update Bundled Themes**. Theme yang dihapus dari `Levels/Themes` ikut dihapus dari situ.
- **Icon UI** (Adrian 2026-10-23): semua gambar ada di `UI/Resources/Icons` (Play, Close, Trash, Settings, Sun, Camera, Music, Map, Layer, Brush, Slider, Star, Gem, Crown, CheckPoint, StartButton, NoWorld, Xbox A/B/X/LB/RB/LT/RT/D-pad/Stick...). `Editor/IconImport.cs` otomatis mengimpornya sebagai **Sprite** (kalau ada yang masih Texture biasa: **RTX ON > Reimport UI Icons**). Kode memanggilnya dengan `UiFactory.Icon("Nama")`; icon yang tidak ada = tombol tetap pakai teks. Ganti gambar = timpa PNG dengan nama yang sama.
  - **Main Menu**: PLAY / SETTINGS / QUIT, BACK (panah), tab Official (bintang) / My Custom (kuas) / Level Editor (layer), Play dan Edit, gem dan crown di daftar, banner kosong = gambar NoWorld, judul bagian Settings (Audio, Sun, Camera, Slider, stik, Xbox A, keyboard), RESET ALL (tong sampah), dan gambar tombol Xbox di Controller Buttons.
  - **Gameplay**: tombol Pause, icon gem dan crown di penghitung atas, gambar Start Button di layar "Tap to start", badge **Check Point** saat lewat checkpoint, Complete dengan gem merah dan crown emas, menu Pause (Resume, Back, stik).
  - **Level Editor**: Load (peta), Save (centang), Music Play (not musik, jadi Pause saat diputar), Play, 3D (kamera, jadi peta saat 3D), Theme (matahari), Exit dan semua tombol X (Close), Erase Tile / Erase Object (tong sampah), Edit (kursor teks), + custom obstacle dan NEW + (Add), Checkpoint (badge), bintang favorit (garis = bukan favorit), LEVEL SETTINGS (slider).
  - Belum dipakai: GameUI-resources.png (atlas, perlu dipotong dulu), Crownbox, Search, Eye / Eye Off, Modal Window, Warning. `UI/AppIcon.png` (bola RS) bisa dipasang di Player Settings > Icon.
- **Sebelum build**:
  1. Jalankan Build Gameplay Scene, Build Main Menu Scene, Open Level Editor Scene, dan Hook Up Tile Types. Main Menu harus nomor 0 di Build Settings.
  2. Android: Player Settings > IL2CPP + ARM64, dan Default Orientation **Auto Rotation** (centang Portrait, Landscape Right, Landscape Left). Game mengatur sendiri: Main Menu bebas, Gameplay portrait, Level Editor landscape.
  3. Di HP, file untuk Import (level, musik, tekstur) ada di `Android/data/<nama paket>/files/`.
  4. Android dan musik: build otomatis menambah izin **READ_MEDIA_AUDIO** / **READ_EXTERNAL_STORAGE** (`Editor/AndroidMusicPermission.cs`), supaya Level Settings > Music > **Choose File...** bisa membaca lagu dari folder Music / Download HP. Izin diminta saat browser file pertama kali dibuka.

## Level Editor
**RTX ON > Open Level Editor Scene**, set Game view ke 16:9, lalu Play. Kalau objek Level Editor di scene terhapus, jalankan menu ini lagi: objeknya dipasang kembali. Gambar level di grid, **Save** menulis `Level1.json`. Panduan dan penjelasan tiap script ada di `LEVEL-EDITOR.md`.

Di editor: **Music Play** (Space/P) memutar lagu level **di dalam editor**: grid ikut bergulir sesuai lagu dan garis kuning menandai posisi bola; tekan lagi untuk berhenti. **Play** (T, dulu Test) memainkan level di scene Gameplay mulai dari baris paling bawah di layar; **Esc** kembali ke editor, edit dan Undo tetap ada. Di kiri nomor baris ada **visual musik** (keras-pelan lagu per baris). Panel kanan: Music `< >`, **Speed** (ketik atau geser slider 1-20), jumlah Crown/Gem/Checkpoint. Editor punya 3 layer per sel: tab **Tiles** (lantai), **Collect** (gem, crown, powerup, Checkpoint, 110 Finish, 113 Portal) dan **Object** (semua rintangan termasuk Pillar). Object tampil di depan tile; lihat LEVEL-EDITOR.md bagian "Layer".

## Audio
Taruh folder `AudioClip` dari game lama di dalam `Assets/`, lalu **RTX ON > Hook Up Audio (open scene)** (Build Gameplay Scene juga memasang audio).

| Clip | Dipakai untuk |
|---|---|
| `Music1`…`Music5` | Musik level (mulai saat tap, fade saat mati/selesai) |
| `02_Jump` / `03_Crash` / `04_Pickup` | Lompat / bola pecah / gem |
| `05_Fragile` | Lantai rapuh yang baru dilewati bola |
| `06_Boom` | Crusher menghantam |
| `07_Slasher Woosh` | Binary, roller, spotlight |
| `08_Laser` | Laser menembak (5 baris) |
| `09_Explosion` | Ledakan tepat setelah laser menembak (4.5 baris), slot **Explosion** |
| `10_Movement1` / `11_Movement2` | Riser dan Tree (termasuk Tree Variants) naik / lantai geser |

Kecepatan World 1 = **5.98 baris/detik** (dari `LevelDesigner.cs` asli) supaya Music1 pas.

**Lagu sendiri**, dua cara:
1. Clip di Unity: taruh di folder bernama `CustomMusic` (di mana saja dalam Assets), lalu Hook Up Audio dan Open Level Editor Scene lagi. Pilih di editor (Music `< >`) atau tulis namanya di `"music"`.
2. File `.ogg` / `.mp3` / `.wav` di `Assets/StreamingAssets/Music/` (ikut ter-build) atau di folder data aplikasi `persistentDataPath/Music/` (bisa ditambah pemain di HP/PC). Dimuat saat main, waktu loading-nya dilewati supaya tetap pas ketukan.

**Suara checkpoint / crown**: clip bernama `Checkpoint…` / `Crown…` dipasang otomatis oleh Hook Up Audio. Hook Up Audio tidak lagi mengosongkan slot yang sudah kamu isi sendiri kalau clip dengan nama itu tidak ada. Kalau tidak ada, game memakai bunyi "ting" buatan sendiri (checkpoint C-E-G-C, crown dua nada tinggi).

**Suara power-up**: Game Audio punya bagian **Power-ups**: **Power Up** (Fly Jump, Short Jump, Bounce, dan cadangan untuk yang lain), **Speed Up**, **Speed Down**, **Switch Lanes**, **Invisible**. Clip bernama `PowerUp…`, `SpeedUp…`, `SpeedDown…`, `Switch…`, `Invisible…` dipasang otomatis oleh Hook Up Audio. Slot kosong memakai suara Power Up; kalau Power Up juga kosong, tiap power-up punya bunyi "ting" buatan sendiri (speed up naik, speed down turun).

## Isi
| File | Fungsi |
|---|---|
| `Scripts/LevelData.cs` | Baca `Level.txt` (layer Tiled Flare) atau `level.json`. |
| `Scripts/TileCatalog.cs` | Peta ID tile lama > lantai + rintangan. |
| `Scripts/TrackBuilder.cs` | Bangun lintasan per baris di depan bola, model custom + auto-fit, props. |
| `Scripts/CustomObstacle.cs` | Data Custom Obstacle (file `.rsrt`, ID 200-999) dan daftarnya (library `Levels/Obstacles`). |
| `Scripts/CustomObstacleBuilder.cs`, `CustomObstacleMotion.cs` | Membangun custom obstacle (model, hitbox, suara) dan animasinya. |
| `Scripts/ObjMesh.cs` | Membaca file OBJ saat game berjalan (model custom obstacle dari file). |
| `Scripts/CustomModelLibrary.cs`, `Editor/CustomModelsMenu.cs` | Daftar model project untuk custom obstacle (RTX ON > Add Selected Models As Custom Models). |
| `Scripts/LevelEditor/CustomObstaclePanel.cs` | Jendela CUSTOM OBSTACLES di Level Editor. |
| `Scripts/EnvironmentDirector.cs`, `EnvironmentPreset.cs` | Ganti tema: matahari, langit, kabut, ambient, neon, bloom. |
| `Scripts/MoverGrid.cs`, `MoverDirector.cs` | Aturan dan animasi lantai bergerak (mover sentuh dan auto). |
| `Scripts/BallController.cs`, `CameraRig.cs` | Bola kinematik dan kamera kejar. |
| `Scripts/GameManager.cs`, `Hud.cs`, `GameAudio.cs` | Alur game, HUD, musik dan efek suara. |
| Rintangan | `FragileTile` + `FragileSections` (lantai rapuh yang bersambung jadi satu kelompok dan pecah bersama saat salah satunya disentuh, seperti level Volcano asli: bola menggelinding biasa tanpa goyang, saat menyentuh kelompok itu satu row penuh yang disentuh (5 lajur) tetap jadi lantai aman dan semua tile lain di kelompok itu langsung jatuh; row itu menahan bola sampai bola keluar dari row itu (maju ke row kedua dari kelompok yang sama = jatuh), collider jadi trigger, kaca pecah dan jatuh selama 4 baris: Checker selalu sendiri, Row bersambung dalam satu baris, Column dalam satu lajur, Fragile 0/1/2 hanya dengan nomor yang sama), `ProximityMover` (riser), `CrusherRig`, `PillarRig`, `SliderFloor` (lantai geser kiri/kanan berayun bolak-balik terus, satu ayunan penuh tiap 20 baris; semua lantai geser dalam satu baris bergerak bersama sebagai satu potongan), `Roller` (menggelinding bolak-balik terus dengan ayunan yang sama), `BinarySpinner` (binary diam di ubinnya dan berputar setengah putaran tiap 2 baris seperti asli; lengannya membuat 1 atau 2 lajur di sampingnya berbahaya: 65-68 dua lajur, 61-64 dan 77-80 satu lajur, 73-76 hanya ubinnya), `Floater`, `Sweeper` (lampu sorot berayun ±30° seperti asli), `LaserBeam` (laser menembak mendatar sepanjang lajurnya ke arah bola saat bola 5 baris lagi, seperti asli), `BeamPulse`. Riser, binary, pillar, lampu sorot dan laser tidak bisa membunuh bola yang sedang melompat dari jump pad, sama seperti game asli. |
| `Shaders/SkyGradient.shader` | Langit gradien + matahari/bulan + bintang. |
| `Shaders/Beam.shader` | Sinar laser/spotlight yang glow. |
| `Scripts/LevelEditor/` | Level editor di dalam game (lihat `LEVEL-EDITOR.md`). |
| `Editor/RTXSceneBuilder.cs` | Menu **RTX ON**. |
| `Editor/RTXLevelEditorBuilder.cs` | Menu **RTX ON > Open Level Editor Scene**. |
| `Scripts/PartArgs.cs`, `PowerUp.cs`, `RowFlip.cs` | PowerUp (Fly Jump, Short Jump, Bounce, Switch, Invisible, Speed Up, Speed Down) dan tile animasi, dengan setting per sel. |
| `Scripts/MainMenu.cs`, `MainMenuPages.cs`, `MainMenuSettings.cs`, `MainMenuMusic.cs`, `MenuStyle.cs`, `MenuTween.cs`, `MenuWidgets.cs`, `LevelProgress.cs`, `LevelSelect.cs`, `BannerLibrary.cs` | Main Menu (Home, daftar level, Detail, Settings, navbar, warna, animasi), rekor level, level yang dipilih, gambar banner. `MenuFx.cs` tidak dipakai lagi. |
| `Scripts/GameSettings.cs`, `GameInput.cs` | Setting pemain (suara, kamera, mouse, stik, accelerator, mapping tombol) dan input keyboard / mouse / stik. |
| `UI/Resources/RTXOnLogo.png` | Logo halaman judul. |
| `Editor/RTXMainMenuBuilder.cs` | Menu **RTX ON > Build Main Menu Scene**. |

## Yang perlu dicek di editor
Paket ini dibuat di cloud tanpa Unity, jadi belum pernah di-compile. Kalau Console merah, kirim screenshot-nya.

- **Animasi musuh asli (EnemyRig)**: kalau model bernama seperti aslinya (01_enemyRiser, 08_enemyTower, 09_enemyTreeTall, 10_enemyTreeSmall1, 11_enemyTreeSmall2, 12_enemyTreePalm, 13_enemyPyramidSmall), model dipasang ukuran asli lalu bagian "part"/"anim"-nya digerakkan persis seperti TileAnimator Rolling Sky 1.0. Bahaya tetap pakai collider remake.
- **Varian Riser / Tree punya ID sendiri (tidak acak lagi)**: riser 13-18 selalu pakai **Riser Model**, tree 21-24 selalu pakai **Tree Model**. Varian dipasang lewat ID-nya sendiri: **151 / 152 / 153** = Riser Variants Element 0 / 1 / 2, **154 / 155 / 156** = Tree Variants Element 0 / 1 / 2 (di tab Object: "Riser variant 1-3", "Tree variant 1-3"). Element kosong = model utama.
- **Pillar (85-92) seperti asli (PillarRig)**: tumpukan blok di bawah lantai yang menghentak naik **1 / 2 / 3 / 4 blok** (85 / 86 / 87 / 88) saat bola 13 sampai 3 baris lagi, lalu turun lagi setelah bola lewat. **Pillar safe** (89-92) menyisakan celah setinggi bola di lantai, blok di atasnya menggantung dan bola lewat di bawahnya (tidak membunuh). Isi **Pillar Model** = `18_enemyPillar` (ukuran asli, 1 blok = 1 ubin). Pillar Model kosong = tiap blok pakai **Block Model**, kosong juga = blok bawaan. **Pillar laser (105)** seperti asli: isi **Pillar Laser Model** = `19_enemyPillarLaser`. Pillar setinggi 3 blok naik seperti pillar biasa, bergetar dari 15 sampai 5 baris, lalu menembak: sinar `state2` miliknya muncul, kamera bergetar, dan 15 ubin di lajurnya ke arah bola jadi berbahaya (lompat = aman). Pillar Laser Model kosong = pillar dengan turret laser (Laser Model) di puncaknya. Bahaya = seluruh ubin, kecuali saat melompat (seperti asli).
- **Crusher (9-12) seperti asli (CrusherRig)**: terus-menerus naik sampai 2 ubin sambil rotornya berputar cepat dan bilahnya keluar, lalu menghantam ubinnya lagi (bunyi + **kamera bergetar**), siklus 24 baris yang terkunci ke jarak bola. Isi **Crusher Model** = `02_enemyCrusher` (ukuran asli, tidak diputar/dibalik lagi). Crusher biasa membunuh di ubinnya kecuali saat melompat; crusher safe (10, 12) tidak pernah membunuh.
- **Musik selalu pas (Game > Follow Music)**: bola mengikuti jam lagu (posisi sample), jadi mulai dari Start Pos, checkpoint, atau saat frame patah-patah, bola tetap di ketukan yang sama dengan lagu. Lagu dimuat sebelum tap, lalu dimulai tepat di detik baris awal; kalau lagu belum siap, posisinya dipasang ulang sampai benar.
- **Kamera bergetar (CameraRig > Shake Strength)** seperti asli: crusher menghantam (0.5, makin jauh makin lemah), hammer memukul (0.5), laser menembak (0.8), piramida mendarat (0.25). **Shake Strength** 1 = seperti asli, 0 = mati.

- **Ganti Look di tengah level**: di Theme Changes (GameManager) atau `"themes"` di level.json, isi **Look** (nama TrackTheme di Game > Looks, mis. `SkyTheme`) dan **Look Rows**. Saat bola sampai di baris itu, **warna seluruh lintasan** (lantai, rapuh, jump, rintangan, gem, mover) memudar ke warna look baru: **Look Rows 2** = pudar halus selama 2 baris, **0** = langsung ganti (kasar). Model look baru dipakai mulai baris itu. Mulai dari checkpoint / Test di tengah level langsung memakai look baris itu. Contoh JSON: `{ "row": 146, "preset": "Night", "rows": 40, "look": "SkyTheme", "lookRows": 2 }`.
- **Glow malam**: garis tepi lantai (`T_EdgeGlow`), retakan lantai rapuh (`T_FragileGlow`), tepi rintangan dan lantai bergerak **mati saat pagi**, sedikit saat sore, dan **menyala sendiri saat malam** (ikut pudar bersama langit). Jump pad (`T_JumpGlow`, sekarang bentuk **kotak**) tetap sedikit menyala di pagi hari supaya kelihatan. Atur per preset di Theme > Environments > **Edge Glow** (-1 = otomatis, 0..1 = manual). Gem dan bintang selalu menyala.
- **Lantai rapuh beda ID = beda warna**: tiap ID punya slot material sendiri di Theme > Materials: **Fragile** (3), **Fragile 1** (4), **Fragile 2** (5), **Fragile Checker** (6, gem/bintang/riser checker), **Fragile Row** (7, riser 16), **Fragile Column** (8). Build Gameplay Scene membuat `M_Fragile1`, `M_Fragile2`, `M_FragileChecker`, `M_FragileRow`, `M_FragileColumn` (Base Map `T_FragileAlbedo` + Emission Map `T_FragileGlow`, warna dasar dan glow beda: biru, hijau, pink, kuning, ungu). Ganti **Base Map** warna dan **Emission** warnanya untuk mengubah tampilan; fade look dan glow malam ikut. Slot kosong = Fragile dengan warna lain. **Fragile Borders** (aktif) memberi garis arsir di keliling tiap kelompok rapuh seperti asli, jadi kelompok yang bersebelahan kelihatan terpisah. Di level editor ID 3-8 juga beda warna. Material buatan (`M_Floor`, `M_Fragile`, `M_Jump`, mover) yang kehilangan Base Map atau Emission Map diisi lagi oleh Build Gameplay Scene.
- **Jump pad** naik sedikit lalu turun lagi (seperti pegas) saat bola melompat darinya.
- **Crown dan checkpoint terpisah**. **Crown** = tile **150** (lantai + crown emas melayang), diambil dengan menyentuhnya; HUD kiri atas `CROWNS x/N`, N = jumlah tile 150 (normalnya 3). **Checkpoint** = baris dari `"checkpoints"` / alat Checkpoint di editor (GameManager > Checkpoints untuk Level.txt): garis bercahaya di lantai, bunyi "ting", baris di depan meloncat. Setelah mati, tap = lanjut dari checkpoint terakhir (musik ikut lompat, lintasan naik dari bawah), **R** = ulang dari awal. Matikan dengan Use Checkpoints.
- **Finish dan portal**: isi `MassifTheme` > **Finish Model** = `03_finish` dan **Portal Model** = `04_portal` (folder MESH/GameObject). Finish dipasang di baris terakhir yang isinya 110 semua, portal di tengah tile 113 baris terakhir; ukuran asli, rotasi prefab, tanpa collider. 110 di tengah level tetap penanda segmen lantai bergerak, jadi tidak diberi finish.
- **Jump + gem (54) / Jump + bintang (120)**: gem sekarang di atas pad, bukan di puncak lompatan; diambil saat bola melompat. 132 (Jump + bintang melayang) tetap di atas.
- **Bola jatuh tembus lantai saat lag**: gerak bola sekarang dipecah jadi langkah kecil (maks 1/60 detik), jadi patah-patah sebentar (scene baru dimuat, lagu dimuat) tidak membuat bola jatuh dan mati di 0%.
- **Gem**: HUD kanan atas `GEMS x/N`, N = jumlah gem + bintang di level (Rolling Sky menargetkan 10; editor menunjukkan berapa yang sudah ada). Bintang dihitung 1.


## Tema official di build (Android) dan pesan error track

- **RTX ON > Update Theme List (Resources)** mendaftar semua TrackTheme ke `Assets/RTXOn/Resources/RTXOnThemeRegistry.asset` (tema lengkap, untuk Gameplay dan Level Editor) dan `RTXOnThemePictures.asset` (hanya nama + background, untuk Main Menu). Ini juga jalan otomatis sebelum setiap Build. Jadi tema seperti VolancoTheme ada di APK walaupun belum ada di daftar Looks scene, dan Level Editor di dalam game menawarkan semua tema official, bukan hanya di Unity.
- Kalau satu baris atau satu petak track gagal dibangun, track tidak berhenti lagi (dulu sisa level jadi kosong). Baris itu dilewati, dan pesan merah muncul di bawah layar selama 15 detik: baris, lajur, ID tile/object/collect, dan jenis error. Screenshot pesan itu kalau muncul di HP.
- **Custom level jadi Level 1**: level official adalah file `Level1.json` .. `Level5.json` di `Assets/Levels`. Simpan level dari Level Editor, salin ke `Assets/Levels` dengan nama `Level1.json` (timpa yang lama, simpan cadangannya dulu), lalu jalankan **RTX ON > Build Main Menu Scene**.
- **Lubang kamera / notch (HP tegak)**: baris atas HUD gameplay (Pause, bar, %, crown, gem), layar Pause, dan Main Menu (logo, judul daftar level, Detail, Settings) turun sekitar **10%** tinggi layar. Kalau safe area HP minta lebih, yang dipakai safe area. Angkanya di `ScreenRotation.PhoneTopShare`. PC dan landscape tidak berubah. Di Unity Editor bisa dilihat kalau build target Android dan Game view tegak.

## Objek mulai bergerak saat dekat (25 baris) dan tile animasi tertutup

- Semua objek dengan clip Loop (model di daftar Models tema) berdiri diam di frame pertama sampai bola **25 baris** di depannya (`TrackBehaviour.WakeRows`), lalu baru loop. **Roller, Spotlight, dan Binary (Mini, Slasher, Hammer) tetap loop terus.** Clip Approach tetap ikut jarak seperti dulu.
- Custom Obstacle tanpa Group sekarang default mulai di 25 baris (dulu 8); spin dan bob-nya juga diam sampai saat itu. Trigger Object (169) yang Group-nya tidak punya trigger tile juga mulai di 25 baris (dulu menunggu selamanya).
- **Crusher hanya sekali**: naik dan menghantam satu kali saat bola datang (mulai sekitar 22 baris di depan), sebelum dan sesudahnya diam di tile-nya.
- **Tile animasi** (145-149, 174-179, 194-199, Custom Move tile 157) tidak menyambung ke tetangga, jadi tetangganya punya dinding sendiri (dinding dobel, tidak apa-apa). Look Classic: tile animasi = **16_OD** (atas) + **15_O dibalik** di bawahnya, jadi satu blok tertutup tanpa bagian bolong saat berputar/terbang. Jalankan **RTX ON > Hook Up Tile Types** sekali supaya slot baru **Tile OD** di tema terisi 16_OD. Tile biasa tidak berubah.

## Big Update 2026-10-25: Event Group, Marquee, ikon baru (belum dites di Unity)

- **Ikon Steampunk**: semua ikon `Icons_Steampunk` + `Controller` (Xbox A/B/X/Y, LB/RB/LT/RT, LS/RS, D-pad, Menu, View, versi Filled) + bar/slider `B_Riveted_Cog` disalin ke `Assets/UI/Resources/Icons` dengan nama bersih (`Settings`, `ArrowLeft`, `XboxDpadUp`, `XboxLSPress`, `BarFrame`...). Nama yang sama dengan ikon lama menimpa yang lama, jadi menu Android lama langsung pakai ikon baru. Jalankan **RTX ON > Reimport UI Icons** sekali.
- **Bintang kesulitan kecil sekarang kuning** (`MenuStyle.StarYellow`).
- **Start Portal**: lompatan keluar portal sekarang **3 rows** (`GameManager > Start Portal Rows`), dan bola + portal selalu mulai di **lane tengah**.
- **Event Group** (seperti RollerCraft): setiap Tile / Object / Collect bisa masuk grup (TileGroupID / EnemyGroupID / CollectGroupID, boleh banyak: `5,6,7`) dan bisa **Fires** grup saat bola menyentuhnya (seperti CustomTriggerTiles 159). Grup 0 / kosong = main seperti biasa.
  - **Wait for trigger** (default ON): animasinya sendiri (crusher, riser, tree, clip Blender, bahkan Roller/Binary) diam sampai grupnya ditembak, lalu bangun cepat.
  - Tiap grup punya **nama** (Run, Prank...) dan **event**: Move, Move From, Rotate, Scale, Hide, Show, Shake, dengan Delay, Seconds, X/Y/Z, Curve (Smooth, Fast, Slow, Linear, Bounce), Loop. Contoh Adrian: gem di grup 6 dengan event Move Z 3 dalam 2 detik Fast = gem "kabur" saat grup 6 ditembak.
  - Disimpan di level.json `"cellGroups"` dan `"groups"`. Level lama tidak berubah.
- **Custom Obstacle**: tab Event punya **Dialogue** (balon teks di atas obstacle, ikut bergerak, muncul saat bola N rows) dan **Power-up on it** (power-up 160-168 yang menempel dan ikut gerak obstacle).
- **LevelPreview (JSON baru)**: setiap level bisa punya level kecil **10 rows** yang ditampilkan Main Menu dalam **3D** di kanan layar level PC. Versi HP (Detail) tetap menampilkan **banner** di depan, kecuali `Preview 3D On Android` dinyalakan. Kamera = posisi default CameraRig, environment = **Day** saja, hanya untuk dilihat (tidak bisa diputar atau dimainkan). Tap level: bola menggelinding di atas preview (tile dan object ikut beranimasi), lalu Loading.
  - File: `Level1.json` → `Previews/LevelPreview1.json` (subfolder di sebelahnya), `Custom2.json` → `Previews/CustomPreview2.json`. Level tanpa preview menampilkan 10 rows pertamanya sendiri.
  - Level baru (New+) otomatis dapat preview salinan **LevelPreview1** saat pertama kali di-Save.
  - **Start Pos** di LevelPreview ikut di-Save: bola preview mulai di sel itu (mana saja di 10 rows).
  - Level Editor > **Load** punya dua tab: **Load Level** dan **Load LevelPreview**. Preview disimpan maksimal 10 rows, tanpa trigger kamera / theme / screen.
  - **.levelrsrt** sekarang membawa level + preview-nya (`"preview"`), dan Import menaruh preview-nya di folder `Previews`.
  - Jalankan **RTX ON > Build Main Menu Scene** dan **Open Level Editor Scene** sekali lagi supaya preview ikut ke build. `Main Menu > Preview 3D On Pc` (default ON) / `Preview 3D On Android` (default OFF, Android tetap banner).
  - Kode: `Scripts/LevelPreviews.cs` (file, nama, potong 10 rows), `Scripts/LevelPreviewStage.cs` (3D di menu memakai TrackBuilder game).
- **LevelPreview lama (SpriteMaker / Custom Theme) sekarang bernama `LevelTexturePreview.json`** (`SpriteMaker/Resources/SpriteMaker/LevelTexturePreview.json`). Nama lama masih dibaca kalau yang baru tidak ada.
- **Event Group tidak hilang lagi** setelah Done / Save atau Play di Level Editor (sebelumnya pengaturan file level menimpa daftar grup).
- **UI baru PC / Xbox** (`NewUiForPc dan Xbox.pdf`). Hanya di PC (layar landscape); Android tetap memakai menu lama dengan ikon baru.
  - **Main Menu PC** (halaman 4): daftar level di kiri (banner, nama, bintang kuning, crown, %, gem; level terpilih diberi bingkai putih), **LevelPreview 3D** di kanan, kotak Tema / Rute / Lagu di kiri atas preview (latar hitam 20%), baris tombol **Layer** (ganti Official / Custom), **Brush** (edit level custom ini, atau buka Level Editor), **Star** (main, terpilih dulu), **Settings**, **Close** (ke Home), dan bar petunjuk di bawah ("Mulai main" / "Start to Play").
  - Kontrol: atas / bawah = pilih level, kiri / kanan = pilih tombol, **Enter / Spasi / A / X = tekan**, Tab / LB / RB = ganti daftar, Esc / B = Home. Mouse: arahkan = pilih tombol, klik level = pilih, klik lagi atau klik preview = main.
  - **HUD PC** (halaman 2): tombol Pause kiri atas dengan jumlah crown dan gem (angka hitam) di bawahnya, bar progress tipis bertepi putih di tengah atas dengan persen hitam yang ikut ujung isi bar.
  - **Pause PC** (halaman 3): HUD tetap terlihat (tombol Pause ditekan lagi = lanjut), tulisan **PAUSED** dan bar yang lebih besar di atas, baris tombol **Levels**, **Settings**, **Play** (terpilih), **Back**, dan bar petunjuk abu-abu tembus pandang ("Lanjut main" / "Tap to Play").
  - **Complete PC** (halaman 1): nama level, crown, "Level Complete 100%", gem, bar, lalu **Levels**, **Restart** (terpilih), **Back**, dan bar petunjuk ("Mulai awal lagi").
  - Bar petunjuk menampilkan ikon dan arti tombol yang dipilih; saat stik tersambung muncul **Ⓧ Pilih / Select** di kanan (halaman 5).
  - Kode: `Scripts/PcMenu.cs` (baris tombol, bar petunjuk), `Scripts/MainMenuPc.cs` (layar level PC), `Scripts/Hud.cs` (Pause dan Complete PC).
- **Save & Play**: tombol Play di Level Editor otomatis Save dulu.

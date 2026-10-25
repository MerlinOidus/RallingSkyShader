# Level Editor di dalam Unity

Editor level yang jalan di dalam game (scene sendiri), mirip editor Rolling Sky fan-made. Dibuat bertahap, tiap langkah bisa langsung dites.

| Langkah | Isi | Status |
|---|---|---|
| 1 | Grid 5 lajur, palet tile, Undo, Save/Load `Level1.json` | **sekarang** |
| 2 | Tombol Play (tes langsung dari editor), Level Settings (nama, speed, musik) | berikutnya |
| 3 | Trigger tema: garis yang mengganti tema dan tekstur saat bola lewat | nanti |
| 4 | Ikon gambar untuk tiap tile | nanti (kamu bikin ikonnya) |
| 5 | Custom Obstacles: model sendiri, collider kotak 1×1×1, event, animasi. Tiap obstacle punya Enemy ID (mulai 200) dan disimpan sebagai `200.rsrt`, bisa Export/Import | sudah (lihat "Custom Obstacle") |

## Langkah 1

### Cara buka
1. Ekstrak `RTXOn.zip` dan timpa file lama seperti biasa. Yang baru: folder `Scripts/LevelEditor/` (5 script) dan `Editor/RTXLevelEditorBuilder.cs`.
2. Menu **RTX ON > Open Level Editor Scene**. Pertama kali, scene `Assets/RTXOn/Scenes/LevelEditor.unity` dibuat dan **Level File** diisi `Level1.json`; setelah itu menu ini hanya membukanya.
3. Set Game view ke **16:9** (landscape), lalu **Play**.

### Cara pakai
| Aksi | Cara |
|---|---|
| Pilih tile | Klik tab utama (**Tiles**, **Collect**, **Object**, **Triggers**). Tab Tiles punya halaman kedua (**Tiles** = lantai, fragile dan jump jadi satu, Moving, Animated). Lalu klik tile-nya |
| Gambar | Klik atau tap di grid, tahan dan geser untuk menggambar banyak. Geser cepat pun tidak ada kotak yang terlewat |
| Edit / Copy | Tombol **Edit** (E) dan **Copy** (C) di tepi bawah palet. Edit: klik sel = jendela detail (Tile, Object, Collect, semua ARGS; ARGS di situ hanya untuk sel itu; Pick = jadi kuas; Copy cell). Copy: klik sel = sel itu (tile, object, collect dan ARGS-nya) tersalin dan terpilih, lalu tiap klik menggambar sel yang sama persis. Klik Copy lagi untuk mengambil sel lain |
| Favorit | Tekan **bintang** di kanan kotak SELECTED (tombolnya jadi emas = mode kumpul). Klik tile/object sebanyak yang diinginkan dari tab mana pun: setiap klik memberi bintang emas, klik lagi untuk membuangnya. Tekan **bintang** lagi: semua yang berbintang masuk tab **Collection** sekaligus (yang dibuang bintangnya keluar). Disimpan di PlayerPrefs |
| Hapus | Dua penghapus di tepi bawah palet, ada di semua tab: **Erase Tile** (hijau) mengosongkan seluruh sel (lantai dan semua di atasnya), **Erase Object** (merah) menghapus semua kecuali lantai (object, collect, props). Klik kanan = Erase Tile di tab Tiles, Erase Object di tab lain |
| Sisip / hapus baris | Tab Triggers: **Row +** (R+) lalu klik sebuah baris = baris kosong masuk di situ, baris itu dan semua sesudahnya geser satu ke atas. **Row -** (R-) lalu klik baris = baris itu dihapus, sesudahnya geser turun. Trigger, checkpoint, props, ARGS dan Start Pos ikut geser. Bisa di-Undo |
| Geser ke atas/bawah | Scroll mouse, W/S, panah, slider di kanan grid, tombol Page Up/Down |
| Lompat | Home/End, atau tombol Go to Start / Go to End |
| Batal / ulang | Tombol **Undo** (satu kali tarik = satu langkah) dan **Redo** (mengembalikan yang di-Undo). Keyboard: **Ctrl+Z** = Undo, **Ctrl+Y** atau **Ctrl+Shift+Z** = Redo. Menggambar lagi sesudah Undo menghapus Redo |
| Simpan | Tombol **Save** |
| Pilih / buat level | Tombol **Load**: jendela berisi semua level dengan banner (Level 1, 2, 3, 4, 5, lalu level buatanmu) dan kartu **NEW +** |
| Keluar ke Main Menu | Tombol merah **Exit** di ujung kanan bar atas (atau **Esc**) |

### Layer: Tiles, Collect, Object
Tiap sel sekarang punya **3 layer**, jadi object dan gem bisa ditaruh di atas lantai apa saja tanpa ID gabungan.

| Tab | Isi | Disimpan di |
|---|---|---|
| **Tiles** (halaman Tiles, Moving, Animated) | Lantai biasa, fragile, jump, slide, Mover 1-3 / Auto Mover 1-3 + panah, tile animasi 174-177 dan 194-199 | `rows` |
| **Collect** | Gem, Floating Gem, Star, Crown, PowerUp 160-168, Gem/Crown Rise/Drop 170-173, Checkpoint, **Finish 110**, **Portal 113** | `collect` (Finish/Portal tetap di `rows`) |
| **Object** | Semua rintangan: Crusher, Riser, Tree, Palm, Roller, Floater, Binary/Gate, **Pillar**, Laser Pillar, Spotlight, Laser | `objects` |
| **Triggers** | Theme, Camera, **Screen FX**, Start Pos, Row +, Row - | `themes`, `cameras`, `filters` |

- Menggambar Tiles mengganti lantai saja; object dan collect di sel itu tetap. **X Empty** di Tiles mengosongkan ketiga layer.
- Menggambar Object atau Collect di sel kosong otomatis memberi lantai biasa (1), kecuali Floater Air (31) yang boleh tanpa lantai.
- Object di atas lantai fragile / jump / slide: cukup gambar lantainya di tab Tiles lalu objectnya di tab Object. ID gabungan lama (mis. 14 = riser di fragile, 54 = gem di jump) otomatis dipecah saat level dibuka.
- **Klik tengah** mengambil isi sel dari layer tab yang terbuka (kalau kosong: object, lalu collect, lalu tile) dan pindah ke tabnya.

**Tampilan sel** (seperti sketsamu):
- **Belakang = tile**: warna (atau ikon) tile, **nomor ID kiri atas**, **nama kanan bawah**. Tile geser dan mover dapat **panah kecil** kiri bawah (mis. 19 "Slide left" + panah kiri).
- **Depan = object**: ikon object di tengah (atau kotak warna dengan namanya kalau belum ada ikon), selalu 1×1.
- **Collect**: di tengah, atau **kanan atas** (lebih kecil) kalau di sel itu juga ada object. Gem, Star dan Crown pakai bentuk bawaan kalau belum ada ikon.
- **Garis merah** melintang = jangkauan lengan Binary/Gate ke lajur sebelah. Objectnya tetap 1 sel, garisnya hanya penanda.

**Nama object dan panah ◄ ►**
- Tombol di tab Object menulis **nama model dari Look yang aktif** di atas (mis. `BinaryMini`, `Slasher`, `Hammer`, `KerisGetout`) dan **jenisnya** di bawah (mis. `Mini ►`). Ganti Look, namanya ikut berganti.
- Tombol yang punya arah dapat **panah besar ◄ / ► di kanan atas**: Binary (sisi yang dibuat berbahaya oleh lengannya), Roller, Slide 19/20, tile 174/175.
- Binary sekarang dipisah jelas, tidak ada lagi yang acak:

| Tile | Jenis | Jangkauan |
|---|---|---|
| 61 ◄ / 62 ► | Mini | 1 lajur |
| 65 ◄ / 66 ► | Slasher | 2 lajur |
| **190 ◄ / 191 ►** | **Hammer** (baru) | 2 lajur |
| 73 ◄ / 74 ► | Mini offset | hanya ubinnya |
| 77 ◄ / 78 ► | Slasher offset | 1 lajur |
| **192 ◄ / 193 ►** | **Hammer offset** (baru) | 1 lajur |

- Dulu 65-68 dan 77-80 kadang jadi Slasher kadang Hammer (acak). Sekarang **selalu Slasher**; pakai 190-193 kalau mau Hammer. Level lama yang dulu kebetulan dapat Hammer di sel itu sekarang tampil Slasher.
- Ikon Hammer 190-193 belum ada: tambahkan di list **Icons** pada Level Editor (ID + sprite), sampai itu tampil kotak merah dengan nomornya.

### Load: pilih level, NEW +
- **Load** membuka jendela **LOAD LEVEL**: tiap level satu kartu dengan bannernya (sama seperti di Main Menu), nomor dan namanya, dan nama filenya di bawah. Level yang sedang terbuka berbingkai biru (*open now*).
- Klik kartu = buka level itu. Kalau ada perubahan belum disimpan, klik pertama hanya memberi peringatan; klik lagi untuk membuka tanpa menyimpan.
- **NEW +** (kartu hijau pertama) = level kosong baru bernama **Custom 1**, **Custom 2**... Gambar tile lalu **Save**: file `Custom1.json` dibuat di folder yang sama dengan `Level1.json` (di game build: `persistentDataPath/Levels`). Level kosong belum bisa di-Save.
- Level baru langsung muncul di daftar Load dan di **Main Menu** (di Unity Editor tanpa perlu menjalankan Build Main Menu Scene lagi). Di game build, level dari folder save ikut muncul juga dan bisa di-**Edit** dari Main Menu.
- Di Unity Editor semua level.json di folder `Level1.json` selalu terdaftar. Untuk game build, daftar diisi oleh **Open Level Editor Scene** (field *Level Files*), jadi jalankan sekali lagi setelah menambah level.

- **Baris 0 = awal level** (paling bawah, tulisan START), sama seperti di game. Nomor baris ini juga yang dipakai `themes` dan `props` di level.json.
- Di atas baris terakhir ada baris kosong (lebih gelap). Gambar di situ untuk memanjangkan level. Baris kosong di ujung otomatis dibuang.
- Tanda `*` di depan nama file = ada perubahan yang belum disimpan.
- Di bawah tile yang dipilih ada penjelasan singkat untuk tile khusus (mover, lantai geser, tile animasi).
- **Edge (69-72, 81-84, 93-96, 107, 108) dan mover lama 37-48 / 57-60 sudah tidak ada di palet.** Bentuk lantai sekarang otomatis dari Tile Type, dan mover cukup pakai Mover kuning, Auto Mover merah dan panah hitam. Level lama yang masih memakainya tetap jalan dan tetap tampil di grid.
- **Tile shuffle (197)** tampil di grid sebagai kotak tembus pandang dengan tanda **?**, karena lajur asalnya kejutan.
- **Lantai bergerak (cara lama, hanya untuk level lama)**: **ungu = Mover Sentuh** (37–40, bergeser saat bola menyentuh panahnya), **hijau = Auto Mover** (41–48, 57–60, bergeser sendiri saat platform lain berhenti di sebelahnya). Platform = panah plus semua lantai biasa yang menempel, jadi gambar lantai di sekitar panah untuk membuat platform yang lebih besar. Tile Edge di tab Floor memotong platform.

### Mover bernomor (1, 2, 3) dan panah terpisah
Seperti editor fan-made di videomu (tab **Tiles > Moving**):
- **Mover 1 / 2 / 3** (kuning, ID 180-182) = platform yang bergeser saat bola menyentuh panahnya. **Auto Mover 1 / 2 / 3** (merah, ID 183-185) = bergeser sendiri saat platform lain berhenti di sebelahnya.
- Sel dengan **warna dan nomor sama yang bersentuhan = satu platform**. Nomor lain di sebelahnya = platform lain, jadi dua platform boleh menempel (mis. baris 1 1 1 1 1 tepat di bawah baris 2 2 2 2 2) dan tetap bergerak sendiri-sendiri. Di grid nomornya tampil besar di kiri atas.
- **Arrow up / down / left / right** (ID 186-189) sekarang terpisah dan masuk **layer Object**: taruh di atas salah satu sel platform. Satu panah cukup per platform; platform tanpa panah diam saja. Panah bisa dipakai di Mover maupun Auto Mover; di lantai biasa panahnya bekerja seperti Auto Mover lama.
- ID lama 37-48 dan 57-60 tetap jalan, jadi level lama tidak rusak.

### Parts: PowerUp dan tile animasi (ID 160-177, 194-199, dan yang baru di bawah)
PowerUp 160-168 dan Gem/Crown Rise/Drop 170-173 ada di tab **Collect**, tile animasi 174-177 dan 194-199 di **Tiles > Animated**. Pilih part, lalu tekan tombol **EDIT ARGS** (kiri kotak SELECTED) untuk membuka jendela **ARGS-SETTING**. Setting itu langsung dipakai semua tile yang kamu cat sesudahnya, tanpa jendela per tile. Tile yang sudah ada tetap memakai setting lamanya sampai dicat ulang. Ganti pilihan part saat jendela terbuka, jendelanya ikut ganti. **Klik tengah** (mouse wheel) di grid mengambil tile itu beserta ARGS-nya, jadi bisa diubah lalu dicat ulang. Isinya hanya setting yang dipakai part tersebut. **Reset** mengembalikan nilai bawaan, **Done** atau Esc menutup. Setting disimpan di level.json sebagai `"parts"`.

| ID | Nama | Yang terjadi | ARGS (bawaan) |
|---|---|---|---|
| 160 | Fly Jump | lompat terbang jauh | Distance 6, Height 2.4 |
| 161 | Short Jump | lompat pendek | Distance 2, Height 0.9 |
| 162 | Bounce | memantul terus sesuai pola Hops sepanjang Distance. Tiap pendaratan menyentuh lantai seperti biasa, jadi kalau mendarat di lubang bola jatuh | Distance 20, Hops `3,4,3`, Height 1.6 (tinggi hop 4 baris) |
| 163 | Switch | **trigger garis**: saat bola melewati baris ini (lajur mana pun), lintasan dicerminkan kiri-kanan (1,1,1,0,0 jadi 0,0,1,1,1), baris demi baris dengan animasi balik | Start rows 2 (jarak dari tile), Distance 15 |
| 164 | Invisible | bola hilang lalu muncul lagi (berkedip sebentar sebelum kembali) | Seconds 3 |
| 165 | Speed Up | **trigger garis**: saat bola melewati baris ini, kecepatan dikali Speed Multiple sepanjang Distance | Multiple 2, Distance 30, Incorporate music offset ON |
| 166 | Speed Down | **trigger garis**, sama, lebih lambat | Multiple 0.5, Distance 30, Incorporate music offset ON |
| 167 | Hide Objects | **trigger garis**: obstacle di Distance baris berikutnya hilang dari pandangan selama Seconds. Hanya disembunyikan: kalau disentuh tetap mati | Distance 20, Seconds 2 (1-4) |
| 168 | Dialogue | **trigger garis**: balon kata muncul di atas bola, berisi Text, selama Seconds | Text "WHO HE?", Seconds 2 (1-4) |
| 170 / 171 | Gem Rise / Gem Drop | gem terbang naik ke Height (harus lompat) / gem melayang lalu turun ke lantai saat bola datang | Start rows 8, Height 2.2 / 2.6 |
| 172 / 173 | Crown Rise / Crown Drop | sama, untuk crown | sama |
| 174 / 175 | Tile slide from left / right | lantai biasa yang meluncur masuk ke lajurnya dari kiri / kanan | Start rows 8 |
| 176 | Tile rise up | lantai naik dari bawah, jadi bisa dilewati | Start rows 10, Height 3 (kedalaman) |
| 177 | Tile sink down | lantai turun saat bola datang, kalau telat bola jatuh | Start rows 4, Height 4 |
| 194 | Tile wave | lantai naik-turun bergelombang bersama tetangganya, rata lagi saat bola dekat | Start rows 3 (mulai rata), Height 0.5 |
| 195 | Tile drop in | lantai jatuh dari atas ke tempatnya, makin lama makin cepat | Start rows 10, Height 6 |
| 196 | Tile spin in | lantai berputar sambil membesar ke tempatnya | Start rows 8 |
| 197 | Tile shuffle | lantai mulai di lajur lain (selalu lajur yang sama tiap main) lalu meluncur ke lajurnya | Start rows 8 |
| 198 | Tile extend | lantai memanjang ke depan dari sisi belakangnya, seperti jembatan yang terbangun sendiri | Start rows 6 |
| 199 | Tile fall away | sesudah bola lewat (Start rows di belakang bola), lantai jatuh sambil berguling. Hanya hiasan | Start rows 1.5, Height 8 |

- Fly Jump, Short Jump, Bounce dan Invisible diambil dengan **menyentuh** kubusnya. Melompati kubus berarti tidak diambil.
- Switch, Speed Up, Speed Down, Hide Objects dan Dialogue adalah **trigger garis**: selalu kena begitu bola melewati barisnya, di lajur mana pun, menggelinding atau melompat. Efeknya ikut semua: Switch membalik skala X baris, Speed mengubah kecepatan dan waktu musik. Di grid dan 3D view barisnya diberi garis melintang.
- **Garis** di grid dan di 3D view menunjukkan jangkauan tiap PowerUp dengan warna berbeda: biru Fly Jump, biru muda Short Jump, oranye Bounce (berlian di tiap pendaratan), ungu Switch (selalu di tengah lintasan, dari baris trigger sampai akhir bagian yang dicerminkan), hijau Speed Up, merah Speed Down, abu-abu Hide Objects. Invisible dan Dialogue tidak punya garis. Garis langsung berubah saat ARGS diubah.
- **Incorporate music offset calculation** ON: waktu musik ikut menghitung perubahan kecepatan, jadi bola tetap pas dengan lagu (Start Pos dan checkpoint sesudahnya juga pas). OFF: musik tidak ikut, jadi sesudah bagian itu bola lebih maju (atau mundur) dari lagu, dan game mengikuti jarak baru itu. Strip musik (gelombang dan garis beat) di kiri grid ikut menyesuaikan skala Speed Up/Down yang ON.
- Model sendiri untuk kubus PowerUp: TrackTheme > **Power Up Models** (Element 0 = 160 ... Element 8 = 168). Kosong = kubus glow berwarna.
- Gem/Crown Rise dan Drop ikut dihitung di jumlah Gems dan Crowns.
- Semua tile animasi selalu sudah kembali ke tempatnya sebelum bola sampai, dan dihitung dari jarak bola, jadi mulai dari checkpoint pun langsung benar.

### Tile animasi baru, Custom Move, Trigger dan Trigger Object (update 2026-10-22, belum dites di Unity)
| ID | Nama | Yang terjadi | ARGS (bawaan) |
|---|---|---|---|
| 145 | Tile flip in | lantai terbalik lalu berputar ke samping jadi tegak | Start rows 8 |
| 146 | Tile pop in | lantai muncul dari nol, sedikit kebesaran, lalu pas | Start rows 6 |
| 147 | Tile drawbridge | lantai berdiri di sisi belakangnya lalu rebah seperti jembatan angkat | Start rows 8 |
| 148 | Tile screw up | lantai naik dari bawah sambil berputar seperti sekrup | Start rows 8, Height 3 |
| 149 | Tile roll in | lantai berguling dari dua lajur di sampingnya ke lajurnya | Start rows 8 |
| 178 | Tile fly away | sesudah bola lewat, lantai berputar terbang ke langit dan mengecil. Hiasan | Start rows 1.5, Height 8 |
| 179 | Tile blink in | lantai berkedip lalu tetap muncul. Collider selalu ada | Start rows 8 |
| 157 | Custom move tile | lantai dengan animasi sendiri, membawa apa pun di atasnya | Move Z 20, With ball ON, Start rows 0 |
| 158 | Custom move block | balok dengan animasi sendiri, mematikan atau aman (tab Object) | Move X 2, Seconds 1.5, Loop ON, Start rows 10, Keep ON, Kill ON |
| 159 | CustomTriggerTiles | bola menggelinding ke tile ini = semua Trigger Object, Custom Obstacle ber-Group dan Custom Move dengan **Group** yang sama mulai | Group 1, Whole row OFF |
| 169 | Trigger Object | satu object dengan satu animasi: diam sampai trigger Group-nya disentuh, lalu animasinya jalan dan collision ikut (tab Object) | Group 1, Idle frames 1, Seconds 0, Height 1.2, Loop OFF |

- **Custom Move**: **Move X / Y / Z** dalam tile (X = kanan, Y = atas, Z = maju), **Spin** derajat. **With ball** ON = bergerak secepat bola (Z 20 = ikut 20 baris di samping bola); OFF = selesai dalam **Seconds** (Z 20 + Seconds 3 = cepat). **Loop** = bolak-balik terus. Mulai **Start rows** sebelum bola, atau saat CustomTriggerTiles dengan Group sama disentuh. Beberapa Custom Move dengan Group sama (tanpa trigger tile) mulai bersamaan dari baris pertama grup, jadi tile yang tersambung bergerak sebagai satu.
- **Speed** (tiles/detik) dipakai ganti Seconds kalau lebih dari 0 (With ball OFF). Contoh: Z 20 + Speed 10 = 2 detik. **Keep** OFF = hilang (mengecil) **Vanish** 1-3 detik setelah bola lewat. **Kill** OFF (hanya Custom move block) = aman, bola menembusnya, dan gem / crown / power-up di sel yang sama naik ke atas balok lalu ikut bergerak (harus lompat untuk mengambilnya). Di grid balok aman bertulis SAFE.
- **Group** = EnemyGroupID (1-99). Di grid, trigger tile menulis **T1**, Trigger Object, Custom Obstacle dan Custom Move menulis **G1**. Whole row ON = trigger kena di lajur mana pun / saat melompat (ada garis hijau melintang).
- **Trigger Object (169)**: satu model dengan satu klip Blender. Sebelum trigger: diam di frame pertama (**Idle frames** 1), atau memutar frame 1..Idle frames berulang (60 per detik). Setelah trigger: sisa klipnya diputar, **Seconds** 0 = kecepatan asli Blender, angka lain = klip direntang jadi sekian detik. **Loop** ON = diputar terus. Collision = satu kotak per bagian mesh yang ikut bergerak bersama animasinya (tekan F1 saat main untuk melihat kotaknya). Model: TrackTheme > **Trigger Object Model** + **Trigger Object Clip** (kosong = klip yang diatur di Models). Tanpa klip modelnya turun utuh; tanpa model: palang menyala setinggi Height yang membanting turun.
- **Custom Obstacle + Group**: pilih alat **Edit (E)**, klik selnya, tekan **ARGS** di bagian Object, isi **Group**. Obstacle itu diam sampai CustomTriggerTiles dengan nomor yang sama disentuh, lalu klip Blender atau tab Animation-nya berjalan (Idle frames, Seconds, Loop sama seperti di atas). Tanpa Group (atau Group tanpa trigger tile di level), klip Blender-nya mulai saat bola tinggal **Start rows** baris (bawaan 8): **Loop OFF** = diputar sekali lalu berhenti, **Loop ON** = berulang. Custom Obstacle tanpa klip (tab Animation) tetap seperti biasa.
- Satu sel bisa punya satu part per layer (misalnya Trigger Object di atas Custom move tile). Jalan dari checkpoint: trigger yang sudah lewat dianggap sudah kena.
- Di 3D view, mengubah ARGS lewat Edit langsung memutar animasinya sekali.

### Disimpan ke mana
- **Di Unity Editor:** Save menulis langsung ke **Level File** (di objek *Level Editor*, biasanya `Levels/Level1.json`). Scene Gameplay dengan Game > Level File = `Level1.json` langsung memakai hasilnya. Kalau Level File kosong atau masih `Level.txt`, Save membuat `NewLevel.json` di folder yang sama.
- **Di game yang sudah di-build (PC/APK):** ke folder data aplikasi (`Application.persistentDataPath/Levels/Level1.json`), karena file game di dalam APK tidak bisa ditulis. Gameplay membaca file ini mulai langkah 2.
- Save mengganti `rows`, `music`, `speed`, `checkpoints`, `themes` dan `props` (props lama di level). Kecepatan awal (`speed`) datang dari kotak Speed di editor.
- Aman: Save menulis ke `Level1.json.tmp` dulu, baru menggantikan file lama. Kalau file lama rusak (misalnya salah ketik koma), editor memberi tahu, Save harus ditekan dua kali, dan file lama disimpan sebagai `Level1.json.broken`.

### Music Play: musik di dalam editor
Tekan **Music Play** di bar atas (atau **Space** / **P**). Lagu level diputar **di editor**, mulai dari baris paling bawah yang terlihat, dan grid ikut bergulir pas dengan lagu (baris → detik lewat speed). **Garis kuning** = posisi bola. Tekan lagi untuk berhenti. Sambil diputar, kamu tetap bisa menggambar. Tidak ada suara? Kamera scene butuh **Audio Listener**; editor sekarang menambahkannya sendiri saat Play kalau belum ada.

**Visual musik**: strip di kiri nomor baris menunjukkan keras-pelan lagu di tiap baris, jadi kamu bisa menaruh rintangan tepat di bagian yang keras. (Garis ketukan ikut hilang bersama BPM.) Warnanya bisa diganti di komponen *Music Wave View* (nanti bisa diganti gambar sendiri). Di Unity Editor lagu dibaca langsung dari filenya; di game yang sudah di-build, clip harus *Load Type = Decompress On Load* (file di folder Music selalu bisa).

### Play: main di game
Tombol **Play** (dulu Test, atau **T**) memainkan level di scene Gameplay dengan musik, mulai dari **Start Pos** kalau ada, kalau tidak dari baris paling bawah yang terlihat. **Save & Play** (2026-10-25): Play otomatis Save dulu kalau ada perubahan; kalau Save gagal (file rusak minta tekan Save dua kali), Play berhenti dan pesannya tetap tampil.

**Start Pos** (tab Triggers, kotak hijau "S") bukan tile: klik sel mana saja (baris dan lajur bebas) untuk menaruh titik mulai Test, muncul tanda hijau START POS. Klik sel yang sama lagi untuk melepas (bisa di-Undo). Start Pos sekarang **sementara**: dipakai Play dan tetap ada selama editor terbuka (juga sesudah kembali dari Play), tapi **tidak ikut di-Save** ke level.json. Game biasa selalu mulai dari baris 0. Pengecualian: di **LevelPreview** Start Pos ikut di-Save (`"testStart"`) dan menjadi tempat bola mulai di preview 3D Main Menu, di sel mana saja dari 10 rows-nya.
- **Esc** = kembali ke editor. Level, posisi scroll, tile terpilih, tanda `*` dan Undo tetap seperti sebelumnya.
- Mati: tap = lanjut dari checkpoint terakhir, R = ulang dari baris awal tes. Selesai: tap = kembali ke editor.
- Scene Gameplay harus ada di daftar scene build: jalankan **RTX ON > Build Gameplay Scene** sekali, lalu **Open Level Editor Scene**.

### 3D view dan Look
- Tombol **3D** (atau **V**) menampilkan lintasan dalam 3D dengan material dan model dari theme tiap baris. Kamu tetap bisa menggambar: klik atau seret di lintasan, klik kanan untuk menghapus, roda mouse atau W/S untuk menggulir. **Tilt** (15-90, 90 = dari atas seperti grid) dan **Zoom** ada di pojok kanan atas. Tekan **2D** / V untuk kembali ke grid.
- Tombol **Theme** (dulu Look) membuka jendela Theme Settings (lihat "Theme Settings dan Custom Theme" di bawah) dan memilih theme level (`"look"` di level.json, misalnya SkyTheme). Default = Theme di komponen Level Editor. Daftarnya (Looks) diisi oleh RTX ON > Open Level Editor Scene dari semua TrackTheme di project.
- **Warna grid per baris** mengikuti look baris itu: garis antar sel = warna glow lantai theme, lantai biasa = warna Base lantai theme. Baris tempat look berganti (`"themes"` dengan `"look"`) menampilkan nama look di nomor barisnya, jadi kelihatan dari baris mana SkyTheme mulai.
- **Animasi saat dipasang**: tile yang baru kamu klik/seret main animasinya **sekali**. Tile animasi (Slide, Rise Up, Sink Down, Drop In, Spin In, Shuffle, Extend, Fall Away, Wave, gem/crown naik-turun) bergerak seperti di game, tile lain "pop" masuk. Di grid 2D selnya membesar sebentar. Hanya untuk yang baru dipasang: scroll, Load, Undo dan Redo tidak memutar animasi.
- **Play Mover** (tombol hijau di bawah Tilt/Zoom, atau tombol **M**): semua Mover (panah kuning) yang ada di layar langsung bergeser, lalu Auto Mover yang tersambung ikut bergeser berantai seperti di game. Setelah **3 detik** semuanya kembali ke posisi awal. Dari 2D, tombol M langsung pindah ke 3D.
- 3D view memakai kamera dan objek sendiri (bukan di dalam UI), dan ada **garis grid** di atas lintasan seperti grid 2D, termasuk di sel kosong.
- **3D lebar**: saat 3D, gambarnya melebar ke kiri (menutupi strip musik), dan **visual musik digambar di samping kiri lintasan** (makin lebar = makin keras), jadi lurus dengan barisnya. **Nomor baris** ada di kanan lintasan (biru tiap 10 baris). Saat Play, garis kuning menandai posisi bola dan kamera 3D ikut bergerak.
- **Tab Triggers** di palet: **Theme trigger** (TH, ungu), **Camera trigger** (CAM, hijau toska), Checkpoint, Start Pos. Klik sebuah baris dengan Theme/Camera untuk menambah trigger di sana (atau membuka yang sudah ada). Barisnya diberi garis warna (ungu = theme di bawah, toska = kamera di atas), di 2D maupun 3D.
  - **Theme trigger**: Environment (Day / Sunset / Night dari look baris itu, atau Keep), Blend rows, Look (TrackTheme atau Keep), Look fade rows (2 = halus, 0 = langsung).
  - **Sun angle / Moon angle** (di Theme trigger): **Auto** = sudut bawaan environment, **Set** = sudut sendiri. **X** = tinggi di langit (0 = di horizon, 90 = di atas kepala), **Y** = arah (0 = di depan, 180 = di belakang kamera, -90 / 90 = kiri / kanan). Matahari dan bulan masing-masing punya arah sendiri: matahari dipakai di Day/Sunset, bulan di environment malam (yang ada bintangnya). Sudut berlaku sampai trigger lain mengubahnya, dan bisa dipakai kapan saja: Environment **Keep** + Sun angle = cuma matahari yang berputar selama Blend rows. Tersimpan di `"themes"` sebagai `"turnSun"`, `"sun": {x, y}`, `"turnMoon"`, `"moon": {x, y}`.
  - **Camera trigger**: Position X/Y/Z (relatif ke bola), Rotation X/Y/Z, Offset rows (lama perpindahan), Curve (-1..1; -0.9 = berhenti halus), preset **Default / High / Far**. Selama jendelanya terbuka, 3D view menampilkan kamera itu. Di game, kamera biasa dipakai sampai trigger pertama, lalu pindah ke posisi/sudut trigger dan berpindah lagi di trigger berikutnya. Tersimpan sebagai `"cameras"` di level.json.
  - **Screen FX** (FX, oranye; garis oranye tipis di bawah garis kamera): efek layar dari baris itu.
    - **Effect**: Glitch, Grayscale, Negative, Chromatic, Scan Lines, Hue Shift.
    - **Strength** 0-1 (0 = mematikan efek itu), **Fade rows** (lama muncul/hilang), **Stay rows** (0 = tetap nyala sampai trigger efek yang sama berikutnya; lebih dari 0 = hilang lagi setelah sekian baris, jadi "burst").
    - **Speed**: Glitch = seberapa sering loncat, Scan Lines = kecepatan gulir, Hue Shift = putaran warna per detik (0 = warna kebalikan tetap).
    - Tombol cepat **On** (penuh, tetap), **Off** (mati), **Burst** (penuh 6 baris lalu hilang).
    - Efek berbeda bisa jalan bersamaan (Grayscale di baris 100, Scan Lines di baris 101). Satu baris = satu trigger FX.
    - Trigger FX baru otomatis memakai efek trigger sebelumnya, dan kalau efek itu masih nyala, trigger baru langsung jadi **Off**.
    - **Preview**: selama jendelanya terbuka, 3D view menampilkan efeknya. Saat Music Play di 3D, semua trigger FX ikut tampil.
    - Tersimpan sebagai `"filters"` di level.json: `{ "row", "effect", "strength", "fade", "rows", "speed" }`.
  - **Delete** menghapus trigger, **Done** / X menutup. (Undo belum mencakup trigger.)
- **Dynamic Loading** (panel kanan, di bawah Speed): **Dynamic: ON** = game membangun baris hanya sejauh **Load Distance** di depan bola (default 55, ketik angkanya atau pakai - / +, 10-200), beberapa baris per frame supaya stabil; baris yang tinggal 20 baris dari bola selalu langsung dibangun. **OFF** = seluruh level dibangun di awal. 3D view juga menggambar sejauh Load Distance. Tersimpan di level.json sebagai `"loadDistance"` dan `"loadAll"`.
- **Layar Loading**: Test dan kembali ke editor sekarang lewat layar LOADING dengan persen dan tips acak (misalnya "This Actually Rolling Sky RTX ON i was fake"). Daftar tips ada di `Scripts/LoadingScreen.cs` (`Tips`).
- level.json baru dari Level Settings: `"team"`, `"author"`, `"difficulty"`, `"banner"`, `"gems"`, `"crowns"`, `"invincible"`, `"startPortal"`.
- Format level.json tetap sama; yang baru hanya `"look"` (ditulis oleh tombol Look), `"cameras"`, `"testStart"`, `"loadDistance"` dan `"loadAll"`. Save sekarang menyimpan `"themes"` dari editor (trigger), bukan dari file.

### Tab Props (dekorasi): DIHAPUS 2026-10-23
Tab **Props** sudah tidak ada di palet; pakai **Custom Obstacle** (tombol + di tab Object, Collision bisa OFF untuk dekorasi). Props yang sudah ada di level lama tetap dimuat, tampil, tersimpan, dan bisa dihapus dengan Erase Object. Catatan lama di bawah hanya untuk level lama.
- Tombolnya diambil dari model yang dicentang **Prop** di daftar **Models** TrackTheme: look level (tombol Look, atau Theme di komponen Level Editor) plus look dari Theme trigger. Model belum ada di daftar? Pilih FBX-nya di Project > **RTX ON > Add Selected Models As Props**, atau tambah baris di Models dan centang Prop, lalu buka tab Props lagi. **Adjust** baris itu (Material, Keep Own Material, rotasi, skala, offset) ikut dipakai seperti di game.
- Pilih model, lalu klik atau seret di sel. Tiga tombol di atas tab mengatur props berikutnya:
  - **Place**: **Cell** = di sel yang diklik, **Left** / **Right** = di samping lintasan (x -3.5 / 3.5) pada baris yang diklik, **Both** = dua-duanya sekaligus.
  - **Turn**: 0°, 90°, 180°, 270°.
  - **Size**: 0.5x sampai 3x.
- Di grid, props tampil sebagai label coklat di bawah sel. Props di samping lintasan tampil di lajur 1 (`<Nama`) atau lajur 5 (`Nama>`). Di 3D view modelnya langsung kelihatan.
- **Erase Object** (tepi bawah palet, atau klik kanan selama tab Props terbuka) menghapus props di sel itu; di lajur 1 dan 5 juga yang di samping lintasan. Bisa di-Undo.
- Save menyimpan props ke `"props"` di level.json (format sama seperti dulu: `row`, `x`, `y`, `model`, `rotY`, `scale`). Mau posisi bebas (misal x = 6 atau melayang y = 3)? Tulis manual di level.json lalu Load; editor tetap menyimpannya.

### Custom Obstacle (tombol +)
- **Baru 2026-10-23 (belum dites di Unity): baris "Is a"** di tab Model memilih jenisnya. **Object** = rintangan (tab Object, seperti sebelumnya). **Tile** = lantai yang bisa dilewati bola (muncul di tab Tiles; Collision OFF = lubang dengan model). **Collect** = pickup yang diambil dengan disentuh (muncul di tab Collect). Di tab Event, **Pickup counts as** memilih Gem (masuk hitungan GEMS), Crown (CROWNS), atau Nothing. Kalau jenisnya diganti lalu Save, sel yang sudah dicat ikut pindah ke layer barunya; sebagai Tile ia menggantikan lantai.
- Model dengan klip Blender sekarang bisa Collision ON: kotak hitbox-nya menempel di bagian yang bergerak.
Seperti Custom Enemy di fan editor (IIE, file `.ieobs`). Formatnya punya kita sendiri: **`.rsrt`** (JSON biasa, bisa dibuka di VS Code).
- **Tab Object**, tombol **+** paling akhir = obstacle baru. Obstacle yang sudah dibuat muncul sesudah tombol +, warnanya magenta atau pakai ikonnya. Pilih salah satu lalu klik **EDIT** (kiri swatch di panel kanan) untuk mengubahnya.
- Jendela **CUSTOM OBSTACLES**: tab di kiri, preview 3D di kanan (seret untuk memutar, roda mouse untuk zoom, **Play** menjalankan animasi approach, **Hitbox** menampilkan kotak merah).
  - **Model**: Enemy ID (otomatis mulai 200), Name, **Model > Select...** (Plain block, file **OBJ**, **Picture on Cube / Plane / Circle**, atau model project), **Picture** (PNG, JPG atau GIF; GIF animasi ikut bergerak, bagian transparan tembus; Plane mengikuti bentuk gambarnya, maksimal 8 MB), OBJ texture (PNG/JPG), **Icon** (file PNG atau **From preview** = foto dari preview), Material (Theme Model Material atau material asli model), Rotation, Scale, Position.
  - **Collision**: tombol **Collision On / Off** di atas. Off = bola tembus, tidak ada hitbox. Model yang punya clip animasi Blender (theme Models > Animation > Clip) **selalu Off**, karena hitbox kotak tidak ikut gerak clip-nya. Grid 5×5 dari atas, kotak tengah = sel obstacle itu, atas = arah depan. Klik kotak = tambah atau hapus hitbox 1×1×1. **Height** 0-3 untuk kotak bertingkat. Kosong = tidak pernah membunuh. Kotak di lajur sebelah ditandai garis merah di grid.
  - **Event**: When touched (Kill / Nothing), Jump safe (tidak membunuh saat bola terbang dari jump pad), Sound (salah satu suara game, diputar sekali saat bola sejauh "Sound at" baris), **Own sound** (file WAV, OGG atau MP3 milikmu, disimpan di dalam .rsrt dan level.json; **Test** memutarnya), Volume.
  - **Animation**: baris **Model clip** di atas. Kalau model punya clip, clip itu yang diputar (juga di preview, tombol Play) dan pengaturan di bawahnya abu-abu (mati). Kalau tidak ada clip: Approach (dari pose From ke posisi akhir antara Start rows dan End rows sebelum bola sampai; hitbox ikut bergerak), Ease (Linear / Smooth / Bounce / Overshoot), Spin (derajat per detik), Bob (naik turun). 
- Tombol atas: **Save** = simpan ke library (`Levels/Obstacles/200.rsrt`) dan masuk palette. **Export** = simpan `.rsrt` ke mana saja untuk dibagikan. **Import** = buka `.rsrt`; kalau ID-nya sudah dipakai obstacle lain, otomatis dapat ID kosong berikutnya. **Delete** (tekan dua kali) = hapus dari library; tidak bisa selama masih dipakai di level. **X** / Esc = tutup (perubahan yang belum di-Save ditanya dulu).
- Cat di grid seperti obstacle biasa (layer Object, bisa di-Undo, klik kanan menghapus). Save level menyimpan semua custom obstacle yang dipakai ke dalam level.json (`"customs"`), jadi level tetap jalan di game dan di komputer lain walaupun file `.rsrt`-nya tidak ada. Kalau obstacle diubah, Save level lagi.
- **Model project**: pilih FBX/prefab di Project > **RTX ON > Add Selected Models As Custom Models** (masuk `Assets/RTXOn/Resources/RTXOnCustomModels`). Props theme juga bisa dipilih.
- **File OBJ**: dari Blender **File > Export > Wavefront (.obj)**. Modelnya disimpan di dalam `.rsrt`, jadi ikut saat Export. Unity tidak bisa membaca FBX saat game berjalan, karena itu dari file luar pakai OBJ.
- Di game yang sudah di-build: **PC (EXE)** tidak punya jendela pilih file, jadi taruh file OBJ, PNG atau `.rsrt` di folder `Import` di save folder game, lalu Select... / Import menampilkan daftarnya; Export menulis ke folder `Export` di sana. **Android (2026-10-24)** memakai jendela file HP sendiri: Import / Select... / Musik bisa ambil file dari mana saja (Download, Drive, WhatsApp), Export bertanya mau disimpan di mana. Kodenya `Scripts/LevelEditor/NativeFiles.cs` dan `Plugins/Android/RTXOnFiles.java` (jangan dihapus).

### Banyak theme (Look)
Semua TrackTheme di project otomatis muncul di tombol **Look** dan di Theme Trigger setiap kali Play, tidak perlu menjalankan Open Level Editor Scene lagi. Cara membuat theme baru: **RTX ON > New Theme From Current**, beri nama (mis. SkyTheme, ForestTheme, LavaTheme), lalu ubah model, material dan Environments-nya. Jumlahnya bebas. Sebelum membuat build (APK/EXE), jalankan sekali **RTX ON > Build Gameplay Scene** dan **Open Level Editor Scene** supaya daftar theme ikut tersimpan di scene.

### Theme Settings dan Custom Theme (update 2026-10-22, belum dites di Unity)
Tombol **Theme** di bar atas membuka **THEME SETTINGS**:
- **Gambar background** (klik, atau Change...): pilih theme level. Tab **Official** = TrackTheme di project (Default = Theme di komponen Level Editor), **Fanmade** = theme hasil Import, **Custom** = theme buatan sendiri, kotak **+** = Custom Theme baru, **Edit** di tiap kotak = ubah. **Import** membaca file apa saja di bawah.
- **Background**: **Yes** = gambar background theme. **No** = gambar disembunyikan, jadi sky / skybox dari Environments terlihat luas ke segala arah. Berlaku untuk semua look di level ini dan tersimpan di `"themeSettings"` (`hideBackground`).
- **Offset**: geser background naik / turun (geser lalu lepas). **Gradient**: background memudar ke warna fog di bawah, atas, atau keduanya (gaya transisi). Default = milik theme.
- **Roller**, **Block**: model itu diambil dari theme lain. **Mover Edge**: Auto, With edge (tiap tile mover berbingkai), Without edge (tanpa bingkai, dan platform mover memakai tekstur General / Floor seperti lantai biasa; hanya panahnya yang menandai mover dan panah itu tetap terlihat). Auto dan With edge memakai M_Mover / M_MoverAuto. **Midground**: blok midground dari theme lain, atau None.
- **Model** dan **Texture** (update 2026-10-23): pilih terpisah, Default (milik theme, tertulis di kurung) > RTX > Classic. Model Classic = tile asli (01_X..15_O, 07_jump, panah asli); Model RTX = model 3D di RTX Models theme (Floor, Fragile, Fragile Checker, Jump, Mover, Mover Auto, Pillar, Laser, Block). Texture Classic = atlas asli; Texture RTX = material RTX (M_Floor dan lainnya). Contoh: Model Classic + Texture RTX = bentuk asli dengan material RTX. Tersimpan di `"themeSettings"` (`modelLook`, `textureLook`: -1 Default, 0 RTX, 1 Classic).
- Kanan: **preview 3D** dari kamera seperti di game, di posisi grid (< 20 rows / 20 rows > / Start).
- Semua masuk ke level (`"themeSettings"` di level.json) dan berlaku untuk semua look level itu. Reset = kembali Default.

**CUSTOM THEME** (tombol + atau Edit), mengedit salinan sampai **Save**:
- **Textures**: **Mode** Classic (upload PNG / JPG: General, Fragile, FragileActive, Mover, MoverAuto, masing-masing dengan **Glow**-nya sendiri: General Glow, Fragile Glow, FragileActive Glow, Mover Glow, MoverAuto Glow) atau RTX ON (pilih material). Untuk dua mode: **Enemy**, **Enemy Glow**, **Icon**, **Background**. Kosong = milik Base Theme. Atlas Classic memakai tata letak yang sama dengan atlas Tile Set theme dasarnya.
- **ColorSets**: **Colour set** < > memilih Theme colours (warna bola, tint background dan midground, fog midground, warna dan kekuatan Glow Tile / Glow Enemy, specular dan gloss enemy) atau satu **Environment** (Day, Sunset, Night...: langit atas / horizon / bawah, matahari, ambient, **Use Fog** on / off, warna dan kepadatan fog). **Add...** menyalin environment dari theme mana saja, **Remove** membuang. Theme trigger memanggil environment dengan namanya. **Sun / moon turn** (-180..180°, berputar mengelilingi track) dan **Sun / moon height** (0..90°, tinggi dari horizon) diatur dengan slider saja, dan berubah langsung saat digeser (tombol di kanan mengembalikan ke 42° / -35°). Malam memakai sudut yang sama untuk bulan. Trigger Sun angle / Moon angle tetap bisa menimpanya. Environment yang sedang dipilih di sini langsung tampil di preview (langit/skybox, matahari, fog, ambient) dan ikut berubah saat warnanya digeser. Preview Custom Theme memakai **LevelTexturePreview.json** (sama dengan 3D SpriteMaker): seret/scroll gambarnya atau tekan **< 4 rows / 4 rows >**.
- **Information**: Name, Author, **Base Theme** (model, bentuk tile, midground dan suara diambil dari sini), List (Custom atau Fanmade).
- Kanan: preview 3D dengan theme ini di semua baris (dibangun ulang sebentar setelah klik terakhir). **Icon from preview** mengambil gambar kotak picker.
- **Save** menulis `Levels/Themes/<nama>.themert`. Level yang memakai theme ini membawa salinannya di level.json (`"customThemes"`) saat Save / Play, jadi level tetap jalan di tempat lain. **Delete** hanya bisa kalau level yang terbuka tidak memakainya. **Export** = file .themert untuk dibagikan.

**SpriteMaker** (tombol **Make...** di baris General, Fragile, FragileActive, Mover, MoverAuto, Enemy dan semua baris Glow-nya): membuat atlas dari lapisan mask putih seperti Texture Lab, bawaan Massif/Default. Kiri = Atlas, Show (Colour atlas / Glow map), Base colour, Project (Massif, Open..., Save...), lalu lapisan yang dipilih (Visible, Colour, Opacity, Blend, In glow map, Order) dan Top right / Palette Order untuk Enemy. Tengah = atlas (klik untuk memilih lapisan). Kanan = daftar lapisan dengan Search, All on / All off, Colour..., Glow. **Use** memasang hasilnya ke baris tadi, **Save PNG** menyimpan file. **3D** menampilkan LevelTexturePreview.json dengan atlas itu (seret/scroll untuk maju-mundur; belum tersimpan sebelum Use). Pemilih warna (Edit) mirip Photoshop: kotak S/B + Hue, H S B / R G B / Hex, **Eyedropper**, Recent, dan warnanya realtime (Cancel/Esc = kembali). **Reference** membuka gambar PNG / JPG bebas (yang terakhir diingat, - / + zoom, seret strip atas): klik gambarnya untuk mengambil warna persis ke pemilih warna yang terbuka, atau ke lapisan yang dipilih. Jalan juga di game hasil build. Panduan lengkap: `Assets/SpriteMaker/README.md`.

**Theme trigger** (alat TH) sekarang punya **Theme set** (tile, warna, background: theme Official atau Custom) dan **Objects** (model enemy dari theme lain; Own = milik Theme set itu; Keep = tidak berubah) yang dipilih terpisah, plus Fade rows. Di level.json: `"look"` dan `"objects"` di tiap trigger.

**Material RTX ON untuk Custom Theme**: buat material di Unity dulu (mis. duplikat M_Floor lalu ganti tekstur dan warnanya), lalu taruh di folder **Assets/Resources/RTXOnMaterials** (buat foldernya kalau belum ada) supaya ikut ke build. Pilihan material juga mencakup material Floor / Fragile / Mover semua theme resmi.

**File untuk dibagikan** (semuanya JSON dengan `"format"` di dalamnya; satu tombol Import membaca semuanya, dari Theme Settings > picker > Import atau Level Settings > Import...):
- `.rsrt` = 1 Object (custom obstacle)
- `.rsrtp` = Object Pack (Level Settings > **Export Objects**: semua custom obstacle yang dipakai level)
- `.themert` = Theme (Custom Theme > Export)
- `.levelrsrt` = Full Level (Level Settings > **Export Level**: level + object + theme di dalamnya). Import level menyimpannya sebagai `CustomN.json` baru lalu membukanya (simpan dulu level yang terbuka).
- Di game hasil build tidak ada jendela file: Import mendaftar file di folder `Import`, Export menulis ke folder `Export` (keduanya di persistentDataPath).
- Catatan: Fog di sini memakai kepadatan (density), bukan jarak start / end. Tombol Video dan password belum ada.

### Jendela Level Settings
Tombol **LEVEL SETTINGS...** di panel kanan membuka jendela seperti fan editor. Isinya yang tampil di **Level List** (Main Menu):
| | |
|---|---|
| **Banner** `< >` | Gambar level. **Auto** = gambar bernama `World N` untuk level nomor N (World 1 - Valea, World 2 - Aperta...). Gambar sendiri: taruh texture di folder bernama `Banners` (lalu jalankan Open Level Editor Scene / Build Main Menu Scene lagi), atau file .png/.jpg di `StreamingAssets/Banners` atau `persistentDataPath/Banners` |
| **Level Name**, **Team**, **Level Author** | Teks di Level List, mis. Massif / Cheetah Mobile / Adrian (Dude) |
| **Difficulty** | Klik bintang: 1 = Easy … 5 = Very Hard (bawaan). Klik bintang yang sama sekali lagi = setengah bintang (3.5) |
| **Start Speed**, **End Speed**, **Speed Max Point** | Start Speed = kecepatan awal (sama dengan Speed di panel kanan). Kecepatan naik pelan-pelan sampai End Speed di baris Speed Max Point (`speedEnd`, `speedRampRows`). Max Point kosong = sampai akhir level. End Speed kosong = tidak naik. Contoh: Start 7, End 7.5 |
| **Gem Number**, **Crown Number** | Hanya teks di Level List. Kosong = dihitung dari level |
| **Invincible** | Rintangan tidak membunuh bola (jatuh dari lintasan tetap mati) |
| **Start Portal** | ON = level mulai di atas portal di lantai dan bola langsung lompat keluar saat tap pertama (hanya start dari awal, bukan Start Pos/checkpoint). Json `"startPortal": true` |
| **Music** `< >` dan **Choose File...** | `< >` = lagu di folder Music (sama dengan Music di panel kanan). **Choose File...** = lagu `.mp3 .ogg .wav` dari folder mana saja: di Unity Editor jendela file Windows; di game PC browser folder (Music, Desktop, Documents, Downloads, semua drive); di Android browser folder (Phone, Music, Download, SD card; izin musik diminta dulu). File disalin ke folder Music (Unity Editor: `Assets/StreamingAssets/Music`, ikut ter-build; game: `persistentDataPath/Music`), lalu namanya jadi `"music"` level. Nama sama tapi lagu beda = `Nama (2)`. Export Level tidak membawa file lagu |
| **Editor-Preview** | Coming Soon |

**Exit** (atau **Esc**) di editor kembali ke Main Menu (dengan perubahan belum disimpan: tekan dua kali). Esc juga menutup jendela yang terbuka.

### Level Settings (panel kanan)
| | |
|---|---|
| **Music** `< >` | Ganti lagu: kosong = lagu nomor level, `Music1`…`Music5`, clip di folder `CustomMusic`, file di `StreamingAssets/Music` |
| **Speed** | Ketik baris/detik atau geser slider (1–20, per 0,1). Level baru: 7. BPM sudah tidak ada |
| Info | Crown x/3, Gem x/10 (gem + bintang), jumlah checkpoint |

**Crown** = tile **150** (tab Collect, kotak emas): gambar di lajur yang dilewati bola. Biasanya 3 per level.
**Checkpoint** (tab Collect, kotak biru "CP") bukan tile: klik sebuah baris untuk memasang checkpoint, klik lagi untuk melepas (bisa di-Undo). Nomor barisnya jadi biru dengan tulisan CHECKPOINT.
**110 Finish line** dan **113 Portal** (tab Collect, abu-abu): mesh `03_finish` dan `04_portal` dari tema. Finish hanya muncul di baris terakhir yang isinya 110 semua.

## Penjelasan tiap script (langkah 1)
Script Unity ditulis dalam **C#**, bukan C++. Sintaksnya mirip; bedanya yang penting ada di tabel paling bawah.

### `Scripts/LevelData.cs` (tambahan)
Data level: daftar baris, tiap baris 5 angka ID tile. Sekarang ada dua layer lagi dengan ukuran sama: `objects` dan `collects` (`GetObject`, `GetCollect`, `Set(TileLayer, row, col, id)`). `SplitLayers()` memecah ID gabungan lama lewat `TileCatalog.Split`; `ItemSpec(id, row, col)` memberi object/collect dengan jenis lantai dari layer tile. Langkah ini menambah fungsi untuk mengedit:
- `Set(row, col, id)` mengganti satu tile. Kalau barisnya belum ada (menggambar di atas ujung level), baris kosong ditambahkan dulu.
- `TrimEmptyEnd()` membuang baris kosong di ujung.
- `HasTiles` bernilai false kalau semua tile 0, supaya level kosong tidak tersimpan.
- `CreateEmpty(name, rows)` membuat level baru dengan beberapa baris `0,1,1,1,0`.
- `CopySettingsFrom(other)` menyalin nama, musik, speed, tema dan props dari salinan lain (dipakai Save).

### `Scripts/LevelEditor/TilePalette.cs`
Daftar semua tile yang bisa dipilih: ID, nama, dan grupnya (tab). Warna per grup sama seperti web level editor. Mau menambah atau mengganti nama tile? Cukup ubah array `All`.
- ID baru RTX ON di tab Object: **151-153** = Riser variant 1-3 (Riser Variants Element 0-2 di theme), **154-156** = Tree variant 1-3 (Tree Variants Element 0-2). Riser 13-18 dan Tree 21-24 selalu pakai model utamanya, tidak diacak.
- Di 3D view, pillar tampil sudah naik penuh (1-4 blok, safe dengan celah di lantai) dan crusher asli tampil ukuran asli, sama seperti di game.
- `readonly struct PaletteTile` = struct kecil yang isinya tidak bisa diubah setelah dibuat (seperti `const` member di C++).
- `group switch { ... }` = *switch expression*, versi singkat `switch` yang langsung mengembalikan nilai.
- `TextColorOn` menghitung terang-gelapnya warna latar (luminance), lalu memilih teks hitam di warna terang dan putih di warna gelap.

### `Scripts/LevelEditor/LevelFiles.cs`
Menentukan file mana yang dibaca dan ditulis.
- `#if UNITY_EDITOR` bekerja seperti `#ifdef` di C++: kode di dalamnya hanya ikut saat jalan di Unity Editor, tidak ikut ke APK.
- `Open(..., out string source, out string problem, out string brokenPath)`: parameter `out` mirip parameter referensi `std::string&` di C++, tapi fungsi **wajib** mengisinya. Cara mengembalikan beberapa nilai sekaligus.
- `Write` menulis ke file `.tmp` lalu `File.Replace`, supaya kalau aplikasi tiba-tiba ditutup, file lama tetap utuh.

### `Scripts/LevelEditor/UiFactory.cs`
Fungsi-fungsi kecil untuk membuat UI lewat kode: canvas, panel, teks, tombol, slider, dan list yang bisa di-scroll. Konsep penting Unity UI:
- **anchor** = titik di parent tempat elemen "ditempel" (0,0 = kiri bawah, 1,1 = kanan atas).
- **pivot** = titik di elemen itu sendiri yang ditempel ke anchor.
- **Canvas Scaler** mode *Expand* membuat UI selalu dihitung seolah layar minimal 1920×1080. Layar yang lebih lebar dapat ruang tambahan di samping, layar 4:3 dapat ruang tambahan di atas-bawah, jadi panel tidak pernah menutupi grid.
- `NoNavigation` mematikan navigasi keyboard antar tombol, supaya W/S dan panah hanya menggulir grid.

### `Scripts/LevelEditor/LevelGridView.cs`
Grid 5 lajur. Hanya ada 10 baris objek UI; waktu scroll, isinya saja yang diganti (`Refresh()`), jadi level 1000 baris tetap ringan.
- Tiap sel = `Image` tile di belakang, lalu anak-anaknya digambar berurutan (anak terakhir paling depan): teks ID dan nama, panah, garis merah jangkauan, chip object, chip collect, badge START POS. `MakeChip` membuat chip (bingkai + gambar + teks), `ShowChip` mengatur ukuran dan posisinya.
- `MarkReach` mencari Binary di baris itu dan menandai lajur yang terkena lengannya (`TileSpec.reach`).
- Right click memakai `editor.EraserId`: Erase Tile (0) di tab Tiles, Erase Object (-6) di tab lain.
- Class ini mengimplementasikan *interface* `IPointerDownHandler`, `IDragHandler`, `IPointerUpHandler`, `IScrollHandler`. Unity memanggil `OnPointerDown`, `OnDrag` dan seterusnya otomatis saat grid diklik atau digeser (mirip *virtual function* yang di-override di C++).
- `PaintAt` mengubah posisi kursor menjadi kolom dan baris: `kolom = floor(x / ukuranSel)`.
- `PaintLine` memakai **algoritma garis Bresenham**: posisi kursor hanya dibaca sekali per frame, jadi kalau digeser cepat, kotak-kotak di antara dua posisi diisi juga.
- `IsStrokePointer` memastikan hanya jari atau tombol mouse yang sama yang bisa menyelesaikan tarikan.
- `OnScroll` hanya menggulir sekali tiap 0,05 detik, karena trackpad mengirim banyak event kecil.

### `Scripts/LevelEditor/LevelEditor.cs`
Otak editor: memilih tile, menggambar, Undo, Save/Load, scroll, dan membangun semua panel. `OpenLevel` / `NewLevel` dipanggil jendela Load; `customPath` = file level yang tidak punya asset (NEW +, atau level di folder save game build). Test mengingat level yang terbuka (statik `playTestFile` / `playTestPath`), jadi kembali dari Test membuka level yang sama.
- `Awake()` dipanggil Unity sekali saat scene mulai; `Update()` dipanggil tiap frame (untuk tombol keyboard); `OnDestroy()` saat scene ditutup (mengembalikan orientasi layar HP).
- Undo memakai `Stack<List<Change>>`: tiap tarikan (tekan sampai lepas) disimpan sebagai satu daftar perubahan, Undo mengembalikan daftar terakhir.
- `button.onClick.AddListener(() => Select(id))` = *lambda*, fungsi kecil tanpa nama (seperti `[id]() { Select(id); }` di C++).
- Bingkai tile terpilih adalah kotak putih di belakang tombol, sedikit lebih besar. `Dictionary<int, Image>` menyimpan bingkai per ID tile.
- Variabel `icons` muncul di Inspector: nanti di langkah 4 kamu tinggal mengisi gambar per ID tile.

### `Scripts/LevelEditor/EditorTrackView.cs`
3D view editor: kamera sendiri yang merender ke RenderTexture (RawImage di atas sel grid), semua objek di layer 31 supaya kamera editor tidak ikut menggambarnya. Hanya membangun ulang sel yang berubah. `CellAt` mengubah titik klik di gambar menjadi baris/lajur (sinar kamera ke lantai y = 0), dan `LevelGridView` memakainya untuk menggambar.

### `Scripts/LevelEditor/TriggerPanel.cs`
Jendela trigger (theme / kamera): field yang langsung menulis ke `Level.ThemeChanges` / `Level.CameraChanges`. Game memakai kamera trigger lewat `CameraRig.SetChanges`.

### `Scripts/LevelEditor/PartPanel.cs`, `Scripts/PartArgs.cs`, `Scripts/PowerUp.cs`, `Scripts/RowFlip.cs`
`PartArgs` = setting satu sel (row, lane, id, distance, multiple, musicOffset, height, pattern, seconds, startRows) plus nilai bawaan dan warna garis per jenis. `PartPanel` = jendela ARGS-SETTING (baris yang tidak dipakai disembunyikan). `PowerUp` = kubus di game yang menjalankan efeknya saat disentuh (Switch/Speed juga saat bola melewati barisnya, `PartArgs.IsRowTrigger`); `LevelEditor.BrushArgs` = ARGS untuk tile baru (EDIT ARGS), `StampArgs` menyalinnya ke sel yang dicat, `PickCell` = klik tengah; `BallController.StartBounce` / `Hide`, `GameManager.StartSpeedZone`, `TrackBuilder.SwitchRows` (pakai `RowFlip`, skala X baris 1 ke -1). `LevelData.TimeAtRow` memakai `MusicSpeedAt`, jadi Speed Up/Down dengan music offset ON ikut di waktu musik dan Play di editor.

### `Scripts/LevelEditor/LevelPickerPanel.cs`
Jendela Load: kartu per level (banner lewat `BannerLibrary.Get`, nomor = urutan di daftar seperti Main Menu) dalam `UiFactory.MakeGridList` 5 kolom, plus kartu NEW +. Daftar dibuat ulang tiap kali dibuka: `LevelFiles.FindProjectLevels()` (Unity Editor), field `levelFiles`, lalu `LevelFiles.SavedOnlyFiles` (game build).

### `Scripts/LevelEditor/LevelSettingsPanel.cs`
Jendela Level Settings: field yang langsung menulis ke `LevelData` (Name, Team, Author, Difficulty, Banner, HeaderGems/HeaderCrowns, Invincible, StartPortal, SpeedEnd, SpeedRampRows). Bintang dan ikon gem/crown digambar dengan kode (`UiFactory.Star`, `Gem`, `Crown`).

### `Scripts/MainMenu.cs`, `LevelSelect.cs`, `BannerLibrary.cs`
Main Menu = **Level List** seperti fan editor: daftar level di kiri (banner kecil, nomor + nama, gem dan crown), banner besar di tengah, kanan nama, Pickups, Author, Team, Difficulty, lalu **Play** dan **Edit**. Klik dua kali atau Enter = Play, panah atas/bawah = pilih. `LevelSelect` membawa level yang dipilih ke scene Gameplay (Esc atau selesai level = kembali ke daftar) atau ke Level Editor (Edit). `BannerLibrary` mencari banner per nama atau per nomor level.

### `Editor/RTXMainMenuBuilder.cs`
Menu **RTX ON > Build Main Menu Scene**: membuat `Scenes/MainMenu.unity`, mengisi Levels (Level1.json, Level2.json... urut nomor, lalu level.json lain di folder itu) dan Banners, dan menaruh scene ini **paling atas** di Build Profiles > Scene List supaya game dibuka di Level List. Jalankan lagi setelah menambah level atau banner.

### `Editor/RTXLevelEditorBuilder.cs`
Menu **RTX ON > Open Level Editor Scene**. Script di folder `Editor` hanya jalan di Unity Editor dan tidak ikut ke game.

## Bonus: script lantai bergerak
Bukan bagian editor, tapi menarik untuk dipelajari.

### `Scripts/MoverGrid.cs` (aturan)
- `Cell[,] cells` = array 2 dimensi (`cells[baris, lajur]`), seperti `Cell cells[R][5]` di C++.
- `ref var cell = ref cells[r, c];` sama dengan `auto& cell = cells[r][c];` di C++: mengubah `cell` langsung mengubah isi array. Tanpa `ref`, `var cell = cells[r, c]` hanya **salinan** (struct di C# selalu disalin, seperti struct biasa di C++).
- `Platform()` adalah *flood fill* depth-first (DFS) dengan `Stack`: mulai dari panah, ambil satu kotak dari stack, masukkan tetangga atas/kanan/bawah/kiri yang tersambung, ulangi sampai stack kosong. Urutannya sama persis dengan game asli, karena urutan itu menentukan Auto Mover mana yang bergerak duluan.
- `Fire()` memanggil dirinya sendiri (rekursi) untuk tiap Auto Mover yang kena reaksi berantai, masing-masing 0,7 baris lebih lambat.

### `Scripts/MoverDirector.cs` (animasi)
- Posisi platform dihitung dari **jarak bola**, bukan waktu, jadi selalu pas dengan ritme musik.
- `Impact(p)` = kurva gerak asli: makin cepat sampai tujuan di tengah jalan, memantul seperempat kotak, lalu diam.
- Collider lantai tetap di tempatnya selama animasi (seperti game asli). Kotak tujuan dapat collider baru begitu platform itu mulai bergeser, dan kotak yang ditinggalkan jadi lubang setelah selesai.

### C# untuk yang sudah bisa C++
| C++ | C# |
|---|---|
| `std::vector<int>` | `List<int>` |
| `std::map<K,V>` | `Dictionary<K,V>` |
| `std::stack<T>` | `Stack<T>` |
| `int arr[5]` / `new int[5]` | `int[] arr = new int[5]` (selalu di heap, tidak perlu `delete`) |
| `int grid[R][C]` | `int[,] grid = new int[R, C]` |
| `auto& x = arr[i];` | `ref var x = ref arr[i];` |
| `void f(int& result)` | `void F(out int result)` (wajib diisi) atau `ref int result` |
| `#ifdef X` | `#if X` |
| pointer, `delete` | tidak ada; *garbage collector* yang membersihkan |
| `virtual void f() override` | `public override void F()` |
| header + .cpp | satu file `.cs`, nama class = nama file (wajib untuk MonoBehaviour) |
| `[&](){ ... }` | `() => { ... }` |

### Latihan kecil (boleh dicoba)
1. Ganti warna tab Object di `TilePalette.ColorOf`.
2. Tambah shortcut keyboard **Z** untuk Undo: di `Update()` pada `LevelEditor.cs`, tambahkan `if (KeyPressed(KeyCode.Z)) Undo();` lalu tambahkan `case KeyCode.Z: return k.zKey.wasPressedThisFrame;` di `KeyPressed`.
3. Ubah `VisibleRows` dari 10 ke 8 lalu `CellSize` dari 92 ke 112: grid jadi lebih besar.
4. Di `MoverGrid.cs`, ubah `ChainDelayRows` dari 0.7 ke 0.3, lalu main Level 2: reaksi berantai Auto Mover jadi lebih cepat.


## Tampilan baru Level Editor (2026-10-24, belum dites di Unity)

- Warna editor sekarang ikut **Settings > Menu Color** di Main Menu (`Scripts/LevelEditor/EditorColors.cs`): top bar dan header = Primary, tab terpilih / On = Primary2, Save / Play / New = hijau, Delete / Exit = merah.
- Semua tombol dan jendela (Level Settings, Load, Cell, Trigger, ARGS, pilih file) bersudut bulat seperti menu (`UiFactory.Round`). Teks memakai Menu Font kalau Main Menu punya.
- Tata letak dan semua fungsi tidak berubah.

## Tema official di Level Editor dalam game

Dulu tombol Look di Level Editor build (Android/PC .exe) hanya menawarkan tema yang ada di daftar Looks scene; tema lain hanya muncul saat Play di Unity. Sekarang semua TrackTheme ikut build lewat `Assets/RTXOn/Resources/RTXOnThemeRegistry.asset` (diisi otomatis sebelum Build, atau manual lewat **RTX ON > Update Theme List (Resources)**).

## Event Groups, Marquee (2026-10-25, belum dites di Unity)

**Events (top bar) / GROUP (jendela Edit)** membuka jendela **EVENT GROUPS** (`Scripts/LevelEditor/EventGroupsPanel.cs`):
- Kiri: daftar grup level (nomor + nama). **+ Group** membuat grup baru dengan satu event Move (Z 3, 2 detik).
- Tengah atas (kalau dibuka dari sel lewat GROUP): **Layer** (Tile / Object / Collect sel itu), **In groups** (`5,6`), **Fires groups** (`7`), **Wait for trigger**, **Fires on whole row**. Tombol **Join / Leave / Fires it** memakai grup yang dipilih di kiri.
- Tengah bawah: nomor, **Name**, berapa sel yang memakai grup, Delete.
- Kanan: event grup satu per satu (`<` `>` `+ Event` `Delete`): Type, Event name, Delay, Seconds, X, Y, Z, Curve, Loop.
- Grid menunjukkan badge oranye di kiri bawah sel: **G5,6** = sel ada di grup 5 dan 6, **F7** = sel menembak grup 7.
- Contoh crusher tidur: Object crusher → GROUP → In groups `5`, Wait for trigger ON. Satu tile di depannya → GROUP → Layer Tile → Fires groups `5`. Saat bola menyentuh tile itu, crusher bangun.
- Data: `Scripts/EventGroups.cs` (CellGroup, EventGroup, GroupEvent), game: `Scripts/GroupEventRunner.cs` (GroupEventRunner = event + tidur, GroupFire = sentuh untuk menembak), dipasang di `TrackBuilder.ApplyEventGroups`. Tidur bekerja lewat `TrackBehaviour.Ahead`: selama tidur jaraknya dianggap 999 rows.

**Marquee** (tab Triggers, ID -15): klik atau drag di beberapa row (Shift+klik = sampai row itu). Panel MARQUEE di kiri grid: **Copy**, **Cut**, **Delete**, **Paste: Insert / Over**, **Clear**. Setelah Copy / Cut, klik row mana saja untuk paste (Insert = row sesudahnya mundur, Over = menimpa). Yang ikut: tile, object, collect, ARGS, Event Group, props. Undo bisa. Kode: `LevelData.CopyRows / DeleteRows / PasteRows`, `LevelEditor` bagian Marquee.

## LevelPreview (2026-10-25, belum dites di Unity)

**Load** sekarang punya dua tab di atas: **Load Level** (seperti biasa, dengan NEW +) dan **Load LevelPreview**.
- Tab LevelPreview menampilkan semua level. Klik satu untuk mengedit preview-nya: level kecil **10 rows** yang ditampilkan Main Menu dalam 3D (kamera default, environment Day). Label "(new)" berarti belum ada file: editor mulai dari 10 rows pertama level itu, lalu **Save** membuat `Previews/LevelPreviewN.json`.
- Saat Save, preview dipotong jadi 10 rows dan trigger kamera / theme / screen dibuang. Look level di row 0 tetap dipakai.
- **Start Pos** di LevelPreview (tab Triggers) boleh di sel mana saja di baris 0-9: bola di preview 3D Main Menu mulai di situ dan menggelinding dari situ saat level dipilih. Tanpa Start Pos bola mulai di baris 0.
- Level baru dari **NEW +**: saat Save pertama, preview-nya dibuat dari **LevelPreview1** (kalau belum ada, dari 10 rows pertama Level1).
- **Export Level** (.levelrsrt) membawa level + preview-nya. Kalau sedang membuka preview, Export memakai file level pemiliknya + preview yang sedang dibuka. **Import** menaruh preview di `Previews`.
- SpriteMaker / Custom Theme memakai **LevelTexturePreview.json** (nama lama LevelPreview.json).

# Mac runtime 發佈材料（精確版本）

這份清單對應目前已測通的 Apple Silicon runtime：Alpine minirootfs 3.22.7、Linux `6.12.114-0-lts`、USB/IP tools `6.15.0-r0`，以及 VIIPER commit `b78e31e93e84b4cb7c4b4c15b8aace10749106d2`。來源依據是已下載 rootfs 的 `lib/apk/db/installed`、每個 APK 的簽章控制區 `.PKGINFO`，不是會變動的 Alpine 分支最新版。

`guest-manifest.json` 只列額外加入的 12 個 APK；initrd 同時保留完整 minirootfs。因此已將全部 26 個二進位套件（19 個 source origins）納入來源與 notices。沒有將私人 guest identity、簽章鑰匙、執行紀錄、Sony 或 VirtualHere 二進位放進來源包。

## 已完成的材料

- `packaging/macos/collect_sources.py`：固定 19 份 APKBUILD 的 SHA256，固定其 190 個 source/config/patch 輸入的 APKBUILD SHA512；另收錄 13 個安裝/trigger hooks，固定官方 aports git blob 與 SHA256。最多同時下載四個檔案，不執行下載內容。
- `.local/mac-runtime-sources-1/`：完整外部 source archives、Alpine recipes/patches/configs/hooks、VIIPER source ZIP、Go 1.27.3 工具鏈（含完整 Go source）、實際連結的兩個 Go module source ZIP、原始授權文字與重建腳本。`MATERIALS.json` 記錄個別 URL、hash 與大小。
- `notices/`：保留上游 COPYING/LICENSE、Linux LICENSES、socat OpenSSL notice、CA source notice、Go/VIIPER/BSD notices，並附每個 Alpine origin 的原始 APKBUILD。
- `packaging/macos/prepare_runtime.py`：只用固定 rootfs/APK/index cache，在全新目錄重建 Image/initrd；沒有下載、VM 啟動或 guest 程式執行。
- `packaging/macos/fetch_runtime_materials.py`：供沒有開發者本機 cache 的使用者，從官方下載固定輸入，或使用發佈附帶的固定 build-input archive；驗證後產生 App builder 需要的五個 runtime 檔案。

## 套件與官方固定來源

授權欄逐字沿用套件 metadata；原始來源中的逐檔授權與作者聲明另完整保留。所有列出的 APKBUILD 及其 source 輸入均已下載並依固定 checksum 驗證。

| Source origin | 二進位套件與版本 | 套件 metadata 授權 | 固定官方 recipe |
| --- | --- | --- | --- |
| alpine-baselayout | `alpine-baselayout 3.7.0-r0`<br>`alpine-baselayout-data 3.7.0-r0` | GPL-2.0-only | [540d84770198](https://gitlab.alpinelinux.org/alpine/aports/-/blob/540d8477019887cc6534152e92c3b12676e35d7b/main/alpine-baselayout/APKBUILD) |
| alpine-keys | `alpine-keys 2.5-r0` | MIT | [6d473fb38eff](https://gitlab.alpinelinux.org/alpine/aports/-/blob/6d473fb38effb2389f567b29fb7eb27039b3a279/main/alpine-keys/APKBUILD) |
| alpine-base | `alpine-release 3.22.7-r0` | MIT | [d5ce2bfde5b9](https://gitlab.alpinelinux.org/alpine/aports/-/blob/d5ce2bfde5b90c0ee710a4bd0323a74671b2983b/main/alpine-base/APKBUILD) |
| apk-tools | `apk-tools 2.14.12-r0`<br>`libapk2 2.14.12-r0` | GPL-2.0-only | [a53adea12086](https://gitlab.alpinelinux.org/alpine/aports/-/blob/a53adea12086d36717300ea195ede9ad75f98e1f/main/apk-tools/APKBUILD) |
| busybox | `busybox 1.37.0-r20`<br>`busybox-binsh 1.37.0-r20`<br>`ssl_client 1.37.0-r20` | GPL-2.0-only | [2e97d754a30d](https://gitlab.alpinelinux.org/alpine/aports/-/blob/2e97d754a30d558f15524b8e422303c8a96832df/main/busybox/APKBUILD) |
| ca-certificates | `ca-certificates-bundle 20261020-r0` | MPL-2.0 AND MIT | [ce7bff87478c](https://gitlab.alpinelinux.org/alpine/aports/-/blob/ce7bff87478cd936f5b7031ce822b02d4fdd313a/main/ca-certificates/APKBUILD) |
| openssl | `libcrypto3 3.5.9-r0`<br>`libssl3 3.5.9-r0` | Apache-2.0 | [b6d0d5c5639c](https://gitlab.alpinelinux.org/alpine/aports/-/blob/b6d0d5c5639c0034e9073c39e9ff139e28e71818/main/openssl/APKBUILD) |
| musl | `musl 1.2.5-r13`<br>`musl-utils 1.2.5-r13` | MIT; MIT AND BSD-2-Clause AND GPL-2.0-or-later | [ddaf59b84bc6](https://gitlab.alpinelinux.org/alpine/aports/-/blob/ddaf59b84bc6983a37d6e24d2210c2c4791642bb/main/musl/APKBUILD) |
| pax-utils | `scanelf 1.3.8-r1` | GPL-2.0-only | [9818c555f344](https://gitlab.alpinelinux.org/alpine/aports/-/blob/9818c555f344e3085d0dd1f3d917d8e23e244cb1/main/pax-utils/APKBUILD) |
| zlib | `zlib 1.3.2-r1` | Zlib | [121fc97db0e7](https://gitlab.alpinelinux.org/alpine/aports/-/blob/121fc97db0e7c59db704891b6d7ef83ae5df2cc2/main/zlib/APKBUILD) |
| eudev | `eudev-libs 3.2.14-r5` | GPL-2.0-or-later | [8bac74716d06](https://gitlab.alpinelinux.org/alpine/aports/-/blob/8bac74716d06c84627ab02d101fd9d2bafd0a34c/main/eudev/APKBUILD) |
| hwdata | `hwdata-usb 0.395-r0` | GPL-2.0-or-later OR XFree86-1.1 | [384053bb3c4e](https://gitlab.alpinelinux.org/alpine/aports/-/blob/384053bb3c4e39ea7b97de07cabfdf6b72cd26ae/main/hwdata/APKBUILD) |
| ncurses | `libncursesw 6.5_p20250503-r0`<br>`ncurses-terminfo-base 6.5_p20250503-r0` | X11 | [3fefbe8dda8b](https://gitlab.alpinelinux.org/alpine/aports/-/blob/3fefbe8dda8b3e85458edb190e2f3a69ad16035c/main/ncurses/APKBUILD) |
| libusb | `libusb 1.0.28-r0` | LGPL-2.1-or-later | [0ad4e679129e](https://gitlab.alpinelinux.org/alpine/aports/-/blob/0ad4e679129e2210dd3376908eca629bdb046266/main/libusb/APKBUILD) |
| linux-lts | `linux-lts 6.12.114-r0` | GPL-2.0-only | [6a0c60d347a1](https://gitlab.alpinelinux.org/alpine/aports/-/blob/6a0c60d347a1193d1d7ebb41a6e72dc0da1e8137/main/linux-lts/APKBUILD) |
| linux-tools | `linux-tools-usbip 6.15.0-r0` | GPL-2.0-only | [c4df575a9e2c](https://gitlab.alpinelinux.org/alpine/aports/-/blob/c4df575a9e2cc2474f7ef10151ca4dd9e5fab74a/community/linux-tools/APKBUILD) |
| readline | `readline 8.2.13-r1` | GPL-3.0-or-later | [be0833caee30](https://gitlab.alpinelinux.org/alpine/aports/-/blob/be0833caee3020f089f02c5d7223e971a9a11ffb/main/readline/APKBUILD) |
| socat | `socat 1.8.1.3-r0` | GPL-2.0-only WITH OpenSSL-Exception | [81db0dd118ad](https://gitlab.alpinelinux.org/alpine/aports/-/blob/81db0dd118ad2b7e90607790f29d28642bd6c7bf/main/socat/APKBUILD) |
| usbutils | `usbutils 018-r0` | GPL-2.0-or-later | [ea15b2a43ef1](https://gitlab.alpinelinux.org/alpine/aports/-/blob/ea15b2a43ef181be9807ee0099be6d6bf8d1df66/main/usbutils/APKBUILD) |

## 兩個不同的 Linux source tree

`linux-lts` 使用 [linux-6.12.tar.xz](https://cdn.kernel.org/pub/linux/kernel/v6.x/linux-6.12.tar.xz) + [patch-6.12.114.xz](https://cdn.kernel.org/pub/linux/kernel/v6.x/patch-6.12.114.xz)，再套用固定 APKBUILD 的六個 Alpine patches 與 `lts.aarch64.config`。Image、initrd 內的模組、另以 share 提供的 `dummy` / `af_packet` 都屬於這個 kernel。

`linux-tools-usbip` 則來自另一份 [linux-6.15.tar.xz](https://kernel.org/pub/linux/kernel/v6.x/linux-6.15.tar.xz) 與固定 `community/linux-tools` recipe；不能以 6.12 kernel source 代替。兩份完整 kernel tarball 均已收錄。

原始研究腳本後來將 `dummy`、`af_packet` 加進 early module 清單；直接重跑會改變 initrd 與虛擬裝置的初始化順序。發佈 wrapper 明確使用已測通的六個 module roots，排除這兩個 early modules，identity 由整合 runtime 在載入另外提供的模組前處理。

在全新暫存目錄實際重建後，下列 SHA256 與原測試資產逐位元一致：

```text
Image      d6763ecc91a9b614898148642572f7f10b44377b888185816a63a7a2c76c511b
initrd.gz  1356a076d56df8d0596e5a7ccebaba12d6d139a4e373536667c2777f2eeda97f
```

這次驗證是固定 APK/rootfs 到 runtime image 的重現；沒有宣稱已在 Alpine 內重新編譯全部 19 個 source origins。修改來源後重建必須同時更新該版本的 expected hashes。

## Backend 的實際來源與歷史紀錄修正

- [VIIPER source ZIP](https://codeload.github.com/hbashton/VIIPER/zip/b78e31e93e84b4cb7c4b4c15b8aace10749106d2)：SHA256 `8305750e881e7e955dcf26dc5fedc2b43d55902c9f1fa5b4eca31fe51901b2a5`，完整上游 source 已在原 cache，現已收錄。
- [Go 1.27.3 Darwin arm64](https://go.dev/dl/go1.27.3.darwin-arm64.tar.gz)：SHA256 `dd3908cf20959d798c1e06ebe5b72213f0505ce01373b40ce3110975a6cd99c2`，包含 Go source 與 BSD notice。
- `golang.org/x/crypto v0.52.0` 和 `golang.org/x/exp v0.0.0-20260410095643-746e56fc9e2f` 是 `CGO_ENABLED=0` backend 實際連結的非標準 modules；保留 module ZIP，重新驗證 Go `h1` 與固定 `go.sum`。其餘 upstream CLI/test 依賴沒有當成已連結 runtime 套件列入。
- `backend/module/` 保留實際編譯用的格式化 main.go、main_test.go、相對路徑 go.mod、go.sum；`rebuild/` 保留 build.py 與 Darwin fail-closed overlay。
- 舊 `BUILD.json.goVersion` 被 module loop 的 `version` 變數覆蓋成 x/exp pseudo-version；只修正 source builder 變數命名，不改歷史 BUILD.json。新 `backend/MATERIALS.json` 明列正確 Go 版本與修正原因。原 `RUNTIME-*.txt`、`MODULES.jsonl` 含開發者絕對路徑，因此沒有原樣放入公開材料。

## 發佈時的組合

Mac 0.1.3 的公開材料使用 `ThreeCatsKeybridge-Mac-0.1.3-source.zip`、`ThreeCatsKeybridge-Mac-0.1.3-runtime-sources.tar.gz` 與 `ThreeCatsKeybridge-Mac-0.1.3-build-inputs.tar.gz`；專案來源 ZIP 根目錄為 `ThreeCatsKeybridge-Mac-0.1.3-source/`。發行標籤為 `mac-v0.1.3`，與 Windows 標籤分開。這是公開附件的品牌名稱；以下既有 cache、內部資料夾、來源套件名稱及固定雜湊不變。

Runtime source archive 與整合 App 的當版 source snapshot、build/install scripts 一起提供；來源包內 README 指明 APKBUILD 重建方式、backend source 佈局與原始 runtime bytes 重現指令。App 內 notices 由來源包的 `notices/` 複製。這份材料不包含 VirtualHere 客戶端/伺服器再散布授權；它們維持由整合安裝流程向官方取得的獨立依賴。

## 公開使用者的完整 runtime 建置步驟

準備與 App 同版的專案 source snapshot、runtime corresponding-source archive，以及固定 build-input archive。兩個 runtime archive 解開後的根目錄分別是 `mac-runtime-sources/`、`mac-runtime-build-inputs/`。後者只有 rootfs、12 個 APK、兩個簽章 index、公開 manifest 與 README，沒有執行期 identity/config。

固定 build-input archive 的 SHA256 是 `35b81b04bcfee79fe0a0a5b17b1cf3ee03a48b2838d6f1c231c2120bd37cb433`，大小 115,775,397 bytes。Corresponding-source archive 的 SHA256 是 `7f15544b93960b4d31bfd42d7413cbae057af3e81d7ee613f11f74bf19403218`，大小 450,919,582 bytes。兩者是開發者重建材料，不增加一般使用者的 App 安裝大小。

在 Apple Silicon Mac 的專案 source 根目錄執行下列步驟，將兩個變數改成解壓後目錄的絕對路徑。`.local/public-backend`、`.local/public-runtime-inputs`、`.local/public-runtime` 必須是尚未存在的新路徑。

```sh
RUNTIME_SOURCES="/absolute/path/mac-runtime-sources"
RUNTIME_INPUTS="/absolute/path/mac-runtime-build-inputs"

mkdir -p .local
mkdir .local/public-backend
mkdir .local/public-backend/downloads .local/public-backend/upstream .local/public-backend/toolchain
cp "$RUNTIME_SOURCES/backend/downloads/go1.27.3.darwin-arm64.tar.gz" .local/public-backend/downloads/
cp "$RUNTIME_SOURCES/backend/downloads/VIIPER-b78e31e93e84b4cb7c4b4c15b8aace10749106d2.zip" .local/public-backend/downloads/
tar -xzf .local/public-backend/downloads/go1.27.3.darwin-arm64.tar.gz -C .local/public-backend/toolchain
unzip -q .local/public-backend/downloads/VIIPER-b78e31e93e84b4cb7c4b4c15b8aace10749106d2.zip -d .local/public-backend/upstream
python3 research/macos-native/guest-backend/build.py .local/public-backend

python3 packaging/macos/fetch_runtime_materials.py \
  --manifest "$RUNTIME_INPUTS/guest-manifest.json" \
  --input-cache "$RUNTIME_INPUTS" \
  --cache .local/public-runtime-inputs \
  --output .local/public-runtime \
  --backend .local/public-backend/GuestDualSenseBackend-linux-arm64
```

Backend builder 以固定 Go 1.27.3、VIIPER commit 與 `go.sum` 編譯，必要 module 由 Go 官方 proxy 取得，下載與編譯 cache 都在新的 backend 目錄內。它只執行純測試，不啟動 backend。接著 fetcher 對公開 manifest 本身、全部輸入與 backend 做固定 SHA256 驗證；APK/index 另經既有 RSA 簽章及 payload 驗證，才重建 Image/initrd。`dummy.ko`、`af_packet.ko` 從同一份已驗證 kernel APK 取出並比對固定輸出 hash，沒有在 Mac 或 VM 載入它們。

若沒有下載 build-input archive，也可以使用 corresponding-source archive 的公開 manifest，省略 `--input-cache`：

```sh
python3 packaging/macos/fetch_runtime_materials.py \
  --manifest "$RUNTIME_SOURCES/runtime/guest-manifest.json" \
  --cache .local/public-runtime-inputs \
  --output .local/public-runtime \
  --backend .local/public-backend/GuestDualSenseBackend-linux-arm64
```

此路徑只向固定 Alpine 官方 HTTPS URLs 下載。官方 index URL 會更新，因此長期重現應使用發佈附帶的固定 build-input archive；下載到較新或不同內容時會中止，不會替換原版本。

成功後的 `.local/public-runtime` 可直接交給 App builder：

```text
Image
initrd.gz
share/GuestDualSenseBackend-linux-arm64
share/dummy.ko
share/af_packet.ko
guest-manifest.json
runtime-materials.json
```

完成專案的本機簽章設定後，App build 指令可使用公開 source 與新產生的 runtime 目錄，無須原開發者的 `.local` 資料：

```sh
python3 research/macos-native/build_preview.py \
  --output .local/public-app-build \
  --runtime-materials .local/public-runtime \
  --source-materials "$RUNTIME_SOURCES" \
  --archive
```

上述流程重現已發佈的固定 APK/rootfs 組合，並從 source 編譯 backend；它與在 Alpine 環境依 19 份 APKBUILD 從頭編譯全部作業系統套件是不同的驗證範圍。要發佈修改後的 backend/kernel，需在自己的 source 中更新 fetcher、prepare wrapper 與 App builder 的固定 hashes，再建立對應版本材料，不能把修改後產物標為原版逐位元重現。

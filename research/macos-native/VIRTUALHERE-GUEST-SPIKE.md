# VirtualHere：Linux guest 中重新分享 synthetic USB 的最小實驗

2026-10-30。這份紀錄接續 `USB-OUTPUT-RESEARCH.md`，保存從接口研究到 Mac USB neutral 成功的實驗；不以 VM FAQ 當作技術上一定失敗的證明。

## 最新結果：Mac USB neutral 建立與移除通過

固定 VIIPER `b78e31e93e84b4cb7c4b4c15b8aace10749106d2` → guest VHCI／usbfs → 官方 VirtualHere Server **4.8.8** → VSOCK → 官方已簽章 Mac client **6.0.3**，已在 Mac 觀測到 USB DualSense。只送 neutral，未操作 Sony 或驗證遊戲。

| 階段 | 已觀測結果 |
| --- | --- |
| Guest 輸出 | `054c:0ce6` 枚舉，64-byte neutral input 讀回 |
| Mac 接入前 | 本次匹配的 HID 裝置列表 `[]` |
| Mac 接入後 | `DualSense Wireless Controller`，`054c:0ce6`，Transport `USB`，usage `01:05` |
| 停止與清理 | 發出 `STOP USING`，回覆不是精確 `OK`；另行確認 guest `UNBOUND`、Mac 自有 entry 消失，列表回到 `[]` |
| 免費條件 | `unlicensed max_devices=1`，預設 TCP 7575；client 未裝為 service |
| 未完成 | user trial 1 的真鍵盤／前景／F8／Sony／PS5 回報與清理核對，以及長時間／重連；本人已確認新 App Accessibility |

去識別證據見 [MAC-USB-DUALSENSE-NEUTRAL.json](inspection/MAC-USB-DUALSENSE-NEUTRAL.json)。另一份本機 `.local/mac-usb-neutral-2/SESSION.json` 記錄 `feedbackBytesDrained=3920`；只代表讀取到回程資料，不把資料量當成 feature 全部正確或 gameplay 成功。

後續 [relay smoke](inspection/DUALSENSE-RELAY-SMOKE.json)已通過合成 frontend → relay → guest → Mac USB 的 neutral／EOF 測試：125 幀輸入、8 幀 feedback，frontend EOF 後 `reason=frontend_eof`、`cleanup=removed`，Mac 自有 entry 在 0.123 秒後消失，列表 `[]`。V5 協定 **22958** 項、[專用 socket](inspection/DUALSENSE-RELAY-SOCKET-TESTS.json) **57** 項檢查通過。新 App 已建置、strict verify 通過並從 `~/Applications/PSKeyboardMacDualSense.app` 啟動，本人已確認 Accessibility。user trial 1 已重新建立 neutral 裝置等待使用者測試回報，尚無 gameplay 結果；先前清理證據不表示新 session 已清理。詳細 BUILD 及狀態見 [研究總覽](README.md)。尚未交付可獨立協調 guest／relay／client 的正式 App。

## 免費模式與預設 relay 埠

早期 host `127.0.0.1:33757` → guest `7575` 雖能列出 server，接入時出現 license 提示。改為 host `127.0.0.1:7575` → guest `7575` 後，`USE` 回覆成功且 Mac USB 裝置建立。全程保留同一 guest 固定識別及 VirtualHere 設定，未變更 license key、重置試用或輪換識別。

此結果與 [作者對不同標準埠可能觸發付費設定的說明](https://www.virtualhere.com/node/4784)一致；[官方設定文件](https://www.virtualhere.com/configuration_faq)將 `TCPPort` 等列為付費進階選項，預設為 **7575**。本路線維持官方預設埠和 Generic 單裝置免費限制；不把錯誤當成必須買 license，也不修改 binary 或授權檢查。

## 重新核對後的判斷

**直接用自製標準 USB/IP server 接 VirtualHere client 不成立；Linux guest 的 VHCI → usbfs → 官方 VirtualHere Server 轉接，後續已通過 Mac neutral 建立與移除。** 以下保留設計時的依據及待測邊界。

- VirtualHere 作者在 [other USBIP client](https://www.virtualhere.com/node/4223) 明確說 client 只接自己的 server，而且協定與 USB/IP 不同。[另一則作者說明](https://www.virtualhere.com/node/4696) 指出 Linux client 在自家協定與 Linux USB/IP kernel modules 之間轉換。現在有一手證據，不必僅依另一專案的相容性文件推斷。
- 未找到公開的完整 VirtualHere wire protocol、可供 synthetic USB 使用的 server SDK 或獨立相容實作。官方 API 是 client 管理指令，沒有 descriptor／input report feed。這輪不重建其授權機制，也不修改 binary。單純替換 port 不會提供互通。
- [VM FAQ](https://www.virtualhere.com/oem_faq) 說 server 在 VM 可能無法正確運作；作者在 [2026-06-12 的解釋](https://www.virtualhere.com/node/4829) 把原因明確指向 hypervisor 的實體 USB passthrough。我們不做該 passthrough：guest 內部的 VIIPER 透過 guest 自己的 `vhci_hcd` 產生 USB device，再讓同一 guest 的官方 server 從 usbfs 讀它。**這是不同的資料路徑，FAQ 不能單獨否定它。** 也不能反過來視為作者已支援這條路。
- Linux [usbfs 實作](https://github.com/torvalds/linux/blob/v6.12/drivers/usb/core/devio.c) 與 [VHCI HCD](https://github.com/torvalds/linux/blob/v6.12/drivers/usb/usbip/vhci_hcd.c) 提供一般 USB control/URB 通路；沒有因來源為 virtual controller 就等同於實體 passthrough 的必要。研究初期尚未證明 VirtualHere 枚舉與轉送；本輪已補上 neutral 接入／移除證據，完整 control／feature、reset、長時間穩定性與 Sony 接受度仍待測。

目前採用的資料流（relay neutral 已通過，鍵盤／Sony 待測）：

`Mac keyboard app → host relay 127.0.0.1:33743 → host 127.0.0.1:33742 → VSOCK 33642 → guest VIIPER API`

`guest VIIPER USB/IP :33641 → guest vhci_hcd → guest usbfs → 官方 Generic VH Server :7575 → VSOCK 7575 → host 127.0.0.1:7575 → 官方已簽章 VH Mac client → Mac USB DualSense`

本機 Sony 接受度是 USB neutral 通過之後的獨立測試；目前未操作 Sony。

## 已交付的 guest 材料

本機唯讀盤點：macOS 26.6.2 arm64，16 GiB RAM、8 logical CPUs、當時約 35 GiB 可用磁碟，`kern.hv_support=1`。PATH 與常見 `/Applications` 路徑未找到 QEMU、UTM、Docker、Lima、OrbStack、VMware、Parallels 等現成工具。採 Apple 已內建 Virtualization.framework，由 root agent 提供 runner，無須安裝 host driver 或新增 hypervisor app。

準備程式：[`guest/prepare_guest.py`](guest/prepare_guest.py)。執行只下載／驗證／產生資料檔，不啟動 VM，也不執行下載的 ELF。命令：

```sh
python3 research/macos-native/guest/prepare_guest.py .local/mac-usb-guest-1
```

以下大小與 hash 是首次準備收據的快照，不代表之後每次 runner／initrd 的內容；重新產生時須以新 manifest 為準。

| 材料 | 固定來源／首次準備結果 |
|---|---|
| rootfs | 官方 Alpine minirootfs 3.22.7 aarch64；SHA256 `7e9b34ff3304381909e566e2c93de450664ccc724191261423974fc121c11623`，已比對官方 `.sha256` |
| kernel | 官方 `linux-lts-6.12.114-r0.apk`，實際 kernel release `6.12.114-0-lts`；不用缺少 USB/IP 的 `linux-virt` |
| kernel 授權／校驗 | Alpine rootfs 內公開 key 驗證 APKINDEX RSA；每個 APK 的 RSA signature、signed index control SHA1、signed `.PKGINFO` 的 payload SHA256 全部核對成功 |
| `Image` | 從 EFI zboot 的 gzip payload 取出 arm64 Image，37,683,200 bytes；SHA256 `d6763ecc91a9b614898148642572f7f10b44377b888185816a63a7a2c76c511b` |
| `initrd.gz` | 5,430,435 bytes；SHA256 `1356a076d56df8d0596e5a7ccebaba12d6d139a4e373536667c2777f2eeda97f` |
| modules | 核對含 usbip-core、vhci-hcd、virtio-pci、virtio-console、virtio-vsock、virtiofs/fuse 及遞迴依賴；以未壓縮 `.ko` 放入 initrd，避免依賴 module 解壓工具 |
| guest utilities | 經 APK 驗證的 usbip、socat、lsusb、usbhid-dump 與動態 library；socat binary 含 VSOCK-LISTEN 支援 |
| 收據 | `inspection/USB-GUEST-PREPARE.json`；本機同份 `.local/mac-usb-guest-1/guest-manifest.json` |

APK 簽章流程依 [Alpine abuild-sign 原碼](https://github.com/alpinelinux/abuild/blob/master/abuild-sign.in)；EFI zboot 欄位依 [Linux zboot-header.S](https://github.com/torvalds/linux/blob/v6.12/drivers/firmware/efi/libstub/zboot-header.S)。只解析資料，沒有修改 kernel code、簽章或平台安全設定。

VZ 配置約定：2 vCPU、**1024 MiB RAM**、`console=hvc0 rdinit=/init panic=-1`；serial、virtio socket、read-only virtiofs；**沒有 VZ NIC、磁碟或實體 USB attachment**。首次 preparation manifest 的 768 MiB 已在準備程式改為 1024 MiB，沒有因此重建正在使用的 initrd。共享 tag `psusb-spike`，host `.local/mac-usb-guest-1/share/` → guest `/mnt/host`。`/init` 載入 guest modules、設定 loopback、掛載共享，印出 `GUEST_READY` 後進入 shell。未自動啟動 backend、bridge 或 VirtualHere；後續加入的 guest dummy 介面只供固定本機識別，不提供外部網路。

## 官方 VirtualHere Server 材料

官方 [Generic ARM64 server](https://www.virtualhere.com/usb_server_software) 已下載至 `share/vhusbdarm64`；檔案為 aarch64 靜態 ELF，445,924 bytes。SHA1 `f000ef0aa8306d62a109a235e7830f7d9b52f214` 與官方 SHA1SUM 相符；另記錄 SHA256 `09deced5586ed37b8b3db7a6187d84029f65d487c3c2b24a949b993b3d582077`。最初下載驗證階段未執行；後續 guest 實測已觀測執行版本 **4.8.8**。

只使用官方 [Generic 單裝置免費條件](https://www.virtualhere.com/embedded_server_license)。不修改 License、不重置授權、不用 optimized build。依 [官方配置說明](https://www.virtualhere.com/configuration_faq)，準備下列基本設定：

```ini
ServerName=PS Keyboard Guest Spike
UseAVAHI=0
AllowedDevices=54c/ce6
```

預設 TCP port 7575；**不使用文件列為付費進階項目的 NetworkInterface／TCPPort／SSL／ReverseClients**。guest 無外部網路，dummy 不設 IP／route／NAT；all-interface listener 只經 guest loopback 與明確的 host loopback relay 接入。單裝置免費限制維持不變。

## 依序判定的最小測試

以下順序已納入本輪發現的必要條件；實際已通過的範圍以本文件頂端及試驗收據為準，不能只以 preparation receipt 判定成功。

1. **Guest 可啟動**：看到 `READONLY_SHARE_READY /mnt/host` 及 `GUEST_READY`，`uname -r` 必須為 `6.12.114-0-lts`；`ls /sys/devices/platform/vhci_hcd.0` 應有 status/attach/detach。若不符先修 guest，不能跳往 Mac client。
2. **Bridge**：執行 `start-loopback-bridges`，只建立 VSOCK 33642/7575 到 guest 127.0.0.1 的 relay，不啟動輸出端。host relay 為 127.0.0.1:**33742/7575**。socat 子程序會關閉 FD 3，避免之後誤持有 backend keeper。
3. **Guest backend**：由固定 VIIPER backend agent 交付 `/mnt/host/GuestDualSenseBackend-linux-arm64`，帶 `--guest-candidate-preview`。stdin 必須持有，不可接 `/dev/null`。正確 FIFO 示意如下；實際 API 的 create/remove 以該 backend 材料為準。

```sh
mkfifo -m 600 /run/pskeyboard-backend-control
exec 3<> /run/pskeyboard-backend-control
/mnt/host/GuestDualSenseBackend-linux-arm64 --guest-candidate-preview \
  < /run/pskeyboard-backend-control 3>&- &
backend_pid=$!
# Stop: printf 'stop\n' >&3 ; wait "$backend_pid" ; exec 3>&-
```

4. **先啟動官方 VH server**：載入 dummy／af_packet，套用並核對已保存的固定 guest MAC，再以保留設定啟動官方 `vhusbdarm64`；記錄 `unlicensed max_devices=1`。不以 `-b` 安裝服務，不先啟動 Mac client。初次測試把 USB/IP attach 放在 server 啟動之前，清單未出現裝置；歷史結果與修正依據見下節。
5. **再做 Guest USB/IP 枚舉與 attach**：create 一個 neutral candidate 後，用 `usbip --tcp-port 33641 list -r 127.0.0.1` 取得本輪 busid，再 attach。核對 `usbip port`、`lsusb -d 054c:0ce6` 及 server 找到同一候選；不猜 port 或 busid。若另做短時間 neutral interrupt read，完成必須釋放 read handle，避免阻擋 VirtualHere claim。本輪 guest 讀回與 server 熱插拔發現已通過。
6. **Mac client 的 create/remove 與 USB 交通**：前項通過後，使用已驗簽官方 Mac client，指定 host loopback **7575**、auto-use 關閉；先 neutral，檢查裝置類型並核對停止後移除。這一階段本輪已通過；Sony 與鍵盤仍是下一個獨立測試。

待區分的失敗：guest kernel module/boot、VIIPER control response、usbfs claim/reset、VH server 枚舉、VH 協定轉送、Mac USB 建立、Sony 接受度。每一步可以產生具體成功或失敗證據，不把末端無反應一概歸因於簽章，也不把 VM FAQ 視為已測失敗。

停止順序需由 supervisor 持有：先 neutral／Mac stop-using 並驗證 host 移除，再關官方 server、detach guest USB/IP port、stop backend／等待退出、停止 bridges/VM。未使用 Mac client 的 guest-only 階段則從 server/guest detach 開始。只清理這次 session，不碰實體手把或 Sony。

## Guest 實測進展與缺少 network identity 的修正實驗

2026-10-30，root agent 回報已完成 guest boot、USB/IP attach、`lsusb` 看見 `054c:0ce6`，以及 `usbhid-dump` 收到 64-byte neutral input。這證明目前 guest 的 VHCI → usbfs 通路可用；未因此宣稱 Mac client 或 Sony 已成功。其後官方 VirtualHere Server 4.8.8 在 guest 啟動即報 `Failed to load serial number` 並退出，當時 guest 只有 loopback。

[作者 2021-04-30 的說明](https://www.virtualhere.com/node/3243) 明確要求 Linux server 啟動時，`/sys/class/net` 至少存在一個非 `lo` 的介面；該介面不必真的接上網路。[2024-04-30 同款錯誤討論](https://www.virtualhere.com/node/4335) 中，作者首先檢查 network interfaces 及其 address；他修正虛擬介面相關 bug 後，使用者已進到 `unlicensed max_devices=1`，接著才遇 `getaddrinfo` 無 usable address 的另一個錯誤。這支持目前缺少機器 network identity 的診斷；不是已證明需要付費或缺少 USB 裝置序號。

為保持這個 guest 的正常穩定識別，由 root agent 產生並保存一次 locally administered、unicast 的虛擬 MAC；以後重開沿用。**不借用 host、實體手把或其他人的識別，不改 VirtualHere license，不輪換 MAC 或重置試用狀態，仍只分享一個裝置。** 固定 hostname 可作可讀名稱，但沒有證據顯示 hostname 單獨能取代缺少的 network interface。

本輪準備 `share/dummy.ko`，從同一個已驗簽 Alpine `linux-lts-6.12.114-r0.apk` 抽出並解壓；與正在跑的 `6.12.114-0-lts` 相符、無額外 module dependencies。16,527 bytes，SHA256 `89573074aefe730879cd6d977c5e9f5531812c65a8f7f2701f898f2dd6f3951e`。root 可從已掛載的唯讀 share 載入，無須重新啟動 VM。這個 module 只在 guest 運作。

```sh
insmod /mnt/host/dummy.ko numdummies=0
ip link add psusb0 type dummy
insmod /mnt/host/af_packet.ko
# 由 root 使用本 guest 已保存的固定 MAC；此處不產生隨機值。
ip link set psusb0 address "$PSUSB_GUEST_MAC"
ip link set psusb0 up
```

後續新 VM 曾在設定固定 MAC 時報 `socket(AF_PACKET,2,0): Address family not supported by protocol`。因此另外從同一已驗簽 kernel APK 準備 `/mnt/host/af_packet.ko`，無額外依賴，108,271 bytes，SHA256 `db354c5ec5cfa1ad11d3e826c84fd68d7579c7eba0b7752dd6915f74507f6f18`；收據為 `inspection/USB-GUEST-AF-PACKET-PREPARE.json`。先載入它，再設定並核對介面 address 必須等於第一次實際使用且已保存的 guest MAC，成功後才重新啟動 VH server。不能因設定命令失敗而默默改用新隨機 MAC。

這次運行的 guest 由唯讀 share 手動載入兩個 module；`guest/prepare_guest.py` 的 module 清單已加入 `dummy` 與 `af_packet`，下次準備會將其及所需依賴一起放進 initrd。本次只修改準備程式，沒有重建正在使用的 initrd。持久 guest MAC 與 VirtualHere config 保留在 `.local`，不入 Git，也不寫進公開收據。

首次實驗不加 IP、route、NAT 或外部連線；VSOCK 仍是唯一 host/guest 通路。如果通過 serial gate 後改報 `getaddrinfo` 的 address 錯誤，應記為下一個正常網路初始化條件，不能當作授權拒絕或繼續更換識別。後續更動由 root 根據實測另行安排。

Apple SDK 也已核對：`VZNetworkDeviceConfiguration.attachment` 雖為 nullable/default nil，但 `VZVirtioNetworkDeviceConfiguration` 的說明要求 valid MACAddress 與 attachment 才是有效配置，不能只看 nullable 就宣稱 nil attachment 能建立可用 NIC。若日後需要 VZ NIC，`VZFileHandleNetworkDeviceAttachment` 支援 connected datagram socket，可接只丟棄封包的本機 socketpair而不通 NAT/LAN；這只是備案，未建立或測試。本輪採 guest dummy，無須修改已成功的 runner。

## VirtualHere 發現裝置：已驗證的啟動順序

2026-10-30 後續 root 實測：guest dummy 已有穩定識別，官方 Server 4.8.8 能正常啟動；Mac client 連線成功，保持 `max_devices=1`。但 server 啟動時，已由 USB/IP attach 的 `054c:0ce6` 沒有出現在 client 裝置清單。當時 guest 的 `lsusb` 與 `usbip port` 仍能確認唯一候選存在，尚不能據此判定 udev 缺失或 VirtualHere 排除 VHCI。

root 隨後只對該 session 自己的 guest port 0 做 detach，再重新 attach 同一個 backend busid `1-1`。VirtualHere syslog 立即回報找到 High speed `054c:0ce6`，address 11。**這次結果證實 VirtualHere 4.8.8 能發現 guest VHCI 裝置；問題目前可由熱插拔順序解決。**

本實驗採用的順序因此更新為：先準備並啟動官方 VH server，再將唯一候選 USB/IP attach 到 guest。若已先 attach，先確認擁有的 guest port，再 detach/re-attach；不操作其他 port 或實體裝置。上方最小測試步驟已採此修正順序。

這不是對 server 內部列舉實作的推測；目前只確認「既有裝置起始清單未出現、同一裝置重新熱插拔即被發現」的可重現觀察。沒有為此安裝或啟動 udevd，也沒有下載 strace 或擴充 guest dependencies。後續 Mac neutral 建立／移除已通過，詳見頂端；Sony 接受度仍未驗證。

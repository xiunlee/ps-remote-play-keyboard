# 實體 DualSense：唯讀 HID 規格盤點

日期：2026-10-30（Asia/Taipei）。使用者已連接一隻 PS5 手把並授權作 Mac 開發研究；官方 Remote Play 保持執行。本輪只取得 HID 中繼資料，沒有開啟或獨佔手把、讀取 input／feature 報告、發送 output、註冊輸入 callback、要求權限或操作 Sony 程序。

## 實際觀測

| 項目 | 結果 |
| --- | --- |
| Product | DualSense Wireless Controller |
| Vendor／Product ID | `054c:0ce6` |
| Transport | **USB**；產品名稱有 Wireless 不表示本次使用 Bluetooth |
| Primary usage | Generic Desktop `0x01`／Game Pad `0x05` |
| Report descriptor | 289 bytes |
| Descriptor SHA-256 | `4f48767516627510521512af13c20a064877465122b7bcc7cac1f3b608d37994` |
| Input | report ID `0x01`，63-byte payload，加 ID 共 64 bytes |
| Output | report ID `0x02`，47-byte payload，加 ID 共 48 bytes |
| Feature 最大長度 | 含 ID 共 64 bytes；各 ID 實際宣告長度見 JSON |

本輪以自製 descriptor parser 計算各類型 report 長度，最大長度與 macOS `MaxInputReportSize`／`MaxOutputReportSize`／`MaxFeatureReportSize` 三項中繼資料全部相符。這只是 descriptor 一致性檢查，不是送入或讀回報告測試。

- [DUALSENSE-USB.json](DUALSENSE-USB.json)：篩選後的裝置屬性、descriptor、各 report／field 位移、工具與來源雜湊及未驗項。
- [dualsense-usb-descriptor.hex](dualsense-usb-descriptor.hex)：本次唯讀取得的靜態 HID descriptor，不含原始按鍵、MAC 或裝置序號。
- [inspect_sony_hid.m](inspect_sony_hid.m)：只匹配 Sony `0x054c`、Generic Desktop／Game Pad，以 `IOHIDManagerCopyDevices`／`IOHIDDeviceGetProperty` 讀明確白名單屬性。
- [decode_descriptor.py](decode_descriptor.py)：本機純資料 parser；支援短項目的 report size/count/ID、usage 與 global push/pop，遇到長項目拒絕；不存取裝置。

## 與既有固定上游的關係

唯讀對照既有 `hbashton/VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2` 的 [descriptor.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/device/dualsense/descriptor.go)、[const.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/device/dualsense/const.go)與 [input_scheduler.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/device/dualsense/input_scheduler.go)：Input `0x01` 的欄位排列與長度、Output `0x02` 的長度相符。實體 descriptor 另有上游一般 DualSense descriptor 未列的 feature `0x0b`／`0x0c`，各為 41-byte payload 加 ID；**不能宣稱兩份 descriptor 位元組相同**。

Input `0x01` 的實際 descriptor 宣告以下欄位。Byte 位移包含第一個 report ID byte；供後續 encoder 設計，並非本輪擷取的實體報告內容。

| Byte／bit | Descriptor 宣告 | 固定上游 encoder 的語義 |
| --- | --- | --- |
| 0 | Report ID `01` | USB input report |
| 1–6 | 六個 8-bit axes，usage `30,31,32,35,33,34` | LX／LY／RX／RY／L2／R2 |
| 7 | Vendor usage `ff00:20` | Sequence |
| 8 低 4 bits | Hat switch，null state | 十字方向，中立 `8` |
| 8 高 4 bits、9、10 低 3 bits | 15 buttons | 面板／肩鍵／選項／PS 等 |
| 10 剩餘 bits 與 11 | 13 vendor bits | 保留欄位 |
| 12–63 | 52 vendor bytes | 固定上游編碼 counters、體感、觸控與狀態 |

固定上游以 byte 33／37 的 `0x80` 表示觸控未接觸；gyro 在 16–21、accel 在 22–27、sensor timestamp 在 28–31。這些是上游來源的行為規格，並不是本次實體資料回報；沒有讀取 feature pairing、序號、firmware 或裝置認證資料。

固定上游的 neutral 還包含：accelZ `-8192`（26–27 為 `00 e0`）、trigger status 42／43 為 `09`、battery 53 預設 `2a`、wired status 54 為 `08`。Byte 7 是 UInt8 sequence，12–15 是 UInt32 packet sequence；28–31 與一般 DualSense 的 49–52 皆為 little-endian timestamp，計算為已經過的整數 microseconds × 3、模 2³²。這些值應由輸出 session 自己產生，不從實體序號或 raw input 複製。

### Feature report 來源與未實作範圍

固定上游 [device.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/device/dualsense/device.go) 的一般 DualSense `featureGetHandlers` 只實作以下四種，回應長度包含 report ID；呼叫方要求較短長度時上游會截短。

| ID | 上游回應 | 後續移植限制 |
| --- | --- | --- |
| `05` | 41 bytes，固定 gyro／accel 校正值 | 描述上游合成感測器尺度，不是本次實體校正資料 |
| `09` | 20 bytes，從該虛擬 session 的 MAC 反序填入並加固定欄位 | 不讀取或複製使用者實體 MAC |
| `20` | 64 bytes，建置時間、hardware／firmware 與固定狀態 | 未驗證 Sony 會要求哪些欄位，也不是本次實體韌體版本 |
| `81` | 64 bytes，依先前 `80` command 的 subcommand 回應 | 序號、電壓與溫度來源是虛擬 meta-state，不能用使用者裝置識別資料填入 |

宣告 feature ID 不等於對應協定已實作；本次實體多出的 `0b`／`0c` 尤其沒有本輪 payload 證據。未支援的 get/report type 應明確回應失敗，不把全零或任意長度內容當成已證實相容。第一版純編碼核心不應冒充已具備完整 feature、觸覺或裝置認證。

## 重現與回收

使用全新本機輸出目錄，從 repo 根目錄執行：

```sh
mkdir .local/mac-hid-inspection-new
clang -fobjc-arc -Wall -Wextra -Werror \
  research/macos-native/inspection/inspect_sony_hid.m \
  -framework Foundation -framework IOKit \
  -o .local/mac-hid-inspection-new/inspect_sony_hid
.local/mac-hid-inspection-new/inspect_sony_hid
python3 research/macos-native/inspection/decode_descriptor.py \
  research/macos-native/inspection/dualsense-usb-descriptor.hex
```

本輪工具已正常退出，沒有裝置或 daemon 需要移除；本輪自建執行檔及原始白名單 JSON 在 `.local/mac-hid-inspection-1/`。後續可核對該精確目錄後刪除，不要全域清除 HID／停止手把或 Sony。提交的紀錄不包含使用者家目錄、序號、MAC、帳號或設定。

同一份 descriptor 能被實體手把使用，不代表 CoreHID 建立相同格式後會被 macOS GameController 或 Sony 接受；虛擬裝置建立、讀回、移除與 PS5 操作仍須另驗。

## Mac 原生 USB 報告核心

本輪另建立 [DualSenseUSBReport.swift](../../../src/macos/DualSenseUSBReport.swift)，只從 22 動作 bitmask、明確的 sequence／packetSequence 與已經過的 monotonic microseconds 產生 64-byte USB input。它不取得系統時鐘、不連接裝置、不讀鍵盤或物理報告，也不回應 feature requests；session counter 的提交及傳輸失敗處理由未來 adapter 負責。

[獨立測試](../../../src/macos/DualSenseUSBReportTests.swift)連結**未變更的原 C native 核心**作為動作 oracle，通過 **4294 checks**：22 個動作的獨立預期值、放開／停用、stick／hat 全部 16 種組合、484 種動作配對、512 個固定種子的混合 bitmask、USB byte 位移及 clock／counter wrap。收據見 [USB-ENCODER-TEST.json](USB-ENCODER-TEST.json)。同一輸入與時間參數重複編碼不會消耗計數器；未知動作 bits 即使停用也會拒絕。

可在全新目錄重現：

```sh
mkdir .local/mac-usb-report-new
clang -std=c11 -Wall -Wextra -Werror -O2 -c \
  research/native-merge/keyboard_merge.c \
  -o .local/mac-usb-report-new/keyboard_merge.o
swiftc -parse-as-library -swift-version 6 \
  -module-cache-path .local/mac-usb-report-new/module-cache \
  -import-objc-header research/native-merge/keyboard_merge.h \
  src/macos/DualSenseUSBReport.swift src/macos/DualSenseUSBReportTests.swift \
  .local/mac-usb-report-new/keyboard_merge.o \
  -o .local/mac-usb-report-new/DualSenseUSBReportTests
.local/mac-usb-report-new/DualSenseUSBReportTests
```

本輪自己的 Swift module cache 已精確回收；保留小型測試 binary／object。這 4294 項是純碼驗證，不能計作 macOS 虛擬裝置讀回、Sony 9.5.0 相容或 PS5 實機成功。

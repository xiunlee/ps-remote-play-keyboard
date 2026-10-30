# Linux guest 的受限 DualSense V5 backend

2026-10-30。此目錄從現有 `research/dualsense-preview/backend` 分出獨立入口，只建置及執行純函式測試。**尚未啟動此 backend、建立裝置、掛載 USB 或驗證 Sony**。Linux guest → VirtualHere → Mac 的整條路徑仍屬實驗。

## 產物與來源

固定來源：[hbashton/VIIPER@b78e31e93e84b4cb7c4b4c15b8aace10749106d2](https://github.com/hbashton/VIIPER/tree/b78e31e93e84b4cb7c4b4c15b8aace10749106d2)。上游 ZIP 的 SHA-256 沿用既有已記錄的 `8305750e881e7e955dcf26dc5fedc2b43d55902c9f1fa5b4eca31fe51901b2a5`，下載後重新核對。模組名稱仍是上游的 `github.com/Alia5/VIIPER`；fork 名稱不同不代表改換來源。

使用 [Go 官方下載](https://go.dev/dl/) 的 `go1.27.3.darwin-arm64.tar.gz`，SHA-256 `dd3908cf20959d798c1e06ebe5b72213f0505ce01373b40ce3110975a6cd99c2`，與官方 JSON 清單核對。只解壓至本輪 `.local/mac-usb-backend-1/toolchain`，沒有安裝全域工具或修改 PATH／Go 使用者設定。`GOENV=off`、`GOWORK=off`、`GOTOOLCHAIN=local`、`CGO_ENABLED=0`；Go caches、模組來源、暫存與關閉的 telemetry 全在本輪輸出目錄。

| 產物 | SHA-256 |
| --- | --- |
| `GuestDualSenseBackend-linux-arm64` | `bc3bafce56289ef33f0c474c6a5c6ea78c86739af04c4cb638b79f7e46cdd951` |
| `GuestDualSenseBackend-darwin-arm64` | `78600c9aaa3d287146e195e25fc99b591a0cccf2eabf81e567e33a0d0dd271cf` |

兩者位於 `.local/mac-usb-backend-1`。`file` 確認 Linux 產物是 ARM aarch64 靜態 ELF；Darwin 產物為 arm64 Mach-O。Linux binary 另複製到 `.local/mac-usb-guest-1/share/GuestDualSenseBackend-linux-arm64`，guest 的 read-only virtiofs 路徑為 `/mnt/host/GuestDualSenseBackend-linux-arm64`。複製前後 hash 相同；複製不會執行它。

固定上游沒有 Darwin 的 `attachLocalhostClientImpl`，第一輪 Mac 編譯因此失敗。第二輪以 [autoattach_darwin.go.overlay](autoattach_darwin.go.overlay) 補上**一律回錯誤**的平台 stub，僅供 Mac 原生編譯與離線測試。Go `-overlay` 不改寫任何上游檔案；Linux 產物完全不使用此 overlay。這個 stub 沒有實作 Mac USB attach。

`BUILD.json` 保存本入口、測試、build script、overlay、二進位與測試輸出的 hash。`MODULES.jsonl` 和 `MATERIALS.json` 保存依賴版本；`notices/` 保存上游 GPL v3、實際連結 Go 擴充模組與 Go 標準庫授權原文。完整固定上游保存在 `upstream/`，原 ZIP 保存在 `downloads/`，本入口與 `go.mod`／未變更的上游 `go.sum` 保存在 `module/`。沒有更動既有 Windows 檔案。

## 啟動及停止契約

唯一接受的參數是 `--guest-candidate-preview`，例如 guest 中：

```sh
mkfifo -m 600 /run/pskeyboard-backend-control
exec 3<> /run/pskeyboard-backend-control
/mnt/host/GuestDualSenseBackend-linux-arm64 --guest-candidate-preview \
  < /run/pskeyboard-backend-control 3>&- &
backend_pid=$!
```

Supervisor 持有 FIFO 的 writer。backend 必須重新以唯讀方式打開 FIFO 並關閉繼承的 keeper FD；**不可用 `<&3`**，否則它自己也持有 writer，失去自然 EOF。其他背景程序不要繼承 writer。單向 owner pipe 也適用；不可把 stdin 接到 `/dev/null`。

停止方法：

```sh
printf 'stop\n' >&3
exec 3>&-
wait "$backend_pid"
rm /run/pskeyboard-backend-control
```

精確 `stop` 行、EOF 或讀取錯誤都會清理 API 與 USB server；清理開始後才啟用 15 秒硬退出 watchdog。正常 session 沒有到期計時。進入清理不等於 guest vhci、VirtualHere 或 Mac driver 已移除，外部 owner 必須核對其各自清理結果。

- API 固定 `127.0.0.1:33642`，USB/IP 固定 `127.0.0.1:33641`，**都留在 guest loopback**；沒有 listen override、環境／設定檔載入或自動掛載。
- `AutoAttachLocalClient=false`、`AutoAttachWindowsNative=false`。guest 的原生 `usbip attach`、VirtualHere 及 Mac client 都由外部程序明確管理。
- `ping` 的識別為 `server=PSKeyboard-DualSenseV5-GuestCandidate`、`version=research.guest.1`、`labMarker=pskeyboard-dualsense-v5-guest`、固定 sourceCommit、`autoAttach=false`、`hostAutoAttach=false`、`loopbackOnly=true`。
- 只允許建立 `dualsensegamepadv5`；音訊、Edge、raw input 等 persona 與 VID/PID 覆寫被拒絕。日誌 sink 為 discard，不記錄封包、鍵盤或 Sony 資料。
- root 提供的 Mac loopback `127.0.0.1:33742` → VSOCK `33642` → guest loopback API relay 是外部整合層；backend 自身沒有 NAT、vSocket 或 host listener。

## API 建立裝置與 neutral

這是 TCP API，不是 HTTP。每個管理命令以 **NUL byte `00`** 結尾，開一條連線、送命令、讀完整 JSON 回應至 EOF。caller 必須限制回應大小／時間並檢查錯誤。guest 用 `127.0.0.1:33642`；Mac 只能在 root 的 relay 啟用後用 `127.0.0.1:33742`。

1. `ping\0`：核對上述完整識別與固定 commit。
2. `bus/create\0`：回傳 `busId`，保存自己擁有的 ID，不硬編碼 1。
3. `bus/{busId}/add {"type":"dualsensegamepadv5"}\0`：核對回傳 `busId`、`type`，保存十進位字串 `devId`。USB/IP bus-id 是 `{busId}-{devId}`。
4. **建立裝置後 5 秒內**另開持續連線，送 `bus/{busId}/{devId}\0`，進入 V5 binary stream。沒有正向 stream ACK；JSON 錯誤、立即 EOF、壞 frame 都必須當作失敗。只完成 add 而未持有 stream 的裝置會被上游清除。
5. 送出下列 neutral frame，保持 stream 開啟並持續讀取／驗證回饋。正常輸入 sequence 從 0 開始，每次 frame 加一；不能因 retry 重用 sequence。不要把回饋讀取停住造成子系統阻塞。

V5 input 是 16-byte header + 33-byte state，全部 little endian。header：`VPCM`、version `5`、type `1`、payload length `33` (`UInt16`)、sequence (`UInt32`)、CRC32 IEEE (`UInt32`)。CRC 覆蓋 header bytes 4…11 後接全部 state，不包含 magic 或 CRC 欄位。

State 的 sticks 是有號 int8，中心為 0；neutral 的 state bytes 15 和 20 必須為 `0x80`（兩個觸點 inactive），byte 32 為 `0xe0`（AccelZ = −8192），其他 bytes 皆 0。舊 bool touch 格式的全零不能直接送 V5，否則會變成 active touches。

以下函式只產生 bytes，不連線、不建立裝置：

```python
import struct, zlib

def neutral_frame(sequence):
    state = bytearray(33)
    state[15] = state[20] = 0x80
    state[32] = 0xe0
    crc_input = struct.pack('<BBHI', 5, 1, 33, sequence) + state
    return b'VPCM' + crc_input[:8] + struct.pack('<I', zlib.crc32(crc_input)) + state
```

Sequence 0 的 CRC 為 `0xb88db821`，frame 長度 49。此 state 已用固定上游 `dualsense.NewInputState().MarshalBinary()` 純 codec 比對通過。

停止輸出時先送新的 sequence 的 neutral，再關 stream；以獨立管理連線送 `bus/remove {busId}\0` 並核對回應，只移除自己擁有的 bus。最後停止 backend，另行確認 vhci／VirtualHere／Mac 端釋放。這些操作範例尚未在本輪執行。

## 離線驗證

[build.py](build.py) 只接受已下載且核對 hash 的本地材料；新輸出目錄須包含 `downloads/`、`toolchain/go/` 及 `upstream/VIIPER-<commit>/`。它不下載、不安裝、不啟動 backend。已完成的 `BUILD.json` 所在目錄不可覆寫。

```sh
python3 research/macos-native/guest-backend/build.py .local/mac-usb-backend-new
```

第一次上游 Darwin 編譯失敗的本輪目錄，以明確 `--resume-failed-build` 繼續；完成後不可再用該旗標改寫最終收據。

通過六個 top-level 純測試及兩個 shutdown 子測試：精確參數、persona allowlist、stdin stop／EOF／error、清理順序與卡住時 watchdog、固定 loopback 與禁止 attach 設定、neutral wire codec／CRC。沒有執行 `run()`／`main()`，沒有 socket、USB、driver 或 Sony 呼叫。Linux 產物是交叉編譯；這輪純測試在 Darwin 上執行，不宣稱 Linux runtime 已驗證。結果在 `.local/mac-usb-backend-1/PURE-TESTS.jsonl`。

最後逐一對照 ZIP，確認解壓後的 697 個上游檔案內容未變。已回收自己建立的 `gocache`／`gotmp`，合計 188,861,204 logical bytes；來源、依賴來源／授權、官方工具鏈、最終二進位與收據保留供後續整合與追溯，詳見 `CLEANUP.json`。

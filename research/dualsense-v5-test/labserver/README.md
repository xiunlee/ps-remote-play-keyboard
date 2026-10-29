# 固定候選的使用者空間測試伺服器

這是本專案獨立研究 fixture，**不是 hbashton 的正式 Windows 執行檔，也不是可供玩家接入 Windows 的新後端**。候選正式 CLI 強制要求 usbip-win2 0.9.7.7；本機既有 0.9.8.1 保持不變。本 fixture 不修改或繞過正式 CLI，而是直接使用固定原始碼的 USB/IP、API 與 DualSense 套件，測試不經驅動的協定往返。

固定來源：[`b78e31e93e84b4cb7c4b4c15b8aace10749106d2`](https://github.com/hbashton/VIIPER/tree/b78e31e93e84b4cb7c4b4c15b8aace10749106d2)，tag `v0.1.9-rc4.6.6`。由 GitHub codeload 下載的來源 ZIP SHA-256 為 `8305750E881E7E955DCF26DC5FEDC2B43D55902C9F1FA5B4ECA31FE51901B2A5`。建置腳本先核對該 ZIP，再解壓到全新輸出目錄；上游既有檔案不修改。

```powershell
.\research\dualsense-v5-test\labserver\Build-LabServer.ps1 `
  -SourceArchive '<已核對的固定來源 ZIP>' `
  -GoExecutable '<已核對的 Go 1.26.2 或更新版 go.exe>' `
  -OutputDirectory '<全新隔離輸出目錄>'
```

腳本只編譯、不啟動。Go 環境變數只於本次程序生效，快取與相依模組全部保存在指定輸出目錄，不安裝到系統、不改 PATH。產出 `BUILD.json`、`MODULES.jsonl` 與獨立 `DualSenseV5LabServer.exe`；不能覆寫舊輸出。取得 Go 相依模組需要網路；它們仍按固定來源 `go.mod`／`go.sum` 驗證。

啟動僅接受 `--isolated-userspace-only`。API 固定 `127.0.0.1:33442`、USB/IP 固定 `127.0.0.1:33441`；任一埠被占用即失敗，不尋找或終止占用者。僅接受 `dualsensecombinedaudioduplexv5`，不接受型號識別覆寫或其他裝置。沒有讀入全域設定、憑證、鍵位，沒有托盤、鍵盤 hook、驅動 probe 或自動接入。`AutoAttachLocalClient` 與 `AutoAttachWindowsNative` 明確為 false；原始封包記錄關閉。

程序在 180 秒後正常關閉；無論何種關閉阻塞，195 秒後僅退出自身。也可向它自己的標準輸入傳送 `stop`，或在先核對 `ping` 的 `labMarker` 與 `sourceCommit` 後，向本次 API 發送 `lab/stop\0`。所有 server 關閉只影響本程序擁有的 socket 與虛擬記憶體裝置。不要以全域 `Stop-Process -Name viiper` 或任何 USBIP detach 作為清理。

本 fixture 的 `ping` 明確回覆 `server=PSKeyboard-DualSenseV5-Lab`、`version=research.1`、`labMarker=pskeyboard-dualsense-v5-userspace-only` 與固定來源 commit；不冒充正式候選執行檔。這些標示描述 fixture 設定，不能代替 Windows／Sony 驗收。使用者空間 USB/IP 通過也不能推論現有 0.9.8.1 驅動與此候選相容。

執行前需審閱原始碼與建置紀錄，確認隔離埠沒有使用者的服務；執行後核對現有產品與 Sony 程序未變。正式散布涉及上游 GPL 及相依 notices／對應原始碼，本研究不公開第三方二進位。

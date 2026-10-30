# Mac 首輪交接紀錄

日期：2026-10-30（Asia/Taipei）。研究基準為 `codex/commercial-preview` 的 `1e46bd0bf5774a724910621ec4d648df27c146ae`。本輪接手 Mac 環境、查核輸出候選並完成純合成資料測試；**尚未建立虛擬裝置，也未驗證官方 Remote Play／PS5 接受輸入，沒有可交付的 Mac App。** 產品版本仍是 Windows 0.1.1。

## 本機與來源

原工作目錄只有未提交任何 commit、沒有 remote 的空白 Git 專案。本輪保留該目錄，在其 `ps-remote-play-keyboard/` 子目錄 clone 指定分支；初始工作樹乾淨，HEAD 與遠端分支相同，GitHub 預設分支也確認為 `codex/commercial-preview`。公開紀錄僅保留相對工作目錄，不含使用者家目錄、裝置序號、帳號或設定。

| 項目 | 唯讀盤點結果 |
| --- | --- |
| 系統 | macOS 26.6.2，build 25G83，arm64 |
| 編譯工具 | Command Line Tools；SDK 26.5、Apple clang 21.0.0、Swift 6.3.3 |
| 其他工具 | Python 3.9.6、Git 2.50.1；目前 PATH 未找到 Go／.NET |
| Sony 官方程式 | `/Applications/RemotePlay.app`，9.5.0，`com.playstation.RemotePlay`，arm64／x86_64 universal |
| Sony 身分 | Developer ID：Sony Interactive Entertainment Inc.；team `8UT4NVUACP`；`codesign --verify --deep --strict` 通過 |
| Sony 執行狀態 | 初始盤點未發現執行程序；稍後唯讀快照已發現 Remote Play 執行中。狀態變動原因未確認，本輪未發出啟動、退出或更新命令，也未讀取帳號資料 |

機器可讀盤點見 [ENVIRONMENT.json](ENVIRONMENT.json)。PATH 未找到不等於全機沒有安裝；簽章驗證與程序快照也不等於 Remote Play 已連線或可接受虛擬手把。

2026-10-30 查核 GitHub latest API 仍是 [v0.1.1](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.1)，Release ID `427807747`，`prerelease=false`。附註 tag object `4a9f43c062d94152040cc4c0eedc605767744bb2` 解參照後為交接指定的來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`，不是以 tag object 取代來源 commit。

對照該 tag 的 45 筆 Windows 建置來源雜湊：44 筆 Git blob 位元組完全一致；`LICENSE` 的 Git blob 使用 LF，轉成 CRLF 後符合 Windows 紀錄的 SHA-256。這是換行差異，沒有修改授權檔或舊建置清單。本輪查核發行中繼資料、Git 固定來源及既有收尾收據，**沒有重新下載四個發行附件或重做 Windows 建置／畫面驗收**。

## 已驗證與待驗證

| 層次 | 本輪結果 | 限制 |
| --- | --- | --- |
| 原始 native C 核心 | 在 arm64 Mac 以 clang 編譯；既有 5743 項合成報告檢查通過 | 不是 C# ABI、真實鍵盤或 Mac HID 測試 |
| native dylib＋Python 參考協定 | 124 項檢查、22 動作向量；neutral／release、長度、CRC、序列與記憶體 EOF 關閉 | Python 參考模型不是原 C# codec／transport，也不是 Go 後端 |
| macOS 輸出候選 | 已研究 CoreHID／IOHIDUserDevice、DriverKit 與 USB/IP，見[候選比較](OUTPUT-FEASIBILITY.md) | 公開 API 存在不等於所需 entitlement 已獲准或 Sony 接受 |
| CoreHID 最小 probe | Swift arm64 編譯成功；無參數、self-test、dry-run 均通過純資料檢查 | 未執行 live mode；descriptor 的 OS 接受、權限、讀回及移除仍待驗 |
| Mac 裝置層 | 尚未建立、送入報告、讀回或移除虛擬裝置 | 真實清理、權限與控制器辨識待驗 |
| Sony／PS5 | 未執行 | 無本輪操作成功證據 |

詳細範圍、重跑指令、依賴與來源雜湊、暫存回收見 [OFFLINE-RESULTS.md](OFFLINE-RESULTS.md)及[離線收據](records/mac-offline.1/RESULT.json)。本輪沒有安裝系統元件、要求新權限、擷取鍵盤或開啟後端 listener；未將 Windows 虛擬裝置工具搬到 Mac 執行。

probe 的[編譯與離線收據](probe/BUILD-DRY-RUN.json)另存。獨立審查核對預設路徑不進入裝置 API，並修正 pending latch 必須依實際 executable 路徑定位；最後重編已納入修正。自建中間 build 與 Swift module cache 已逐項移除，只保留約 190 KiB 的最後測試程式；完整研究來源與收據保留，不把回收邏輯 bytes 當成磁碟淨增加量。

## 下一步

先使用[輸出可行性問題清單與試驗門檻](OUTPUT-FEASIBILITY.md)確認 CoreHID 的應用程式 entitlement／簽章途徑，再檢視 [最小 probe](probe/README.md)。先驗一個本程式自有 generic gamepad 的建立、neutral／按下／放開、輸入讀回及完全移除，通過後才另排 Sony 相容性試驗。generic HID 成功不能直接宣稱 DualSense 相容。

依 [MAC-HANDOFF.md](../../docs/MAC-HANDOFF.md) 的既定範圍，新系統權限或可能影響現有連線的動作必須先說明具體方案、再安排本人操作；本輪只準備與離線驗證，不自動進行該步。不要為試驗降低系統安全設定、改用第三方 Remote Play 或安裝付費依賴。

Windows 0.1.1、歷史 tags／Release、個人鍵位／備份保持原樣。本輪研究沒有發布新版本；下一個產品程式變更版本仍依原規則為 0.1.2。

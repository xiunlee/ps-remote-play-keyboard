# 版本控管

## 版本與來源

- 根目錄 `VERSION` 是建置版本號的唯一來源。依使用者最新要求，**首個正式發行版改用 `0.1.0`，目前準備中**；之後使用 `主版.次版.修訂`，不再讓對外版號附帶 alpha 序號。歷史 `主版.次版.修訂-alpha.序號` 標籤與附件保留，不能重新命名或移動。命名調整不等於實機驗收或穩定性改善。
- **0.1.0-alpha.12 九語介面版已公開為免費 alpha**，標籤固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`；最終乾淨建置、公開封裝、四附件匿名下載雜湊核對、本機獨立交付與桌面入口均已完成，沒有代使用者啟動一般模式。本版逐項實機驗收仍待完成。0.1.0 的固定來源、最終建置、封裝、發布及交付須另行完成，不預填結果。
- alpha.11 固定來源 `6dbc154a6a887398796d442fd34b73455672aba3` 與附件保持不變；根目錄 README 的現行公開下載已更新至 alpha.12。發布後的證據與文件可另作提交，不改寫 alpha.12 建置來源。
- alpha.10 來源固定於 `8359c1ded5cf3310ad85ed3b42e0f47fd55da586`；alpha.10／11 標籤均已推送並核對，歷史標籤不變。見 [alpha.10 紀錄](../research/dualsense-preview/ALPHA10-TRIAL.md)與 [alpha.11 紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)。
- 每次交付有應用程式碼變更的新版，就依使用者慣例增加版號並更新 [CHANGELOG.md](../CHANGELOG.md)：**V0.1.0 → V0.1.1 → … → V0.1.9 → V0.2.0**，之後不再使用 alpha。根目錄 `VERSION` 保存不含前綴的數字，UI 可顯示 `v0.1.0`，Git 標籤使用小寫 `v0.1.0`。不要自行改成 `0.1.10`，也不預設尚未指定的更高位進位規則。不能改寫已發布版本的二進位、來源或標籤。
- 尚未建立版本標籤、交付或公開發布的候選可在同一版號下提交修正，但每次仍建置到全新目錄，保留各自來源提交與雜湊；不得覆寫舊候選產物或把未發布建置說成已交付。
- `v<版本號>` 為附註 Git 標籤，固定經測試的原始碼提交。禁止強制移動、刪除重建或強制覆寫遠端標籤。
- 編譯檔的 ProductVersion 包含版本與 Git 短碼；未提交工作區另有 `.dirty`。視窗顯示版本，狀態／啟動紀錄保存來源提交及 dirty 狀態。
- 驗收文件與建置清單可於交付後另作提交，不因此移動版本標籤。alpha.9 的 PS5 操作成功回報不等於新版每個 session 已獲 Sony 接受。2026-10-29《Final Fantasy Resonance》的使用者正常操作回報未指定工具 build，也不能算作 alpha.12 逐鍵或連點驗收。
- `alpha.5` 是事後命名的成功基準，原檔沒有內嵌版本。歷史校驗與來源保留；本機副本可依清理授權移除，事後重建不能宣稱就是當初原檔。

## 建置與驗證

1. 更新 `VERSION` 與變更紀錄，完成來源審查及離線測試；提交程式碼並保持工作區乾淨。
2. **現行 DualSense 主線**使用以下流程，每個輸出目錄必須尚不存在；精確重建已發布的 alpha.11／12 須另用該固定標籤的 checkout：

   ```powershell
   .\research\dualsense-preview\backend\Build-Backend.ps1 `
     -SourceArchive <固定上游來源ZIP> -GoExecutable <已核對Go執行檔> `
     -OutputDirectory <全新後端建置目錄>

   .\research\dualsense-preview\Build-Candidate.ps1 `
     -BackendDirectory <上述後端建置目錄> -Zig <Zig執行檔完整路徑> `
     -OutputDirectory <全新玩家建置目錄> -RunTests -RequireClean

   .\research\dualsense-preview\Verify-Candidate.ps1 `
     -BuildDirectory <玩家建置目錄> -RequireClean
   ```

   `-Zig` 必填，不再使用 alpha.10 的 `-MappingDirectory`。建置從未變更的 `research/native-merge/keyboard_merge.c`／`.h` 編譯 mapping DLL，不依賴舊版二進位檔。固定 Zig 0.17.0，套件／執行檔雜湊及 native 測試由建置流程核對；保存 `NATIVE-BUILD.json`、`NATIVE-MATERIALS.json` 與適用 notices。

3. 後端固定 hbashton/VIIPER commit `b78e31e93e84b4cb7c4b4c15b8aace10749106d2`，以受限入口建置，不使用原候選的舊 native attach。MATERIALS 公開輸出須移除本機個人路徑；這不表示控制協定已更換。
4. `BUILD.json`／`SHA256SUMS.txt` 記錄版本、提交、來源與產物雜湊、工具、依賴及測試狀態。來源在建置中變動、版本標籤指向別份提交或輸出目錄已存在時，必須停止，不覆寫舊產物。
5. 建置與離線驗證不安裝驅動、不接入 Windows 裝置、不啟停 Sony。離線渲染只使用合成設定，不開啟瀏覽器／郵件程式或擷取真實按鍵。
6. alpha.12 另納入 `PreviewText.cs`／`PreviewTextTests.cs`：核對主介面 50 個文字鍵與共用 93 個文字鍵的九語覆蓋、格式參數、英文回退、固定金額與回饋主旨，再檢查九語主畫面及設定頁排版。純資源或離線繪圖通過不能代替母語校閱、高 DPI 實機或 Sony 操作驗收。

雜湊只證明檔案身分與完整性，不等於數位簽章或 Sony 實機驗收。alpha.12 最終來源通過 **5816 項 C# 檢查**（144＋2472＋149＋276＋101＋36＋167＋2471）、5743 項 native 與 ABI 檢查、33／33 項公開包稽核；18 張最終離線 UI 逐張目視，四附件匿名下載雜湊一致。後端沿用 r4，其先前 4 個 Go 頂層測試及 2 個子情境結果另列，不冒稱本輪重跑。詳見 [alpha.12 發布紀錄](../research/dualsense-preview/ALPHA12-TRIAL.md)；三個 runtime 二進位均為 NotSigned。r4 使用相對 go.mod replace，避免將本機來源路徑嵌入二進位，未改控制協定。alpha.11 的歷史結果保留於[該版紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)。

`research/controller-free-preview/Build.ps1` 與 `packaging/Verify-ControllerFreeBuild.ps1` 僅供 alpha.8 PS4／舊 DualSense 歷史路線。`packaging/Build-Preview.ps1` 是 alpha.4 實體中繼封裝工具；不能用上述舊工具交付目前主線。

## 免費公開發布

- 原始碼使用 [GPL-3.0-only](../LICENSE)，作者 **xiunlee**；詳見 [COPYRIGHT.md](../COPYRIGHT.md)。儲存庫維持公開，預設及工作分支均為 `codex/commercial-preview`；因使用者指出公開首頁仍舊資訊而調整預設顯示，`main` 保留歷史，沒有合併。分支名稱不代表收費計畫。
- 測試通過後，以附註標籤固定來源並推送指定分支／標籤，再核對遠端；不移動既有 tag。歷史 alpha 測試版使用 GitHub **pre-release**；0.1.0 作為首個正式發行版，只有完成自身建置、封裝與下載核對後才記為已發布，仍明示未簽章及待實機驗收項目。
- 公開執行包與完整對應原始碼包須在同一 Release 提供。來源包含本專案精確來源、固定 VIIPER 與實際 Go 依賴來源、native 來源、建置腳本及重建說明；執行包保留各元件 notices、材料清單與 SHA-256。
- 發布前檢查來源包是否足以重建所交付元件，並核對第三方授權、公開檔案白名單及個人路徑排除；不要只附本專案 source ZIP 就宣稱後端對應原始碼完整。
- 本包不含 USBip 驅動，也不會自動安裝或更換。下載頁清楚列出 0.9.8.1 成套先決條件與未驗收項目。完整核對見 [發行工作表](../packaging/RELEASE-CHECKLIST.md)。
- 使用者最後指定[PayPal 贊助頁](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)，公開頁設定已唯讀核對為啟用、USD 1.00，以自願贊助 US$1 幫三隻貓咪買罐頭為文案；程式僅在點擊後開啟瀏覽器，不接收付款資料。最終來源、文件及包內只保留此入口，排除先前已撤回的連結。HTTP GET 或頁面核對不等於實付驗收入帳，也不阻擋完成核對的免費版公開發布。
- [alpha.11 已發布頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11)：Release ID `427698167`，`draft=false`、`prerelease=true`。四項附件均完成公開下載雜湊核對；本次收尾文件可超前固定標籤，但不重建或替換已發布附件。
- [alpha.12 已發布頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.12)：Release ID `427756025`，固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`；Windows ZIP、完整對應原始碼 ZIP、RELEASE.json 與 SHA256SUMS.txt 四附件匿名下載後雜湊全數吻合。

## 更新、本機清理與回退

- 使用者要求本機舊版本清除，只留最新版本；alpha.12 已交付後清理局部執行，因接續準備 0.1.0 而中斷，尚未完成。待 0.1.0 最終交付後再核對清理至唯一現行版本，不預填刪除數量或釋放空間。保留必要依賴、可追溯紀錄、個人設定／備份；公開發布不代表本機清理或 Windows 最終交接已完成。
- 新版先建置到全新資料夾並驗證，不能覆寫運行中的版本。舊版正常退出後才切換／清除，不讓兩版爭用 F8 或同一組服務埠。
- 清理前核對解析後的完整路徑、專案歸屬與程序占用，只刪確認的舊執行檔、捷徑、重複建置與封存。不對工具根目錄整包遞迴刪除，不停止 Sony、驅動或其他軟體。
- 不刪 `%LOCALAPPDATA%\PSKeyboard\bindings.json`、`profile-backups` 或最新版必要檔案。清理完成後補記範圍、保留項目與驗證結果。
- GitHub 歷史標籤、PS4 原始碼、研究、建置與雜湊紀錄繼續保留。清舊後如需回退，依舊標籤與固定依賴另行重建，不能再指示開已移除的捷徑。
- 設定與程式分離；alpha.9 唯讀載入，alpha.10 起僅使用者按儲存且成功後更新格式 2。開啟或取消不寫檔；格式變更前另存備份，不用舊程式強行覆寫新格式。

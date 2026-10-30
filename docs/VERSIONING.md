# 版本控管

## 版本與來源

- **目前最新 Windows v0.1.4：**[發行紀錄](../research/dualsense-preview/RELEASE-0.1.4.md)，公開發行下載僅保留 v0.1.4；v0.1.3 和其他較早版本轉為草稿，附件及標籤保留。下一個程式變更版號為 0.1.5；下方較早的發行和本機保留狀態屬歷史紀錄。
- **各平台的公開發行下載只保留該平台最新版本。** 較早版本的附件與 Release 保留為草稿；舊 tag 不刪、不移動，公開原始碼歷史仍存在。草稿中的附件下載連結不對外，歷史紀錄不能誤稱現仍公開。
- **歷史 Windows v0.1.2（美術優化）**：固定來源 `da12240649df74dc4e2846404317e8a91b231a05`，四附件匿名下載通過，40／40 公開包稽核通過；[發行紀錄](../research/dualsense-preview/RELEASE-0.1.2.md)。本版實機驗收仍待完成。
- 根目錄 `VERSION` 是 Windows 建置版本來源；Mac 獨立使用 [Mac manifest](../packaging/macos/version.json)。依使用者最新要求，**首個正式發行版 `v0.1.0` 已發布**，固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`；之後使用 `主版.次版.修訂`，不再讓對外版號附帶 alpha 序號。歷史 `主版.次版.修訂-alpha.序號` 標籤與附件保留，不能重新命名或移動。命名調整不等於實機驗收或穩定性改善。
- **Mac 0.1.3 發行候選**：`codex/mac-0.1.3-release` 與 `codex/commercial-preview` 整合 Windows 0.1.4 及 Mac 最終品牌建置，目前尚未正式公開發布。Mac manifest 為 `0.1.3 / build 4`；Windows 的 `VERSION=0.1.4` 不改變或阻擋 Mac 建置／封裝。最終對應來源以交付包的 `RELEASE.json`／`SOURCE-MANIFEST.json` 為準；較早的 `e9a9bca` 來源及封裝收據保留為歷史。Mac 採 `mac-v<版本>` 標籤，個別來源、產物及實機驗收分開記錄。品牌為「上班不要玩／Three Cats Keybridge」，固定 App 身分不變。[Mac 驗證清單](MAC-RELEASE-READINESS.md)。
- **歷史 Windows v0.1.1 國際化品牌修正版**：固定來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`。中文保留「上班不要玩」，七個非中文語系統一為 **PS Remote Play Keyboard**，包含標題、說明、設定頁、回饋主旨及英文文件；獨立建置、公開封裝、四附件匿名下載核對與本機交付完成，沒有覆寫 v0.1.0。
- **0.1.0-alpha.12 九語介面版保留為歷史公開 alpha**，標籤固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`；當版乾淨建置、公開封裝、四附件匿名下載雜湊核對及本機交付均已完成，沒有代使用者啟動一般模式。其測試結果不取代 0.1.0 的獨立結果。
- alpha.11 固定來源 `6dbc154a6a887398796d442fd34b73455672aba3` 與附件保持不變；根目錄 README 的現行 Windows 公開下載已更新至 v0.1.4。發布後的證據與文件可另作提交，不改寫任何已發布版本的建置來源。
- alpha.10 來源固定於 `8359c1ded5cf3310ad85ed3b42e0f47fd55da586`；alpha.10／11 標籤均已推送並核對，歷史標籤不變。見 [alpha.10 紀錄](../research/dualsense-preview/ALPHA10-TRIAL.md)與 [alpha.11 紀錄](../research/dualsense-preview/ALPHA11-TRIAL.md)。
- 每次交付有應用程式碼變更的新版，就依使用者慣例增加版號並更新 [CHANGELOG.md](../CHANGELOG.md)：**V0.1.0 → V0.1.1 → V0.1.2 → … → V0.1.9 → V0.2.0**，之後不再使用 alpha。各平台的版本來源保存不含前綴的數字；UI 應依該建置版本顯示，Mac 例如 **Three Cats Keybridge V 0.1.3**，中文語系為 **上班不要玩 V 0.1.3**；已發布 Windows 0.1.2 的歷史顯示名稱不改寫。Windows Git 標籤使用 `v<版本號>`，Mac 使用 `mac-v<版本號>`；只有實際建立及發布後才能記為已發布。不要自行改成 `0.1.10`，也不預設尚未指定的更高位進位規則。不能改寫已發布版本的二進位、來源或標籤。
- 尚未建立版本標籤、交付或公開發布的候選可在同一版號下提交修正，但每次仍建置到全新目錄，保留各自來源提交與雜湊；不得覆寫舊候選產物或把未發布建置說成已交付。
- Windows `v<版本號>` 與 Mac `mac-v<版本號>` 均為附註 Git 標籤，固定經測試的原始碼提交。禁止強制移動、刪除重建或強制覆寫遠端標籤。
- 編譯檔的 ProductVersion 包含版本與 Git 短碼；未提交工作區另有 `.dirty`。視窗顯示版本，狀態／啟動紀錄保存來源提交及 dirty 狀態。
- 驗收文件與建置清單可於交付後另作提交，不因此移動版本標籤。alpha.9 的 PS5 操作成功回報不等於新版每個 session 已獲 Sony 接受。2026-10-29《Final Fantasy Resonance》的使用者正常操作回報未指定工具 build，也不能算作 v0.1.4 逐鍵或連點驗收。下一個程式變更版本為 **0.1.5**。
- Mac 候選沿用同一個 `~/Applications/PSKeyboardMacDualSense.app`、bundle identifier 與固定簽章身分，透過版號區分迭代；設定及備份保持不變。Mac 遊戲成功證據屬 alpha.1，後續版本需另行驗收。
- `alpha.5` 是事後命名的成功基準，原檔沒有內嵌版本。歷史校驗與來源保留；本機副本可依清理授權移除，事後重建不能宣稱就是當初原檔。

## 發行頁固定格式

使用者於 2026-10-30 指定以已公開的 [Windows v0.1.2 發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.2)為固定格式。後續沿用相同基礎內容、段落順序及編號清單，不自行重寫介紹或增加設計來源說明。

1. 繁中順序固定為「軟體功能」「注意事項」「適合使用系統」「此版本更新」，最後是 `**發布時間：**`。
2. 分隔線後保留英文相同順序：`Features`、`Important notes`、`Supported system`、`What's new in this version`，最後是 `**Published:**`。
3. Mac 首次套用修正品牌及必要的平台事實：上班不要玩／Three Cats Keybridge、鍵盤控制與模擬手把的中性介紹、獨立且與 Sony 無隸屬或背書關係的中英文段落、Apple Silicon／macOS 15+、Esc、App 管理的官方 VirtualHere 下載、輔助使用及實際自簽／未公證狀態，不複製 Windows 的 F8、.NET 或 usbip-win2 安裝要求。[Mac 固定草稿](MAC-RELEASE-NOTES.md)是後續 Mac 發行頁基底。
4. **未來只更新「此版本更新／What's new in this version」及實際發布時間**，沿用已確認的平台基礎內容；實際品牌、功能或系統需求改變時，必須同步更正固定段落，不能為保持範本而留錯誤。0.1.3 更新欄僅寫「美術優化」及 `Visual improvements`，不列外部設計名稱、技能名稱或參考來源。
5. GitHub 發行頁的實際時間只按 `published_at` 換算台北時間，不預填預定或本機建置時間。不可變的隨包發行稿以該版本 GitHub 發行頁連結取代時間，避免在附件永久留下「未發布」或事後改動附件。詳盡驗證與未驗範圍保留在同版安裝手冊、驗證文件及產物收據，不塞入更新項目。

## 建置與驗證

1. 更新相應平台的版本來源與變更紀錄，完成來源審查及離線測試；提交程式碼並保持工作區乾淨。
2. **現行 DualSense 主線**使用以下流程，每個輸出目錄必須尚不存在；精確重建 v0.1.1 或其他歷史版本須另用該固定標籤的 checkout：

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

**v0.1.0 獨立乾淨建置**通過 5834 項 C# 檢查（162＋2472＋149＋276＋101＋36＋167＋2471）、5743 項 native 與 ABI 檢查、67 項版號純邏輯檢查，以及 35／35 項公開包稽核。九張新版主畫面已逐張目視；九張設定頁與 alpha.12 圖片 SHA-256 完全相同，沿用已完成的視覺審閱。三個 runtime 二進位仍為 NotSigned，後端仍沿用 r4；詳見 [正式發布紀錄](../research/dualsense-preview/RELEASE-0.1.0.md)。

**v0.1.1 獨立乾淨建置**通過 **6912 項 C# 檢查**（162＋2890＋149＋276＋101＋36＋167＋3131）、5743 項 native 及 C# ABI 驗證，以及 **39／39 項公開包稽核**。九張最終主畫面全部目視；九張設定頁與已審閱的 v0.1.0 圖片 SHA-256 相同。開發階段另有 11 個離線排版情境、0 項問題；不將其冒充實際高 DPI 驗收。後端沿用 r4 及先前 Go 證據，未重跑 Go 測試；三個 runtime 二進位仍為 NotSigned。詳見 [v0.1.1 發布紀錄](../research/dualsense-preview/RELEASE-0.1.1.md)。

`research/controller-free-preview/Build.ps1` 與 `packaging/Verify-ControllerFreeBuild.ps1` 僅供 alpha.8 PS4／舊 DualSense 歷史路線。`packaging/Build-Preview.ps1` 是 alpha.4 實體中繼封裝工具；不能用上述舊工具交付目前主線。

## 免費公開發布

- 原始碼使用 [GPL-3.0-only](../LICENSE)，作者 **xiunlee**；詳見 [COPYRIGHT.md](../COPYRIGHT.md)。儲存庫維持公開，預設／Windows 工作分支為 `codex/commercial-preview`，本次 Mac 發行工作分支為 `codex/mac-0.1.3-release`；因使用者指出公開首頁仍舊資訊而調整預設顯示，`main` 保留歷史，沒有合併。分支名稱不代表收費計畫。
- 測試通過後，以附註標籤固定來源並推送指定分支／標籤，再核對遠端；不移動既有 tag。歷史 alpha 測試版使用 GitHub **pre-release**；0.1.0 作為首個正式發行版，只有完成自身建置、封裝與下載核對後才記為已發布，仍明示未簽章及待實機驗收項目。
- 公開執行包與完整對應原始碼包須在同一 Release 提供。來源包含本專案精確來源、固定 VIIPER 與實際 Go 依賴來源、native 來源、建置腳本及重建說明；執行包保留各元件 notices、材料清單與 SHA-256。
- 發布前檢查來源包是否足以重建所交付元件，並核對第三方授權、公開檔案白名單及個人路徑排除；不要只附本專案 source ZIP 就宣稱後端對應原始碼完整。
- 本包不含 USBip 驅動，也不會自動安裝或更換。下載頁清楚列出 0.9.8.1 成套先決條件與未驗收項目。完整核對見 [發行工作表](../packaging/RELEASE-CHECKLIST.md)。
- 使用者最後指定[PayPal 贊助頁](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)，公開頁設定已唯讀核對為啟用、USD 1.00，以自願贊助 US$1 幫三隻貓咪買罐頭為文案；程式僅在點擊後開啟瀏覽器，不接收付款資料。最終來源、文件及包內只保留此入口，排除先前已撤回的連結。HTTP GET 或頁面核對不等於實付驗收入帳，也不阻擋完成核對的免費版公開發布。
- [alpha.11 已發布頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.11)：Release ID `427698167`，`draft=false`、`prerelease=true`。四項附件均完成公開下載雜湊核對；本次收尾文件可超前固定標籤，但不重建或替換已發布附件。
- [alpha.12 已發布頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0-alpha.12)：Release ID `427756025`，固定來源 `f2c0b1759610e27da0d3bf835b4faeef07030d7a`；Windows ZIP、完整對應原始碼 ZIP、RELEASE.json 與 SHA256SUMS.txt 四附件匿名下載後雜湊全數吻合。
- [v0.1.0 正式發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.0)：Release ID `427775269`，`prerelease=false`，發布時以 `make_latest=true` 設為最新；當時 GitHub latest API 與四附件匿名下載的 bytes／SHA-256 已核對。本機程式、封存及桌面入口已交付，沒有啟動一般模式。固定來源 `ca1d24fd96aa8b327650b1bd82bff08f2e08a7fe`，執行包 6,283,796 bytes、SHA-256 `9C28E917B8343C18591BB4A37142EBA32ACA5EEC57F2914A31889DB147F1552F`；完整來源包 12,416,180 bytes、SHA-256 `B29C57B0C7628ADAE8B9DBCB149D788C9D4BE2489CC7A07BCD293742ECD2F939`。公開附件雜湊另見[清單](../packaging/build-records/0.1.0-archives.json)。

- [v0.1.1 正式發行頁](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.1)：Release ID `427807747`，`prerelease=false`，發布當時已核對為 GitHub latest。四個公開附件匿名下載的 bytes／SHA-256 全部通過，本機獨立程式、封存與桌面入口已交付；固定來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`。本版附件雜湊見[清單](../packaging/build-records/0.1.1-archives.json)。

## 更新、本機清理與回退

- **v0.1.0 交付後的清理已完成**：最後 26 項、1,583,433,983 bytes，加上先前 92 項、4,006,258,013 bytes，累計 118 項、**5,589,691,996 logical bytes**。這是包含重建暫存的累計刪除邏輯大小，不是淨磁碟可用空間增加量；97 項原本已不存在，未計作本輪刪除，另刻意保留 2 份小型 log。個人設定／備份、驅動、Sony 與 Git 標籤比對通過。此紀錄不含後續 0.1.1 重建暫存清理。
- **v0.1.1 本輪清理已完成**：另移除 9 項、**753,491,748 logical bytes**，失敗 0 項；101 項略過中，99 項原已不存在、2 份小型 log 保留。`versions` 與 `versioned-archives` 都只剩 0.1.1；個人設定／備份、驅動、Sony 及標籤前後一致，`preservationVerified=true`。本輪邏輯刪除大小同樣不是淨磁碟差。
- 新版先建置到全新資料夾並驗證，不能覆寫運行中的版本。舊版正常退出後才切換／清除，不讓兩版爭用 F8 或同一組服務埠。
- 清理前核對解析後的完整路徑、專案歸屬與程序占用，只刪確認的舊執行檔、捷徑、重複建置與封存。不對工具根目錄整包遞迴刪除，不停止 Sony、驅動或其他軟體。
- 不刪 `%LOCALAPPDATA%\PSKeyboard\bindings.json`、`profile-backups` 或最新版必要檔案。後續新版交付後如再清舊，另記該次範圍、保留項目與驗證結果，不重複累計前次刪除。
- GitHub 歷史標籤、PS4 原始碼、研究、建置與雜湊紀錄繼續保留。清舊後如需回退，依舊標籤與固定依賴另行重建，不能再指示開已移除的捷徑。
- 設定與程式分離；alpha.9 唯讀載入，alpha.10 起僅使用者按儲存且成功後更新格式 2。開啟或取消不寫檔；格式變更前另存備份，不用舊程式強行覆寫新格式。

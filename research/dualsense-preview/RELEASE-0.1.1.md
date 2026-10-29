# v0.1.1 正式發布與驗證紀錄

日期：2026-10-29（Asia/Taipei）。固定標籤 `v0.1.1`，乾淨來源 `3eab9678afe6ea1eddcd35080f2c7e15c1051629`。

**本版已正式公開發布及本機交付。** [GitHub Release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.1)，名稱 **PS Remote Play Keyboard V 0.1.1**，ID `427807747`，`draft=false`、`prerelease=false`，發布時已設定並 GET 核對為 latest。四個附件均匿名重新下載，大小及 SHA-256 相同。沒有啟動交付版，本版尚無逐項實機驗收。

## 本版變更與使用範圍

中文介面保留「上班不要玩」；英文、日文、韓文、德文、法文、西班牙文與巴西葡語統一使用 **PS Remote Play Keyboard**。視窗標題顯示品牌及 `V 0.1.1`；同步修正主畫面、設定頁、提示、回饋主旨、英文手冊、授權署名文件與重建說明的品牌名稱。英文組件產品名稱亦一致。

九語依 Windows UI 語言選擇，不支援的語言回退英文。沿用一般 DualSense 純手把 `dualsensegamepadv5`、22 動作改鍵、每鍵連點、官方 Remote Play 前景限制、切窗停止並清除連點、F8 暫停與主動儲存設定。本次品牌修正不改遊戲輸入、後端協定或驅動。

本程式針對 Sony 官方 Windows PS Remote Play，適合 2D 遊戲，尤其以選單與方向操作為主的 JRPG。沒有滑鼠瞄準或右搖桿方向映射，不適合需要靈活視角或精細類比控制的 3D 遊戲。2026-10-29 使用者回報單款《Final Fantasy Resonance》可正常操作，名稱依使用者提供；回報未指定工具 build，不視為本版逐鍵／連點驗收，也不保證所有 JRPG。alpha.9 實際操作回報另見[原紀錄](ALPHA9-TRIAL.md)。

所有功能免費，作者 xiunlee，原創碼 GPL-3.0-only，第三方授權保留。[PayPal 自願 US$1](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW)幫三隻貓咪買罐頭；回饋 `xiun1019@gmail.com` 僅開郵件草稿，不自動寄信或附加資料。

## 驗證

| 項目 | 結果 |
| --- | --- |
| C# 檢查 | 6912 通過：162 保護條件／支援連結、2890 主畫面資源、149 fake loopback 傳輸、276 設定、101 連點、36 輸入流程、167 UI 互動、3131 共用語言資源 |
| native／ABI | 5743 項合成報告及 x64 C#／native ABI 通過 |
| 後端 | 沿用相同 r4 成品與先前 4 個 Go 頂層／2 個子情境結果；本輪未重跑 Go |
| 公開包 | 39／39 獨立檢查通過，含來源、ZIP、雜湊、授權、品牌、英文文件、版本中繼資料與回饋主旨 |
| 最終畫面 | [九語主畫面](../../assets/previews/0.1.1)全部重新目視；九語設定頁與已審 0.1.0 同語 PNG 的 SHA-256 相同，沿用該像素檢查 |
| 開發版排版 | 九語一般尺寸，加英／德文最小視窗、150% 字體與長錯誤訊息，共 11 情境，排版掃描零發現；另目視英文一般與德文壓力畫面 |
| 公開發行頁 | 核對正式英文名稱、無準備中狀態、英文區無漢字；四附件的 GitHub digest 與本機雜湊一致 |

以上未擷取實體鍵盤、接入驅動或操作 Sony。排版測試不是實際高 DPI／多螢幕驗收。母語校閱、逐鍵、連點、重連、長時間、新電腦安裝與本版重開機仍待驗證；裝置辨識或綠燈不等於 PS5 收到操作。

## 封裝與檔案身分

詳見 [BUILD](../../packaging/build-records/0.1.1.json)、[附件清單](../../packaging/build-records/0.1.1-archives.json)及[固定版本重建說明](https://github.com/xiunlee/ps-remote-play-keyboard/blob/v0.1.1/docs/REBUILD.md)。三個執行元件均為 **NotSigned**；SHA-256 只供版本與完整性核對。

| 附件 | Bytes | SHA-256 |
| --- | ---: | --- |
| Windows ZIP | 6283729 | `2C0A1A6564BC34637290D7285505E1F98AEF64D1D27BF06E67EA3A6864789DCF` |
| 完整來源 ZIP | 13868673 | `4018FED4193AF42CFB2F9A3071BCEDF04ED12307E06CD3717A2C0815921D5813` |
| RELEASE.json | 618 | `A5D91D7D07A0B5E0E2720FC3D4BE35AFBBF716572ACC73D0D37AD7F635994251` |
| SHA256SUMS.txt | 275 | `46A67C8FC9FD5C1F119482DEAF63D8C270B7F189B68932195E27365893D6EA33` |

前端 SHA-256 `BBB48660C04BBA7D6C1F6F6F87E1A705585DB503F81BA7425C408E12979018CA`；後端 `9CFF2F8BF15DA8C4807BD631A24909FC9EF9820E5F7914F8742B839A981C2A61`；native `7AB0AF6185F81ACF195AF01A97E12C85AC2179583E643AC103C4D65A89E0F600`。

後端及其來源由已核對的 0.1.0 執行包與完整來源包還原，還原時 1686 筆來源校驗通過；本版完整來源包包含固定上游 ZIP、三個實際連結 Go 模組、wrapper、授權材料及 native 重建資料。未將僅有 GitHub 自動產生的專案來源 ZIP 當作完整相應來源。

## 本機交付與後續

交付於 `%USERPROFILE%\.pskeyboard-tools\versions\0.1.1`，桌面「上班不要玩 0.1.1」指向該版；同版 `versioned-archives` 保存四個公開附件及固定上游來源 ZIP。沒有啟動交付版，沒有關閉、重啟或更新 Sony，也沒有改動既有完整 usbip-win2 0.9.8.1。

本輪已刪除舊 0.1.0 執行目錄、封存、捷徑，以及重建工具、封裝與下載暫存，共 **9 項、753,491,748 bytes**；`complete=true`、`preservationVerified=true`、失敗 0。這是刪除檔案的邏輯大小，不等於磁碟淨增加容量。其餘 99 個路徑原已不存在，2 個小型歷史 log 刻意保留，不算刪除。前一輪清理另見 [0.1.0 紀錄](RELEASE-0.1.0.md)，不重複計入本輪。

清理後 `versions` 與 `versioned-archives` 各僅保留 `0.1.1`。個人設定與備份、驅動檔、Sony 程序身分及 Git 標籤均核對前後一致；PS4 功能原始碼及 GitHub 歷史版本仍保留。小型驗證記錄與截圖保留作為交接證據。

版本採 `0.1.0 → 0.1.1 … 0.1.9 → 0.2.0`，舊標籤與 GitHub 附件不覆寫、不移動。Mac 尚未開發；Windows 交付完成後，下一串從 [MAC-HANDOFF.md](../../docs/MAC-HANDOFF.md)接手，先唯讀核對 Mac 環境與官方 Remote Play，再做隔離輸出可行性驗證。

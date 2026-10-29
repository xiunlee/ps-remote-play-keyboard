# DualSense 候選與 usbip-win2 0.9.8.1 相容性查核

2026-10-29（Asia/Taipei）。本文件記錄固定來源的靜態分析及待驗證條件，接續 [V5 使用者空間結果](../dualsense-v5-test/RESULTS-20261029.md)。後續四方案已完成 hosted Windows HID 實測，結果另見 [實測紀錄](RESULTS-20261029.md)；下文的音訊及 Sony 驗證限制仍成立。

目前結論：USB/IP 網路封包格式有繼續驗證的基礎，但原版候選的 Windows 啟動檢查及預設 native attach ABI 都不適用現有 0.9.8.1。這不是更換或降級驅動的理由。產品仍為 alpha.8，既有 PS4 模式、Sony Remote Play、個人設定及現有驅動均不因本文件而變更。

## 固定來源

| 元件 | 固定版本與提交 | 本次來源 ZIP SHA-256 |
| --- | --- | --- |
| hbashton/VIIPER | `v0.1.9-rc4.6.6`，[`b78e31e93e84b4cb7c4b4c15b8aace10749106d2`](https://github.com/hbashton/VIIPER/tree/b78e31e93e84b4cb7c4b4c15b8aace10749106d2) | `8305750E881E7E955DCF26DC5FEDC2B43D55902C9F1FA5B4ECA31FE51901B2A5` |
| vadimgrn/usbip-win2 | `v.0.9.8.1`，[`55e1fa7f0c2157017b02dc1f3236e98169a535e4`](https://github.com/vadimgrn/usbip-win2/tree/55e1fa7f0c2157017b02dc1f3236e98169a535e4) | `9C428B198A0364CFAE1DAB0506E2FA4056273F333E1400E075B6CECB3BAAE237` |
| vadimgrn/usbip-win2 | `v.0.9.7.7`，[`7c219953101cc5d0ec9a0bcb3eb87259cf72bedd`](https://github.com/vadimgrn/usbip-win2/tree/7c219953101cc5d0ec9a0bcb3eb87259cf72bedd) | `88E8026B3FBB833368E90C4CD113FEACC6E2F76EED872B936B2BE52038B5A1A3` |

兩版 driver 原始碼只下載及解壓至儲存庫 `.local/driver-compat`；沒有執行其中的安裝、測試簽章、attach 或系統設定腳本。ZIP 雜湊表示此次取得的封存內容，不表示安裝中的 driver 二進位已與該來源逐位元比對。

## 三個不同的相容性邊界

### 1. 原候選 Windows CLI 的啟動檢查

候選在開啟 server 前，無條件要求 usbip-win2 為 `0.9.7.7`；設定 auto-attach 關閉也不略過此檢查。這是原候選 CLI 的執行政策，不是 USB/IP wire 的版本協商。來源：[usbip_prerequisite_windows.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/internal/cmd/usbip_prerequisite_windows.go)、[server.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/internal/cmd/server.go)。

現有 lab server 是使用固定候選底層套件的獨立研究入口，沒有修改或移除原候選 exe 的檢查。未來若用它做 hosted driver 測試，必須如實記錄測的是這個入口及來源快照，不能寫成原版候選 exe 已通過。

### 2. Native attach IOCTL：已確認的 ABI 不相容

候選預設 `AutoAttachWindowsNative=true`。其 `attachIOCTL` 固定為 1100 bytes，欄位為 `Size`、`PortOutput`、`BusID[32]`、`Service[32]`、`Host[1025]`；BusID 位於 offset 8。候選自己的測試也把 1100 寫成 0.9.7.7 的 ABI 契約。來源：[autoattach_windows.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/internal/server/api/autoattach_windows.go#L60)、[config_windows.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/internal/server/api/config_windows.go)、[ABI 測試](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/internal/server/api/autoattach_windows_test.go#L33)。

0.9.8.1 在 `imported_device_location` 增加 `location_hash`，BusID 因而移至 offset 12；`plugin_hardware` 另增加 `serial[16]` 與 `wsk_events`。Driver 先要求 input buffer 至少為新版結構大小，再要求實際長度完全相等，最後核對內嵌 `size`。所以舊 payload 不能直接送給新版 driver，不能只刪掉版本檢查就宣稱相容。來源：[0.9.7.7 vhci.h](https://github.com/vadimgrn/usbip-win2/blob/7c219953101cc5d0ec9a0bcb3eb87259cf72bedd/include/usbip/vhci.h)、[0.9.8.1 vhci.h](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/include/usbip/vhci.h#L68)、[新版 input 驗證](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/vhci_ioctl.cpp#L740)。

**可驗證的替代路徑**是：隔離研究入口不使用候選 native autoattach，由與 driver 配套的 **0.9.8.1 CLI／DLL** 建立自己的 attach 請求，再透過 USB/IP 網路與候選底層套件通訊。這只避開已知舊 ABI，不保證後續枚舉、HID、音訊或 Sony 相容。不能混入 0.9.7.7 CLI／DLL，也不能把原版候選 exe 移除 gate 後的執行等同這條路徑。

### 3. USB/IP network wire：欄位相同，尚非 driver 實測

固定兩版 `include/usbip/proto.h` 與 `proto_op.h` 的封包欄位排列相同；差異主要為標頭、packing 寫法及額外檢查，並未把 Windows native IOCTL 的新欄位加進網路封包。來源：[0.9.7.7 proto.h](https://github.com/vadimgrn/usbip-win2/blob/7c219953101cc5d0ec9a0bcb3eb87259cf72bedd/include/usbip/proto.h)、[0.9.8.1 proto.h](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/include/usbip/proto.h)、[0.9.7.7 proto_op.h](https://github.com/vadimgrn/usbip-win2/blob/7c219953101cc5d0ec9a0bcb3eb87259cf72bedd/include/usbip/proto_op.h)、[0.9.8.1 proto_op.h](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/include/usbip/proto_op.h)。

| 邊界 | 0.9.8.1 原始碼要求 | 離線可核對項目 |
| --- | --- | --- |
| Import handshake | 8-byte common header，version `0x0111`、正確 reply code／status；312-byte device body；busid 與要求一致 | 實際回包的長度、大小端、busid 與 speed；失敗回包不冒充成功 |
| Submit／return | 48-byte header；非 ISO 使用 `number_of_packets=-1`；driver 亦把回包的 `-1` 正規化為 0 | request／response framing、short reply、actual length 不超過要求 |
| 序號 | driver 將方向放在 seqnum 低位，回覆須保留 seqnum | 用 `(counter << 1) \| direction` 產生測試序號；回包 devid／direction／ep 為 0 本身不是錯誤 |
| ISO | packet count 0–1024，descriptor 每個 16 bytes | count、原始 offset、length、actual length、status 及 payload 一致性 |
| 錯誤／取消 | Linux `-EPIPE`、`-ECONNRESET` 有對應 Windows 狀態 | STALL、UNLINK 與下一個 request 的 framing，不把成功送出當成功完成 |

來源：[版本常數](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/include/usbip/consts.h#L30)、[common header 驗證](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/network.cpp#L50)、[import 驗證](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/vhci_ioctl.cpp#L119)、[submit 組裝](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/proto.cpp)、[序號契約](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/context.h#L274)、[reply 驗證](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/wsk_receive.cpp#L487)、[狀態轉換](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/libdrv/usbd_helper.cpp)。

## 複合描述符與 ISO 的具體限制

本節描述的是標準 DualSense composite（`dualsensecombinedaudioduplexv5`、`054C:0CE6`）。後續 matrix 若同時測試 gamepad-only 或 DualSense Edge，須逐項記錄 persona；不能把本節四介面／PID 描述直接套到其他 persona。

候選宣告 USB 2.0、HighSpeed `3`、EP0 64 bytes，音訊介面 0–2，HID 介面 3。音訊 OUT `0x01` 是 adaptive ISO、最大 392 bytes；IN `0x82` 是 asynchronous ISO、最大 196 bytes，兩者 `bInterval=4`。HID IN `0x84`／OUT `0x03` 最大 64 bytes，interval 分別 4／6。音訊格式為 48 kHz、16-bit，OUT 四聲道、IN 雙聲道。來源：[候選 descriptor.go](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/device/dualsense/descriptor.go)、[OUT 常數](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/device/dualsense/bthaptics.go#L28)、[IN 常數](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/device/dualsense/const.go#L117)。

0.9.8.1 能把 speed `3` 映到 `UdecxUsbHighSpeed`。低／全速的 `patch_config()` 只在 `speed < USB_SPEED_HIGH` 時套用，因此不能把其他裝置的 full-speed descriptor rewrite 問題直接套到本候選。描述符欄位可核對，不表示 Windows 類別驅動必定成功啟動。來源：[speed 映射](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/device.cpp#L25)、[條件式 patch](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/wsk_receive.cpp#L344)。

正常 ISO IN 回包須保留每包原始 offset，`actual_length <= length`，且各包 actual 加總等於回包 actual。網路 payload 是去除間隙後的緊密資料，driver 再依 offset 展開。ISO OUT 回包不攜帶 OUT 資料 payload。兩條 driver 接收路徑都需考量；CLI 預設是 `zero-copy`，另有 `low-latency` WSK event 模式。使用者空間讀回不等於兩條 kernel 路徑都已實測。來源：[ISO 收包](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/wsk_receive.cpp#L153)、[CLI 預設](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/userspace/usbip/usbip.h#L36)。

**已確認的 ISO 接受域差異：零長度 packet。** Driver 的 `repack()` 接受相鄰 offset 相等，產生 length 0；候選明確拒絕 length 0，呼叫端會結束該 USB/IP stream。可用向量是 OUT 總長 384、兩個 offset `[0,0]`、length `[0,384]`。這是來源可證的邊界不一致，尚未證明 Windows 實際音訊工作負載會產生此向量，也未執行實機重現。來源：[driver repack](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/device_ioctl.cpp#L379)、[候選拒絕條件](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/internal/server/usb/endpoint_scheduler.go#L1696)、[stream 錯誤出口](https://github.com/hbashton/VIIPER/blob/b78e31e93e84b4cb7c4b4c15b8aace10749106d2/internal/server/usb/urb_stream.go#L205)。

離線／hosted 測試應分開標記以下範圍，不預填結果：

1. Import 與 driver 形狀的序號、非 ISO `-1`、短回包；HID report descriptor 要求長度大於實際描述長度時仍正確完成，例如要求 `descriptorLength+64`。
2. ISO OUT 8 × 384 bytes、IN 8 × 192／196 bytes；回包 count、offset、actual 加總符合 driver 條件。
3. 零長度 packet 向量獨立列為邊界差異，不能和正常音訊功能通過混在一起。
4. 有 pending ISO 時切 alt-0、重選 configuration 1、再 UNLINK；核對 ACK 後沒有舊 generation 的成功回包，下一筆 control framing 仍完整。
5. Hosted 環境若只測 HID，明確標記音訊啟動、ISO 及 teardown 未測；裝置存在、HID 可開啟、合成按下／放開回讀分開計錄。

## 已知問題的證據與適用範圍

以下 issue 狀態為 2026-10-29 查核快照；issue 可繼續更新，不能視為固定 release 的完整缺陷清單。

| 證據 | 可以得出的結論 | 不能推論的結論 |
| --- | --- | --- |
| [#172：0.9.7.7 send watchdog](https://github.com/vadimgrn/usbip-win2/issues/172) | 有 VIIPER 複合 DualSense loopback 的 crash 回報；維護者確認舊 send spinlock 問題，後續移除。固定 0.9.8.1 的 `send_pending` 使用 Interlocked 與待送佇列。 | 不能因此降級到 0.9.7.7，也不能以此證明 0.9.8.1 所有工作負載都安全。 |
| [#181：0.9.7.8 audio teardown／pool corruption](https://github.com/vadimgrn/usbip-win2/issues/181) | 討論後續修正了首文假說；維護者確認 `libdrv::argv` overrun，提出 `4139f44` 修正。固定 0.9.8.1 已有 `StackSize+1` 與 `IoSetNextIrpStackLocation`。 | 不能照抄首文，宣稱相同已修正缺陷仍存在於 0.9.8.1；也不能把來源修正等同本候選實機 teardown 已過。 |
| [#196：0.9.8.1 自製 HID 啟動 timeout](https://github.com/vadimgrn/usbip-win2/issues/196) | 有自製 HID report descriptor 回覆相關失敗回報；維護者要求 driver logs，根因未確認。需驗證長度與 short reply，並保留 kernel 實測門檻。 | 回報者對「HID report 被當 USB descriptor chain 走訪」的解釋仍是假說；該裝置不是本候選，不能直接判定本候選會失敗。 |
| [#199：Full-Speed dongle regression](https://github.com/vadimgrn/usbip-win2/issues/199) | 回報涉及 full-speed 描述符修改；本候選宣告 HighSpeed，不走同一 patch 條件。 | 不能視為本候選已相容，也不能把其他裝置 regression 當成本候選已重現。 |
| [0.9.8.1 release 已知 serial 問題](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1) | Release 註記 serial override 問題及後續 `a2a1159` 修正；本候選預設 `iSerialNumber=0`，研究不需要額外 override。 | 不能聲稱 tag 已包含後續修正，或為繞開它擅自更新現有 driver。 |

對應固定來源：[send_pending](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/device_ioctl.cpp#L134)、[IRP stack 修正](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude_filter/int_dev_ctrl.cpp#L94)、[GET_DESCRIPTOR 後處理](https://github.com/vadimgrn/usbip-win2/blob/55e1fa7f0c2157017b02dc1f3236e98169a535e4/drivers/ude/wsk_receive.cpp#L344)。最後一項來源中可看到 trace 及有條件的 descriptor 處理；不能只因 trace 印出 HID 頭兩個 bytes 就把 #196 的根因當成已證實。

另案 HIDMaestro 固定提交 `77c4ddbad6cff428e79bc5f76d84eeef85cc8c7f` 的 [UsbipPackage.json](https://github.com/hifihedgehog/HIDMaestro/blob/77c4ddbad6cff428e79bc5f76d84eeef85cc8c7f/sdk/HIDMaestro.Core/UsbipPackage.json) 明確固定 0.9.8.1 與套件雜湊，並有[四介面 DualSense composite profile](https://github.com/hifihedgehog/HIDMaestro/blob/77c4ddbad6cff428e79bc5f76d84eeef85cc8c7f/profiles/sony/dualsense-composite.json)。[v2.1.0 release](https://github.com/hifihedgehog/HIDMaestro/releases/tag/v2.1.0) 作者另報告 Windows 11 的 101 項通過及 Windows 10 IoT 的 97 項通過／4 項略過，所測 build 為 `68040e2ab0654091018b19a6e753b4bf435492d1`，不可外推為上述 master 快照的測試。這支持繼續評估 0.9.8.1，不能僅憑舊候選版本 gate 判定新版驅動必然不可用；本專案沒有自行重跑 HIDMaestro，也不把不同實作或非 Sony 的結果當成本候選成功證據。

## 下一階段與成功定義

優先在獨立 hosted Windows runner 或同等可丟棄環境，用配套 0.9.8.1 CLI／driver 測試固定研究入口。需要記錄實際載入版本、來源與二進位雜湊、裝置精確身份、HID 回讀及清理結果。CI 成功、attach 成功、Windows 枚舉成功、HID 回讀成功是不同層次，任何一項都不能替代後一項。

使用者本機的現有 PS4／Sony 連線不作此階段的試驗環境。不得自行重啟、更新或關閉 Sony，不改 driver、不做全域 detach，不將個人設定、裝置序號或原始按鍵資料納入公開 artifact。

**只有使用者透過官方 Remote Play 實際操作 PS5 成功，才能稱為 PS5 操作通過。** Hosted Windows 沒有 Sony／主機實測時，即使 HID 測試完整通過，PS5 狀態仍是未驗證。後續接入產品也必須維持官方 Remote Play 前景才輸出、切窗停止與 F8 暫停，保留可用 PS4 模式及舊版本。

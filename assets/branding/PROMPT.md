# PS 品牌圖示

生成方式：Codex 內建 imagegen。未使用 API／CLI。原始生成圖選為 `ps-mark.png`；`app.ico` 僅作縮放及格式封裝，沒有重新描繪圖案。

用途：上班不要玩 Windows 桌面圖示與視窗品牌標記。白色 PS 字母以粗線、斜切角及負空間構成。

## 最終採用的生成提示

```text
Use case: logo-brand.
Asset type: finished square Windows app icon, 1024x1024, for a custom PC gaming keyboard utility.
Design an original premium futuristic white "PS" monogram. The exact only letters are uppercase P and S, interlocking into a single crisp geometric symbol, with confident thick strokes and two or three deliberate 45-degree precision cuts / negative-space breaks. Both letters must be legible at tiny icon sizes. Angular engineering, poised and minimal, no tiny circuitry.
The background must be completely flat solid hexadecimal #D62355 (RGB 214,35,85), extending edge to edge. Mark pure white #FFFFFF. Exactly this two-color palette, with only antialiasing at edges.
Centered composition, the monogram occupies about 70% of canvas width and 65% of height, generous balanced margins. Flat straight-on vector-like artwork, one icon, no surrounding presentation. Do not add words, slogan, watermark, border, frame, controller silhouette, glow, metallic gradient, bevel, shadow, texture or 3D perspective. No Adobe-style square frame. No rounded-corner mask. Make it feel intentionally drawn rather than typed in an ordinary font.
```

## 色彩與輸出限制

生成器提供 1254 × 1254 圖片。雖指定 `#D62355` 與純白，實際點陣有輕微色彩變化；原圖左上角為 `#CA2A52`，上方中央取樣為 `#D7204F`。因此此生成圖不能宣稱每一背景像素均精準符合指定色碼；應用程式的品牌色與 UI 則應直接指定 `#D62355`。

另試一次純色清理與兩次透明背景輸出。純色輸出仍有差異，透明輸出有破損與雜點，均未採用；保留原生成圖的乾淨輪廓。

`Build-Icon.ps1` 將採用圖封裝成 16、24、32、48、64、128、256 像素多尺寸 Windows ICO，僅做縮放與格式轉換。小尺寸使用 32-bit DIB，256 像素使用 PNG。

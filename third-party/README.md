# Third-party component

The keyboard-only USB candidate uses the separately downloaded official
[VIIPER v0.9.0](https://github.com/Alia5/VIIPER/releases/tag/v0.9.0) server
(GPL-3.0; [corresponding source](https://github.com/Alia5/VIIPER/tree/v0.9.0))
and the signed [usbip-win2 0.9.8.1](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1)
driver. Their binaries are not committed here. Versions, hashes, installation
impact, and redistribution requirements are recorded in
[the keyboard app README](../src/keyboard-pad/README.md). The app communicates
with VIIPER only over loopback and does not use a commercial remapping tool.

`ViGEmClient.dll` is the x64 ViGEm client binary distributed in [vgamepad at commit 74318af82918336f8c8f311f4cfb26cdda0dccab](https://github.com/yannbouteiller/vgamepad/tree/74318af82918336f8c8f311f4cfb26cdda0dccab). Its SHA-256 is `2BF0CB1D809039573C922737D298A1653D4DBC61408060FF45A9BCFDE82E97D2`. The upstream [ViGEmClient](https://github.com/nefarius/ViGEmClient) and vgamepad license notices are included here.

`minhook/` contains the unmodified `src`, `include`, and `LICENSE.txt` from [MinHook v1.3.4, commit c3fcafdc10146beb5919319d0683e44e3c30d537](https://github.com/TsudaKageyu/minhook/tree/c3fcafdc10146beb5919319d0683e44e3c30d537). The native observation experiment builds this free source library directly into its DLL. Its two-clause BSD license and bundled disassembler notices are retained in [minhook/LICENSE.txt](minhook/LICENSE.txt). It is not a driver or a commercial remapping application.

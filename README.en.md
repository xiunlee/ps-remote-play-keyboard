# Three Cats Keybridge

[Traditional Chinese](README.md) · **English**

**A Windows tool that turns keyboard input into virtual controller actions for supported remote-play environments.** This is an independent open-source project, not an official Sony product, and is not affiliated with or endorsed by Sony. See the instructions below for supported software and requirements.

**Designed for 2D games, especially JRPGs that rely on menus and directional input.** There is no mouse aiming or right-stick direction mapping, so this version is not suitable for 3D games that need flexible camera movement or fine analog control. Suitability depends on each game's controls; support is not guaranteed for every JRPG.

Free and open source · Author: **xiunlee** · **GPL-3.0-only**. Every feature is free; donations do not affect access to any feature.

## Download and status

**Three Cats Keybridge V 0.1.3 is available: name and icon update.** [Download the Windows x64 package](https://github.com/xiunlee/ps-remote-play-keyboard/releases/download/v0.1.3/PSKeyboard-0.1.3-windows-x64.zip), or visit the [v0.1.3 release page](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/v0.1.3) for the corresponding Windows source and SHA-256 checksums. All four anonymously downloaded asset hashes have been verified. Only the current release is public; older releases are retained as drafts, while tags and source history remain public. Detailed PS5 acceptance, long sessions and installation on a new PC still need testing.

The Chinese interfaces retain their Chinese product name; all other supported interfaces use **Three Cats Keybridge**.

The three runtime binaries are currently **unsigned**. Checksums establish file identity, not digital signing or compatibility with every setup. See the [release and verification record](research/dualsense-preview/RELEASE-0.1.3.md). A Mac app is not available. [Mac research](research/macos-native/README.md) continues; official Remote Play compatibility has not been demonstrated. See the [Mac handoff](docs/MAC-HANDOFF.md) for next steps. Research records are in Traditional Chinese.

**Interface language:** the app supports English, Traditional Chinese, Simplified Chinese, Japanese, Korean, German, French, Spanish and Brazilian Portuguese. Language follows the Windows UI language, with English as the fallback. Native-speaker review, real high-DPI behavior, multi-monitor use and detailed PS5 acceptance remain unverified. [Main-window preview](assets/previews/0.1.3/main-en.png) · [Binding-editor preview](assets/previews/0.1.3/editor-en.png), both rendered offline.

**User game report:** On 2026-10-29, the user reported that *Final Fantasy Resonance* could be controlled normally. The report did not identify the tool's build, so it does not establish v0.1.3 acceptance for individual actions or auto-repeat, nor compatibility with all JRPGs.

## Requirements

- A Windows x64 desktop environment with .NET Framework.
- **Sony's official PS Remote Play app, already connected to your PS5.** This tool does not manage your PSN account or replace the official client.
- A complete, matching **usbip-win2 0.9.8.1 driver, CLI and DLL installation**. Drivers are not bundled. The application does not install, replace, downgrade or restart them automatically; it refuses to attach when the required components do not match.

No physical controller is needed for this mode, but **it is not driver-free or a one-click installer**. See the [official usbip-win2 0.9.8.1 release](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1) for the prerequisite package and upstream instructions. Do not interrupt an active Remote Play session to experiment with driver setup.

## Getting started

1. Download and extract the entire application package. Keep its EXE, DLL, backend and accompanying files together; do not copy out just one executable.
2. Connect Sony's official PS Remote Play app to your PS5 and leave it running. Exit any other version of this keyboard tool normally; two versions cannot share the F8 hotkey.
3. Open `PSKeyboard-DualSensePreview.exe` and click **Connect controller**. Launching the tool does not attach a controller automatically, and keyboard control starts paused.
4. Switch to the official Remote Play window and press **F8** to enable keyboard control. Press F8 again to pause. Game input and repeat are sent only while the official Remote Play window is in the foreground.
5. To edit bindings or repeat, click **Keys and auto-repeat**. Changes apply only after a successful **Save**. Cancel keeps your previous settings. Editing leaves control paused; return to Remote Play and press F8 when ready.
6. When finished, click **Disconnect controller**, or close the tool normally. If it cannot confirm cleanup, it blocks another attachment. Keep the error message and report it instead of repeatedly attaching devices or restarting Sony's app.

## Bindings, repeat and saved settings

Each of the 22 controller actions can have one keyboard key and an independent repeat mode: **Off**, **Hold to repeat**, or **Toggle repeat**. Intervals range from 20 to 5,000 ms per key, with a default of 100 ms. F8 is reserved for enabling and pausing control. Some games may miss very short repeat pulses.

These defaults apply to a new profile or after you save a restored default profile. Existing custom bindings are preserved.

| Controller action | Default keyboard key |
| --- | --- |
| Left stick up / down / left / right | W / S / A / D |
| Triangle / Square / Cross / Circle | ↑ / ← / ↓ / → |
| L1 / L2 | E / Q |
| R1 / R2 | Delete / Page Down |
| D-pad up / down / left / right | Home / End / Insert / Page Up |
| L3 / R3 click | F / Space |
| Create / Options | Tab / Enter |
| Touchpad click / PS button | R / F1 |
| Enable / pause | F8 |

The arrow keys are face buttons. **Use Home / End for D-pad up / down by default**, including menus that require D-pad input. R3 means clicking the right stick, not moving the camera. See the [default layout guide (Traditional Chinese)](docs/DEFAULT-KEYS.md) for more detail.

Your profile is stored separately from the application at `%LOCALAPPDATA%\PSKeyboard\bindings.json`. Updating, launching the tool or cancelling edits does not reset it. Failed saves do not apply the edited profile. **Restore defaults** changes only the draft until you save; it also turns all repeat modes off and resets intervals to 100 ms.

## FAQ

**The controller is connected, but nothing happens on my PS5.** The connection indicator reports the Windows output connection, not whether Sony accepted an input. Check that the official Remote Play app is connected to your PS5, its window is in the foreground, F8 has enabled control and you are using your current bindings. If a driver or cleanup error appears, keep the message and report it. Do not repeatedly attach devices, change drivers or restart Sony's app to work around an unexplained error.

**Will cancelling a binding change overwrite my settings?** No. Changes apply only after Save succeeds. Cancel, closing the editor or a failed save preserves the previous profile. Keyboard control remains paused after editing.

**Does repeat keep running when I switch windows?** No. Switching away stops and clears repeat; returning does not restart it. You must trigger it again with a fresh key press. F8, editing, disconnection and exit also stop output.

**Can I aim with a mouse, record macros or use this on a Mac?** Not currently. Mouse-to-right-stick input, recording/replay and macOS are unsupported. Full audio, haptics and adaptive-trigger support are not claimed.

Long sessions, reboots, repeated reconnections, installation on a second PC and all actions in the latest build still need further testing. See the [support scope](docs/PRODUCT-SCOPE.md) and [DualSense notes](research/dualsense-preview/README.md), currently in Traditional Chinese. This is an independent project, not official Sony software.

## Optional support and feedback

If you find the app useful, you can [contribute US$1 through PayPal to support our three cats](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW) and help buy them canned food. Contributions are entirely optional, unlock no features and do not create a subscription. Clicking **Treat the cats · US$1** opens your browser; PayPal handles payment, and this app does not receive or store payment details.

[Send feedback to xiun1019@gmail.com](mailto:xiun1019@gmail.com). Clicking the app's **Send feedback** button opens your mail application with only the recipient and a version-specific subject filled in. It does not send the message or attach settings, diagnostics or personal data automatically.

## Source and license

Original project source is licensed under the [GNU General Public License, version 3 only](LICENSE) (`GPL-3.0-only`). Author: **xiunlee**. Third-party components retain their own licenses; see [COPYRIGHT.md](COPYRIGHT.md). Public application packages must be accompanied by complete corresponding source, pinned upstream and dependency sources, rebuild instructions, third-party notices and checksums.

Build instructions are in [Versioning (Traditional Chinese)](docs/VERSIONING.md). Active development is on `codex/commercial-preview`; this historical branch name does not mean the project is paid software. `main` may be behind. Maintainers should read the [handoff](docs/HANDOFF.md), [changelog](CHANGELOG.md) and [release checklist](packaging/RELEASE-CHECKLIST.md).

## Historical research

PS4 emulation, physical DS4 relay experiments and earlier candidates remain in Git history and research records. They are not installation instructions for the current version. These records are primarily in Traditional Chinese.

- [First controller-free PS4 success and reboot report](research/controller-free-preview/LIVE-TRIAL.md)
- [Failed DualSense trial in alpha.6](research/controller-free-preview/ALPHA6-TRIAL.md)
- [Isolated V5 protocol validation](research/dualsense-v5-test/RESULTS-20261029.md) and [hosted usbip-win2 0.9.8.1 driver tests](research/dualsense-driver-test/RESULTS-20261029.md)
- [Physical DS4 relay](research/physical-usb-relay/README.md) and [native input mapping](research/native-merge/README.md)

# PS Remote Play Keyboard

English user guide · [Traditional Chinese](README.md)

Use your keyboard to control your PS5 through **Sony's official PS Remote Play for Windows**, with 22 remappable actions, per-action rapid fire and an F8 pause hotkey. The app creates a virtual DualSense, so a physical controller is not required. Sony's official app still handles your PS5 connection, sign-in and streaming.

**Designed for 2D games, especially JRPGs that rely on menus and directional input.** There is no mouse aiming or right-stick direction mapping, so this version is not suitable for 3D games that need flexible camera movement or fine analog control. Suitability depends on each game's controls; support is not guaranteed for every JRPG.

**Free and open source · Author: xiunlee · GPL-3.0-only.** This guide covers **0.1.2**, the main-window design update. The version number does not establish complete hardware acceptance or long-term reliability; see the testing scope below. All features are free; sponsorship does not affect access. [Downloads, updates and corresponding source](https://github.com/xiunlee/ps-remote-play-keyboard/releases).

The runtime binaries are unsigned. SHA-256 checksums establish file identity, not digital signing or compatibility with your setup.

## Before you start

- Windows x64, .NET Framework 4.x and Sony's official PS Remote Play working with your PS5.
- A complete, matching **usbip-win2 0.9.8.1 driver, CLI and DLL installation**. The app uses `C:\Program Files\USBip\usbip.exe` and `libusbip.dll` in the same folder. It stops attachment if component verification fails.
- Extract the entire application package and keep its EXE, DLL, backend and accompanying files together. Do not copy out just the main EXE.

Drivers are not bundled. The app does not automatically install, replace, downgrade or restart drivers; a new PC needs the prerequisites prepared separately. See the [upstream usbip-win2 0.9.8.1 release](https://github.com/vadimgrn/usbip-win2/releases/tag/v.0.9.8.1). Preserve an established Remote Play connection: do not close, restart or update Sony Remote Play for this app.

## Start playing

1. Leave Sony Remote Play open. Normally exit any other version of this keyboard app first, so only one instance owns F8 and the controller connection.
2. Open `PSKeyboard-DualSensePreview.exe` and click **Connect controller**. Starting the app does not attach a controller automatically, and keyboard controls start paused.
3. Once attached, switch to official Remote Play and press **F8** to enable keyboard controls. Press F8 again to pause.
4. Keyboard controls and rapid fire work only while official Remote Play is the foreground window. Switching away clears held inputs and rapid fire. On return, press the key again; a previous toggle does not resume by itself. If controls are paused, press F8 to enable them first.
5. Click **Keys and auto-repeat** to edit controls. Editing pauses input, and it stays paused when the editor closes. Return to Remote Play and press F8 to continue.
6. Click **Disconnect controller** or close the app's window normally when finished. If cleanup cannot be confirmed, keep the error message and report it instead of repeatedly creating devices.

The main window, binding editor, connection states, error prompts, sponsorship and feedback text follow the Windows UI language: English, Traditional Chinese, Simplified Chinese, Japanese, Korean, German, French, Spanish or Brazilian Portuguese. Unsupported languages fall back to English. All non-Chinese interfaces use the name **PS Remote Play Keyboard**; Chinese interfaces retain their Chinese product name. Technical diagnostic codes remain unchanged. User guides are currently available in English and Traditional Chinese. Resource and layout checks do not establish native-speaker approval; translation feedback is welcome.

## Default keys

An existing personal profile takes precedence. These defaults apply to a new profile or after you restore defaults and save. **The keyboard arrow keys control the four face buttons; D-pad up/down use Home/End.**

| PS5 controller action | Default key |
| --- | --- |
| Left stick up | W |
| Left stick down | S |
| Left stick left | A |
| Left stick right | D |
| D-pad up | Home |
| D-pad down | End |
| D-pad left | Insert |
| D-pad right | Page Up |
| Cross × | ↓ |
| Circle ○ | → |
| Square □ | ← |
| Triangle △ | ↑ |
| L1 | E |
| R1 | Delete |
| L2 | Q |
| R2 | Page Down |
| Create (shown as Share in the editor) | Tab |
| Options | Enter |
| L3 (left stick click) | F |
| R3 (right stick click) | Space |
| PS | F1 |
| Touchpad click | R |

**F8 is reserved for enabling/pausing controls and cannot be assigned to an action.** This version has no right-stick direction or mouse-aim mapping. R3 clicks the right stick; it does not move it.

## Remapping, rapid fire and saved settings

Select an action in the editor, click **Press a key** and press the desired key. Double-clicking a row also starts key capture. **Clear key** leaves the action unassigned. Each action uses one key; duplicate assignments and reserved keys such as F8 are rejected.

Choose a separate rapid-fire mode and interval for each action:

| Mode | Behavior |
| --- | --- |
| Off (0) | Normal press/release behavior, without automatic repetition |
| Hold | Repeats while the key is held; releasing it stops repetition |
| Toggle | Press once to start and again to stop; switching windows or pausing with F8 also clears it |

Intervals range from **20 to 5000 milliseconds**, with **100 ms** as the default. Some games ignore very short press/release pulses, so adjust the interval for your game. **Restore defaults** changes the editing draft to the table above, turns rapid fire off for every action and resets intervals to 100 ms.

Changes are written and applied only after **Save changes succeeds**. Canceling or a failed save keeps the previous settings. The profile is stored at `%LOCALAPPDATA%\PSKeyboard\bindings.json` and is reused across application updates; it does not need to move with the program folder. Keep this file and your backups. If an existing profile cannot be read, the app displays a warning and uses defaults temporarily without automatically overwriting the original file.

## Troubleshooting

| What you see | What to do |
| --- | --- |
| F8 or keyboard capture cannot start | Normally exit other versions of this keyboard app, then reopen this version. Leave Sony Remote Play running. |
| USBip missing or 0.9.8.1 component verification failed | Check that the external driver, CLI and DLL are complete and match. Do not mix versions or downgrade an existing driver just to try the app. Installation is not automatic. |
| Controller attached but the game does not respond | Check that official Remote Play is in front, keyboard controls are enabled and your actual saved bindings are correct. A green light confirms the local controller channel, not that PS5 accepted an input. |
| Unexpected D-pad controls | Defaults are Home/End for up/down and Insert/Page Up for left/right. Keyboard arrow keys control the four face buttons. |
| Controller component missing or verification failed | Extract a fresh, complete copy of the same release. Do not mix executables, DLLs or backends from different versions. |
| Settings cannot be saved | Keep the original file and backups. Check whether the profile folder is writable or another program has locked the file. A failed save does not partially apply the new settings. |
| Output disconnected, channel occupied or cleanup unconfirmed | Controls stop. Check for another open version of this app. If reconnection is locked, keep the displayed error and version for a report; do not force retries, delete devices or restart Sony Remote Play. |

## What has been tested

All four alpha.9 candidates received user reports of successful PS5 operation through official Remote Play. This release uses the standard DualSense gamepad-only mode. **Detailed user acceptance testing of 0.1.2 on a real PS5 setup remains pending.** Windows detecting a device, a green light or passing offline tests does not establish successful gameplay. Long sessions, different reconnection sequences, reboots and installation on another PC still need more testing.

On 2026-10-29, the user reported that *Final Fantasy Resonance* could be controlled normally. The tool's build was not specified, so this report does not establish 0.1.2 acceptance for individual actions or auto-repeat, nor compatibility with all JRPGs.

There is no 30-minute session limit. Audio, haptics, adaptive triggers, mouse input, recording/replay and macOS are not included as completed features in this version. See the [support scope](https://github.com/xiunlee/ps-remote-play-keyboard/blob/codex/commercial-preview/docs/PRODUCT-SCOPE.md) and [alpha.9 operation record](https://github.com/xiunlee/ps-remote-play-keyboard/blob/codex/commercial-preview/research/dualsense-preview/ALPHA9-TRIAL.md) for details; these records are currently in Traditional Chinese.

## Feedback, sponsorship and licensing

Send feedback to [xiun1019@gmail.com](mailto:xiun1019@gmail.com), including the application version, displayed error and steps to reproduce it. The **Send feedback** button opens your email application with only the recipient and version subject filled in. It does not send mail automatically or attach your profile or diagnostics. PSN passwords and payment details are not needed.

If you find the app useful, you can [contribute US$1 through PayPal to support our three cats](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW) and help buy them canned food. Clicking **Treat the cats · US$1** opens your browser; PayPal handles payment, and this app does not receive or store payment details. Contributions are optional. All features remain free, with no paid unlocks or subscription.

The original program is by **xiunlee**, licensed under [GNU GPL version 3 only](LICENSE). Third-party components retain their own licenses; see [COPYRIGHT.md](COPYRIGHT.md) and the included `notices` folder. The [public repository](https://github.com/xiunlee/ps-remote-play-keyboard/tree/codex/commercial-preview) has project information. Corresponding source and rebuild materials accompany public binaries on the [same release page](https://github.com/xiunlee/ps-remote-play-keyboard/releases). This independent project is not affiliated with or endorsed by Sony, PlayStation or PayPal.

# Mac installation, use, and updates

Applies to **Three Cats Keybridge Mac 0.1.3**. Updated: 2026-10-30 (Asia/Taipei). [繁體中文](MAC-INSTALL.md)

Three Cats Keybridge (“上班不要玩” in Chinese) is a keyboard-control and virtual-controller tool for the Sony remote-play environment specified below. It does not claim support for every game or application.

This is an independent project, not an official Sony product. It is not affiliated with or endorsed by Sony.

This version brings key bindings, repeat controls, nine interface languages, and controller component preparation into one App, with visual improvements. **The native helper and complete App have passed neutral connection, disconnection, reconnection, and cleanup on quit. The main window and settings have been reviewed in Light and Dark modes. Gameplay, installation on a fresh Mac or new user account, sleep, and crash scenarios remain unverified for this version.** The version is provided with these known limits; public distribution does not establish compatibility with every system and game. Check the [Mac 0.1.3 release](https://github.com/xiunlee/ps-remote-play-keyboard/releases/tag/mac-v0.1.3) for availability, attachments, and the publication time. The completed interface and neutral-connection checks below apply to build 9. Later name and cat-icon changes are verified separately and do not establish new gameplay results.

In alpha.1, the user confirmed game control, stopping when W was released, and Enter opening Options. Version 0.1.2 verified a local interface update and retention of Accessibility permission for that update. Those results do not establish integrated installation or gameplay acceptance for 0.1.3.

## Before you start

- Use an Apple Silicon Mac running macOS 15 or later. Intel Macs are outside the current support scope.
- Install and configure **Sony's official PS Remote Play** separately, and confirm that it connects to your PS5. This tool does not replace Sony's sign-in or streaming software.
- Internet access is needed when obtaining required components for the first time. You do not need to install Python, Go, Swift, Docker, or Parallels, or have an Apple developer account.

The App includes native helpers, a small diskless Alpine environment, and the controller backend. It uses the virtualization framework built into macOS. On the first connection, it obtains the unmodified **Mac Client 6.0.3** and **Generic ARM64 Server 4.8.8** from VirtualHere, checks the pinned versions and hashes and the applicable signature, and keeps them in this App's data directory.

The free configuration uses one USB device. VirtualHere Client runs as a normal App, not as a service requiring a paid server. VirtualHere has its own licenses; free use does not make it open source. See the [Server free-use terms](https://www.virtualhere.com/embedded_server_license), [Client license](https://www.virtualhere.com/node/316), and [service conditions](https://www.virtualhere.com/client_service).

## Install and open the App

1. If an older version is running, disconnect, wait for shutdown and cleanup, and quit it normally. Keep your existing Sony Remote Play connection open.
2. Obtain the complete App supplied for this version. Fully extract a ZIP before use, or open the DMG disk image.
3. In Finder, press **Shift+Command+G** and enter `~/Applications`. Put `PSKeyboardMacDualSense.app` in that folder. If the folder does not exist, create an `Applications` folder inside your home folder.
4. Open this installed copy from now on. Do not run copies from the DMG, Downloads, or differently named folders. Updates replace the same path instead of installing another App for each version.

The fixed location is `~/Applications/PSKeyboardMacDualSense.app`, and the bundle identifier is `com.xiunlee.pskeyboard.mac-dualsense`. Its displayed name is “上班不要玩” in Chinese and “Three Cats Keybridge” in other supported languages. The existing filename, bundle identifier, and data path are retained to preserve settings and existing permissions; the display-name change does not install a second App.

This App uses a project self-signed certificate. **It is not signed with Apple Developer ID and is not notarized by Apple.** If macOS blocks opening it, and you have checked the source and package integrity, go to **System Settings → Privacy & Security** after attempting to open it. If macOS offers **Open Anyway**, you can confirm through that system workflow. Different systems or managed Macs may not offer the same option. See [Apple's instructions](https://support.apple.com/en-us/102445).

You do not need the author's private certificate, keychain, or signing setup scripts. If the App still cannot open, retain the exact message when reporting the problem. Do not disable SIP, remove quarantine attributes, edit the TCC database, or lower global security settings.

## Permissions and connection

1. Open the App and choose **Open Accessibility Settings**. Allow this installed copy in the macOS Accessibility list. If it is absent, use **+** to select `PSKeyboardMacDualSense.app` at the fixed location above.
2. Complete any authentication in the macOS system window yourself. Return to the App and confirm that it shows **Accessibility: allowed**. You do not need to install or authorize ChatGPT or Computer Use.
3. Keep Sony Remote Play open and choose **Connect and enable control**. The first connection may download components. Wait for preparation, controller creation, and connection to finish; disconnecting cancels preparation.
4. Once the controller has connected, switch to Remote Play. A preparation status does not mean that the controller is ready.
5. When finished, disconnect and wait for cleanup. Connect again when you want to continue. Quitting the App normally also stops its output and cleans up its own processes first.

**Components and licenses** describes the dependencies and links to their official licenses. If downloading or verification fails, preparation stops with an error. Retrying checks the components again. If a component in this App's own directory is damaged, the replacement is verified before replacing it. Other VirtualHere installations are not modified.

**This version has locally completed connection, disconnection, reconnection, and quit through the App, with removal of its controller and owned processes independently confirmed after each stop. This does not establish gameplay or fresh-Mac installation acceptance for this version.** If cleanup cannot be confirmed, retain the error and version when reporting it. Do not repeatedly connect or delete state files to bypass that condition.

## Keys and repeat controls

The defaults do not require Fn or function keys. The main window shows your current bindings; use that display after changing them.

| PS5 action | Mac default |
| --- | --- |
| Left stick up / down / left / right | W / S / A / D |
| D-pad up / down / left / right | ↑ / ↓ / ← / → |
| △ / □ / × / ○ | I / J / K / L |
| L1 / R1 | Q / E |
| L2 / R2 | Z / C |
| Create / Options | Tab / Return (Enter) |
| L3 / R3 | F / Space |
| PS / touchpad press | P / T |
| Enable / pause | Esc, reserved |

**I is above, J is left, K is below, and L is right, matching the controller's face-button diamond.** Each of the 22 game actions can be assigned a single key, cleared, or restored. Esc remains reserved for enabling and pausing control and cannot be assigned to a game action.

In the key and repeat settings, each action can use Off, Hold, or Toggle repeat with an interval of **20–5000 milliseconds**. Defaults are Off and 100 milliseconds. Very short pulses may not be recognized by every game. Changes apply only after you save successfully. Canceling, closing the editor, or a save failure retains the previous settings. Opening the editor pauses control, which remains paused after you leave it.

Keyboard control only operates while the official Remote Play window is in front. Switching to another window or holding Command, Control, Option, or Shift clears held actions and repeat states. Press the desired keys again after returning; an earlier Toggle repeat does not resume. **Esc retains its normal purpose in other Apps.** You can also use the enable or pause button in this App.

The interface follows the preferred macOS language and supports Traditional Chinese, Simplified Chinese, English, Japanese, Korean, German, French, Spanish, and Brazilian Portuguese. Unsupported languages fall back to English. See [Mac default bindings](MAC-DEFAULT-KEYS.md) for more details (Traditional Chinese). The intended scope is 2D, directional, and menu-based control; mouse look, right-stick directions, and touchpad swipes are not provided.

The updated interface offers **Appearance** in the App menu, with System appearance, Light, and Dark choices. This affects only this App, not the macOS appearance. The main window and settings have been reviewed locally in Light and Dark modes. Other language layouts and display configurations remain subject to further review.

## Updates and retained data

Before updating, disconnect, wait for cleanup, and quit the App. Replace `~/Applications/PSKeyboardMacDualSense.app` with the new version. Do not move, delete, or overwrite it while it or its owned components are running. After reopening, check the version, bindings, and Accessibility status. A stable App identity reduces repeated permission prompts, but does not guarantee that macOS will never ask again.

Data is stored in `~/Library/Application Support/PSKeyboard`. Keep the entire directory when updating the App.

| Name | Purpose |
| --- | --- |
| `mac-bindings.json` | Mac key bindings and per-action repeat settings; separate from Windows settings. |
| `mac-bindings.json.backup` | The settings file as it was before the previous save; not a multi-version backup manager. |
| `RuntimeState` | Stable controller-environment identity, component configuration, and required cleanup records for this installation. Preserve it across updates. |
| `Dependencies` | Official VirtualHere components obtained by this App. Verified existing components do not need downloading each time. |
| `Diagnostics` | Local summary diagnostics, not raw keystroke recording. |

If settings cannot be read, the App warns you and temporarily uses defaults without overwriting the original file. Preserve both the main file and backup before investigating. Do not replace the Mac file with Windows `bindings.json`. Do not delete `RuntimeState` to troubleshoot a connection or reset free-use conditions.

## Uninstall

1. Disconnect, wait for cleanup, and quit normally. If cleanup is unconfirmed, retain the state and error until that issue is resolved.
2. In Finder, move `~/Applications/PSKeyboardMacDualSense.app` to the Trash.
3. If you may reinstall later, keep `~/Library/Application Support/PSKeyboard`. For complete removal, back up any settings you want, then remove this App's data directory. At minimum, retain `mac-bindings.json` and its `.backup` to keep bindings; preserving the stable identity in `RuntimeState` is also recommended when reinstalling.
4. You can remove this App's entry from the macOS Accessibility list.

Only remove this App and its own data at those locations. Do not delete Sony Remote Play, VirtualHere installed elsewhere, or other Apps' devices or services. This App does not require uninstalling a global driver and does not quit Sony for you.

## Feedback, donations, and corresponding source

All features are free. The feedback button opens a mail draft with [xiun1019@gmail.com](mailto:xiun1019@gmail.com) as the recipient and the version in its subject. It does not send mail or attach personal settings or diagnostics automatically. Include the version, error text, and reproduction steps when reporting a problem.

The optional [US$1 PayPal donation](https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW) helps buy food for three cats. It opens a browser only when clicked. There is no paid unlock or subscription, and the App does not process payment details.

Original project code is GPL-3.0-only; third-party components retain their respective licenses. The [source repository](https://github.com/xiunlee/ps-remote-play-keyboard/tree/mac-v0.1.3) and [GitHub Releases](https://github.com/xiunlee/ps-remote-play-keyboard/releases) show what has actually been delivered. Choose attachments marked Mac/arm64; the Windows application package does not run on macOS.

The Mac 0.1.3 installation assets are `ThreeCatsKeybridge-Mac-0.1.3-arm64.dmg` and `ThreeCatsKeybridge-Mac-0.1.3-arm64.zip`. They contain the same App; choose one. The optional `ThreeCatsKeybridge-Mac-0.1.3-build-inputs.tar.gz` is for rebuilding, not normal installation.

A Mac delivery includes the App in a DMG or ZIP, SHA-256 checksums, its matching project source package, and the runtime corresponding-source package. The source packages use names such as `ThreeCatsKeybridge-Mac-<version>-source.zip` and `ThreeCatsKeybridge-Mac-<version>-runtime-sources.tar.gz`; **the actual attachment manifest determines names and availability**. Runtime source includes the versions, modifications, licenses, and build and installation materials used by the guest and backend. Proprietary VirtualHere binaries are obtained separately from their publisher. Users do not need source packages or a compiler to install the App.

This project is also not affiliated with or endorsed by VirtualHere or PayPal. Current preparation and acceptance status is recorded in the [Mac release-readiness checklist](MAC-RELEASE-READINESS.md) and [feature parity checklist](MAC-PARITY-CHECKLIST.md), both in Traditional Chinese.

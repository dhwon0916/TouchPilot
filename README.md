# TouchPilot

Independent touch, pen and mouse control for Windows 10/11 (x64).

## Run
Extract the entire ZIP and open **TouchPilot.exe**. Keep **TouchPilot.Input.exe** beside it. No separate runtime installation is needed.

Enable **touch independence**, choose your preferences, and click **Save & apply**. Touch independence and stylus support are off by default. The settings window closes to the tray; choose **Exit** from the tray menu to stop the app and its input service. Tray **Pause** temporarily disables restoration without changing saved preferences.

**Start with Windows** launches TouchPilot at sign-in. Keep its folder in a permanent location; after moving it, open the app and click Save & apply to refresh the startup path. Run normally unless you need to interact with apps running as administrator.

## Controls
- Keep the mouse position while tapping or dragging with touch. The first physical mouse movement returns the pointer; a physical click or wheel instead accepts its current position.
- Optional stylus mode handles Windows-tagged pen input. Hover does not restore typing focus.
- Restore typing focus after release (120 ms default, adjustable up to 5 seconds), or on the next mouse movement.
- Select all displays or individual displays. Selecting none with all displays off disables the features everywhere. Check selections after rearranging/reconnecting displays, since Windows device names can change.
- Hold Ctrl, Alt, Shift or Win while tapping to temporarily keep control in the touched app.
- Keep-focus and restore-only rules use executable names separated by semicolons. Keep-focus rules win.
- Native touchscreen input covers panels that consume touch directly, including AppBar widgets.

Settings and bounded diagnostic logs live in **%LOCALAPPDATA%\TouchPilot**. The input service exits when its parent app closes. Do not enable a second touch-restoration utility at the same time.

Focus restoration is best effort: Windows and some custom controls may refuse it. Pen input consumed without tagged mouse events is not covered. This portable build is unsigned. Actual touch/pen behavior still needs testing on your hardware.

## Source and build
Source is included in the companion source ZIP. License: GPL-3.0-only; see LICENSE. The supplied touch/focus modules were adapted for an independent input service on 2026-09-21.

Prerequisites for building: .NET SDK 10, Rust and Windows C++ build tools.

    powershell -ExecutionPolicy Bypass -File build.ps1
    dotnet test tests/TouchPilot.Tests.csproj -c Release
    cargo test --manifest-path engine/Cargo.toml

Output: dist/TouchPilot. The included settings are separate from other apps.

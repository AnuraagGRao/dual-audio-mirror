<p align="center">
  <img src="assets/app.png" width="128" height="128" alt="Dual Audio Mirror Icon" style="border-radius: 28px;" />
</p>

# Dual & Multi Audio Mirror ⚡

Ultra-lightweight, zero-bloat native Windows WASAPI audio mirror designed to duplicate audio to multiple headphones or audio outputs simultaneously (2, 3, 4, 5+ endpoints) in microsecond lockstep with near-zero latency.

Replaces VoiceMeeter's bloated 90s GUI, EQ filters, Intellipan, and 30-day donation popups with a clean, standalone native executable.

---

## Features

- **Dynamic Multi-Output Mirroring:** Route audio simultaneously to 2, 3, 4, 5+ outputs or headphones in parallel.
- **Dynamic Channel Management:** Click `+ Add Output` or `✕ Remove` on the fly to add or remove playback destinations dynamically.
- **Smart Auto-Detection:** Automatically discovers all your audio endpoints and pairs default outputs seamlessly on startup.
- **Independent Volume & Mute:** Adjust individual volume sliders (0% to 125% boost) and toggle quick mutes for each channel independently.
- **Real-Time LED VU Meters:** Live animated peak level meters for Master Input and every individual output channel.
- **Ultra-Low Latency:** Native 48.0 kHz 32-bit float stereo audio pipeline running via MMCSS `Pro Audio` priority (<10-15ms buffer).
- **Smooth Cyber Dark UI:** Polished glassmorphic dark interface with scrollable channels container and responsive window resizing.
- **System Tray Integration:** Click "Send to Tray" to minimize quietly into the Windows taskbar tray. Right-click tray icon to show dashboard or exit.
- **Windows Startup Toggle:** Checkbox to launch automatically on Windows login.
- **Self-Contained:** Single executable under 250 KB. Compiles directly with Windows' built-in C# compiler (`csc.exe`). Zero external runtimes or dependencies needed.

---

## Architecture & How It Works

```
   [ Windows Audio / Games / Discord / Spotify ]
                         │
                         ▼
        [ Capture Source (Default / Voicemeeter / Speaker) ]
                         │
                 WASAPI Loopback
                         │
                         ▼
             [ DualAudioMirror Engine ]
           (MMCSS Real-Time Priority Thread)
           ┌─────────────┼─────────────┐───────────────┐
           ▼             ▼             ▼               ▼
      (Output 1)    (Output 2)    (Output 3)      (Output N...)
      [ PRO X ]     [ G435 ]      [ Speakers ]    [ BT Headset ]
```

---

## Build & Run

### Running
Double-click `DualAudioMirror.exe` or run:
```powershell
.\DualAudioMirror.exe
```

### Rebuilding from Source
Run the included build script:
```cmd
build.bat
```
*(Uses the native 64-bit .NET compiler built into Windows — no Visual Studio or external tools required).*

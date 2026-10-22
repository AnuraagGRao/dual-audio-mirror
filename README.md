<p align="center">
  <img src="assets/app.png" width="128" height="128" alt="Dual Audio Mirror Icon" style="border-radius: 28px;" />
</p>

# Dual & Multi Audio Mirror ⚡

Ultra-lightweight, zero-bloat audio mirror designed to duplicate audio to multiple headphones or audio outputs simultaneously (2, 3, 4, 5+ endpoints) in microsecond lockstep with near-zero latency.

- **Windows:** Native standalone WASAPI binary (< 250 KB, compiles via built-in `csc.exe`).
- **macOS & Linux:** Standalone native Rust + `egui` + `cpal` app with the identical cyber dark UI.

Replaces VoiceMeeter's bloated 90s GUI, EQ filters, Intellipan, and 30-day donation popups with clean, standalone executables.

---

## Features

- **Dynamic Multi-Output Mirroring:** Route audio simultaneously to 2, 3, 4, 5+ outputs or headphones in parallel.
- **Dynamic Channel Management:** Click `+ Add Output` or `✕ Remove` on the fly to add or remove playback destinations dynamically.
- **Smart Auto-Detection:** Automatically discovers all your audio endpoints and pairs default outputs seamlessly on startup.
- **Independent Volume & Mute:** Adjust individual volume sliders (0% to 125% boost) and toggle quick mutes for each channel independently.
- **Real-Time LED VU Meters:** Live animated peak level meters for Master Input and every individual output channel at 60 FPS.
- **Ultra-Low Latency:** Native 48.0 kHz 32-bit float stereo audio pipeline (<10-15ms buffer).
- **Smooth Cyber Dark UI:** Polished glassmorphic dark interface with scrollable channels container and responsive window resizing.
- **System Tray Integration (Windows):** Click "Send to Tray" to minimize quietly into the Windows taskbar tray. Right-click tray icon to show dashboard or exit.
- **Windows Startup Toggle:** Checkbox to launch automatically on Windows login.

---

## Architecture & How It Works

```
   [ System Audio / Games / Discord / Spotify / Music ]
                         │
                         ▼
        [ Capture Source (Default / Monitor / Speaker) ]
                         │
                 Loopback / Tap
                         │
                         ▼
             [ Multi-Audio Mirror Engine ]
        (Lock-Free SPSC Ringbuffers / MMCSS Real-Time)
           ┌─────────────┼─────────────┐───────────────┐
           ▼             ▼             ▼               ▼
      (Output 1)    (Output 2)    (Output 3)      (Output N...)
      [ PRO X ]     [ G435 ]      [ Speakers ]    [ BT Headset ]
```

---

## Platform Guides & Builds

### 1. Windows (Pure WASAPI Standalone)
- **Running:** Double-click `DualAudioMirror.exe`
- **Rebuilding from Source:** Run `build.bat` *(Uses the 64-bit .NET compiler built into Windows — no external runtimes required).*

### 2. macOS & Linux (Native Rust + `egui` App)
Located in [`rust/`](rust/):
```bash
cd rust
cargo run --release
```
- **macOS:** Supports CoreAudio output devices and Universal binaries (Apple Silicon M1/M2/M3/M4 & Intel).
- **Linux:** Automatically connects with PipeWire / PulseAudio / ALSA sinks.
- **Automated CI/CD:** Builds on every push via [GitHub Actions](.github/workflows/build-cross-platform.yml) to produce standalone `.tar.gz` and Universal macOS binaries.

### 3. Lightweight Native Scripts
- **Linux PipeWire Script:** [`platforms/linux/multi-audio-mirror.sh`](platforms/linux/multi-audio-mirror.sh)
- **macOS CoreAudio Setup & Swift Tool:** [`platforms/macos/`](platforms/macos/)

# macOS Multi-Output Audio Mirroring Guide 🍏

macOS contains built-in kernel-level multi-output audio synchronization within **CoreAudio**. It performs hardware clock drift correction between multiple DACs, Bluetooth headphones, and internal speakers with **0ms perceived latency** and **0% CPU overhead**.

---

## Method 1: Instant Native Setup (Zero Installs, 30 Seconds)

macOS ships with a native app designed specifically for this: **Audio MIDI Setup**.

1. Press `Cmd + Space`, search for **Audio MIDI Setup**, and press `Enter`.
2. In the bottom-left corner of the window, click the **`+`** icon and select **"Create Multi-Output Device"**.
3. In the right pane, check the boxes next to each output you want to play audio to simultaneously:
   - e.g. *External Headphones / DAC*
   - e.g. *AirPods / Bluetooth Headset*
   - e.g. *MacBook Speakers*
4. Designate one device as the **Master Device** (usually your lowest-latency wired device or default DAC).
5. Ensure **Drift Correction** is enabled for all other secondary devices. CoreAudio will automatically resample clock jitters in real-time.
6. Right-click the newly created **Multi-Output Device** in the left sidebar and select **"Use This Device For Sound Output"**.

---

## Method 2: Per-Device Volume Control on macOS

### Why does macOS lock volume keys on Multi-Output Devices?
By default, macOS disables the hardware volume keys (F11/F12) when an Aggregate or Multi-Output device is active because different physical outputs have different DAC step gains.

### Recommended Lightweight Tools:
1. **Multi-Output Volume Bar (Free, Open-Source):**
   - Available via Homebrew:
     ```bash
     brew install --cask background-music
     ```
     or using **Sound Control** / **eqMac** / **AU-Lab**.
2. **Native Swift Controller:**
   You can run the accompanying Swift script in this directory to inspect and set volume on individual devices:
   ```bash
   swift platforms/macos/volume_controller.swift
   ```

#!/usr/bin/env bash
# ==============================================================================
# Dual & Multi Audio Mirror for Linux (PipeWire / PulseAudio)
# Synchronously duplicates audio across multiple output sinks with zero lag.
# ==============================================================================

set -e

SINK_NAME="MultiAudioMirror"

cleanup() {
    echo ""
    echo "[*] Cleaning up MultiAudioMirror virtual sink..."
    MODULE_ID=$(pactl list short modules | grep "module-combine-sink" | grep "$SINK_NAME" | awk '{print $1}')
    if [ -n "$MODULE_ID" ]; then
        pactl unload-module "$MODULE_ID" 2>/dev/null || true
        echo "[✓] Virtual mirror sink unloaded cleanly."
    fi
    exit 0
}

trap cleanup INT TERM EXIT

echo "=========================================================="
echo "      Dual & Multi Audio Mirror (Linux / PipeWire)       "
echo "=========================================================="

# Check for pactl / pipewire-pulse
if ! command -v pactl &> /dev/null; then
    echo "[!] Error: 'pactl' not found. Please install pulseaudio-utils or pipewire-pulse."
    exit 1
fi

# Unload any pre-existing instance of our virtual sink
PREV_ID=$(pactl list short modules | grep "module-combine-sink" | grep "$SINK_NAME" | awk '{print $1}')
if [ -n "$PREV_ID" ]; then
    echo "[*] Unloading prior instance (Module ID: $PREV_ID)..."
    pactl unload-module "$PREV_ID" 2>/dev/null || true
fi

echo ""
echo "[*] Discovering available hardware audio sinks..."
echo "----------------------------------------------------------"

# Extract sinks excluding monitor and virtual sinks
mapfile -t SINK_LIST < <(pactl list short sinks | grep -v "$SINK_NAME" | awk '{print $2}')

if [ ${#SINK_LIST[@]} -lt 2 ]; then
    echo "[!] Only found ${#SINK_LIST[@]} sink(s). Multi-mirror requires at least 2 outputs."
    echo "Available sinks:"
    for s in "${SINK_LIST[@]}"; do
        echo "  - $s"
    done
    exit 1
fi

echo "Available Audio Sinks:"
for i in "${!SINK_LIST[@]}"; do
    DESC=$(pactl list sinks | grep -A 10 "Name: ${SINK_LIST[$i]}" | grep "Description:" | cut -d ':' -f2- | sed 's/^[ \t]*//')
    echo "  [$i] ${SINK_LIST[$i]}"
    if [ -n "$DESC" ]; then
        echo "      ($DESC)"
    fi
done

echo ""
read -rp "Enter sink indices to mirror (comma-separated, e.g. 0,1 or press Enter for all): " SELECTION

SELECTED_SINKS=()
if [ -z "$SELECTION" ]; then
    SELECTED_SINKS=("${SINK_LIST[@]}")
else
    IFS=',' read -ra INDICES <<< "$SELECTION"
    for idx in "${INDICES[@]}"; do
        idx_clean=$(echo "$idx" | tr -d ' ')
        if [ "$idx_clean" -ge 0 ] && [ "$idx_clean" -lt ${#SINK_LIST[@]} ]; then
            SELECTED_SINKS+=("${SINK_LIST[$idx_clean]}")
        else
            echo "[!] Invalid index: $idx_clean, skipping."
        fi
    done
fi

if [ ${#SELECTED_SINKS[@]} -lt 2 ]; then
    echo "[!] Need at least 2 valid sinks selected."
    exit 1
fi

SLAVES_ARG=$(IFS=,; echo "${SELECTED_SINKS[*]}")

echo ""
echo "[*] Creating combined multi-output sink with slaves:"
for s in "${SELECTED_SINKS[@]}"; do
    echo "  -> $s"
done

# Load module-combine-sink
MOD_ID=$(pactl load-module module-combine-sink \
    sink_name="$SINK_NAME" \
    slaves="$SLAVES_ARG" \
    sink_properties=device.description="Multi-Audio-Mirror")

echo ""
echo "[✓] MultiAudioMirror initialized! (Module ID: $MOD_ID)"
echo "[*] Setting default audio sink to MultiAudioMirror..."
pactl set-default-sink "$SINK_NAME"

echo ""
echo "=========================================================="
echo " Audio is now playing in sync across ${#SELECTED_SINKS[@]} outputs!"
echo " Control individual volumes in Pavucontrol or GNOME Settings."
echo " Press Ctrl+C at any time to detach and restore defaults."
echo "=========================================================="

# Keep running until user terminates with Ctrl+C
while true; do
    sleep 2
done

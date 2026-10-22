#!/usr/bin/env bash
# ==============================================================================
# Installer for Multi Audio Mirror on Linux
# ==============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BIN_SOURCE="$SCRIPT_DIR/multi-audio-mirror"
DESKTOP_SOURCE="$SCRIPT_DIR/multi-audio-mirror.desktop"
ICON_SOURCE="$SCRIPT_DIR/multi-audio-mirror.png"

if [ ! -f "$BIN_SOURCE" ]; then
    echo "[!] Error: 'multi-audio-mirror' executable not found in $SCRIPT_DIR"
    exit 1
fi

echo "=========================================================="
echo "         Multi Audio Mirror Installer (Linux)             "
echo "=========================================================="

# Check if running as root or normal user
if [ "$EUID" -eq 0 ]; then
    INSTALL_BIN="/usr/local/bin"
    INSTALL_APP="/usr/share/applications"
    INSTALL_ICON="/usr/share/icons/hicolor/512x512/apps"
else
    INSTALL_BIN="$HOME/.local/bin"
    INSTALL_APP="$HOME/.local/share/applications"
    INSTALL_ICON="$HOME/.local/share/icons/hicolor/512x512/apps"
fi

echo "[*] Target binary directory: $INSTALL_BIN"
echo "[*] Target launcher directory: $INSTALL_APP"
echo "[*] Target icon directory: $INSTALL_ICON"

mkdir -p "$INSTALL_BIN"
mkdir -p "$INSTALL_APP"
mkdir -p "$INSTALL_ICON"

# Copy binary
echo "[*] Installing binary..."
cp "$BIN_SOURCE" "$INSTALL_BIN/multi-audio-mirror"
chmod +x "$INSTALL_BIN/multi-audio-mirror"

# Copy icon
if [ -f "$ICON_SOURCE" ]; then
    echo "[*] Installing app icon..."
    cp "$ICON_SOURCE" "$INSTALL_ICON/multi-audio-mirror.png"
fi

# Copy desktop file
if [ -f "$DESKTOP_SOURCE" ]; then
    echo "[*] Installing desktop shortcut..."
    cp "$DESKTOP_SOURCE" "$INSTALL_APP/multi-audio-mirror.desktop"
    if command -v update-desktop-database &> /dev/null; then
        update-desktop-database "$INSTALL_APP" 2>/dev/null || true
    fi
fi

echo ""
echo "[✓] Installation complete!"
echo "You can now launch 'Multi Audio Mirror' from your applications menu or by typing 'multi-audio-mirror'."
echo "=========================================================="

#!/usr/bin/env bash

set -Eeuo pipefail

SOURCE_DIR="${1:-$PWD}"
SOURCE_DIR="$(realpath "$SOURCE_DIR")"

PROJECT_NAME="$(basename "$SOURCE_DIR")"
DEST_DIR="/tmp/${PROJECT_NAME}-source"
ZIP_FILE="/tmp/${PROJECT_NAME}-source.zip"

echo "Source:      $SOURCE_DIR"
echo "Clean copy:  $DEST_DIR"
echo "Archive:     $ZIP_FILE"

rm -rf "$DEST_DIR" "$ZIP_FILE"
mkdir -p "$DEST_DIR"

rsync -a "$SOURCE_DIR/" "$DEST_DIR/" \
    --exclude='.git/' \
    --exclude='.github/' \
    --exclude='.gitlab/' \
    --exclude='.vs/' \
    --exclude='.idea/' \
    --exclude='.vscode/' \
    --exclude='bin/' \
    --exclude='obj/' \
    --exclude='TestResults/' \
    --exclude='artifacts/' \
    --exclude='coverage/' \
    --exclude='coverage-report/' \
    --exclude='node_modules/' \
    --exclude='packages/' \
    --exclude='publish/' \
    --exclude='logs/' \
    --exclude='*.log' \
    --exclude='*.nupkg' \
    --exclude='*.snupkg' \
    --exclude='*.user' \
    --exclude='*.suo' \
    --exclude='*.userprefs' \
    --exclude='*.DotSettings.user' \
    --exclude='*.cache' \
    --exclude='.DS_Store' \
    --exclude='Thumbs.db'

# Remove empty directories left after filtering.
find "$DEST_DIR" -type d -empty -delete

(
    cd /tmp
    zip -qr "$ZIP_FILE" "$(basename "$DEST_DIR")"
)

echo
echo "Done:"
du -sh "$DEST_DIR" "$ZIP_FILE"
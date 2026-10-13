#!/usr/bin/env bash
# Release builds for GitHub Releases → Builds/Release/:
#   SkylineJumper-<version>.apk           Android (ARM64, Android 8+), signed with Unity's debug key
#   SkylineJumper-<version>-unsigned.ipa  iPhone, unsigned: install with Sideloadly or AltStore (they sign it with your Apple ID)
# Needs Unity 6000.6.5f1 with Android and iOS Build Support, and Xcode. Usage: scripts/build-release.sh
set -euo pipefail
cd "$(dirname "$0")/.."

UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity}"
VERSION=$(sed -n 's/.*const string Version = "\(.*\)";.*/\1/p' src/Assets/_Game/Editor/BuildTools.cs)
OUT=Builds/Release
LOGS=Builds/logs
mkdir -p "$OUT" "$LOGS"

echo "Skyline Jumper $VERSION"
echo "→ Android APK"
"$UNITY" -batchmode -quit -projectPath src -buildTarget Android \
  -executeMethod StickmanJumper.EditorTools.BuildTools.BuildAndroidRelease -logFile "$LOGS/android-release.log"

echo "→ iOS Xcode project"
"$UNITY" -batchmode -quit -projectPath src -buildTarget iOS \
  -executeMethod StickmanJumper.EditorTools.BuildTools.BuildIosRelease -logFile "$LOGS/ios-release.log"

echo "→ iOS app (xcodebuild, no signing)"
xcodebuild -project Builds/iOS-Device-Release/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
  -sdk iphoneos -destination 'generic/platform=iOS' -derivedDataPath Builds/iOS-Device-DerivedData \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" build > "$LOGS/xcode-release.log" 2>&1 \
  || { tail -30 "$LOGS/xcode-release.log"; exit 1; }

echo "→ .ipa"
APP=$(find Builds/iOS-Device-DerivedData/Build/Products/Release-iphoneos -maxdepth 1 -name "*.app" | head -1)
rm -rf Builds/ipa && mkdir -p Builds/ipa/Payload
cp -R "$APP" Builds/ipa/Payload/
rm -f "$OUT/SkylineJumper-$VERSION-unsigned.ipa"
(cd Builds/ipa && zip -qry "../Release/SkylineJumper-$VERSION-unsigned.ipa" Payload)

ls -lh "$OUT"

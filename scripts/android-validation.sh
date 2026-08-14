#!/usr/bin/env bash
# Android hardware validation kit (D-092 / docs/mobile/HARDWARE_VALIDATION.md).
#
# Automates the mechanical parts of the validation pass — environment checks, build, install, log
# capture, posture dumps — so the human tester spends their time on the parts that actually need a
# human: tapping biometric prompts, changing fingerprint enrolment, and observing UX.
#
# It deliberately REFUSES to run against an emulator. An emulated TEE reports
# insideSecureHardware:true while proving nothing, and a validation run that cannot distinguish that
# from real hardware is worse than no run at all (HARDWARE_VALIDATION.md §7).
#
#   Usage: scripts/android-validation.sh [preflight|build|install|posture|capture|all]

set -uo pipefail

ADB="${ADB:-/c/Android/Sdk/platform-tools/adb.exe}"
FLUTTER="${FLUTTER:-/c/flutter/bin/flutter}"
MOBILE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../mobile" && pwd)"
OUT_DIR="${OUT_DIR:-$MOBILE_DIR/../validation-evidence}"
PKG="in.kurx.kurx_mobile"

green() { printf '\033[32m%s\033[0m\n' "$*"; }
red()   { printf '\033[31m%s\033[0m\n' "$*"; }
info()  { printf '  %s\n' "$*"; }

require_physical_device() {
  local serial count characteristics model
  count=$("$ADB" devices | awk 'NR>1 && $2=="device"' | wc -l)
  if [ "$count" -eq 0 ]; then
    red "FAIL: no device attached."
    info "Connect a handset with USB debugging enabled and accept the RSA prompt on the device."
    return 1
  fi
  if [ "$count" -gt 1 ]; then
    red "FAIL: $count devices attached. Detach the others or set ANDROID_SERIAL."
    return 1
  fi

  serial=$("$ADB" devices | awk 'NR>1 && $2=="device" {print $1}')
  # An emulator serial is always emulator-NNNN; ro.build.characteristics also reports it.
  characteristics=$("$ADB" -s "$serial" shell getprop ro.build.characteristics 2>/dev/null | tr -d '\r')
  model=$("$ADB" -s "$serial" shell getprop ro.product.model 2>/dev/null | tr -d '\r')

  if [[ "$serial" == emulator-* ]] || [[ "$characteristics" == *emulator* ]]; then
    red "REFUSING TO RUN: '$serial' ($model) is an emulator."
    info "Hardware-backed keystore, StrongBox, and biometric-enrolment invalidation CANNOT be"
    info "validated on an emulator — an emulated TEE reports insideSecureHardware:true regardless."
    info "See docs/mobile/HARDWARE_VALIDATION.md section 7."
    return 1
  fi

  green "OK: physical device $serial ($model)"
  echo "$serial" > "$OUT_DIR/device.txt"
  return 0
}

preflight() {
  mkdir -p "$OUT_DIR"
  green "== Preflight =="
  require_physical_device || return 1

  local serial; serial=$(cat "$OUT_DIR/device.txt")

  # Record exactly what was tested. A result with no device/OS/build provenance is not evidence.
  {
    echo "timestamp:      $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    echo "serial:         $serial"
    echo "model:          $("$ADB" -s "$serial" shell getprop ro.product.model | tr -d '\r')"
    echo "manufacturer:   $("$ADB" -s "$serial" shell getprop ro.product.manufacturer | tr -d '\r')"
    echo "android:        $("$ADB" -s "$serial" shell getprop ro.build.version.release | tr -d '\r')"
    echo "sdk:            $("$ADB" -s "$serial" shell getprop ro.build.version.sdk | tr -d '\r')"
    echo "build:          $("$ADB" -s "$serial" shell getprop ro.build.fingerprint | tr -d '\r')"
    echo "strongbox:      $("$ADB" -s "$serial" shell pm list features | grep -c strongbox_keystore || true)"
    echo "backend_commit: $(git -C "$MOBILE_DIR/.." rev-parse --short HEAD 2>/dev/null || echo unknown)"
  } | tee "$OUT_DIR/device-info.txt"

  # A screen lock is a prerequisite: without one, no auth-required Keystore key can be created and
  # TC-01 fails for a reason that has nothing to do with the code.
  local secure; secure=$("$ADB" -s "$serial" shell cmd lock_settings get-disabled 2>/dev/null | tr -d '\r')
  if [ "$secure" = "true" ]; then
    red "WARNING: no screen lock is set. Set a PIN and enrol a biometric before TC-01/02/03."
  else
    green "OK: screen lock configured"
  fi
}

build_apk() {
  green "== Build =="
  (cd "$MOBILE_DIR" && "$FLUTTER" build apk --debug) || { red "build failed"; return 1; }
  green "OK: APK built"
}

install_apk() {
  green "== Install =="
  local serial; serial=$(cat "$OUT_DIR/device.txt")
  # Clean install: a leftover keystore entry from a previous run would make TC-01 (first
  # enrollment) silently test the wrong thing.
  "$ADB" -s "$serial" uninstall "$PKG" >/dev/null 2>&1 || true
  "$ADB" -s "$serial" install -r -t "$MOBILE_DIR/build/app/outputs/flutter-apk/app-debug.apk" \
    || { red "install failed"; return 1; }
  green "OK: clean install"
}

# Dumps the device's real key posture — the primary artefact for TC-01.
posture() {
  green "== Key posture =="
  local serial; serial=$(cat "$OUT_DIR/device.txt")
  info "Exercise 'set up this device' in the app, then watch below."
  "$ADB" -s "$serial" logcat -d | grep -E "DEVICE SECURITY POSTURE|KEY POSTURE AFTER CREATE" \
    | tee "$OUT_DIR/key-posture.txt"
  if [ ! -s "$OUT_DIR/key-posture.txt" ]; then
    red "No posture lines yet — run the enrollment flow in the app first."
  fi
}

# Streams the log lines that matter, into a file per run.
capture() {
  green "== Capturing logs (Ctrl-C to stop) =="
  local serial; serial=$(cat "$OUT_DIR/device.txt")
  "$ADB" -s "$serial" logcat -c
  "$ADB" -s "$serial" logcat \
    | grep -Ei "kurx|DeviceKey|Passkey|CredentialManager|Keystore|BiometricPrompt|strongbox|webauthn|assetlinks|FCM|Firebase" \
    | tee "$OUT_DIR/run-$(date -u +%Y%m%dT%H%M%SZ).log"
}

case "${1:-all}" in
  preflight) preflight ;;
  build)     build_apk ;;
  install)   preflight && install_apk ;;
  posture)   posture ;;
  capture)   capture ;;
  all)       preflight && build_apk && install_apk && \
             green "Ready. Run the test cases, then: scripts/android-validation.sh posture" ;;
  *)         echo "usage: $0 [preflight|build|install|posture|capture|all]"; exit 2 ;;
esac

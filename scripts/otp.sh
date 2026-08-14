#!/usr/bin/env bash
# macOS/Linux counterpart of otp.ps1 — prints the most recent dev one-time code (SMS or email)
# from the backend container log. See otp.ps1's header for why this exists; the short version is
# that dev uses SMS_PROVIDER=console and EMAIL_PROVIDER=console, so the code is only ever written
# to the API log, and the request logger buries it thousands of lines deep within minutes.
#
# Serilog writes CLEF (one compact JSON object per line), so this parses with jq rather than the
# regexes otp.ps1 uses. Same four things a plain `docker logs | grep` does not do: searches the
# WHOLE log, prints the code's AGE against the 5-minute expiry, prints the CHANNEL and
# DESTINATION, and reads the code out of the message body where both senders put it.
#
#   ./scripts/otp.sh                 latest code, any channel
#   ./scripts/otp.sh -c email        latest email code
#   ./scripts/otp.sh -w              follow the log and print each new code
set -euo pipefail

CONTAINER=kurx-backend
CHANNEL=any
WATCH=0

usage() {
  cat <<'EOF'
Usage: otp.sh [-w|--watch] [-c|--channel any|sms|email] [--container NAME]

  -w, --watch      Follow the log and print each new code as it is issued.
  -c, --channel    Limit to one channel. Default: any.
      --container  Backend container name. Default: kurx-backend.
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -w|--watch) WATCH=1; shift ;;
    -c|--channel) CHANNEL="$(echo "${2:-}" | tr '[:upper:]' '[:lower:]')"; shift 2 ;;
    --container) CONTAINER="${2:-}"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

case "$CHANNEL" in any|sms|email) ;; *) echo "Bad -c: $CHANNEL (any|sms|email)" >&2; exit 2 ;; esac
command -v jq >/dev/null || { echo "jq is required: brew install jq" >&2; exit 127; }

# One jq program for both modes. `fromjson? // empty` drops the non-JSON lines docker prints
# (startup banner, stack traces), which would otherwise abort the stream.
#
# `@t` carries the issuing time, which is trusted over the wall clock because `docker logs` prints
# historical lines with no indication of age. Fractional seconds are stripped because
# fromdateiso8601 rejects them.
read -r -d '' PROGRAM <<'JQ' || true
  fromjson? // empty
  | (.Message // .Body // "") as $text
  | select($text | test("Your Kurx code is [0-9]{6}"))
  | ((.["@mt"] // "") | if startswith("[email") then "email" else "sms" end) as $ch
  | select($want == "any" or $ch == $want)
  | ($text | capture("Your Kurx code is (?<c>[0-9]{6})").c) as $code
  | (.To // .Phone // "(unknown)") as $dest
  | ((.["@t"] // "") | sub("\\.[0-9]+"; "")) as $ts
  | (try ($ts | fromdateiso8601) catch null) as $issued
  | [ $ts, $ch, $dest, $code, (if $issued == null then "" else (now - $issued | floor | tostring) end) ]
  | @tsv
JQ

# age -> the verdict otp.ps1 prints; 300s matches the server's expiry.
render() {
  while IFS=$'\t' read -r ts ch dest code age; do
    if [[ -z "$age" ]]; then
      verdict="age unknown"
    elif (( age < 300 )); then
      verdict="${age}s old - valid"
    else
      verdict="$(( age / 60 ))m old - EXPIRED, request a new one"
    fi
    # otp.ps1 prints "yyyy-MM-dd HH:mm:ss", so drop the ISO 'T' rather than echoing @t verbatim.
    stamp="${ts%Z}"
    printf '%sZ  [%s]  %s  code %s  (%s)\n' "${stamp/T/ }" "$ch" "$dest" "$code" "$verdict"
  done
}

if (( WATCH )); then
  what=$([[ "$CHANNEL" == any ]] && echo "codes" || echo "$CHANNEL codes")
  echo "Watching $CONTAINER for new $what - request one in the app. Ctrl+C to stop."
  # --since 0s so only codes issued from now on appear; --line-buffered keeps it live through jq.
  docker logs -f --since 0s "$CONTAINER" 2>&1 \
    | jq -R --unbuffered --arg want "$CHANNEL" -r "$PROGRAM" \
    | render
  exit 0
fi

line="$(docker logs "$CONTAINER" 2>&1 | jq -R --arg want "$CHANNEL" -r "$PROGRAM" | tail -n 1)"

if [[ -z "$line" ]]; then
  hint=$([[ "$CHANNEL" == any ]] && echo "" || echo " for channel '$CHANNEL'")
  echo "No code found in $CONTAINER's log$hint. Request one in the app first, then re-run."
  [[ "$CHANNEL" == email ]] && \
    echo "If you requested one, check EMAIL_PROVIDER=console and that ConsoleEmailSender logs body=."
  exit 0
fi

printf '%s\n' "$line" | render

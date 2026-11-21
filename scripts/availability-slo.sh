#!/usr/bin/env bash
# Measures the AVL-uptime SLI from outside the application.
# Method, limits, and current value: docs/operations/availability-slo.md
#
#   PROBE_URL=https://host/api/v1/info/event PROBE_OUTPUT=probes.csv scripts/availability-slo.sh probe
#   PROBE_OUTPUT=probes.csv scripts/availability-slo.sh report
set -u

interval="${PROBE_INTERVAL_SECONDS:-60}"
deadline="${PROBE_DEADLINE_SECONDS:-3}"
output="${PROBE_OUTPUT:?Set PROBE_OUTPUT to the CSV file of probe results.}"
target="${SLO_TARGET_PERCENT:-99}"
header='epoch,utc,result,reason,http_status,content_type,elapsed_ms'

# A good probe is the real event-information answer: 200, JSON, a known status, and no error.
# The status code alone is not enough, because unknown paths answer 200 with the app's HTML page.
classify() {
  local curl_exit="$1" status="$2" content_type="$3" body_file="$4"
  if [ "$curl_exit" -eq 28 ]; then echo "bad deadline-exceeded"
  elif [ "$curl_exit" -ne 0 ]; then echo "bad no-response"
  elif [ "$status" != "200" ]; then echo "bad status-$status"
  elif [[ "$content_type" != application/json* ]]; then echo "bad not-json"
  elif ! grep -Eq '^\{"data":\{"status":"(ACTIVE|OFF_SEASON)".*"error":null\}$' "$body_file"; then echo "bad unexpected-body"
  else echo "good ok"
  fi
}

probe_once() {
  local body_file meta curl_exit status content_type seconds verdict
  body_file="$(mktemp)"
  meta="$(curl --silent --max-time "$deadline" --output "$body_file" \
    --write-out '%{http_code}|%{content_type}|%{time_total}' "$PROBE_URL" 2>/dev/null)"
  curl_exit=$?
  IFS='|' read -r status content_type seconds <<< "$meta"
  verdict="$(classify "$curl_exit" "$status" "$content_type" "$body_file")"
  rm -f "$body_file"
  printf '%s,%s,%s,%s,%s,%s,%s\n' "$(date -u +%s)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
    "${verdict% *}" "${verdict#* }" "$status" "${content_type//,/;}" \
    "$(awk -v s="${seconds:-0}" 'BEGIN { printf "%d", s * 1000 }')" >> "$output"
}

run_probes() {
  : "${PROBE_URL:?Set PROBE_URL to the event-information endpoint.}"
  [ -s "$output" ] || echo "$header" > "$output"
  local count="${PROBE_COUNT:-0}" done_count=0 next
  next="$(date -u +%s)"
  while [ "$count" -eq 0 ] || [ "$done_count" -lt "$count" ]; do
    probe_once
    done_count=$((done_count + 1))
    [ "$count" -ne 0 ] && [ "$done_count" -ge "$count" ] && break
    next=$((next + interval))
    local wait_seconds=$((next - $(date -u +%s)))
    [ "$wait_seconds" -gt 0 ] && sleep "$wait_seconds"
  done
}

report() {
  awk -F, -v interval="$interval" -v target="$target" '
    NR == 1 { next }
    { if (!first) { first = $1; first_utc = $2 } last = $1; last_utc = $2; all++
      if ($3 == "good") good++; else { bad++; reasons[$4]++ } }
    END {
      if (!all) { print "Not measured: no probes recorded."; exit 1 }
      scheduled = int((last - first) / interval + 0.5) + 1
      if (scheduled < all) scheduled = all
      allowed = all * (100 - target) / 100
      printf "Window (UTC):     %s to %s\n", first_utc, last_utc
      printf "Probes attempted: %d of %d scheduled (%.1f%% coverage)\n", all, scheduled, 100 * all / scheduled
      printf "Good / bad:       %d / %d\n", good, bad
      printf "SLI:              %.2f%% (target %s%%)\n", 100 * good / all, target
      printf "Error budget:     %.1f bad probes allowed, %d spent, %.1f left\n", allowed, bad, allowed - bad
      for (r in reasons) printf "Bad reason:       %s = %d\n", r, reasons[r]
    }' "$output"
}

case "${1:-}" in
  probe) run_probes ;;
  report) report ;;
  *) echo "Usage: $0 probe|report" >&2; exit 2 ;;
esac

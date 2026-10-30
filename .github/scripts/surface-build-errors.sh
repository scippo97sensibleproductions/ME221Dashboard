#!/usr/bin/env bash
# Publishes a failed platform build to the job summary and as annotations.
# Annotations are readable through the public check-run API, so a red platform
# build can be diagnosed without access to the raw job log.
#
# Usage: surface-build-errors.sh <label> <build-log>
set -uo pipefail

label=${1:?label required}
log=${2:?build log required}

if [ ! -f "$log" ]; then
  echo "No $log: the build step never ran, nothing to surface."
  exit 0
fi

matches=$(grep -E "error (CS|XA|NU|MSB|NETSDK)|error :" "$log" | head -n 40 | tr -d '\r' || true)

{
  echo "### $label build failed"
  echo '```'
  printf '%s\n' "${matches:-no error lines matched}"
  echo '```'
  echo '<details><summary>last 40 log lines</summary>'
  echo '```'
  tail -n 40 "$log" | tr -d '\r'
  echo '```'
  echo '</details>'
} >> "${GITHUB_STEP_SUMMARY:-/dev/stdout}"

printf '%s\n' "$matches" | head -n 10 | while IFS= read -r line; do
  if [ -n "$line" ]; then
    echo "::error title=$label build::$line"
  fi
done

tail -n 40 "$log" | tr -d '\r'

exit 0

#!/usr/bin/env bash
# Only the unlinked disposable pharmalabvr stack is owned by this workflow.
# Docker may transiently reject stop; keep failure visible if containers remain.
set -euo pipefail
retry_delay="${PLV_STOP_RETRY_DELAY:-2}"

for attempt in 1 2 3; do
  if ! supabase stop --no-backup; then
    echo "Supabase CLI stop failed (attempt $attempt/3); checking Docker state" >&2
  fi

  if containers="$(docker ps -a --format '{{.Names}}')"; then
    remaining="$(printf '%s\n' "$containers" | grep -E '^supabase_.*_pharmalabvr$' || true)"
    if [[ -z "$remaining" ]]; then
      echo "Verified: no disposable pharmalabvr Supabase containers remain"
      exit 0
    fi
    echo "Supabase project containers remain after attempt $attempt/3:" >&2
    printf "%s\n" "$remaining" >&2
  else
    echo "Unable to verify Docker container state after attempt $attempt/3" >&2
  fi

  if (( attempt < 3 )); then
    sleep "$retry_delay"
  fi
done

echo "Unable to confirm disposable Supabase stack teardown after three attempts" >&2
exit 1

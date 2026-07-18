#!/usr/bin/env bash
# Push web/.env.local variables to Vercel (Production + Preview)
# Requires: npm i -g vercel && vercel login && cd web && vercel link
set -euo pipefail
cd "$(dirname "$0")/.."

if [[ ! -f .env.local ]]; then
  echo "Missing .env.local — run: node scripts/import-legacy-env.mjs"
  exit 1
fi

echo "Pushing env vars to Vercel (production + preview)..."
while IFS= read -r line || [[ -n "$line" ]]; do
  line="${line%%#*}"
  line="$(echo "$line" | xargs)"
  [[ -z "$line" ]] && continue
  [[ "$line" != *=* ]] && continue
  key="${line%%=*}"
  val="${line#*=}"
  [[ -z "$val" ]] && echo "Skipping empty: $key" && continue
  echo "  → $key"
  printf '%s' "$val" | vercel env add "$key" production preview --force 2>/dev/null || \
    printf '%s' "$val" | vercel env add "$key" production --force
done < .env.local

echo "Done. Redeploy on Vercel dashboard."

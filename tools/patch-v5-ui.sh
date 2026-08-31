#!/usr/bin/env bash
set -euo pipefail

FILE="${1:?index.html path required}"

# Idempotent: never patch the same deployed source twice.
if grep -q 'id="copyAll"' "$FILE"; then
  exit 0
fi

perl -0pi -e 's#<button class="copy-btn" id="copyOwn" type="button">KOPYALA</button>#<button class="copy-btn" id="copyAll" type="button">KOPYALA</button>#g; s#<button class="copy-btn" id="copyOpp" type="button">KOPYALA</button>##g; s#document\.getElementById\('\''copyOwn'\''\)\.onclick=\(\)=>copyLineup\('\''own'\''\);\s*document\.getElementById\('\''copyOpp'\''\)\.onclick=\(\)=>copyLineup\('\''opp'\''\);#document.getElementById('\''copyAll'\'').onclick=()=>{const b=document.getElementById('\''copyAll'\'');copyText(buildCopyText('\''own'\'')+'\\n\\n'+buildCopyText('\''opp'\''),b)};#g' "$FILE"

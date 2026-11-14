#!/usr/bin/env bash
# Test of the composite action (action-test.yml): publishes the uploader of this runner from the
# sources as a fake release v0.0.0-test, with its .sha256, and serves it on a local port. Nothing
# leaves the runner. Writes url and sha256 to GITHUB_OUTPUT.
set -euo pipefail

case "$RUNNER_OS" in
  Linux) rid=linux-x64; ext="" ;;
  Windows) rid=win-x64; ext=".exe" ;;
  macOS) rid=osx-arm64; ext="" ;;
  *) echo "::error::Unsupported runner $RUNNER_OS."; exit 1 ;;
esac

release="$RUNNER_TEMP/release/v0.0.0-test"
mkdir -p "$release"

dotnet publish src/CodeCoverage.Uploader/CodeCoverage.Uploader.csproj -c Release -r "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:InvariantGlobalization=true -o "$RUNNER_TEMP/publish" --nologo -v q

cp "$RUNNER_TEMP/publish/CodeCoverage.Uploader$ext" "$release/coverage-upload-$rid$ext"

if command -v sha256sum > /dev/null 2>&1; then
  sum=$(sha256sum "$release/coverage-upload-$rid$ext" | cut -d ' ' -f 1)
else
  sum=$(shasum -a 256 "$release/coverage-upload-$rid$ext" | cut -d ' ' -f 1)
fi

printf '%s' "$sum" > "$release/coverage-upload-$rid$ext.sha256"

python=$(command -v python3 || command -v python)
"$python" -m http.server 8799 --bind 127.0.0.1 --directory "$RUNNER_TEMP/release" > /dev/null 2>&1 &

for _ in $(seq 1 50); do
  if curl --silent --output /dev/null http://127.0.0.1:8799/; then
    break
  fi
  sleep 0.2
done

echo "url=http://127.0.0.1:8799" >> "$GITHUB_OUTPUT"
echo "sha256=$sum" >> "$GITHUB_OUTPUT"

#!/usr/bin/env bash
# Offline unit test of install.sh (M5.01): where the release is read from when download-url is not
# given. curl is replaced by a stub that records the address and fails, so nothing leaves the machine.
# Run by action-test.yml; runs in Git Bash too.
set -uo pipefail

here=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

mkdir -p "$work/bin"
cat > "$work/bin/curl" <<'STUB'
#!/usr/bin/env bash
# The last argument is the address.
echo "${@: -1}" >> "$CURL_LOG"
exit 22
STUB
chmod +x "$work/bin/curl"

failures=0

# run <name> <expected address prefix or "none"> <env assignments...>
run() {
  local name=$1 expected=$2
  shift 2
  : > "$work/curl.log"
  : > "$work/output"
  local log
  log=$(env -i PATH="$work/bin:$PATH" CURL_LOG="$work/curl.log" GITHUB_OUTPUT="$work/output" RUNNER_TEMP="$work" \
        RUNNER_OS=Windows RUNNER_ARCH=X64 INPUT_VERSION=1.2.3 "$@" bash "$here/../install.sh" 2>&1)
  local first
  first=$(head -n 1 "$work/curl.log")

  if [ "$expected" = "none" ]; then
    if [ -n "$first" ] || ! grep -q 'repository of the action is unknown' <<< "$log"; then
      echo "FAILED: $name: $first $log"; failures=$((failures + 1)); return
    fi
  elif [ "$first" != "$expected" ]; then
    echo "FAILED: $name: read '$first', expected '$expected'"; failures=$((failures + 1)); return
  fi

  if ! grep -q '^installed=false$' "$work/output"; then
    echo "FAILED: $name: installed is not false"; failures=$((failures + 1)); return
  fi
  echo "  ok: $name"
}

release=v1.2.3/coverage-upload-win-x64.exe

run 'the repository of the action' "https://github.com/octo-org/code-coverage/releases/download/$release" \
  ACTION_REPOSITORY=octo-org/code-coverage GITHUB_ACTION_PATH=/home/runner/work/_actions/other/repo/v1/action
run 'the action path when the repository is empty' "https://github.com/octo-org/code-coverage/releases/download/$release" \
  ACTION_REPOSITORY= GITHUB_ACTION_PATH=/home/runner/work/_actions/octo-org/code-coverage/v1/action
run 'a Windows action path' "https://github.com/octo-org/code-coverage/releases/download/$release" \
  'GITHUB_ACTION_PATH=D:\a\_actions\octo-org\code-coverage\feature\x\action'
run 'download-url wins' "https://mirror.example.com/releases/$release" \
  INPUT_DOWNLOAD_URL=https://mirror.example.com/releases/ ACTION_REPOSITORY=octo-org/code-coverage
run 'neither repository nor path' none ACTION_REPOSITORY= GITHUB_ACTION_PATH=/home/runner/work/code-coverage/action
run 'an injection-shaped repository' none 'ACTION_REPOSITORY=octo/x;touch /tmp/pwned'

if [ "$failures" -gt 0 ]; then
  echo "$failures install.sh case(s) failed."
  exit 1
fi
echo "All install.sh cases passed."

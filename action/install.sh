#!/usr/bin/env bash
# Downloads the uploader of the runner from a pinned release and checks its SHA-256 (M5.01).
# Inputs (environment only): INPUT_VERSION, INPUT_SHA256, INPUT_DOWNLOAD_URL, INPUT_FAIL_ON_ERROR,
# ACTION_REPOSITORY, GITHUB_ACTION_PATH, RUNNER_OS, RUNNER_ARCH, RUNNER_TEMP, GITHUB_OUTPUT.
# Outputs: installed=true|false, path=<binary>. A failure warns and exits 0 unless fail-on-error.
set -uo pipefail

fail() {
  local message=$1
  echo "installed=false" >> "$GITHUB_OUTPUT"

  if [ "${INPUT_FAIL_ON_ERROR:-false}" = "true" ]; then
    echo "::error::CodeCoverage uploader not installed: $message"
    exit 1
  fi

  echo "::warning::CodeCoverage uploader not installed, no coverage is uploaded: $message"
  exit 0
}

# The file is read on stdin: given a name with a backslash (RUNNER_TEMP on Windows, D:\a\_temp),
# GNU sha256sum escapes it and prefixes its line with a backslash (M4.02g). Prints the 64 lowercase
# hexadecimal characters of the sum, or nothing.
sha256_of() {
  local line

  if command -v sha256sum > /dev/null 2>&1; then
    line=$(sha256sum < "$1")
  else
    line=$(shasum -a 256 < "$1")
  fi

  line=$(printf '%s' "$line" | tr 'A-F' 'a-f')

  if [[ "$line" =~ ^([0-9a-f]{64}) ]]; then
    printf '%s\n' "${BASH_REMATCH[1]}"
  fi
}

version=${INPUT_VERSION:-}
version=${version#v}

if ! [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-+][0-9A-Za-z.-]+)?$ ]]; then
  fail "the version '$version' is not a release version such as 1.2.3."
fi

case "${RUNNER_OS:-}/${RUNNER_ARCH:-}" in
  Linux/X64)
    if [ -f /etc/alpine-release ] || (ldd --version 2>&1 | grep -qi musl); then
      rid=linux-musl-x64
    else
      rid=linux-x64
    fi
    file="coverage-upload-$rid"
    ;;
  Windows/X64)
    rid=win-x64
    file="coverage-upload-$rid.exe"
    ;;
  macOS/ARM64)
    rid=osx-arm64
    file="coverage-upload-$rid"
    ;;
  *)
    fail "no uploader is published for ${RUNNER_OS:-?}/${RUNNER_ARCH:-?} (linux-x64, linux-musl-x64, win-x64 and osx-arm64 are)."
    ;;
esac

base=${INPUT_DOWNLOAD_URL:-}

if [ -z "$base" ]; then
  repository=${ACTION_REPOSITORY:-}

  # github.action_repository can be empty in a composite step: the runner checks the action out
  # under .../_actions/<owner>/<repo>/<ref>/action, which names the repository too.
  if [ -z "$repository" ]; then
    action_path=${GITHUB_ACTION_PATH:-}
    action_path=${action_path//\\//}
    if [[ "$action_path" =~ /_actions/([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)/ ]]; then
      repository="${BASH_REMATCH[1]}/${BASH_REMATCH[2]}"
    fi
  fi

  if ! [[ "$repository" =~ ^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]; then
    fail "the repository of the action is unknown: give download-url."
  fi
  base="https://github.com/$repository/releases/download"
fi

base=${base%/}
directory="${RUNNER_TEMP:-/tmp}/coverage-upload-$version"
binary="$directory/$file"
mkdir -p "$directory"

curl --fail --silent --show-error --location --retry 3 --output "$binary" "$base/v$version/$file" \
  || fail "the download of $base/v$version/$file failed."
curl --fail --silent --show-error --location --retry 3 --output "$binary.sha256" "$base/v$version/$file.sha256" \
  || fail "the download of $base/v$version/$file.sha256 failed."

actual=$(sha256_of "$binary")

if [ -z "$actual" ]; then
  rm -f "$binary"
  fail "the SHA-256 of $file could not be computed."
fi
published=$(tr -d ' \r\n' < "$binary.sha256")

if [ "$actual" != "$published" ]; then
  rm -f "$binary"
  fail "the SHA-256 of $file ($actual) is not the one of the release ($published)."
fi

pinned=$(printf '%s' "${INPUT_SHA256:-}" | tr -d ' \r\n' | tr 'A-F' 'a-f')

if [ -n "$pinned" ] && [ "$actual" != "$pinned" ]; then
  rm -f "$binary"
  fail "the SHA-256 of $file ($actual) is not the pinned one ($pinned)."
fi

chmod +x "$binary"
echo "CodeCoverage uploader $version ($rid) installed, SHA-256 $actual."
echo "installed=true" >> "$GITHUB_OUTPUT"
echo "path=$binary" >> "$GITHUB_OUTPUT"

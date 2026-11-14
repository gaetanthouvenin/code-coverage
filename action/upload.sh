#!/usr/bin/env bash
# Runs the installed uploader (M5.01). Every option comes from the COVERAGE_* variables the action
# sets from its inputs; the uploader reads them itself (--file falls back on COVERAGE_FILE, and so on).
# New lines in the file list become ";", the separator of COVERAGE_FILE.
set -uo pipefail

COVERAGE_FILE=$(printf '%s' "${COVERAGE_FILE:-}" | tr '\r\n' ';;')
export COVERAGE_FILE

"$UPLOADER"

#!/bin/sh
# Offline test of job-started.sh (M4.02b): the hook runs with the environment of accepted and refused
# jobs. Run by the build-and-test job of ci-cd.yml. Exits with 1 when a check fails.
set -u

hook="$(dirname "$0")/job-started.sh"
failures=0
repo=owner/code-coverage

# check <name> <expected: accept|refuse> <repository setting> <repository> <event> <ref> <workflow ref>
check() {
  if env -i PATH="$PATH" CC_DEPLOY_REPOSITORY="$3" GITHUB_REPOSITORY="$4" GITHUB_EVENT_NAME="$5" \
       GITHUB_REF="$6" GITHUB_WORKFLOW_REF="$7" GITHUB_JOB=deploy sh "$hook" >/dev/null 2>&1; then
    actual=accept
  else
    actual=refuse
  fi

  if [ "$actual" = "$2" ]; then
    echo "  ok: $1"
  else
    echo "  FAILED: $1 (expected $2, got $actual)"
    failures=$((failures + 1))
  fi
}

ok_ref="$repo/.github/workflows/ci-cd.yml@refs/tags/v1.2.3"

check 'a v* tag pushed to ci-cd.yml is accepted' accept "$repo" "$repo" push refs/tags/v1.2.3 "$ok_ref"
check 'a pre-release tag is accepted' accept "$repo" "$repo" push refs/tags/v1.3.0-beta.4 "$repo/.github/workflows/ci-cd.yml@refs/tags/v1.3.0-beta.4"
check 'a pull request is refused' refuse "$repo" "$repo" pull_request refs/pull/7/merge "$repo/.github/workflows/ci-cd.yml@refs/pull/7/merge"
check 'a pull_request_target is refused' refuse "$repo" "$repo" pull_request_target refs/tags/v1.2.3 "$ok_ref"
check 'a workflow_dispatch is refused' refuse "$repo" "$repo" workflow_dispatch refs/tags/v1.2.3 "$ok_ref"
check 'a branch push is refused' refuse "$repo" "$repo" push refs/heads/main "$repo/.github/workflows/ci-cd.yml@refs/heads/main"
check 'a tag other than v* is refused' refuse "$repo" "$repo" push refs/tags/release-1 "$repo/.github/workflows/ci-cd.yml@refs/tags/release-1"
check 'another workflow is refused' refuse "$repo" "$repo" push refs/tags/v1.2.3 "$repo/.github/workflows/evil.yml@refs/tags/v1.2.3"
check 'ci-cd.yml of a branch is refused' refuse "$repo" "$repo" push refs/tags/v1.2.3 "$repo/.github/workflows/ci-cd.yml@refs/heads/feature/x"
check 'a workflow of another repository is refused' refuse "$repo" "$repo" push refs/tags/v1.2.3 "fork/code-coverage/.github/workflows/ci-cd.yml@refs/tags/v1.2.3"
check 'another repository is refused' refuse "$repo" fork/code-coverage push refs/tags/v1.2.3 "fork/code-coverage/.github/workflows/ci-cd.yml@refs/tags/v1.2.3"
check 'without CC_DEPLOY_REPOSITORY every job is refused' refuse "" "$repo" push refs/tags/v1.2.3 "$ok_ref"
check 'a missing workflow ref is refused' refuse "$repo" "$repo" push refs/tags/v1.2.3 ""

if [ "$failures" -ne 0 ]; then
  echo "$failures check(s) failed."
  exit 1
fi

echo 'All the hook checks passed.'

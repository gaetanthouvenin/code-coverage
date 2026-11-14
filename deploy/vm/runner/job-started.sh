#!/bin/sh
# Pre-job hook of the self-hosted runner of the production host (M4.02b, docs/operations.md
# "Continuous deployment"). The runner runs it before every job, with the environment of the job, when
# its .env names it:
#
#   ACTIONS_RUNNER_HOOK_JOB_STARTED=/opt/actions-runner-hooks/job-started.sh
#   CC_DEPLOY_REPOSITORY=<owner>/code-coverage
#
# A non-zero exit fails the job before any of its steps. Only the deploy job of ci-cd.yml, run by the
# push of a v* tag of this repository from the ci-cd.yml of that tag, may run on this runner: a pull
# request, a workflow_dispatch, another workflow or a workflow of a branch is refused, whatever label
# it targets. Without CC_DEPLOY_REPOSITORY, every job is refused.
set -eu

refuse() {
  echo "job-started: refused, $1. This runner only deploys the v* tags of ${CC_DEPLOY_REPOSITORY:-<CC_DEPLOY_REPOSITORY unset>} (docs/operations.md, Continuous deployment)." >&2
  exit 1
}

repository=${CC_DEPLOY_REPOSITORY:-}
[ -n "$repository" ] || refuse "CC_DEPLOY_REPOSITORY is not set"

[ "${GITHUB_REPOSITORY:-}" = "$repository" ] || refuse "the repository is '${GITHUB_REPOSITORY:-}'"
[ "${GITHUB_EVENT_NAME:-}" = "push" ] || refuse "the event is '${GITHUB_EVENT_NAME:-}', not push"

case "${GITHUB_REF:-}" in
  refs/tags/v*) ;;
  *) refuse "the ref is '${GITHUB_REF:-}', not a v* tag" ;;
esac

case "${GITHUB_WORKFLOW_REF:-}" in
  "$repository/.github/workflows/ci-cd.yml@refs/tags/v"*) ;;
  *) refuse "the workflow is '${GITHUB_WORKFLOW_REF:-}', not ci-cd.yml of a v* tag" ;;
esac

echo "job-started: accepted ${GITHUB_WORKFLOW_REF} (${GITHUB_JOB:-job})"

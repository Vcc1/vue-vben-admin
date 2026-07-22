#!/usr/bin/env bash
set -Eeuo pipefail

workspace="${WORKSPACE:-/workspace}"
runs_dir="${RUNS_DIR:-/runs}"
base_branch="${BASE_BRANCH:-main}"
optimization_branch="${OPTIMIZATION_BRANCH:-codex/long-term-room-matching}"
optimization_project="${OPTIMIZATION_PROJECT:-Models/Room/RoomMatchings/Core}"
prompt_file="${PROMPT_FILE:-/opt/codex/prompts/optimize.md}"
run_id="$(date -u +%Y%m%d-%H%M%S)"
run_dir="${runs_dir}/${run_id}"
lock_dir="${runs_dir}/.optimize.lock"

if ! mkdir "${lock_dir}" 2>/dev/null; then
    echo "ERROR: another optimization run is already in progress." >&2
    exit 5
fi
trap 'rmdir "${lock_dir}" 2>/dev/null || true' EXIT
mkdir -p "${run_dir}"

if ! git -C "${workspace}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "ERROR: ${workspace} is not a Git repository." >&2
    exit 2
fi
git -C "${workspace}" config --global --add safe.directory "${workspace}"

if [[ -n "${GIT_AUTHOR_NAME:-}" ]]; then
    git -C "${workspace}" config user.name "${GIT_AUTHOR_NAME}"
fi
if [[ -n "${GIT_AUTHOR_EMAIL:-}" ]]; then
    git -C "${workspace}" config user.email "${GIT_AUTHOR_EMAIL}"
fi

if [[ ! -f "${prompt_file}" ]]; then
    echo "ERROR: prompt file not found: ${prompt_file}" >&2
    exit 2
fi

if ! git -C "${workspace}" check-ref-format --branch "${optimization_branch}" >/dev/null 2>&1; then
    echo "ERROR: invalid OPTIMIZATION_BRANCH: ${optimization_branch}" >&2
    exit 2
fi

if [[ ! -d "${workspace}/${optimization_project}" ]]; then
    echo "ERROR: OPTIMIZATION_PROJECT does not exist: ${optimization_project}" >&2
    exit 2
fi

if [[ -n "$(git -C "${workspace}" status --porcelain)" && "${ALLOW_DIRTY:-false}" != "true" ]]; then
    echo "ERROR: worktree is dirty. Commit/stash it, or set ALLOW_DIRTY=true deliberately." >&2
    exit 3
fi

if [[ "${GIT_SYNC:-true}" == "true" ]]; then
    git -C "${workspace}" fetch origin "${base_branch}"
fi

if git -C "${workspace}" show-ref --verify --quiet "refs/heads/${optimization_branch}"; then
    git -C "${workspace}" switch "${optimization_branch}"
elif git -C "${workspace}" show-ref --verify --quiet "refs/remotes/origin/${optimization_branch}"; then
    git -C "${workspace}" switch --track "origin/${optimization_branch}"
else
    git -C "${workspace}" switch "${base_branch}"
    if [[ "${GIT_SYNC:-true}" == "true" ]]; then
        git -C "${workspace}" merge --ff-only "origin/${base_branch}"
    fi
    git -C "${workspace}" switch -c "${optimization_branch}"
fi

set +e
{
    cat "${prompt_file}"
    printf '\nThe only in-scope project path for this run is: %s\n' "${optimization_project}"
} | codex exec \
    --cd "${workspace}" \
    --sandbox workspace-write \
    --json \
    --output-last-message "${run_dir}/last-message.md" \
    - 2>&1 | tee "${run_dir}/events.jsonl"
codex_exit=${PIPESTATUS[1]}
set -e

if (( codex_exit != 0 )); then
    echo "ERROR: Codex exited with status ${codex_exit}. Changes remain on ${optimization_branch}." >&2
    exit "${codex_exit}"
fi

if [[ -n "${VERIFY_COMMAND:-}" ]]; then
    bash -lc "cd \"${workspace}\" && ${VERIFY_COMMAND}" 2>&1 | tee "${run_dir}/verify.log"
fi

git -C "${workspace}" status --short | tee "${run_dir}/git-status.txt"

while IFS= read -r changed_path; do
    [[ -z "${changed_path}" ]] && continue
    case "${changed_path}" in
        "${optimization_project}"/*) ;;
        *)
            echo "ERROR: Codex changed a path outside ${optimization_project}: ${changed_path}" >&2
            echo "Changes remain on ${optimization_branch}; no commit was created." >&2
            exit 6
            ;;
    esac
done < <(
    {
        git -C "${workspace}" diff --name-only
        git -C "${workspace}" diff --cached --name-only
        git -C "${workspace}" ls-files --others --exclude-standard
    } | sort -u
)

if git -C "${workspace}" diff --quiet && git -C "${workspace}" diff --cached --quiet \
    && [[ -z "$(git -C "${workspace}" ls-files --others --exclude-standard)" ]]; then
    echo "Codex completed without changing files. Branch: ${optimization_branch}"
    exit 0
fi

if [[ "${AUTO_COMMIT:-true}" == "true" ]]; then
    git -C "${workspace}" add --all
    git -C "${workspace}" commit -m "chore: apply automated Codex improvement"
fi

if [[ "${AUTO_PUSH:-false}" == "true" ]]; then
    if [[ "${AUTO_COMMIT:-true}" != "true" ]]; then
        echo "ERROR: AUTO_PUSH=true requires AUTO_COMMIT=true." >&2
        exit 4
    fi
    git -C "${workspace}" push --set-upstream origin "${optimization_branch}"
fi

echo "Codex run completed. Branch: ${optimization_branch}; logs: ${run_dir}"

#!/usr/bin/env bash
set -Eeuo pipefail

workspace="${WORKSPACE:-/workspace}"
repository_url="${GIT_REPOSITORY_URL:-}"
base_branch="${BASE_BRANCH:-main}"

if [[ -d "${workspace}/.git" ]]; then
    echo "Git workspace already exists: ${workspace}"
    exit 0
fi

if [[ -z "${repository_url}" ]]; then
    echo "ERROR: GIT_REPOSITORY_URL is required to initialize the workspace." >&2
    exit 2
fi

if [[ -n "$(find "${workspace}" -mindepth 1 -maxdepth 1 -print -quit 2>/dev/null)" ]]; then
    echo "ERROR: ${workspace} is not empty and is not a Git repository." >&2
    exit 3
fi

git clone --branch "${base_branch}" "${repository_url}" "${workspace}"
git -C "${workspace}" config --global --add safe.directory "${workspace}"
echo "Repository initialized on ${base_branch}: ${workspace}"

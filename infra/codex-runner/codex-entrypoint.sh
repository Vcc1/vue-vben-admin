#!/usr/bin/env bash
set -Eeuo pipefail

codex_home="${CODEX_HOME:-${HOME}/.codex}"
mkdir -p "${codex_home}"
chmod 0700 "${codex_home}"

toml_escape() {
    local value="$1"
    value="${value//\\/\\\\}"
    value="${value//\"/\\\"}"
    value="${value//$'\n'/}"
    printf '%s' "${value}"
}

if [[ -n "${CODEX_BASE_URL:-}" ]]; then
    base_url="$(toml_escape "${CODEX_BASE_URL}")"
    model_provider="$(toml_escape "${CODEX_MODEL_PROVIDER:-OpenAI}")"
    model="$(toml_escape "${CODEX_MODEL:-gpt-5.5}")"
    review_model="$(toml_escape "${CODEX_REVIEW_MODEL:-gpt-5.5}")"
    reasoning_effort="$(toml_escape "${CODEX_REASONING_EFFORT:-xhigh}")"

    cat > "${codex_home}/config.toml" <<EOF
model_provider = "${model_provider}"
model = "${model}"
review_model = "${review_model}"
model_reasoning_effort = "${reasoning_effort}"
disable_response_storage = true
network_access = "enabled"
windows_wsl_setup_acknowledged = true

[model_providers.${model_provider}]
name = "${model_provider}"
base_url = "${base_url}"
wire_api = "responses"
requires_openai_auth = true

[features]
goals = true
EOF
    chmod 0600 "${codex_home}/config.toml"
fi

api_key="${OPENAI_API_KEY:-}"
if [[ -n "${OPENAI_API_KEY_FILE:-}" && -f "${OPENAI_API_KEY_FILE}" ]]; then
    api_key="$(<"${OPENAI_API_KEY_FILE}")"
fi
if [[ -n "${api_key}" ]]; then
    OPENAI_API_KEY="${api_key}" AUTH_JSON_PATH="${codex_home}/auth.json" node -e \
        'require("fs").writeFileSync(process.env.AUTH_JSON_PATH, JSON.stringify({OPENAI_API_KEY: process.env.OPENAI_API_KEY}, null, 2) + "\n")'
    chmod 0600 "${codex_home}/auth.json"
    unset OPENAI_API_KEY
fi

exec "$@"

FROM node:22-bookworm-slim AS codex-cli

ARG CODEX_VERSION=0.145.0

RUN npm install --global "@openai/codex@${CODEX_VERSION}" \
    && npm cache clean --force

FROM mcr.microsoft.com/dotnet/sdk:10.0

ARG USER_UID=1000
ARG USER_GID=1000

RUN groupadd --gid "${USER_GID}" codex \
    && useradd --uid "${USER_UID}" --gid "${USER_GID}" --create-home --shell /bin/bash codex

COPY --from=codex-cli /usr/local/ /usr/local/
COPY infra/codex-runner/init-workspace.sh /usr/local/bin/init-workspace
COPY infra/codex-runner/optimize.sh /usr/local/bin/optimize
COPY infra/codex-runner/codex-entrypoint.sh /usr/local/bin/codex-entrypoint
COPY infra/codex-runner/prompts/ /opt/codex/prompts/

RUN chmod 0755 /usr/local/bin/init-workspace /usr/local/bin/optimize /usr/local/bin/codex-entrypoint \
    && mkdir -p /workspace /runs /home/codex/.codex /home/codex/.nuget/packages \
    && chown -R codex:codex /workspace /runs /home/codex

ENV CODEX_HOME=/home/codex/.codex \
    DOTNET_CLI_HOME=/home/codex \
    NUGET_PACKAGES=/home/codex/.nuget/packages \
    DOTNET_NOLOGO=true \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true \
    NUGET_XMLDOC_MODE=skip

USER codex
WORKDIR /workspace

ENTRYPOINT ["/usr/local/bin/codex-entrypoint"]
CMD ["sleep", "infinity"]

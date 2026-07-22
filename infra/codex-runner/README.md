# Codex + Git + .NET 10 Docker 运行环境

该目录提供一个可部署到 `192.168.1.130` 的 Codex 工具容器。镜像包含：

- .NET 10 SDK
- Git 和 OpenSSH Client
- Node.js 22
- Codex CLI 0.145.0（可通过构建参数更新）
- `ripgrep`、`jq`、`curl` 等代码分析工具

源码、Codex 登录凭据、NuGet 缓存和运行日志都在卷中持久化，不会打进镜像。

## 1. 部署

### Portainer Stack（推荐）

不要把 `portainer-stack.yaml` 直接粘贴到 Portainer 的 Web Editor，因为该文件需要 Git
仓库作为 Docker build context。请在 Portainer 选择 **Stacks -> Add stack -> Git Repository**：

1. Repository URL 填当前代码仓库地址。
2. Compose path 填 `infra/codex-runner/portainer-stack.yaml`。
3. Branch 填 `main`，保存并部署。
4. 在 Environment variables 中设置 `GIT_REPOSITORY_URL`，值为要持续优化的代码仓库地址。
5. 如果是私有仓库，设置 `GIT_SSH_PATH` 为服务器上的 deploy key 目录；公有仓库可保留默认值。

该 Compose 文件位于 `infra/codex-runner`，所以 build context 使用 `../..` 回到 Portainer
检出的 Git 仓库根目录，再从根目录读取 `infra/codex-runner/Dockerfile`。

`repo-init` 容器第一次启动时会把仓库克隆到名为 `workspace` 的持久卷；它随后显示为
`Exited (0)` 是正常的。`codex` 容器应保持运行。

如果当前 Portainer 版本不支持 Git Stack 的 `build`，先在 Portainer 的 Images 页面从该 Git
仓库构建 `mengtuv-codex-runner:0.145.0`，再把 Stack YAML 中的两个 `build:` 小节删除后部署。

把本目录放到服务器，例如 `/opt/mengtu-codex`，然后执行：

```bash
cd /opt/mengtu-codex
cp .env.example .env
mkdir -p workspace
git clone <你的仓库地址> workspace
docker compose build
docker compose up -d
docker compose ps
```

如果仓库已经在服务器上，把 `.env` 的 `WORKSPACE_PATH` 改为绝对路径。该目录必须允许
`.env` 中的 `USER_UID`/`USER_GID` 写入；在 Linux 上通常可用 `id -u` 和 `id -g` 确认值。

私有 Git 仓库建议在服务器上先配置只具备该仓库权限的 deploy key，然后复制
`compose.ssh.yaml.example` 为 `compose.ssh.yaml`，修改 SSH 目录后使用：

```bash
docker compose -f compose.yaml -f compose.ssh.yaml up -d
```

不要把私钥打入镜像。

## 2. 登录 Codex

在 Portainer 打开 `codex` 容器的 **Console**，选择 `/bin/bash`，连接后执行下面的命令。

推荐使用设备登录，登录结果保存在 `codex-home` 卷：

```bash
docker compose exec codex codex login --device-auth
docker compose exec codex codex login status
```

也可以使用 API Key，Key 只通过标准输入传入，不写进 `.env` 或 Compose 文件：

```bash
read -rsp "OPENAI_API_KEY: " OPENAI_API_KEY
printf '%s' "$OPENAI_API_KEY" | docker compose exec -T codex codex login --with-api-key
unset OPENAI_API_KEY
```

## 3. 交互使用

```bash
docker compose exec codex bash
docker compose exec codex codex
```

容器默认长期运行，但不会自行修改代码。

## 4. 执行一次自动优化

```bash
docker compose exec codex optimize
```

每次运行会复用固定的 `codex/long-term-room-matching` 分支，范围锁定在
`Models/Room/RoomMatchings/Core`。第一次运行时该分支从 `main` 创建，后续运行
继续在同一分支积累；如果远端已经存在同名分支，会先跟踪它。默认提示词位于
`prompts/optimize.md`，运行日志保存在 `codex-runs` 卷。工作区不干净时脚本会停止，避免覆盖
人工改动；脚本还会拒绝提交房型匹配目录之外的变更。每轮成功后默认自动提交到该专用分支。

建议先保持默认的 `AUTO_PUSH=false`，人工检查几次专用分支结果。稳定后可以在 `.env` 中启用
验证命令和推送：

```dotenv
VERIFY_COMMAND=dotnet test MengtuV.slnx --no-restore
AUTO_PUSH=true
```

启用自动推送前，需要为容器配置最小权限 Git 凭据，并在 Git 服务端保护 `main`，只允许通过
Pull Request 合并。脚本只会推送 `OPTIMIZATION_BRANCH`，不会直接推送到 `main`。

## 5. 定时执行

确认单次执行可靠后，可在服务器宿主机的 crontab 中每天运行一次：

```cron
30 2 * * * cd /opt/mengtu-codex && /usr/bin/docker compose exec -T codex optimize >> /var/log/mengtu-codex.log 2>&1
```

不要并发运行多个 `optimize`，因为它们共享同一个 Git 工作区。若需要并行任务，应为每个任务
使用独立 clone 或 Git worktree。

## 6. 更新 Codex CLI

修改 `.env` 中的 `CODEX_VERSION` 后重建：

```bash
docker compose build --pull
docker compose up -d --force-recreate
```

可用以下命令检查完整环境：

```bash
docker compose exec codex bash -lc 'git --version && codex --version && dotnet --info'
```

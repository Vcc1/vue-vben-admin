# 通用前端生产镜像（@vben/admin、@vben/paycardhub 共用）
# 构建上下文必须是 UI 单体仓库根目录（pnpm workspace 需要根 lockfile）
# 由 build-and-push.ps1 通过 --build-arg 注入 APP_DIR / APP_PKG / VITE_*

# ---------- 构建阶段 ----------
FROM node:22-slim AS builder

ENV PNPM_HOME="/pnpm"
ENV PATH="$PNPM_HOME:$PATH"
ENV NODE_OPTIONS=--max-old-space-size=8192
ENV TZ=Asia/Shanghai
ENV CI=true

RUN npm i -g corepack

WORKDIR /app
COPY . /app

# 要构建的本地 vue app：目录（相对仓库根）与包名（pnpm --filter 用）
ARG APP_DIR=apps/paycardhub
ARG APP_PKG=@vben/paycardhub

RUN --mount=type=cache,id=pnpm,target=/pnpm/store pnpm install --frozen-lockfile

# 生产环境变量（Vite 构建时烤入镜像）。
# 留空的变量不会写入，自动回退到该 app 的 .env.production 同名值。
ARG VITE_GLOB_API_URL=""
ARG VITE_OIDC_AUTHORITY=""
ARG VITE_OIDC_CLIENT_ID=""
ARG VITE_OIDC_REDIRECT_URI=""
RUN set -eux; \
    f="$APP_DIR/.env.production.local"; : > "$f"; \
    { [ -n "$VITE_GLOB_API_URL" ]      && echo "VITE_GLOB_API_URL=\"$VITE_GLOB_API_URL\""           >> "$f"; } || true; \
    { [ -n "$VITE_OIDC_AUTHORITY" ]    && echo "VITE_OIDC_AUTHORITY=\"$VITE_OIDC_AUTHORITY\""       >> "$f"; } || true; \
    { [ -n "$VITE_OIDC_CLIENT_ID" ]    && echo "VITE_OIDC_CLIENT_ID=\"$VITE_OIDC_CLIENT_ID\""       >> "$f"; } || true; \
    { [ -n "$VITE_OIDC_REDIRECT_URI" ] && echo "VITE_OIDC_REDIRECT_URI=\"$VITE_OIDC_REDIRECT_URI\"" >> "$f"; } || true; \
    echo "---- $f ----"; cat "$f"

RUN pnpm run build --filter="$APP_PKG"

# ---------- 运行阶段 ----------
FROM nginx:stable-alpine AS production

RUN echo "types { application/javascript js mjs; }" > /etc/nginx/conf.d/mjs.conf \
    && rm -f /etc/nginx/conf.d/default.conf

# 该 app 的构建产物
ARG APP_DIR=apps/paycardhub
COPY --from=builder /app/${APP_DIR}/dist /usr/share/nginx/html
COPY scripts/deploy/nginx.conf /etc/nginx/nginx.conf

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]

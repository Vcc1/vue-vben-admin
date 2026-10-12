# 统一会话管理与分级登录 需求文档

- 状态：草案 v1.2（增补：第三方标准 OIDC 对接 profile；踢人推送改用标准 Back-Channel Logout；deviceId 改由 AS 侧 cookie 承载）
- 日期：2026-09-24
- 范围：Admin.Host（主认证系统，AdminIdentity + OidcServer）、Admin 前端、paycardhub 前端/后端、未来接入的 OIDC 子系统

## 1. 背景

现状：

- Admin.Host 同时承载 AdminIdentity（用户名密码 + JWT）与 OidcServer（OIDC 授权服务器）。access token 为无状态 JWT（60 分钟），refresh token 落库轮换（`RefreshTokens` 表）。
- 子系统（paycardhub 及未来第三方后台）走 OIDC 授权码流程，后端无状态验签（JWKS），不连主系统存储。

痛点：

1. **无法踢单个登录凭证**：吊销 refresh 只能阻止续期，已签发的 access token 活到自然过期（最长 60 分钟）。
2. **没有在线会话/设备列表**：`AdminRefreshToken` 仅有 IP、时间，无设备信息；OIDC 侧更无。
3. **登出不对称**：paycardhub 退出能清主系统 Cookie，Admin 退出清不掉其他 OIDC 客户端。
4. **登录无分级**：新设备、被踢设备与受信设备同等对待（仅密码）。

约束：

- 生产环境全 HTTP（无 HTTPS），跨站 Secure Cookie 不可用。
- 子系统会持续增加，接入成本必须低：**不共享 Redis、不共享数据库**。
- 踢人不能误伤同一用户的其他凭证（多设备并存是常态）。
- **协议标准性**：第三方系统（非本项目开发、无法要求其改代码）必须能用**纯标准 OIDC** 接入。所有自有扩展只能是"附加、可忽略"的，不得改变标准端点的请求/响应契约，不得要求 client 传递自定义参数。详见 FR-10。

## 2. 目标与非目标

目标：

- G1 凭证级会话模型（sid），提供在线会话/设备列表。
- G2 踢单个凭证：推送命中秒级生效；最坏硬上界 ≤ access 寿命（15 分钟）+ 30 秒时钟容差。
- G3 统一登出：一个浏览器内退出 = 该浏览器的主系统 + 全部子系统同时退出；其他设备不受影响。
- G4 分级登录：受信设备仅密码；新设备 / 被踢设备 = 密码 + 额外验证（邮箱 OTP）。
- G5 子系统零共享存储：仅接收 AS 签名推送 + 进程内存标记。
- G6 协议标准性：第三方系统用纯标准 OIDC（授权码 + PKCE + discovery/JWKS + 标准 Back-Channel Logout）即可完成接入，含 SSO、统一登出、秒级踢人。自有扩展一律附加且可忽略。

非目标：

- 不做每请求共享状态校验（introspection / 每请求查 Redis）。
- 不做 WebSocket 实时推送（列为备选，不实施）。
- 不改 API Key 服务间认证体系（不受用户踢人影响）。
- 一期不做用户自助设备管理页（仅管理员会话管理页）。

## 3. 术语

| 术语 | 定义 |
|------|------|
| 主系统 / AS | Admin.Host（AdminIdentity + OidcServer） |
| 子系统 / 客户端 | OIDC 接入的前后端组合（paycardhub、未来第三方后台） |
| 凭证 / 会话（sid） | 一次成功登录派生的全部登录态：主系统 Cookie 会话、Admin refresh token、各子系统 OIDC token。一个浏览器一次登录 = 1 个 sid；5 个浏览器 = 5 个 sid |
| 设备（deviceId） | 前端生成并持久化于 localStorage 的 UUID，标识"某浏览器客户端"。仅用于 UX 分级，**不是安全边界** |
| 受信设备 | (userId, deviceId) 有成功登录记录且未被踢 |
| 踢人标记 / logout_token | AS 推给子系统的标准 **OIDC Back-Channel Logout 1.0** `logout_token`（JWT，含 `events` + `sid`/`sub`），子系统本地缓存其 sid 作为吊销标记 |
| 会话等级（L1/L2/L3） | 会话当前的 assurance 级别：L1 已登录、L2 近期密码核验、L3 近期强认证；随时间衰减 |
| deviceInfo | 客户端上报的结构化设备信息（开放 JSON schema），web 与 native 共用契约 |

## 4. 总体架构

存储归属：

| 存储 | 位置 | 内容 |
|------|------|------|
| Redis | 仅主系统 | 活跃会话登记、sid 踢人标记、OTP 限流计数 |
| DB | 仅主系统 | `TrustedDevices` 受信设备表、`RefreshTokens`（加 `Sid` 列）、审计 |
| 进程内存 | 各子系统 | 踢人标记缓存（`ConcurrentDictionary<sid, kickedAt>`，TTL 15 分钟） |

通信：

- AS → 子系统：HTTP POST 签名踢人/登出消息（back-channel），fire-and-forget，失败记日志（重试队列列二期）。
- 浏览器 → 各客户端：front-channel `/logout` 清页（同浏览器统一登出）。

Token 约定：

- access token 寿命 60 → **15 分钟**；refresh 寿命不变（Admin 14 天 / OIDC 30 天）。
- 所有 access / refresh token 增加 **`sid` claim**（OIDC 标准 claim 名），以及事实类 claim：`pva`（密码核验时间）、`saa`（强认证时间）、`amr`（认证方式列表），无对应事实时不携带。
- `AdminRefreshToken` 表增加 `Sid` 列。

## 5. 功能需求

### FR-1 会话生成与登记

- 登录成功（Admin 密码登录、OIDC authorize）生成 sid，写入：Cookie 会话、token `sid` claim、`RefreshTokens.Sid`。
- 会话登记（Redis，key = sid）：userId、deviceId、IP、UA、登录时间、已签发客户端列表（签发时追加）、状态。
- TTL = refresh 寿命，refresh 成功时滑动续期。
- 会话列表 = 登记活跃条目；Redis 丢失时以 DB（refresh 行 + 审计）兜底重建近似列表。

### FR-2 会话管理页（Admin）

- 列表字段：用户、设备（UA 解析）、IP、登录时间、最近活跃、包含的客户端、"当前会话"标记。
- 操作：踢单个 sid；踢某用户全部 sid（= 循环单踢）。
- 权限：`Admin.ManageUser` 策略。全部操作入审计。

### FR-3 踢单个凭证 revoke(sid)

1. Redis 标记 sid revoked（TTL 15 分钟 + 容差）；
2. Cookie 会话校验拒绝该 sid（authorize / 静默续登失败）；
3. Admin 换 token：refresh 行带 sid → 查标记拒签；
4. OIDC 换 token：从 refresh token claim 读 sid → 查标记拒签；
5. 向该 sid 已签发过的全部客户端推送标准 `logout_token`（端点 = 各客户端注册的 `backchannel_logout_uri`）；
6. 写审计。

生效语义：推送命中 → 秒级 401；推送丢失 → 该子系统凭证在 access 自然过期（≤15 分钟）后首次请求死亡，且续期被拒。

### FR-4 子系统接入规范

- 推送格式采用 **OIDC Back-Channel Logout 1.0** 标准，不用自定义 payload：
  - AS 用自身签名密钥签发 `logout_token`（JWT）：`iss`、`sub`、`aud`（= 目标 client_id）、`iat`、`jti`、`events` = `{"http://schemas.openid.net/event/backchannel-logout": {}}`、`sid`。**不含 `nonce`**（规范要求）。
  - POST 到客户端注册的 `backchannel_logout_uri`，表单参数 `logout_token=<JWT>`；客户端返回 200 视为送达，非 2xx 重试（有限次 + 审计），失败不阻塞踢人（有 ≤15 分钟兜底）。
  - 该 URI 在客户端注册记录中新增字段（与现有 `RedirectUris` / `PostLogoutRedirectUris` 同级），Admin 客户端管理页可配。
- 客户端侧实现：验 `logout_token` 签名（JWKS）+ `iss`/`aud`/`exp` + `events` 存在 + 无 `nonce`，用 `jti` 防重放，把 `sid` 写入内存标记（TTL 15 分钟，惰性清理）。
- 请求管线在 JWT 验签后增加一步：`token.sid` 命中标记 → 401，错误码 `SESSION_REVOKED`（自有子系统）/ 标准 `invalid_token`（第三方）。
- 前端：401 且 refresh 失败 → 清本地 token 跳登录（复用现有拦截器）。
- 多副本 caveat：内存标记只覆盖收到推送的进程；子系统未来多副本部署时必须升级自有共享存储或逐副本推送。写入接入文档。

### FR-5 统一登出（用户主动）

- 任一客户端登出 = revoke(当前 sid)，流程同 FR-3（来源标记为 self-logout）。
- front-channel：登出后浏览器依次访问各客户端 `/logout` 页（跳转链或隐藏 iframe），各前端清 localStorage/sessionStorage；清单与 backchannel 清单同一份 AS 配置。
- 效果：本浏览器全部页签退出；其他设备不受影响。

### FR-6 登录分级与会话 assurance 等级

**等级定义**（当前等级 = 满足的最高级）：

- **L1 已登录**：sid 有效，未被踢/未登出。静默 SSO 会话（本次未输入密码）起步 L1。
- **L2 密码核验**：L1 且密码核验事实 `pv` 有效。授予时机：密码登录、密码 step-up。维持条件：活跃间隔 ≤ G2（默认 **1 小时**）；间隔 > G2 → **掉窗**，`pv` 粘性失效（不因下次活动自动恢复），须重新验密码才回 L2。间隔由各服务器自有观测判定（主系统用 Redis 全局活跃（含子系统心跳）；子系统用本地请求）；掉窗粘性：主系统清 Redis pv 事实，子系统内存记 `droppedAt` 并拒绝至 token `pva` > `droppedAt`。
- **L3 近期强认证**：L1 且强认证时间 `saa` 在窗口 S（默认 10 分钟）内。强认证通道可扩展：邮箱 OTP / 短信 / TOTP。step-up 成功后 AS **重发 token**，携带 `saa`、`amr` claim；各服务器按 `now - saa <= S` 判定，子系统无需共享存储。

**等级声明与执行**：

- 控制器/Action 打特性 `[RequireSessionLevel(n)]`；不足返回 401 `{error: stepup_required, current_level, required_level}`，前端触发 step-up、换新 token 重试。
- **放置决策：独立于角色/权限码，但挂在授权管线上**（自定义 AuthorizationRequirement + 特性）。理由：角色是每用户静态、管理员分配、不过期；等级是每会话动态、随时间衰减、系统定义。混入权限码会产生"给用户分配 L3"的语义错误与码爆炸。等级事实不进角色表、不进权限分配 UI。OIDC 语义对齐标准 `acr`/`amr`。
- **对第三方暴露等级用标准 `acr` claim**（可选读取，不强制）：`urn:mengtu:acr:l1|l2|l3`；同时给 `amr`（`pwd` / `otp` / `sms` / `totp`）与 `auth_time`。第三方若想自己做敏感操作二次确认，读 `acr` 即可，不需要理解我们的 `pva`/`saa`。注意 `acr` 反映**签发时刻**的等级，L2 掉窗/L3 过期后 token 内的 `acr` 不会自动降级——需要即时等级的第三方应调用 `userinfo`（返回实时 `acr`）而不是只看本地 token。

**登录时分级（受信设备）**：

- **deviceId 归属：由 AS 侧承载，不依赖 client 传参**（否则第三方无法接入受信设备分级）：
  - AS 登录页（`adminhost` 域）首次访问生成 UUID，种非 HttpOnly cookie（生产 HTTP，不可 Secure）+ 落 localStorage 兜底；后续 authorize 请求由 AS 自己读该 cookie 得到 deviceId。第三方客户端跳转过来时天然生效，无需任何改造。
  - 自有客户端（Admin 前端、native App）可显式传 `deviceId`/`deviceInfo` 覆盖 cookie 值——native 无浏览器 cookie，必须走这条；覆盖仅接受来自第一方 client_id 的请求（白名单），防止第三方伪造他人 deviceId 蹭受信。
- `TrustedDevices` 表：id、userId、deviceId、DeviceInfoJson、firstSeenUtc、lastSeenUtc、status(trusted/revoked)、revokedReason、revokedAtUtc；唯一索引 (userId, deviceId)。
- 密码校验通过后、签发 sid 前判定：
  - (userId, deviceId) 存在且 trusted → 直接签发 sid（仅密码），密码事实登录时授予（`pva=登录时间`），会话起步 L2（窗内）；
  - 不存在（新设备）或 revoked（被踢设备）→ 428 `needStepUp` + stepupToken；OTP 通过后签发 sid，`pva=登录时间` 且 `saa=now`（起步即 L3），写/更新 TrustedDevices。
- 踢人/踢设备时：对应 (userId, deviceId) 置 revoked → 该设备下次登录必须 step-up。
- 未绑 OTP 通道（如无邮箱）：返回 `needBindEmail`，提示联系管理员绑定。
- **428 只在自有登录 API 出现**：走 OIDC 的客户端（含第三方）不会看到 428——step-up 由 AS 统一登录页在页面内完成（显示 OTP 输入框，验证通过后继续 authorize 重定向）。对 client 而言只是"登录页多花了一步"，协议契约不变。
- 受信不永久：受信设备 90 天无登录 → 登录时惰性降级为需 step-up。
- 安全定位：deviceId 可伪造/可清除，本机制是"提高被踢与新设备登录成本"的 UX 安全层；强边界是密码 + step-up OTP。仅窃得密码的攻击者在新设备上必然命中 step-up。

**会话内 step-up**：已登录会话访问高等级控制器被拒时，前端调 step-up 端点（带当前 sid）：要求 L2 → 验当前密码 → 重发带新 `pva` 的 token；要求 L3 → OTP/短信/TOTP → 重发带 `saa` 的 token；随后重试原请求。

### FR-7 审计

事件：LoginSuccess（含 sid/deviceId/是否 step-up）、StepupSent、StepupSuccess/Fail、Kick（操作者、目标 sid）、Logout、RefreshDenied(reason=revoked)。复用现有审计表与机制。

### FR-8 设备信息采集与活跃心跳

- deviceInfo 开放 JSON schema（加字段不改表），web 与 native 共用契约：

```json
{
  "deviceId": "uuid",
  "platform": "web | windows | android | ios",
  "clientKind": "browser | native",
  "os": "Windows 11 / Android 14 / iOS 18",
  "clientName": "Chrome 126 / MengtuApp 1.2.0",
  "model": "Pixel 8 / iPhone 15（移动/native 可选）",
  "timezone": "Asia/Shanghai",
  "ua": "原始 UA（取证保留）"
}
```

- web 端优先 `navigator.userAgentData`，回退 UA 解析；native 由 SDK 填充。
- 活跃心跳：无专用前端端点。任何认证请求服务端节流更新 sid 的 lastActivity（距上次写 ≥60 秒才写）。主系统直写 Redis；子系统异步 fire-and-forget 上报 `POST /api/identity/activity {sid, at}`（AS 校验 sid 有效未吊销后计入），保证设备页"在线"反映全系统活跃。

### FR-9 用户设备自管理

- 入口：用户下拉 → 个人中心/设备管理（各客户端提供，Admin 先行）。
- `GET /api/me/devices`：本人 TrustedDevices 列表 + 每设备聚合会话；字段：设备摘要（deviceInfo 解析）、最近活跃、**在线 = 任一 sid 15 分钟内活跃**、当前设备标记（与当前 sid 的 deviceId 比对）、status。
- `POST /api/me/devices/{deviceId}/revoke`（踢设备）：吊销该设备全部 sid（流程同 FR-3 循环）+ TrustedDevices 置 revoked（再登录 step-up）+ 推送踢人标记 + 审计。被踢设备即时下线（推送命中）或 ≤15 分钟兜底。
- 管理员全局视图（FR-2 会话管理页）不变；用户仅见本人设备。

### FR-10 第三方标准对接 profile

设计红线：**协议层只做标准，扩展只做附加**。任何第三方 OIDC 客户端（我们无法要求其改代码）接入后应自动获得 SSO、统一登出、秒级踢人；反之，自有扩展不得要求 client 传自定义参数或理解自定义 claim。

**10.1 标准性核对**

| 能力 | 依据规范 | 第三方是否需定制 |
|------|---------|----------------|
| 授权码 + PKCE 登录 | OIDC Core / RFC 6749 / RFC 7636 | 否 |
| discovery + JWKS 验签 | OIDC Discovery 1.0 | 否 |
| `/connect/authorize`、`/connect/token`、`/connect/userinfo`、`/connect/endsession` | OIDC Core / RP-Initiated Logout 1.0 | 否 |
| refresh token 轮换 | OAuth 2.0 | 否 |
| `sid`、`amr`、`acr`、`auth_time` claim | OIDC Core 标准 claim | 否（不认识可忽略） |
| 踢人 / 登出推送 | **OIDC Back-Channel Logout 1.0** | 否（可选实现，见 10.2） |
| 浏览器内清页 | Front-Channel Logout 1.0 | 否（可选实现） |
| `pva`、`saa` claim | **自定义（非标准）** | 忽略即可，不影响验签 |
| `[RequireSessionLevel(n)]` | **自定义（服务端内部）** | 不可见，不强制 |
| 428 needStepUp / stepupToken | **自定义（仅自有登录 API）** | 不可见：step-up 在 AS 登录页内完成 |
| deviceId / deviceInfo | **自定义** | 无需传：AS 侧 cookie 承载（见 FR-6） |
| `/api/identity/activity` 心跳 | **自定义** | 可选。不报只影响主系统设备页"在线"精度 |

**10.2 两种 profile**

- **标准 profile（第三方，必须项）**
  1. 向管理员申请 client 注册：`client_id` + 凭据（机密客户端用 client_secret；公共客户端用 PKCE）、`redirect_uris` 白名单、`post_logout_redirect_uris` 白名单、可选 `backchannel_logout_uri`。
  2. 从 discovery 文档取端点，用 JWKS 验签 ID/access token，校验 `iss`、`aud`、`exp`、`nonce`。
  3. 实现 refresh token 轮换（旧 refresh 用过即废，收到 `invalid_grant` 必须清态跳登录）。
  4. 登出：跳 `/connect/endsession?id_token_hint=&post_logout_redirect_uri=`。
  5. 可选（推荐）：实现 `backchannel_logout_uri` 收 `logout_token` → 获得秒级踢人；不实现则退化为 ≤15 分钟 + 30 秒兜底，功能不受影响。
  6. 可选：读 `userinfo` 的实时 `acr` 自行做敏感操作二次确认。
  - 得到：SSO、统一登出、踢人生效（秒级或 15 分钟兜底）、审计留痕。
  - 得不到：主系统侧的等级强制（L2/L3）、设备管理页、活跃心跳（除非实现 10.4）。

- **增强 profile（自有子系统，额外项）**
  - 全部标准 profile 项，**外加**：`[RequireSessionLevel]` 等价的本地等级执行（L2 本地活跃观测 + 掉窗粘性、L3 校验 `saa` 窗口）、`SESSION_REVOKED` 专有错误码、心跳上报、设备管理页（可 iframe/跳转嵌入主系统页面）、front-channel `/logout` 清页。

**10.3 主系统需为标准对接提供的配套**

- 客户端注册管理页新增 `backchannel_logout_uri` 字段与校验（必须与已注册 redirect 同 host，或人工审批）。
- discovery 文档暴露 `backchannel_logout_supported: true`、`frontchannel_logout_supported: true`、`scopes_supported`、`acr_values_supported`。
- 一份对外《第三方 OIDC 接入指引》：标准端点、示例代码（含 logout_token 校验）、HTTP 环境说明、白名单审批流程、故障排查（`invalid_grant` / 时钟偏移）。

**10.4 HTTP 环境的已知偏差（必须写进对外文档）**

- OIDC/OAuth 规范要求 redirect_uri 使用 HTTPS（`http://localhost` 例外）。本项目生产为全 HTTP，属**主动接受的偏差**：OpenIddict 需在非 Development 环境显式放开 HTTP 回调，否则第三方注册即失败。
- 因无 HTTPS：ID/access token 在传输中可被嗅探，`logout_token` 推送同样明文。缓解措施仅限缩短 token 寿命（15 分钟）与内网部署。对外文档必须明示该风险，第三方接入需走审批而非自助。
- Cookie 无法 Secure/SameSite=None，跨站 front-channel 登出依赖跳转而非隐藏 iframe（iframe 在第三方 Cookie 拦截下不可靠）。

## 6. 接口清单（草案）

主系统：

- `POST /api/admin/auth/login {userName, password, deviceId, deviceInfo}` → 200 tokens | 428 {needStepUp, stepupToken}
- `POST /api/admin/auth/stepup/send-code {stepupToken|sid, channel}`（OTP 类通道：email_otp/sms/totp）
- `POST /api/admin/auth/stepup/verify {stepupToken|sid, channel: password|email_otp|sms|totp, credential, deviceId}` → 通过后签发/重发 token：password 通道带新 `pva`；强认证通道带 `saa` + `amr`；均带 `sid`
- `GET /api/admin/sessions?userId=&includeRevoked=`
- `POST /api/admin/sessions/{sid}/revoke`
- `POST /api/admin/users/{id}/sessions/revoke-all`
- `GET /api/me/devices`、`POST /api/me/devices/{deviceId}/revoke`
- `POST /api/identity/activity {sid, at}`（子系统心跳上报）
- OIDC（全部标准端点，第三方直接可用）：`/.well-known/openid-configuration`、`/.well-known/jwks`、`/connect/authorize`、`/connect/token`、`/connect/userinfo`、`/connect/endsession`；签发 claim 含标准 `sid`/`amr`/`acr`/`auth_time` 与自定义 `pva`/`saa`；refresh 校验 sid 吊销标记；endsession = revoke(当前 sid) 并按标准 Back-Channel/Front-Channel Logout 通知其他客户端
- 客户端注册（管理员）：现有 client 管理接口新增 `backchannel_logout_uri` 字段（见 FR-10.3）

子系统（每家实现）：

- 标准 profile：`POST {backchannel_logout_uri}` 收 `logout_token`（FR-4 / FR-10.2）
- 增强 profile 额外：`GET /logout`（front-channel 清页）、请求管线 sid 标记检查 + `[RequireSessionLevel]` 等级执行（L2 本地观测、L3 验 `saa` claim）、认证请求节流上报心跳至 `POST /api/identity/activity`

前端公共：

- deviceId 生成/持久化 + deviceInfo 采集上报；登录表单 step-up 步骤（OTP 输入）；`SESSION_REVOKED` 与 `stepup_required` 401 处理（后者触发会话内 step-up 后重试）；设备管理页；焦点校验（二期可选）。

## 7. 非功能需求

- 生效上界：踢 → 子系统不可操作 ≤ 15 分钟 + 30 秒；通常秒级（推送命中）。
- 性能：子系统标记检查 = 内存字典 O(1)；主系统 Redis 读仅发生在登录/refresh/authorize/踢人决策点；Admin 自家 API 可每请求查自家 Redis（同机亚毫秒）。
- 降级：
  - Redis 宕：踢人 API 报错；存量会话不受影响；登录分级走 DB TrustedDevices 仍可用；推送不依赖 Redis。
  - 子系统宕错过推送：按生效上界兜底。
- 安全：推送与 stepupToken 均验签/防重放（jti + 5 分钟窗）；OTP 明文不落库、日志脱敏；stepupToken 一次性、10 分钟有效。
- 兼容：paycardhub 现有 OIDC 流程除 sid claim 与 backchannel 端点外不变；Admin 前端登录加 deviceId 与 step-up 步骤。

## 8. 分期

- **一期（核心闭环 + 分级）**：sid claim + 会话登记 + 踢人/踢设备 + **标准 Back-Channel Logout 推送（logout_token）** + 子系统中间件 + 管理员会话管理页 + FR-6 受信设备与 step-up OTP（含 AS 侧 deviceId cookie）+ 会话等级模型与 `[RequireSessionLevel]` + access 15 分钟 + 主系统活跃直写 + 客户端注册新增 `backchannel_logout_uri` 字段。
  - 一期即按标准格式实现推送的理由：自定义 payload 日后改标准需第三方返工；标准格式成本相同。
- **二期（自管理与体验）**：FR-9 用户设备自管理页 + 子系统心跳上报（FR-8）+ front-channel 统一登出 + 焦点校验 + native SDK 契约（Android/iOS）+ **对外《第三方 OIDC 接入指引》文档与示例**（有第三方接入需求时应提前）。
- 分期可调：FR-9 可提前至一期（心跳未上时"在线"仅反映主系统活跃，需在 UI 注明）。

## 9. 验收标准（关键用例）

1. 5 个浏览器登录 → 会话列表 5 条（同浏览器内主+子合并为 1 条）。
2. 踢 #3 → #3 的主与子秒级 401 跳登录；其余 4 个不受影响。
3. 停某子系统后踢人 → 恢复后该子系统凭证 ≤15 分钟内首次请求死亡。
4. 被踢设备重登 → 需 step-up；通过后为新 sid，列表可见。
5. 新设备首登 → step-up；二次登录 → 仅密码。
6. 一个浏览器登出 → 该浏览器主+子全部页签退出；其他设备不受影响。
7. 密码泄露攻击者新设备登录 → 被 step-up 挡住。
8. Redis 宕 → 登录/踢人按降级语义行为。
9. 篡改推送 → 子系统拒收并记日志。
10. 受信设备会话（L2）访问 L3 控制器 → 401 `stepup_required` → OTP 换带 `saa` 新 token 重试成功；S 窗口过后再访问 → 再次要求 step-up。
11. 静默 SSO 会话（L1）访问 L2 控制器 → 要求密码 step-up，验过通过；密码核验会话闲置 > 1 小时后首个请求 → 掉窗回 L1，须重新验密码（不因活动自动恢复）。
12. 设备管理页在线标记以 15 分钟活跃为准；踢自己设备 → 该设备下线且再登录需 step-up；其他设备不受影响。
13. 仅在子系统活跃（未触达主系统）→ 设备页仍显示在线（心跳上报生效）。
14. native 客户端（Android/iOS）接入后 deviceInfo 字段完整展示（二期）。
15. **纯标准第三方客户端**（不改任何代码、仅注册 client + 配 redirect/post_logout/backchannel URI）完成登录、拿 token、验签通过、userinfo 取到 `sid`/`acr`/`amr`；主系统踢该 sid → 第三方收到 `logout_token` 并秒级失效。
16. 第三方客户端**不实现** `backchannel_logout_uri` → 登录/登出/refresh 全部正常，踢人在 ≤15 分钟 + 30 秒内生效（不报错、不阻塞）。
17. 第三方客户端走 `/connect/authorize` 且其设备为受信设备 → 仅密码即登录成功（deviceId 由 AS cookie 识别，第三方未传任何自定义参数）；换到新浏览器 → AS 登录页内部要求 OTP，第三方侧无感知（不出现 428）。

## 10. 风险与未决

- OpenIddict 注入 sid claim 与 refresh 拒签钩子的实现点需技术验证（一期 spike）。
- **OpenIddict 的 Back-Channel Logout 支持需验证**：OpenIddict 原生实现了 RP-Initiated Logout，但作为 AS **主动发送** `logout_token` 需自行实现（签发 JWT + POST + 重试队列）。一期 spike 内容之一。
- **HTTP redirect_uri 放开**：OpenIddict 在非 Development 环境默认拒绝非 HTTPS 回调，需显式配置；同时 `logout_token` 明文 POST 存在被截获风险（攻击者截获后可伪造该 sid 已吊销 → 造成子系统拒绝服务）。缓解：仅向内网/审批过的 URI 推送 + 记录推送失败告警。
- 子系统多副本部署模型（当前均单实例）。
- 未绑邮箱用户的 step-up 通道缺失 → 运营流程兜底（管理员绑邮箱）。
- 全 HTTP 生产：弃用跨站 Cookie 方案；deviceId 改由 **AS 侧（adminhost 域）cookie + localStorage 兜底**承载，第三方无需传参（FR-6）。自有客户端覆盖 deviceId 仅限第一方 client_id 白名单，防伪造蹭受信。
- 时钟偏差统一 30 秒容差。
- 闲置页签视觉在线问题：二期焦点校验解决，需产品确认接受度。
- L2 判定口径：主系统用 Redis 全局活跃（含心跳），子系统用本服务器观测；窗口 G2=1 小时，掉窗粘性（主系统清 pv 事实 / 子系统记 droppedAt 比对 `pva`），允许跨服务器轻微差异，已写入 FR-6。
- `saa` 随 refresh token 复制到新 access token，但窗口校验 `now - saa <= S` 恒成立，不存在过期强认证复活问题。
- native SDK 的 deviceId/deviceInfo 契约与推送接入规范二期定义；一期仅 web。

## 附录 A：现状代码索引

- AdminIdentity：`Models/Backoffice/AdminIdentity/*`
- RefreshToken 实体：`Providers/Infrastructure.Sql.Admin/Identity/AdminRefreshToken.cs`
- OIDC 服务端：Admin.Host 内 OidcServer；paycardhub 前端 `UI/apps/paycardhub/src/api/core/auth.ts`
- OIDC 客户端注册模型（需加 `backchannel_logout_uri`）：`Models/Backoffice/OidcServer/OidcServer.App/OidcServerModels.cs`、`.../OidcServer.Infra/OidcServerStore.cs`、`.../OidcServerBootstrapService.cs`
- 登出端点：`Models/Backoffice/OidcServer/OidcServer.Api/Controllers/EndSessionController.cs`
- 审计：`Gateway_AuditLogs` / `UserAuditLogs`
- 前端刷新队列：`@vben` request client（isRefreshing + refreshTokenQueue）

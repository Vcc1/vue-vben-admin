'use strict';

const state = { user: null, csrf: null, view: 'tasks', catalog: { databases: [], handlers: [] }, logTimer: null };
const $ = selector => document.querySelector(selector);
const content = $('#content');
const PASSWORD_RULE_MESSAGE = '密码至少 10 位，并且必须包含大写字母、小写字母和数字。';

async function request(url, options = {}) {
  const method = (options.method || 'GET').toUpperCase();
  const headers = new Headers(options.headers || {});
  if (options.body && typeof options.body !== 'string') {
    headers.set('Content-Type', 'application/json');
    options.body = JSON.stringify(options.body);
  }
  if (!['GET', 'HEAD'].includes(method) && state.csrf) headers.set('X-CSRF-TOKEN', state.csrf);
  let response;
  try {
    response = await fetch(url, { credentials: 'same-origin', ...options, method, headers });
  } catch {
    const message = window.location.protocol === 'file:'
      ? '不能直接打开 HTML 文件，请启动 Admin.Host 后通过服务地址访问 /admin/。'
      : '无法连接 Admin.Host API，请确认服务已启动且当前访问地址和 API 属于同一服务。';
    throw new Error(message);
  }
  if (response.status === 401) {
    if (url === '/api/admin/auth/login') throw new Error('账号或密码错误');
    showLogin();
    throw new Error('登录已失效');
  }
  if (!response.ok) {
    let message = `请求失败（HTTP ${response.status}）`;
    try { const body = await response.json(); message = body.detail || body.title || message; } catch { }
    throw new Error(message);
  }
  if (response.status === 204) return null;
  return response.json();
}

function escapeHtml(value) {
  return String(value ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[ch]);
}

function localTime(value) { return value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '-'; }
function maskEmail(email) {
  if (!email) return '-';
  const at = email.indexOf('@');
  if (at < 0) return email;
  const local = email.substring(0, at);
  const domain = email.substring(at);
  return (local.length <= 3 ? local : local.substring(0, 3)) + '***' + domain;
}
function maskPhone(phone) {
  if (!phone) return '-';
  const digits = phone.replace(/\D/g, '');
  if (digits.length <= 5) return phone;
  const head = digits.substring(0, 3);
  const tail = digits.substring(digits.length - 3);
  return head + '*****' + tail;
}
function passwordRuleError(password) {
  return password.length >= 10 && /\p{Lu}/u.test(password) && /\p{Ll}/u.test(password) && /\p{Nd}/u.test(password)
    ? null
    : PASSWORD_RULE_MESSAGE;
}
function isAdmin() { return state.user?.roles?.includes('Admin'); }
function canOperate() { return isAdmin() || state.user?.roles?.includes('Operator'); }
function badge(status) {
  const tone = ['Succeeded', 'Enabled'].includes(status) ? 'ok' : ['Failed', 'TimedOut', 'Cancelled', 'Disabled'].includes(status) ? 'bad' : 'warn';
  return `<span class="badge ${tone}">${escapeHtml(status)}</span>`;
}

function showNotice(message, error = false) {
  const notice = $('#notice');
  notice.textContent = message;
  notice.className = `notice${error ? ' error' : ''}`;
  notice.hidden = false;
  window.setTimeout(() => { notice.hidden = true; }, 4500);
}

function showLogin() {
  clearInterval(state.logTimer);
  state.user = null;
  state.csrf = null;
  $('#appView').hidden = true;
  $('#loginView').hidden = false;
}

async function initialize() {
  try {
    state.user = await request('/api/admin/auth/me');
    const token = await request('/api/admin/auth/antiforgery');
    state.csrf = token.token;
    state.catalog = await request('/api/admin/scheduled-tasks/catalog');
    showApp();
  } catch { showLogin(); }
}

function showApp() {
  $('#loginView').hidden = true;
  $('#appView').hidden = false;
  $('#accountName').textContent = state.user.name || state.user.displayName || state.user.userName;
  $('#accountRoles').textContent = state.user.roles.join(', ');
  const displayName = state.user.name || state.user.displayName || state.user.userName;
  $('#topbarName').textContent = displayName;
  $('#topbarAvatar').textContent = (displayName || '?').charAt(0).toUpperCase();
  document.querySelectorAll('.admin-only').forEach(x => x.hidden = !isAdmin());
  populateCatalog();
  navigate('tasks');
}

function populateCatalog() {
  $('#sqlDatabase').innerHTML = state.catalog.databases.map(x => `<option value="${escapeHtml(x.key)}">${escapeHtml(x.key)}${x.hasExecutionConnection ? '' : '（未配置执行连接）'}</option>`).join('');
  $('#programHandler').innerHTML = state.catalog.handlers.map(x => `<option value="${escapeHtml(x.key)}">${escapeHtml(x.displayName)} (${escapeHtml(x.key)})</option>`).join('');
}

async function navigate(view) {
  clearInterval(state.logTimer);
  state.view = view;
  document.querySelectorAll('.nav-item').forEach(x => x.classList.toggle('active', x.dataset.view === view));
  const definitions = {
    tasks: ['定时任务', '配置与控制调度任务'],
    runs: ['执行历史', '查看状态、重试与 RunId 日志'],
    users: ['用户管理', 'Identity 用户与角色'],
    apiKeys: ['API Key', '服务调用凭据'],
    oidcClients: ['OIDC 应用', 'OAuth2/OIDC 客户端应用管理'],
    oidcScopes: ['权限范围', '管理可用的 Scope']
  };
  $('#pageTitle').textContent = definitions[view][0];
  $('#pageMeta').textContent = definitions[view][1];
  $('#pageActions').innerHTML = '';
  content.innerHTML = '<div class="empty">加载中</div>';
  try {
    if (view === 'tasks') await renderTasks();
    if (view === 'runs') await renderRuns();
    if (view === 'users') await renderUsers();
    if (view === 'apiKeys') await renderApiKeys();
    if (view === 'oidcClients') await renderOidcClients();
    if (view === 'oidcScopes') await renderOidcScopes();
  } catch (error) { content.innerHTML = `<div class="empty">${escapeHtml(error.message)}</div>`; }
}

async function renderTasks() {
  const tasks = await request('/api/admin/scheduled-tasks');
  if (isAdmin()) $('#pageActions').innerHTML = '<button id="newTaskButton" class="primary">新建任务</button>';
  content.innerHTML = tasks.length ? `<div class="table-frame"><table><thead><tr><th style="width:22%">名称</th><th>类型</th><th style="width:19%">Cron / 时区</th><th>状态</th><th style="width:17%">更新</th><th style="width:28%">操作</th></tr></thead><tbody>${tasks.map(task => `
    <tr><td><strong>${escapeHtml(task.name)}</strong><br><span class="muted">${escapeHtml(task.description || '')}</span></td><td>${escapeHtml(task.taskType)}</td><td>${escapeHtml(task.cronExpression)}<br>${escapeHtml(task.timeZoneId)}</td><td>${badge(task.isEnabled ? 'Enabled' : 'Disabled')}</td><td>${escapeHtml(task.updatedBy)}<br>${localTime(task.updatedAtUtc)}</td><td><div class="actions">
      ${isAdmin() ? `<button data-action="edit" data-id="${task.id}">编辑</button><button data-action="toggle" data-id="${task.id}">${task.isEnabled ? '停用' : '启用'}</button>` : ''}
      ${canOperate() ? `<button data-action="run" data-id="${task.id}">立即执行</button>` : ''}
      <button data-action="runs" data-id="${task.id}">历史</button>${isAdmin() ? `<button data-action="audit" data-id="${task.id}">审计</button><button class="danger" data-action="delete" data-id="${task.id}">删除</button>` : ''}
    </div></td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">暂无定时任务</div>';
  $('#newTaskButton')?.addEventListener('click', () => openTaskDialog());
  content.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', () => taskAction(button.dataset.action, button.dataset.id, tasks)));
}

async function taskAction(action, id, tasks) {
  const task = tasks.find(x => x.id === id);
  try {
    if (action === 'edit') return openTaskDialog(task);
    if (action === 'toggle') await request(`/api/admin/scheduled-tasks/${id}/enabled`, { method: 'PUT', body: { isEnabled: !task.isEnabled, rowVersion: task.rowVersion } });
    if (action === 'run') { const result = await request(`/api/admin/scheduled-tasks/${id}/run`, { method: 'POST' }); showNotice(`已进入队列：${result.runId}`); }
    if (action === 'runs') { state.runTaskId = id; return navigate('runs'); }
    if (action === 'audit') return showAudit(task);
    if (action === 'delete') {
      if (!confirm(`确定删除任务“${task.name}”？`)) return;
      await request(`/api/admin/scheduled-tasks/${id}?rowVersion=${encodeURIComponent(task.rowVersion)}`, { method: 'DELETE' });
    }
    if (['toggle', 'delete'].includes(action)) { showNotice('操作已完成'); await renderTasks(); }
  } catch (error) { showNotice(error.message, true); }
}

function openTaskDialog(task = null) {
  $('#taskDialogTitle').textContent = task ? '编辑任务' : '新建任务';
  $('#taskId').value = task?.id || '';
  $('#taskRowVersion').value = task?.rowVersion || '';
  $('#taskName').value = task?.name || '';
  $('#taskDescription').value = task?.description || '';
  $('#taskType').value = task?.taskType || 'Process';
  $('#taskCron').value = task?.cronExpression || '0 0/5 * * * ?';
  $('#taskTimeZone').value = task?.timeZoneId || 'Asia/Shanghai';
  $('#taskTimeout').value = task?.timeoutSeconds || 300;
  $('#taskRetryCount').value = task?.retryCount || 0;
  $('#taskRetryInterval').value = task?.retryIntervalSeconds || 60;
  $('#taskMisfire').value = task?.misfirePolicy || 'DoNothing';
  $('#taskEnabled').checked = task?.isEnabled || false;
  $('#taskRetryTimeout').checked = task?.retryOnTimeout || false;
  const config = task?.configuration || {};
  $('#processShell').value = config.shell || 'None';
  $('#processExecutable').value = config.executable || '';
  $('#processWorkingDirectory').value = config.workingDirectory || '';
  $('#processArguments').value = JSON.stringify(config.arguments || [], null, 2);
  $('#processEnvironment').value = JSON.stringify(config.environment || {}, null, 2);
  $('#sqlDatabase').value = config.databaseKey || state.catalog.databases[0]?.key || '';
  $('#sqlTimeout').value = config.commandTimeoutSeconds || '';
  $('#sqlCommand').value = config.commandText || '';
  $('#sqlParameters').value = JSON.stringify(config.parameters || {}, null, 2);
  $('#programHandler').value = config.handlerKey || state.catalog.handlers[0]?.key || '';
  $('#programParameters').value = JSON.stringify(config.parameters || {}, null, 2);
  $('#taskFormError').textContent = '';
  updateTaskTypeFields();
  $('#taskDialog').showModal();
}

function updateTaskTypeFields() {
  const type = $('#taskType').value;
  $('#processFields').hidden = type !== 'Process';
  $('#sqlFields').hidden = type !== 'Sql';
  $('#programFields').hidden = type !== 'Program';
}

function parseJson(selector, expected) {
  const value = JSON.parse($(selector).value || (expected === 'array' ? '[]' : '{}'));
  if (expected === 'array' && !Array.isArray(value)) throw new Error(`${selector} 必须是 JSON 数组`);
  if (expected === 'object' && (Array.isArray(value) || value === null || typeof value !== 'object')) throw new Error(`${selector} 必须是 JSON 对象`);
  return value;
}

function taskPayload() {
  const type = $('#taskType').value;
  let configuration;
  if (type === 'Process') configuration = { shell: $('#processShell').value, executable: $('#processExecutable').value.trim(), arguments: parseJson('#processArguments', 'array'), workingDirectory: $('#processWorkingDirectory').value.trim(), environment: parseJson('#processEnvironment', 'object') };
  if (type === 'Sql') configuration = { databaseKey: $('#sqlDatabase').value, commandText: $('#sqlCommand').value, parameters: parseJson('#sqlParameters', 'object'), commandTimeoutSeconds: $('#sqlTimeout').value ? Number($('#sqlTimeout').value) : null };
  if (type === 'Program') configuration = { handlerKey: $('#programHandler').value, parameters: parseJson('#programParameters', 'object') };
  return { name: $('#taskName').value.trim(), description: $('#taskDescription').value.trim() || null, taskType: type, cronExpression: $('#taskCron').value.trim(), timeZoneId: $('#taskTimeZone').value.trim(), misfirePolicy: $('#taskMisfire').value, isEnabled: $('#taskEnabled').checked, timeoutSeconds: Number($('#taskTimeout').value), retryCount: Number($('#taskRetryCount').value), retryIntervalSeconds: Number($('#taskRetryInterval').value), retryOnTimeout: $('#taskRetryTimeout').checked, configuration };
}

async function saveTask(event) {
  event.preventDefault();
  try {
    const id = $('#taskId').value;
    const task = taskPayload();
    if (id) await request(`/api/admin/scheduled-tasks/${id}`, { method: 'PUT', body: { task, rowVersion: $('#taskRowVersion').value } });
    else await request('/api/admin/scheduled-tasks', { method: 'POST', body: task });
    $('#taskDialog').close(); showNotice('任务已保存'); await renderTasks();
  } catch (error) { $('#taskFormError').textContent = error.message; }
}

async function renderRuns() {
  const suffix = state.runTaskId ? `?taskId=${encodeURIComponent(state.runTaskId)}&take=100` : '?take=100';
  const runs = await request(`/api/admin/scheduled-task-runs${suffix}`);
  $('#pageActions').innerHTML = `<button id="refreshRuns">刷新</button>${state.runTaskId ? '<button id="allRuns">全部任务</button>' : ''}`;
  content.innerHTML = runs.length ? `<div class="table-frame"><table><thead><tr><th style="width:19%">RunId</th><th>状态</th><th>触发</th><th>开始 / 结束</th><th>耗时</th><th style="width:22%">操作</th></tr></thead><tbody>${runs.map(run => {
    const duration = run.startedAtUtc && run.finishedAtUtc ? `${Math.max(0, Math.round((new Date(run.finishedAtUtc) - new Date(run.startedAtUtc)) / 1000))} 秒` : '-';
    return `<tr><td><code>${run.id}</code><br>第 ${run.attemptNumber} 次</td><td>${badge(run.status)}</td><td>${escapeHtml(run.triggerType)}<br>${escapeHtml(run.triggeredBy || 'system')}</td><td>${localTime(run.startedAtUtc)}<br>${localTime(run.finishedAtUtc)}</td><td>${duration}</td><td><div class="actions"><button data-run="logs" data-id="${run.id}">日志</button>${canOperate() && ['Queued', 'Running'].includes(run.status) ? `<button data-run="cancel" data-id="${run.id}">取消</button>` : ''}${canOperate() && !['Queued', 'Running'].includes(run.status) ? `<button data-run="retry" data-id="${run.id}">重试</button>` : ''}</div></td></tr>`;
  }).join('')}</tbody></table></div>` : '<div class="empty">暂无执行记录</div>';
  $('#refreshRuns').addEventListener('click', renderRuns);
  $('#allRuns')?.addEventListener('click', () => { state.runTaskId = null; renderRuns(); });
  content.querySelectorAll('[data-run]').forEach(button => button.addEventListener('click', () => runAction(button.dataset.run, button.dataset.id)));
}

async function runAction(action, id) {
  try {
    if (action === 'logs') return showLogs(id);
    if (action === 'cancel') await request(`/api/admin/scheduled-task-runs/${id}/cancel`, { method: 'POST' });
    if (action === 'retry') { const result = await request(`/api/admin/scheduled-task-runs/${id}/retry`, { method: 'POST' }); showNotice(`已进入队列：${result.runId}`); }
    await renderRuns();
  } catch (error) { showNotice(error.message, true); }
}

async function showLogs(runId) {
  clearInterval(state.logTimer);
  $('#detailTitle').textContent = `执行日志 ${runId}`;
  $('#detailBody').innerHTML = '<pre id="logLines" class="log-lines"></pre>';
  $('#detailDialog').showModal();
  let sequence = 0;
  const load = async () => {
    try {
      const page = await request(`/api/admin/scheduled-task-runs/${runId}/logs?afterSequence=${sequence}&take=500`);
      const target = $('#logLines');
      page.items.forEach(item => { target.textContent += `${localTime(item.timestampUtc)} [${item.level}] ${item.category}\n${item.message}${item.exception ? `\n${item.exception}` : ''}\n`; });
      sequence = page.nextSequence;
      target.scrollTop = target.scrollHeight;
      if (page.isComplete) clearInterval(state.logTimer);
    } catch (error) { clearInterval(state.logTimer); $('#logLines').textContent += `\n${error.message}`; }
  };
  await load(); state.logTimer = setInterval(load, 2000);
}

async function showAudit(task) {
  const audits = await request(`/api/admin/scheduled-tasks/${task.id}/audits?take=100`);
  $('#detailTitle').textContent = `操作审计 · ${task.name}`;
  $('#detailBody').innerHTML = audits.length ? `<div class="audit-list">${audits.map(item => `<div class="audit-item"><strong>${escapeHtml(item.action)}</strong>${localTime(item.occurredAtUtc)} · ${escapeHtml(item.actorName)} (${escapeHtml(item.authenticationType)})${item.after ? `<pre>${escapeHtml(JSON.stringify(item.after, null, 2))}</pre>` : ''}</div>`).join('')}</div>` : '<div class="empty">暂无审计记录</div>';
  $('#detailDialog').showModal();
}

async function renderUsers() {
  const users = await request('/api/admin/users');
  $('#pageActions').innerHTML = '<button id="newUserButton" class="primary">新建用户</button>';
  content.innerHTML = `<div class="table-frame"><table><thead><tr><th>用户</th><th>角色</th><th>状态</th><th>最近登录</th><th>操作</th></tr></thead><tbody>${users.map(user => `<tr><td><strong>${escapeHtml(user.name)}</strong><br><span class="muted">${escapeHtml(user.displayName)}</span><br><code>${escapeHtml(user.userName)}</code></td><td>${escapeHtml(user.roles.join(', '))}</td><td>${badge(user.isEnabled ? 'Enabled' : 'Disabled')}</td><td>${localTime(user.lastLoginAtUtc)}</td><td><div class="actions"><button data-user="toggle" data-id="${user.id}">${user.isEnabled ? '停用' : '启用'}</button><button data-user="role" data-id="${user.id}">角色</button><button data-user="password" data-id="${user.id}">重置密码</button></div></td></tr>`).join('')}</tbody></table></div>`;
  $('#newUserButton').addEventListener('click', () => { $('#userForm').reset(); $('#userFormError').textContent = ''; $('#userDialog').showModal(); });
  content.querySelectorAll('[data-user]').forEach(button => button.addEventListener('click', () => userAction(button.dataset.user, button.dataset.id, users)));
}

async function userAction(action, id, users) {
  const user = users.find(x => x.id === id);
  try {
    if (action === 'toggle') await request(`/api/admin/users/${id}/enabled`, { method: 'PUT', body: { isEnabled: !user.isEnabled } });
    if (action === 'role') {
      const role = prompt('角色：Admin、Operator 或 Viewer', user.roles[0] || 'Viewer');
      if (!role) return;
      await request(`/api/admin/users/${id}/roles`, { method: 'PUT', body: { roles: [role] } });
    }
    if (action === 'password') {
      $('#passwordForm').reset();
      $('#passwordUserId').value = id;
      $('#passwordDialogTitle').textContent = `修改密码 · ${user.userName}`;
      $('#passwordFormError').textContent = '';
      $('#passwordDialog').showModal();
      return;
    }
    showNotice('用户已更新'); await renderUsers();
  } catch (error) { showNotice(error.message, true); }
}

async function createUser(event) {
  event.preventDefault();
  $('#userFormError').textContent = '';
  const passwordError = passwordRuleError($('#newPassword').value);
  if (passwordError) {
    $('#userFormError').textContent = passwordError;
    return;
  }
  try {
    await request('/api/admin/users', { method: 'POST', body: { userName: $('#newUserName').value.trim(), displayName: $('#newDisplayName').value.trim(), name: $('#newName').value.trim(), email: $('#newEmail').value.trim() || null, password: $('#newPassword').value, roles: [$('#newRole').value] } });
    $('#userDialog').close(); showNotice('用户已创建'); await renderUsers();
  } catch (error) { $('#userFormError').textContent = error.message; }
}

async function submitPasswordReset(event) {
  event.preventDefault();
  const password = $('#resetPassword').value;
  const passwordError = passwordRuleError(password);
  if (passwordError) {
    $('#passwordFormError').textContent = passwordError;
    return;
  }
  if (password !== $('#confirmResetPassword').value) {
    $('#passwordFormError').textContent = '两次输入的密码不一致。';
    return;
  }

  try {
    await request(`/api/admin/users/${$('#passwordUserId').value}/password`, {
      method: 'PUT',
      body: { newPassword: password }
    });
    $('#passwordDialog').close();
    showNotice('密码已修改');
    await renderUsers();
  } catch (error) {
    $('#passwordFormError').textContent = error.message;
  }
}

async function renderApiKeys() {
  const [keys, users] = await Promise.all([request('/api/admin/api-keys'), request('/api/admin/users')]);
  state.keyUsers = users;
  $('#pageActions').innerHTML = '<button id="newKeyButton" class="primary">新建 API Key</button>';
  content.innerHTML = keys.length ? `<div class="table-frame"><table><thead><tr><th>名称</th><th>用户</th><th>前缀</th><th>有效期</th><th>最近使用</th><th>操作</th></tr></thead><tbody>${keys.map(key => `<tr><td>${escapeHtml(key.name)}</td><td>${escapeHtml(users.find(x => x.id === key.userId)?.userName || key.userId)}</td><td><code>${escapeHtml(key.prefix)}</code></td><td>${key.revokedAtUtc ? badge('已撤销') : localTime(key.expiresAtUtc)}</td><td>${localTime(key.lastUsedAtUtc)}</td><td><button class="danger" data-key="${key.id}" ${key.revokedAtUtc ? 'disabled' : ''}>撤销</button></td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">暂无 API Key</div>';
  $('#newKeyButton').addEventListener('click', () => {
    $('#keyForm').reset(); $('#keyFormError').textContent = '';
    $('#keyUser').innerHTML = users.map(x => `<option value="${x.id}">${escapeHtml(x.userName)} · ${escapeHtml(x.displayName)}</option>`).join('');
    $('#keyDialog').showModal();
  });
  content.querySelectorAll('[data-key]').forEach(button => button.addEventListener('click', async () => { try { if (!confirm('确定撤销此 API Key？')) return; await request(`/api/admin/api-keys/${button.dataset.key}`, { method: 'DELETE' }); showNotice('API Key 已撤销'); await renderApiKeys(); } catch (error) { showNotice(error.message, true); } }));
}

async function createKey(event) {
  event.preventDefault();
  try {
    const result = await request('/api/admin/api-keys', { method: 'POST', body: { userId: $('#keyUser').value, name: $('#keyName').value.trim(), expiresAtUtc: $('#keyExpires').value ? new Date($('#keyExpires').value).toISOString() : null } });
    $('#keyDialog').close();
    $('#detailTitle').textContent = 'API Key 已创建';
    $('#detailBody').innerHTML = `<label>仅显示一次<input value="${escapeHtml(result.secret)}" readonly></label>`;
    $('#detailDialog').showModal();
    await renderApiKeys();
  } catch (error) { $('#keyFormError').textContent = error.message; }
}

// --- OIDC Client Management ---

async function renderOidcClients() {
  const clients = await request('/api/admin/oidc/clients');
  $('#pageActions').innerHTML = '<button id="newOidcClientButton" class="primary">新建 OIDC 应用</button>';
  content.innerHTML = clients.length ? `<div class="table-frame"><table><thead><tr><th>Client ID</th><th>名称</th><th>类型</th><th>授权类型</th><th>Scopes</th><th>操作</th></tr></thead><tbody>${clients.map(client => `
    <tr><td><code>${escapeHtml(client.clientId)}</code></td><td>${escapeHtml(client.displayName)}</td><td>${escapeHtml(client.clientType)}</td><td>${escapeHtml(client.grantTypes.join(', ') || '-')}</td><td>${escapeHtml(client.scopes.join(', ') || '-')}</td><td><div class="actions">
      <button data-oidc="edit" data-id="${client.id}">编辑</button>
      <button data-oidc="regenerate" data-id="${client.id}">重置密钥</button>
      <button class="danger" data-oidc="delete" data-id="${client.id}">删除</button>
    </div></td></tr>`).join('')}</tbody></table></div>` : '<div class="empty">暂无 OIDC 应用</div>';
  $('#newOidcClientButton').addEventListener('click', () => openOidcClientDialog());
  content.querySelectorAll('[data-oidc]').forEach(button => button.addEventListener('click', () => oidcClientAction(button.dataset.oidc, button.dataset.id)));
}

async function openOidcClientDialog(client = null) {
  $('#oidcClientDialogTitle').textContent = client ? '编辑 OIDC 应用' : '新建 OIDC 应用';
  $('#oidcClientId').value = client?.id || '';
  $('#oidcClientIdInput').value = client?.clientId || '';
  $('#oidcClientIdInput').disabled = !!client;
  $('#oidcClientDisplayName').value = client?.displayName || '';
  $('#oidcClientType').value = client?.clientType || 'public';
  $('#oidcConsentType').value = client?.consentType || 'simple';
  $('#oidcRedirectUris').value = (client?.redirectUris || []).join('\n');
  $('#oidcPostLogoutUris').value = (client?.postLogoutRedirectUris || []).join('\n');
  $('#oidcClientSecret').value = '';
  $('#oidcClientSecret').parentElement.hidden = client?.clientType === 'public' && !client?.hasSecret;

  // Load scopes and populate checkboxes
  const scopes = await request('/api/admin/oidc/scopes');
  $('#oidcScopeCheckboxes').innerHTML = scopes.map(scope => `
    <label class="check"><input type="checkbox" value="${escapeHtml(scope.name)}" ${client?.scopes?.includes(scope.name) ? 'checked' : ''}> ${escapeHtml(scope.displayName || scope.name)}</label>
  `).join('');

  // Set grant type checkboxes
  document.querySelectorAll('#oidcGrantTypes input[type="checkbox"]').forEach(cb => {
    cb.checked = client?.grantTypes?.includes(cb.value) || false;
  });

  $('#oidcClientFormError').textContent = '';
  $('#oidcClientDialog').showModal();
}

async function saveOidcClient(event) {
  event.preventDefault();
  $('#oidcClientFormError').textContent = '';

  const grantTypes = Array.from(document.querySelectorAll('#oidcGrantTypes input[type="checkbox"]:checked')).map(cb => cb.value);
  const scopes = Array.from(document.querySelectorAll('#oidcScopeCheckboxes input[type="checkbox"]:checked')).map(cb => cb.value);
  const redirectUris = $('#oidcRedirectUris').value.split('\n').map(s => s.trim()).filter(Boolean);
  const postLogoutRedirectUris = $('#oidcPostLogoutUris').value.split('\n').map(s => s.trim()).filter(Boolean);

  const payload = {
    clientId: $('#oidcClientIdInput').value.trim(),
    displayName: $('#oidcClientDisplayName').value.trim(),
    clientType: $('#oidcClientType').value,
    consentType: $('#oidcConsentType').value,
    grantTypes,
    scopes,
    redirectUris,
    postLogoutRedirectUris,
    clientSecret: $('#oidcClientSecret').value || undefined
  };

  try {
    const id = $('#oidcClientId').value;
    if (id) {
      await request(`/api/admin/oidc/clients/${id}`, { method: 'PUT', body: payload });
    } else {
      await request('/api/admin/oidc/clients', { method: 'POST', body: payload });
    }
    $('#oidcClientDialog').close();
    showNotice('OIDC 应用已保存');
    await renderOidcClients();
  } catch (error) {
    $('#oidcClientFormError').textContent = error.message;
  }
}

async function oidcClientAction(action, id) {
  try {
    if (action === 'edit') {
      const clients = await request('/api/admin/oidc/clients');
      const client = clients.find(c => c.id === id);
      return openOidcClientDialog(client);
    }
    if (action === 'regenerate') {
      if (!confirm('确定重置此应用的客户端密钥？')) return;
      const result = await request(`/api/admin/oidc/clients/${id}/regenerate-secret`, { method: 'POST' });
      $('#detailTitle').textContent = '客户端密钥已重置';
      $('#detailBody').innerHTML = `<label>仅显示一次，请妥善保存：<input value="${escapeHtml(result.secret)}" readonly></label>`;
      $('#detailDialog').showModal();
      return;
    }
    if (action === 'delete') {
      if (!confirm('确定删除此 OIDC 应用？')) return;
      await request(`/api/admin/oidc/clients/${id}`, { method: 'DELETE' });
      showNotice('OIDC 应用已删除');
      await renderOidcClients();
    }
  } catch (error) {
    showNotice(error.message, true);
  }
}

// --- OIDC Scope Management ---

async function renderOidcScopes() {
  const scopes = await request('/api/admin/oidc/scopes');
  $('#pageActions').innerHTML = '<button id="newOidcScopeButton" class="primary">新建权限范围</button>';
  content.innerHTML = scopes.length ? `<div class="table-frame"><table><thead><tr><th>Name</th><th>显示名称</th><th>说明</th><th>操作</th></tr></thead><tbody>${scopes.map(scope => `
    <tr><td><code>${escapeHtml(scope.name)}</code></td><td>${escapeHtml(scope.displayName)}</td><td>${escapeHtml(scope.description || '-')}</td><td><button class="danger" data-scope="delete" data-id="${scope.id}">删除</button></td></tr>
  `).join('')}</tbody></table></div>` : '<div class="empty">暂无权限范围</div>';
  $('#newOidcScopeButton').addEventListener('click', () => openOidcScopeDialog());
  content.querySelectorAll('[data-scope]').forEach(button => button.addEventListener('click', async () => {
    if (!confirm('确定删除此权限范围？')) return;
    try {
      await request(`/api/admin/oidc/scopes/${button.dataset.id}`, { method: 'DELETE' });
      showNotice('权限范围已删除');
      await renderOidcScopes();
    } catch (error) { showNotice(error.message, true); }
  }));
}

function openOidcScopeDialog() {
  $('#oidcScopeForm').reset();
  $('#oidcScopeFormError').textContent = '';
  $('#oidcScopeDialog').showModal();
}

async function saveOidcScope(event) {
  event.preventDefault();
  $('#oidcScopeFormError').textContent = '';
  try {
    await request('/api/admin/oidc/scopes', {
      method: 'POST',
      body: {
        name: $('#oidcScopeName').value.trim(),
        displayName: $('#oidcScopeDisplayName').value.trim(),
        description: $('#oidcScopeDescription').value.trim() || null
      }
    });
    $('#oidcScopeDialog').close();
    showNotice('权限范围已创建');
    await renderOidcScopes();
  } catch (error) {
    $('#oidcScopeFormError').textContent = error.message;
  }
}

$('#loginForm').addEventListener('submit', async event => {
  event.preventDefault(); $('#loginError').textContent = '';
  try {
    const result = await request('/api/admin/auth/login', { method: 'POST', body: { userName: $('#loginUserName').value.trim(), password: $('#loginPassword').value, mode: 'cookie', rememberMe: false } });
    state.user = result.user;
    state.csrf = (await request('/api/admin/auth/antiforgery')).token;
    state.catalog = await request('/api/admin/scheduled-tasks/catalog');
    showApp();
  } catch (error) { $('#loginError').textContent = error.message; }
});
$('#logoutButton').addEventListener('click', async () => { try { await request('/api/admin/auth/logout', { method: 'POST' }); } finally { showLogin(); } });

// --- User dropdown menu ---
$('#userMenuButton').addEventListener('click', event => {
  event.stopPropagation();
  $('#userDropdown').hidden = !$('#userDropdown').hidden;
});
document.addEventListener('click', () => { $('#userDropdown').hidden = true; });
$('#userDropdown').addEventListener('click', async event => {
  const btn = event.target.closest('[data-menu]');
  if (!btn) return;
  $('#userDropdown').hidden = true;
  if (btn.dataset.menu === 'profile') openProfileDialog();
  if (btn.dataset.menu === 'logout') { try { await request('/api/admin/auth/logout', { method: 'POST' }); } finally { showLogin(); } }
});

// --- Profile ---
function openProfileDialog() {
  $('#profileLoginName').textContent = state.user.userName;
  $('#profileName').textContent = state.user.name || '-';
  $('#profileEmail').textContent = maskEmail(state.user.email);
  $('#profilePhone').textContent = maskPhone(state.user.phoneNumber);
  $('#profileRoles').textContent = state.user.roles.join(', ');
  $('#profileCreatedAt').textContent = localTime(state.user.createdAtUtc);
  $('#profileDisplayName').value = state.user.displayName || '';
  $('#profileFormError').textContent = '';
  $('#profileDialog').showModal();
}

async function saveProfile(event) {
  event.preventDefault();
  $('#profileFormError').textContent = '';
  try {
    const updated = await request('/api/admin/auth/profile', {
      method: 'PUT',
      body: { displayName: $('#profileDisplayName').value.trim() }
    });
    state.user = updated;
    $('#accountName').textContent = updated.name || updated.displayName || updated.userName;
    const displayName = updated.name || updated.displayName || updated.userName;
    $('#topbarName').textContent = displayName;
    $('#topbarAvatar').textContent = (displayName || '?').charAt(0).toUpperCase();
    $('#profileDialog').close();
    showNotice('个人资料已更新');
  } catch (error) { $('#profileFormError').textContent = error.message; }
}

// --- Change password ---
function openChangePasswordDialog() {
  $('#changePasswordForm').reset();
  $('#changePasswordError').textContent = '';
  $('#changePasswordDialog').showModal();
}

async function submitChangePassword(event) {
  event.preventDefault();
  $('#changePasswordError').textContent = '';
  const newPwd = $('#newPwd').value;
  const pwdError = passwordRuleError(newPwd);
  if (pwdError) { $('#changePasswordError').textContent = pwdError; return; }
  if (newPwd !== $('#confirmNewPwd').value) { $('#changePasswordError').textContent = '两次输入的密码不一致。'; return; }
  try {
    await request('/api/admin/auth/password', {
      method: 'PUT',
      body: { currentPassword: $('#currentPassword').value, newPassword: newPwd }
    });
    $('#changePasswordDialog').close();
    showNotice('密码已修改');
  } catch (error) { $('#changePasswordError').textContent = error.message; }
}
$('#navigation').addEventListener('click', event => { const button = event.target.closest('[data-view]'); if (button) { if (button.dataset.view === 'runs') state.runTaskId = null; navigate(button.dataset.view); } });
$('#taskType').addEventListener('change', updateTaskTypeFields);
$('#taskForm').addEventListener('submit', saveTask);
$('#userForm').addEventListener('submit', createUser);
$('#passwordForm').addEventListener('submit', submitPasswordReset);
$('#keyForm').addEventListener('submit', createKey);
$('#oidcClientForm').addEventListener('submit', saveOidcClient);
$('#oidcScopeForm').addEventListener('submit', saveOidcScope);
$('#profileForm').addEventListener('submit', saveProfile);
$('#changePasswordForm').addEventListener('submit', submitChangePassword);
$('#closeDetail').addEventListener('click', () => { clearInterval(state.logTimer); $('#detailDialog').close(); });
$('#openChangePasswordBtn').addEventListener('click', () => { $('#profileDialog').close(); openChangePasswordDialog(); });
$('#detailDialog').addEventListener('close', () => clearInterval(state.logTimer));
document.querySelectorAll('[data-close-dialog]').forEach(button => button.addEventListener('click', () => button.closest('dialog').close()));

initialize();

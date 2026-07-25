namespace Tests.Host.DebugTools;

internal static class HotelMatchDataPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>酒店匹配数据</title>
    <style>
        :root {
            color-scheme: light;
            --surface: #fff;
            --subtle: #f7f9fb;
            --text: #202936;
            --muted: #687586;
            --line: #dfe5eb;
            --brand: #2468a9;
            --brand-soft: #eaf2fa;
            --success: #257942;
            --success-soft: #eaf6ee;
            --warning: #93620b;
            --warning-soft: #fff6df;
            --danger: #b13a36;
            --danger-soft: #fceceb;
        }

        * { box-sizing: border-box; }
        button, input, select { font: inherit; letter-spacing: 0; }
        button { cursor: pointer; }
        .page {
            min-width: 0;
            min-height: 100%;
            padding: 16px;
            color: var(--text);
            background: #f3f5f7;
            font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
        }
        .filter-bar {
            display: grid;
            grid-template-columns: 100px minmax(130px, 1fr) minmax(130px, 1fr) 130px 130px auto auto;
            gap: 10px;
            align-items: end;
            padding: 14px;
            border: 1px solid var(--line);
            border-radius: 6px;
            background: var(--surface);
        }
        .field { min-width: 0; }
        .field label {
            display: block;
            margin: 0 0 6px;
            color: var(--muted);
            font-size: 12px;
            font-weight: 600;
        }
        .field input, .field select {
            width: 100%;
            height: 36px;
            padding: 0 10px;
            border: 1px solid #cbd4dd;
            border-radius: 5px;
            background: #fff;
            color: var(--text);
            outline: none;
        }
        .field input:focus, .field select:focus {
            border-color: var(--brand);
            box-shadow: 0 0 0 3px rgba(36, 104, 169, .12);
        }
        .command {
            height: 36px;
            padding: 0 15px;
            border: 1px solid #bcc7d2;
            border-radius: 5px;
            background: #fff;
            color: #344253;
            font-weight: 600;
        }
        .command.primary { border-color: var(--brand); background: var(--brand); color: #fff; }
        .command:disabled { cursor: wait; opacity: .62; }
        .table-panel {
            margin-top: 12px;
            border: 1px solid var(--line);
            border-radius: 6px;
            background: var(--surface);
            overflow: hidden;
        }
        .table-toolbar {
            min-height: 46px;
            padding: 0 14px;
            border-bottom: 1px solid var(--line);
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 16px;
        }
        .table-title { margin: 0; font-size: 15px; font-weight: 650; }
        .table-meta { color: var(--muted); font-size: 12px; }
        .table-scroll { width: 100%; overflow-x: auto; }
        table { width: 100%; min-width: 1080px; border-collapse: collapse; table-layout: fixed; }
        th, td {
            padding: 10px 12px;
            border-bottom: 1px solid #e8edf1;
            text-align: left;
            vertical-align: middle;
            font-size: 13px;
            overflow-wrap: anywhere;
        }
        th { background: var(--subtle); color: #596779; font-size: 12px; font-weight: 650; }
        tbody tr:hover { background: #fafcfd; }
        tbody tr:last-child td { border-bottom: 0; }
        .hotel-name { margin-top: 3px; color: var(--muted); font-size: 12px; }
        .hotel-meta {
            margin-top: 4px;
            color: #526173;
            font-size: 11px;
            line-height: 1.5;
        }
        .code { color: #26384b; font-family: Consolas, monospace; }
        .muted { color: var(--muted); }
        .badge {
            display: inline-flex;
            min-height: 24px;
            padding: 3px 8px;
            border-radius: 4px;
            align-items: center;
            font-size: 12px;
            font-weight: 650;
            white-space: nowrap;
        }
        .badge.success { background: var(--success-soft); color: var(--success); }
        .badge.warning { background: var(--warning-soft); color: var(--warning); }
        .badge.danger { background: var(--danger-soft); color: var(--danger); }
        .badge.neutral { background: #edf1f4; color: #596779; }
        .badge.info { background: var(--brand-soft); color: var(--brand); }
        .link-button {
            border: 0;
            padding: 4px 0;
            background: transparent;
            color: var(--brand);
            font-weight: 650;
        }
        .pagination {
            min-height: 48px;
            padding: 8px 14px;
            border-top: 1px solid var(--line);
            display: flex;
            align-items: center;
            justify-content: flex-end;
            gap: 8px;
        }
        .pagination button, .icon-button {
            width: 34px;
            height: 32px;
            border: 1px solid #cbd4dd;
            border-radius: 5px;
            background: #fff;
            color: #344253;
            font-size: 17px;
        }
        .pagination button:disabled { cursor: default; color: #a8b1bb; background: #f5f7f9; }
        .page-number { min-width: 84px; text-align: center; color: var(--muted); font-size: 12px; }
        .empty { height: 180px; color: var(--muted); text-align: center; }
        .drawer-layer {
            position: fixed;
            inset: 0;
            z-index: 100;
            display: grid;
            grid-template-columns: minmax(0, 1fr) minmax(720px, 980px);
        }
        .drawer-layer[hidden] { display: none; }
        .drawer-backdrop { background: rgba(20, 30, 42, .38); }
        .drawer {
            min-width: 0;
            height: 100vh;
            display: grid;
            grid-template-rows: 58px minmax(0, 1fr);
            background: #fff;
            box-shadow: -12px 0 32px rgba(20, 30, 42, .18);
        }
        .drawer-head {
            padding: 0 18px;
            border-bottom: 1px solid var(--line);
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 12px;
        }
        .drawer-head h2 { margin: 0; font-size: 16px; }
        .icon-button { height: 34px; font-size: 20px; line-height: 1; }
        .drawer-body { min-height: 0; overflow: auto; }
        .detail-section { padding: 16px 18px; border-bottom: 1px solid var(--line); }
        .detail-section:last-child { border-bottom: 0; }
        .detail-section h3 { margin: 0 0 12px; font-size: 14px; font-weight: 680; }
        .summary-grid {
            display: grid;
            grid-template-columns: repeat(4, minmax(0, 1fr));
            border: 1px solid var(--line);
            border-radius: 6px;
            overflow: hidden;
        }
        .summary-item {
            min-width: 0;
            min-height: 76px;
            padding: 12px;
            border-right: 1px solid var(--line);
            background: var(--subtle);
        }
        .summary-item:last-child { border-right: 0; }
        .summary-item span { display: block; margin-bottom: 7px; color: var(--muted); font-size: 12px; }
        .summary-item strong, .summary-item .summary-value {
            display: block;
            min-width: 0;
            font-size: 14px;
            overflow-wrap: anywhere;
        }
        .supplier-strip {
            margin-top: 10px;
            padding: 10px 12px;
            border: 1px solid var(--line);
            border-radius: 6px;
            display: flex;
            flex-wrap: wrap;
            gap: 10px 24px;
            background: #fff;
            color: #435265;
            font-size: 13px;
        }
        .supplier-strip span strong { margin-right: 6px; color: var(--muted); font-size: 12px; }
        .candidate-table {
            max-height: 320px;
            overflow: auto;
            border: 1px solid var(--line);
        }
        .candidate-table table { min-width: 1060px; }
        .candidate-table th { position: sticky; top: 0; z-index: 1; }
        .candidate-table tr.selected { background: var(--brand-soft); }
        .candidate-name {
            min-width: 0;
            display: grid;
            gap: 3px;
        }
        .candidate-name-line {
            display: block;
            min-width: 0;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }
        .candidate-name-line.en { color: var(--muted); font-size: 12px; }
        .evidence-table table { min-width: 900px; }
        .source-hotel { line-height: 1.55; }
        .source-hotel small { display: block; color: var(--muted); }
        .coordinate-panel {
            margin-bottom: 12px;
            padding: 14px;
            border: 1px solid var(--line);
            border-radius: 6px;
            display: grid;
            grid-template-columns: minmax(0, 1fr) 190px minmax(0, 1fr);
            align-items: center;
            gap: 12px;
            background: #fbfcfd;
        }
        .coordinate-point span { display: block; margin-bottom: 5px; color: var(--muted); font-size: 12px; }
        .coordinate-point strong { font-family: Consolas, monospace; font-size: 13px; }
        .distance-line { position: relative; height: 40px; display: grid; place-items: center; }
        .distance-line::before {
            content: "";
            position: absolute;
            left: 8px; right: 8px; top: 20px;
            border-top: 2px solid #97a7b7;
        }
        .distance-line::after {
            content: "";
            position: absolute;
            left: 4px; right: 4px; top: 16px;
            height: 9px;
            border-left: 9px solid var(--brand);
            border-right: 9px solid var(--brand);
        }
        .distance-line strong {
            position: relative;
            z-index: 1;
            padding: 3px 8px;
            background: #fbfcfd;
            color: var(--brand);
            font-size: 13px;
        }
        .error-box {
            padding: 12px;
            border: 1px solid #efc2bf;
            border-radius: 6px;
            background: var(--danger-soft);
            color: var(--danger);
            font-size: 13px;
        }
        details summary { cursor: pointer; color: #435265; font-size: 13px; font-weight: 650; }
        pre {
            max-height: 420px;
            margin: 10px 0 0;
            padding: 12px;
            overflow: auto;
            border: 1px solid var(--line);
            border-radius: 5px;
            background: var(--subtle);
            color: #2c3948;
            font: 12px/1.55 Consolas, monospace;
            white-space: pre-wrap;
            overflow-wrap: anywhere;
        }
        @media (max-width: 1050px) {
            .filter-bar { grid-template-columns: repeat(3, minmax(0, 1fr)); }
            .drawer-layer { grid-template-columns: 1fr minmax(620px, 88vw); }
        }
        @media (max-width: 720px) {
            .page { padding: 10px; }
            .filter-bar { grid-template-columns: 1fr 1fr; }
            .drawer-layer { display: block; }
            .drawer-backdrop { display: none; }
            .drawer { width: 100%; }
            .summary-grid { grid-template-columns: 1fr 1fr; }
            .summary-item:nth-child(2) { border-right: 0; }
            .summary-item:nth-child(-n+2) { border-bottom: 1px solid var(--line); }
            .coordinate-panel { grid-template-columns: 1fr; }
            .distance-line { width: min(240px, 100%); margin: 0 auto; }
        }
    </style>
</head>
<body>
    <main class="page">
        <form class="filter-bar" id="filterForm">
            <div class="field"><label for="supId">SupId</label><input id="supId" type="number" min="1" /></div>
            <div class="field"><label for="supplierHotelCode">SHotelCode</label><input id="supplierHotelCode" autocomplete="off" /></div>
            <div class="field"><label for="mtHotelCode">MTHotelCode</label><input id="mtHotelCode" autocomplete="off" /></div>
            <div class="field"><label for="regionCode">坐标国家代码</label><input id="regionCode" maxlength="8" placeholder="CN" autocomplete="off" /></div>
            <div class="field"><label for="statusFilter">状态</label><select id="statusFilter"></select></div>
            <button class="command primary" id="queryButton" type="submit">查询</button>
            <button class="command" id="resetButton" type="button">重置</button>
        </form>
        <section class="table-panel">
            <div class="table-toolbar">
                <h1 class="table-title" id="pageTitle">酒店匹配数据</h1>
                <div class="table-meta" id="tableMeta">等待查询</div>
            </div>
            <div class="table-scroll">
                <table><thead><tr id="tableHead"></tr></thead><tbody id="tableBody"></tbody></table>
            </div>
            <div class="pagination">
                <button id="previousButton" type="button" title="上一页" aria-label="上一页">‹</button>
                <span class="page-number" id="pageNumber">第 1 页</span>
                <button id="nextButton" type="button" title="下一页" aria-label="下一页">›</button>
            </div>
        </section>
    </main>
    <div class="drawer-layer" id="drawerLayer" hidden>
        <div class="drawer-backdrop" id="drawerBackdrop"></div>
        <aside class="drawer" aria-label="匹配详情">
            <header class="drawer-head">
                <h2 id="drawerTitle">匹配详情</h2>
                <button class="icon-button" id="closeDrawerButton" type="button" title="关闭" aria-label="关闭">×</button>
            </header>
            <div class="drawer-body" id="drawerBody"></div>
        </aside>
    </div>
    <script>
        const path = window.location.pathname.toLowerCase();
        const kind = path.includes('conflict') ? 'conflicts' : path.includes('deliver') ? 'deliveries' : 'results';
        const configs = {
            results: {
                title: '酒店匹配结果',
                statuses: [['', '全部'], ['matched', '已匹配'], ['unmatched', '未匹配']],
                headers: ['更新时间', '供应商酒店', 'MTHotelCode', '匹配结论', '经纬度距离', '名称方式', '证据来源', '操作']
            },
            conflicts: {
                title: '酒店重复候选',
                statuses: [['active', '处理中'], ['resolved', '已解决'], ['', '全部']],
                headers: ['最近冲突', '供应商酒店', '候选 MT 酒店', '候选数', '状态', '版本', '首次冲突', '操作']
            },
            deliveries: {
                title: '酒店匹配投递',
                statuses: [['', '全部'], ['0', '待投递'], ['1', '投递中'], ['2', '成功'], ['3', '失败']],
                headers: ['更新时间', '供应商酒店', 'MTHotelCode', '投递状态', '重试次数', '下次重试', '错误', '操作']
            }
        };
        const config = configs[kind];
        const state = { page: 1, pageSize: 50, total: 0, loading: false, selectedCandidate: 0, detail: null };
        const form = document.querySelector('#filterForm');
        const queryButton = document.querySelector('#queryButton');
        const tableBody = document.querySelector('#tableBody');
        const drawerLayer = document.querySelector('#drawerLayer');

        document.querySelector('#pageTitle').textContent = config.title;
        document.querySelector('#statusFilter').innerHTML = config.statuses
            .map(([value, label]) => `<option value="${escapeHtml(value)}">${escapeHtml(label)}</option>`).join('');
        document.querySelector('#tableHead').innerHTML = config.headers
            .map(label => `<th>${escapeHtml(label)}</th>`).join('');

        form.addEventListener('submit', event => {
            event.preventDefault();
            state.page = 1;
            loadList();
        });
        document.querySelector('#resetButton').addEventListener('click', () => {
            form.reset();
            document.querySelector('#statusFilter').value = config.statuses[0][0];
            state.page = 1;
            loadList();
        });
        document.querySelector('#previousButton').addEventListener('click', () => {
            if (state.page > 1 && !state.loading) { state.page -= 1; loadList(); }
        });
        document.querySelector('#nextButton').addEventListener('click', () => {
            if (state.page * state.pageSize < state.total && !state.loading) { state.page += 1; loadList(); }
        });
        document.querySelector('#closeDrawerButton').addEventListener('click', closeDrawer);
        document.querySelector('#drawerBackdrop').addEventListener('click', closeDrawer);
        document.addEventListener('keydown', event => {
            if (event.key === 'Escape' && !drawerLayer.hidden) closeDrawer();
        });

        async function loadList() {
            state.loading = true;
            queryButton.disabled = true;
            document.querySelector('#tableMeta').textContent = '查询中...';
            tableBody.innerHTML = `<tr><td colspan="${config.headers.length}" class="empty">正在加载...</td></tr>`;
            const params = new URLSearchParams({ page: String(state.page), pageSize: String(state.pageSize) });
            addParam(params, 'supId', document.querySelector('#supId').value);
            addParam(params, 'supplierHotelCode', document.querySelector('#supplierHotelCode').value);
            addParam(params, 'mtHotelCode', document.querySelector('#mtHotelCode').value);
            addParam(params, 'regionCode', document.querySelector('#regionCode').value.toUpperCase());
            addParam(params, 'status', document.querySelector('#statusFilter').value);
            try {
                const response = await fetch(`/api/hotel-match-data/${kind}?${params}`);
                if (!response.ok) throw new Error(`查询失败，HTTP ${response.status}`);
                const data = await response.json();
                state.total = data.total || 0;
                renderRows(data.items || []);
                updatePager();
                document.querySelector('#tableMeta').textContent = `共 ${state.total} 条，当前显示 ${(data.items || []).length} 条`;
            } catch (error) {
                tableBody.innerHTML = `<tr><td colspan="${config.headers.length}" class="empty">${escapeHtml(error.message)}</td></tr>`;
                document.querySelector('#tableMeta').textContent = '查询失败';
            } finally {
                state.loading = false;
                queryButton.disabled = false;
            }
        }

        function renderRows(items) {
            if (!items.length) {
                tableBody.innerHTML = `<tr><td colspan="${config.headers.length}" class="empty">没有符合条件的记录</td></tr>`;
                return;
            }
            tableBody.innerHTML = items.map(item => {
                if (kind === 'results') {
                    return `<tr><td>${formatTime(item.updateTime)}</td><td>${hotelCell(item)}</td>
                        <td class="code">${escapeHtml(item.mtHotelCode || '-')}</td>
                        <td>${item.isMatched ? badge('匹配成功', 'success') : badge('未匹配', 'neutral')}</td>
                        <td>${item.isMatched ? `<strong>${formatDistance(item.distanceMeters)}</strong>` : '-'}</td>
                        <td>${escapeHtml(nameKind(item.nameMatchKind))}</td>
                        <td>OTA ${number(item.otaFactCount)} / 供应商 ${number(item.supplierFactCount)}</td>
                        <td>${detailButton(item.id)}</td></tr>`;
                }
                if (kind === 'conflicts') {
                    const codes = (item.mtHotelCodes || []).map(code => `<span class="code">${escapeHtml(code)}</span>`).join('<br>');
                    return `<tr><td>${formatTime(item.lastConflictTime)}</td><td>${hotelCell(item)}</td>
                        <td>${codes || '-'}</td><td><strong>${number(item.candidateCount)}</strong></td>
                        <td>${item.isActive ? badge('重复候选', 'warning') : badge('已解决', 'neutral')}</td>
                        <td>${number(item.matchingVersion)}</td><td>${formatTime(item.firstConflictTime)}</td>
                        <td>${detailButton(item.id)}</td></tr>`;
                }
                return `<tr><td>${formatTime(item.updateTime)}</td><td>${hotelCell(item)}</td>
                    <td class="code">${escapeHtml(item.mtHotelCode || '-')}</td><td>${deliveryBadge(item.deliveryStatus)}</td>
                    <td>${number(item.retryCount)}</td><td>${formatTime(item.nextRetryTime)}</td>
                    <td class="muted">${escapeHtml(shortText(item.lastError || '-', 42))}</td>
                    <td>${detailButton(item.id)}</td></tr>`;
            }).join('');
            document.querySelectorAll('[data-detail-id]').forEach(button => {
                button.addEventListener('click', () => openDetail(button.dataset.detailId));
            });
        }

        function updatePager() {
            const pages = Math.max(1, Math.ceil(state.total / state.pageSize));
            document.querySelector('#pageNumber').textContent = `第 ${state.page} / ${pages} 页`;
            document.querySelector('#previousButton').disabled = state.page <= 1;
            document.querySelector('#nextButton').disabled = state.page >= pages;
        }

        async function openDetail(id) {
            drawerLayer.hidden = false;
            document.body.style.overflow = 'hidden';
            document.querySelector('#drawerTitle').textContent = `${config.title}详情`;
            document.querySelector('#drawerBody').innerHTML = '<div class="detail-section muted">正在加载详情...</div>';
            try {
                const response = await fetch(`/api/hotel-match-data/${kind}/${encodeURIComponent(id)}/detail`);
                if (!response.ok) throw new Error(`详情加载失败，HTTP ${response.status}`);
                state.detail = await response.json();
                const candidates = state.detail.current?.candidates || [];
                const recordCode = state.detail.record?.mtHotelCode;
                const preferred = candidates.findIndex(candidate => candidate.mtHotelCode === recordCode);
                const matched = candidates.findIndex(candidate => candidate.isMatched);
                state.selectedCandidate = preferred >= 0 ? preferred : Math.max(0, matched);
                renderDetail();
            } catch (error) {
                document.querySelector('#drawerBody').innerHTML =
                    `<div class="detail-section"><div class="error-box">${escapeHtml(error.message)}</div></div>`;
            }
        }

        function closeDrawer() {
            drawerLayer.hidden = true;
            document.body.style.overflow = '';
            state.detail = null;
        }

        function renderDetail() {
            const data = state.detail;
            const record = data.record || {};
            const current = data.current;
            const storedConclusion = kind === 'conflicts'
                ? `${number(record.candidateCount)} 个重复候选`
                : kind === 'deliveries'
                    ? deliveryStatusText(record.deliveryStatus)
                    : record.isMatched ? `匹配 ${record.mtHotelCode}` : '未匹配';
            const currentConclusion = current
                ? current.matchedMTHotelCodes.length === 1
                    ? `匹配 ${current.matchedMTHotelCodes[0]}`
                    : current.matchedMTHotelCodes.length > 1
                        ? `${current.matchedMTHotelCodes.length} 个重复候选`
                        : '未匹配'
                : '-';
            let html = `<section class="detail-section"><div class="summary-grid">
                ${summaryItem('供应商', `SupId ${record.supId ?? '-'} / ${record.supplierHotelCode || '-'}`)}
                ${summaryHotelNameItem(record.supplierHotelName, record.supplierHotelNameEn)}
                ${summaryItem('表内记录', storedConclusion)}
                ${summaryItem('当前重新匹配', currentConclusion)}
            </div><div class="supplier-strip">
                <span><strong>来源国家</strong>${escapeHtml(countryText(record.supplierCountryName, record.supplierCountryCode))}</span>
                <span><strong>坐标国家</strong>${escapeHtml(countryText(record.supplierRegionName, record.supplierRegionCode))}</span>
                <span><strong>星级</strong>${escapeHtml(starText(record.supplierStarLevel))}</span>
                <span><strong>手机号/电话</strong>${escapeHtml(record.supplierPhone || '-')}</span>
            </div></section>`;
            if (data.recallError) {
                html += `<section class="detail-section"><div class="error-box">${escapeHtml(data.recallError)}</div></section>`;
            }
            if (current) {
                html += `<section class="detail-section"><h3>当前召回候选</h3>
                    <div class="table-scroll candidate-table"><table>
                    <thead><tr><th>MTHotelCode</th><th>名称</th><th>坐标国家</th><th>名称匹配</th><th>坐标 / 距离</th><th>事实数量</th><th>结论</th><th>操作</th></tr></thead>
                    <tbody>${renderCandidateRows(current.candidates || [], current.settings)}</tbody>
                    </table></div></section>`;
                if ((current.candidates || []).length) {
                    html += renderCandidateEvidence(current.candidates[state.selectedCandidate]);
                }
            }
            html += `<section class="detail-section"><details><summary>查看表内原始记录</summary>
                <pre id="rawRecord"></pre></details></section>`;
            document.querySelector('#drawerBody').innerHTML = html;
            document.querySelector('#rawRecord').textContent = JSON.stringify(record, null, 2);
            document.querySelectorAll('[data-candidate-index]').forEach(button => {
                button.addEventListener('click', () => {
                    state.selectedCandidate = Number(button.dataset.candidateIndex);
                    renderDetail();
                });
            });
        }

        function renderCandidateRows(candidates, settings) {
            if (!candidates.length) return '<tr><td colspan="8" class="empty">召回范围内没有 OTA 候选</td></tr>';
            return candidates.map((candidate, index) => {
                const selectedClass = index === state.selectedCandidate ? ' class="selected"' : '';
                const reason = (candidate.failedConditions || []).join('；');
                return `<tr${selectedClass}><td class="code">${escapeHtml(candidate.mtHotelCode)}</td>
                    <td>${candidateNameCell(candidate)}</td>
                    <td>${escapeHtml(countryText(candidate.regionName, candidate.regionCode))}</td>
                    <td>${candidate.nameMatched ? badge('通过', 'success') : badge('不通过', 'danger')}</td>
                    <td><strong>${formatDistance(candidate.minimumDistanceMeters)}</strong><br>
                        <span class="muted">阈值 ${number(settings.matchDistanceMeters)}m</span><br>
                        ${candidate.coordinateMatched ? badge('通过', 'success') : badge('不通过', 'danger')}</td>
                    <td>OTA ${number(candidate.otaFactCount)} / 供应商 ${number(candidate.supplierFactCount)}</td>
                    <td>${candidate.isMatched ? badge('匹配成功', 'success') :
                        `${badge('未匹配', 'neutral')}<div class="hotel-name">${escapeHtml(reason || '-')}</div>`}</td>
                    <td><button type="button" class="link-button" data-candidate-index="${index}">查看证据</button></td></tr>`;
            }).join('');
        }

        function candidateNameCell(candidate) {
            return hotelNameLines(candidate.hotelName, candidate.hotelNameEn);
        }

        function hotelNameLines(nameValue, nameEnValue) {
            const name = String(nameValue || '').trim();
            const nameEn = String(nameEnValue || '').trim();
            const primaryName = name || nameEn;
            const showEnglish = name && nameEn && nameEn.toLocaleLowerCase() !== name.toLocaleLowerCase();
            if (!name && !nameEn) return '<span class="muted">-</span>';
            return `<div class="candidate-name">
                <span class="candidate-name-line" title="${escapeHtml(primaryName)}">${escapeHtml(primaryName)}</span>
                ${showEnglish ? `<span class="candidate-name-line en" title="${escapeHtml(nameEn)}">${escapeHtml(nameEn)}</span>` : ''}
            </div>`;
        }

        function renderCandidateEvidence(candidate) {
            if (!candidate) return '';
            const coordinate = (candidate.evidenceRows || []).find(row => row.field === '经纬度');
            const coordinatePanel = coordinate ? `<div class="coordinate-panel">
                <div class="coordinate-point"><span>待匹配供应商酒店</span><strong>${escapeHtml(coordinate.pendingValue || '-')}</strong></div>
                <div class="distance-line"><strong>${formatDistance(coordinate.distanceMeters)}</strong></div>
                <div class="coordinate-point"><span>${escapeHtml(evidenceHotelTitle(coordinate.evidenceHotel))}</span>
                    <strong>${escapeHtml(coordinate.evidenceValue || '-')}</strong></div>
            </div>` : '';
            const evidenceRows = (candidate.evidenceRows || []).map(row => `<tr>
                <td>${row.required ? badge('必要', 'info') : badge('辅助', 'neutral')} ${escapeHtml(row.field)}</td>
                <td>${escapeHtml(row.pendingValue || '-')}</td><td class="source-hotel">${evidenceHotelCell(row.evidenceHotel)}</td>
                <td>${escapeHtml(row.evidenceValue || '-')}</td><td>${escapeHtml(row.comparison || '-')}</td>
                <td>${row.isMatched ? badge('相同', 'success') : badge('不相同', 'danger')}</td></tr>`).join('');
            const facts = (candidate.facts || []).map(fact => `<tr>
                <td>${badge(sourceText(fact.source), fact.source === 'Ota' ? 'info' : 'neutral')}</td>
                <td>${number(fact.hotelId)}</td><td class="code">${escapeHtml(fact.hotelCode || '-')}</td>
                <td>${hotelNameLines(fact.hotelName, fact.hotelNameEn)}</td>
                <td>${escapeHtml(countryText(fact.countryName, fact.countryCode))}</td>
                <td>${escapeHtml(countryText(fact.regionName, fact.regionCode))}</td>
                <td>${escapeHtml(starText(fact.starLevel))}</td>
                <td>${escapeHtml(coordinateText(fact))}</td>
                <td>${escapeHtml((fact.phones || []).join(', ') || '-')}</td></tr>`).join('');
            return `<section class="detail-section"><h3>${escapeHtml(candidate.mtHotelCode)} 参与匹配的酒店事实</h3>
                <div class="table-scroll"><table><thead><tr><th>来源</th><th>HotelId</th><th>酒店编码</th><th>酒店名称（中 / 英）</th><th>来源国家</th><th>坐标国家</th><th>星级</th><th>经纬度</th><th>标准化手机号</th></tr></thead>
                <tbody>${facts || '<tr><td colspan="9">没有事实数据</td></tr>'}</tbody></table></div></section>
                <section class="detail-section"><h3>${escapeHtml(candidate.mtHotelCode)} 字段对应关系</h3>
                ${coordinatePanel}<div class="table-scroll evidence-table"><table>
                <thead><tr><th>字段</th><th>待匹配字段值</th><th>证据来自哪家酒店</th><th>证据字段值</th><th>比较结果</th><th>是否相同</th></tr></thead>
                <tbody>${evidenceRows || '<tr><td colspan="6">没有匹配证据</td></tr>'}</tbody></table></div></section>`;
        }

        function hotelCell(item) {
            return `<span class="code">${escapeHtml(`${item.supId ?? '-'} / ${item.supplierHotelCode || '-'}`)}</span>
                <div class="hotel-name">${hotelNameLines(item.supplierHotelName, item.supplierHotelNameEn)}</div>
                <div class="hotel-meta">来源国家 ${escapeHtml(countryText(item.supplierCountryName, item.supplierCountryCode))}<br>
                    坐标国家 ${escapeHtml(countryText(item.supplierRegionName, item.supplierRegionCode))}
                    · 星级 ${escapeHtml(starText(item.supplierStarLevel))}<br>
                    手机号/电话 ${escapeHtml(item.supplierPhone || '-')}</div>`;
        }
        function detailButton(id) {
            return `<button type="button" class="link-button" data-detail-id="${escapeHtml(String(id))}">查看详情</button>`;
        }
        function evidenceHotelCell(hotel) {
            if (!hotel) return '<span class="muted">没有证据酒店</span>';
            return `${badge(sourceText(hotel.source), hotel.source === 'Ota' ? 'info' : 'neutral')}
                <span class="code"> HotelId=${number(hotel.hotelId)}</span>
                <small>${escapeHtml(hotel.hotelCode || '-')} / MT ${escapeHtml(hotel.mtHotelCode || '-')}</small>
                <small>${escapeHtml(hotel.hotelName || '-')}</small>
                <small>来源国家 ${escapeHtml(countryText(hotel.countryName, hotel.countryCode))}
                    · 坐标国家 ${escapeHtml(countryText(hotel.regionName, hotel.regionCode))}
                    · 星级 ${escapeHtml(starText(hotel.starLevel))}
                    · 手机号 ${escapeHtml((hotel.phones || []).join(', ') || '-')}</small>`;
        }
        function evidenceHotelTitle(hotel) {
            return hotel ? `${sourceText(hotel.source)} HotelId=${hotel.hotelId} / ${hotel.hotelCode || '-'}` : '没有证据酒店';
        }
        function summaryItem(label, value) {
            return `<div class="summary-item"><span>${escapeHtml(label)}</span><strong>${escapeHtml(String(value))}</strong></div>`;
        }
        function summaryHotelNameItem(name, nameEn) {
            return `<div class="summary-item"><span>酒店名称</span><div class="summary-value">${hotelNameLines(name, nameEn)}</div></div>`;
        }
        function badge(label, tone) { return `<span class="badge ${tone}">${escapeHtml(String(label))}</span>`; }
        function deliveryBadge(status) {
            const value = Number(status);
            return value === 2 ? badge('成功', 'success') : value === 3 ? badge('失败', 'danger') :
                value === 1 ? badge('投递中', 'info') : badge('待投递', 'warning');
        }
        function deliveryStatusText(status) {
            return ['待投递', '投递中', '成功', '失败'][Number(status)] || `未知状态 ${status}`;
        }
        function nameKind(value) {
            return { 0: '-', 1: '分词和顺序相同', 2: '分词相同，顺序不同', 3: '去除忽略词后相同' }[Number(value)] || String(value ?? '-');
        }
        function sourceText(value) { return String(value).toLowerCase() === 'ota' ? 'OTA' : '供应商'; }
        function coordinateText(fact) {
            return Number.isFinite(Number(fact.latitude)) && Number.isFinite(Number(fact.longitude))
                ? `${fact.latitude}, ${fact.longitude}` : '-';
        }
        function countryText(name, code) {
            if (name && code) return `${name} (${code})`;
            return name || code || '-';
        }
        function starText(value) {
            const parsed = Number(value);
            return Number.isFinite(parsed) ? `${parsed} 星` : '-';
        }
        function formatDistance(value) {
            const parsed = Number(value);
            return Number.isFinite(parsed) ? `${parsed.toFixed(1)} m` : '-';
        }
        function formatTime(value) {
            if (!value) return '-';
            const date = new Date(value);
            return Number.isNaN(date.getTime()) ? escapeHtml(String(value)) : date.toLocaleString('zh-CN', { hour12: false });
        }
        function shortText(value, maxLength) {
            const text = String(value);
            return text.length > maxLength ? `${text.slice(0, maxLength)}...` : text;
        }
        function number(value) { return value === null || value === undefined ? '-' : escapeHtml(String(value)); }
        function addParam(params, name, value) {
            const normalized = String(value || '').trim();
            if (normalized) params.set(name, normalized);
        }
        function escapeHtml(value) {
            return String(value ?? '').replace(/[&<>'"]/g, char =>
                ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[char]);
        }
        loadList();
    </script>
</body>
</html>
""";
}

namespace Tests.Host.DebugTools;

internal static class HotelMappingRedisSyncPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>酒店 Redis 同步</title>
    <style>
        :root {
            color-scheme: light;
            --surface: #fff;
            --subtle: #f6f8fa;
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
        button { font: inherit; letter-spacing: 0; }
        button { cursor: pointer; }
        .page {
            min-width: 0;
            min-height: 100%;
            padding: 16px;
            color: var(--text);
            background: #f3f5f7;
            font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
        }
        .sync-toolbar {
            min-height: 66px;
            padding: 14px 16px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 16px;
        }
        .sync-heading {
            min-width: 0;
            display: flex;
            align-items: center;
            gap: 12px;
        }
        .sync-heading h1 {
            margin: 0;
            font-size: 17px;
            line-height: 1.3;
            letter-spacing: 0;
        }
        .status-badge {
            min-width: 62px;
            height: 26px;
            padding: 0 9px;
            border-radius: 13px;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            background: #eef1f4;
            color: #596575;
            font-size: 12px;
            font-weight: 650;
        }
        .status-badge.running { background: var(--brand-soft); color: var(--brand); }
        .status-badge.succeeded { background: var(--success-soft); color: var(--success); }
        .status-badge.partial { background: var(--warning-soft); color: var(--warning); }
        .status-badge.failed { background: var(--danger-soft); color: var(--danger); }
        .start-button {
            height: 38px;
            padding: 0 16px;
            border: 1px solid var(--brand);
            border-radius: 5px;
            background: var(--brand);
            color: #fff;
            font-weight: 650;
        }
        .start-button:hover:not(:disabled) { background: #1e5b96; }
        .start-button:disabled { cursor: not-allowed; opacity: .62; }
        .overview {
            padding: 18px 16px 16px;
        }
        .progress-header {
            display: flex;
            justify-content: space-between;
            align-items: baseline;
            gap: 12px;
        }
        .phase-name { font-size: 15px; font-weight: 650; }
        .progress-percent { color: var(--brand); font-size: 22px; font-weight: 700; }
        .progress-track {
            height: 9px;
            margin-top: 12px;
            overflow: hidden;
            border-radius: 5px;
            background: #e7ebef;
        }
        .progress-fill {
            width: 0;
            height: 100%;
            border-radius: inherit;
            background: var(--brand);
            transition: width .35s ease;
        }
        .progress-fill.succeeded { background: var(--success); }
        .progress-fill.partial { background: var(--warning); }
        .progress-fill.failed { background: var(--danger); }
        .stage-list {
            margin: 18px 0 0;
            padding: 0;
            list-style: none;
            display: grid;
            grid-template-columns: repeat(5, minmax(90px, 1fr));
            gap: 8px;
        }
        .stage {
            min-width: 0;
            padding-top: 11px;
            border-top: 2px solid #dfe4e9;
            color: var(--muted);
            font-size: 12px;
        }
        .stage.active { border-color: var(--brand); color: var(--brand); font-weight: 650; }
        .stage.done { border-color: var(--success); color: var(--success); }
        .time-row {
            margin-top: 18px;
            display: grid;
            grid-template-columns: repeat(3, minmax(0, 1fr));
            gap: 12px;
            color: var(--muted);
            font-size: 12px;
        }
        .time-row span { display: block; margin-top: 4px; color: var(--text); font-size: 13px; }
        .metric-grid {
            display: grid;
            grid-template-columns: repeat(6, minmax(120px, 1fr));
            gap: 12px;
        }
        .metric {
            min-height: 92px;
            padding: 14px;
            border: 1px solid var(--line);
            border-radius: 6px;
            background: var(--surface);
        }
        .metric-label { color: var(--muted); font-size: 12px; }
        .metric-value {
            margin-top: 9px;
            overflow-wrap: anywhere;
            font-size: 25px;
            line-height: 1;
            font-weight: 700;
            font-variant-numeric: tabular-nums;
        }
        .metric.add .metric-value { color: var(--success); }
        .metric.update .metric-value { color: var(--warning); }
        .metric.delete .metric-value { color: var(--danger); }
        .pipeline-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 12px;
        }
        .pipeline {
            padding: 16px;
        }
        .pipeline-title {
            display: flex;
            align-items: baseline;
            justify-content: space-between;
            gap: 12px;
            font-size: 14px;
            font-weight: 650;
        }
        .pipeline-count {
            color: var(--muted);
            font-size: 13px;
            font-weight: 500;
            font-variant-numeric: tabular-nums;
        }
        .pipeline .progress-track { height: 7px; margin-top: 11px; }
        .pipeline-detail {
            margin-top: 10px;
            color: var(--muted);
            font-size: 12px;
        }
        .error-panel {
            padding: 13px 16px;
            border-color: #efc4c1 !important;
            background: var(--danger-soft) !important;
            color: var(--danger);
            font-size: 13px;
            overflow-wrap: anywhere;
        }
        .error-panel.warning {
            border-color: #ead49f !important;
            background: var(--warning-soft) !important;
            color: var(--warning);
        }
        [hidden] { display: none !important; }

        @media (max-width: 1100px) {
            .metric-grid { grid-template-columns: repeat(3, minmax(120px, 1fr)); }
        }
        @media (max-width: 720px) {
            .sync-toolbar { align-items: stretch; flex-direction: column; }
            .start-button { width: 100%; }
            .stage-list { grid-template-columns: repeat(3, minmax(80px, 1fr)); }
            .time-row { grid-template-columns: 1fr; }
            .metric-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
            .pipeline-grid { grid-template-columns: 1fr; }
        }
    </style>
</head>
<body>
    <main class="page">
        <section class="sync-toolbar card">
            <div class="sync-heading">
                <h1>酒店 Redis 全量同步</h1>
                <span class="status-badge" id="statusBadge">空闲</span>
            </div>
            <button class="start-button" id="startButton" type="button">开始同步</button>
        </section>

        <section class="overview card">
            <div class="progress-header">
                <span class="phase-name" id="phaseName">等待开始</span>
                <span class="progress-percent" id="overallPercent">0%</span>
            </div>
            <div class="progress-track" role="progressbar" aria-label="总体进度" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0">
                <div class="progress-fill" id="overallProgress"></div>
            </div>
            <ol class="stage-list" id="stageList">
                <li class="stage" data-stage="preparingQueue">准备变更队列</li>
                <li class="stage" data-stage="loadingSource">读取源数据</li>
                <li class="stage" data-stage="readingRedis">读取 Redis</li>
                <li class="stage" data-stage="planning">计算差异</li>
                <li class="stage" data-stage="writingRedis">写入映射及队列</li>
            </ol>
            <div class="time-row">
                <div>开始时间<span id="startedAt">-</span></div>
                <div>完成时间<span id="completedAt">-</span></div>
                <div>运行时长<span id="duration">-</span></div>
            </div>
        </section>

        <section class="metric-grid" aria-label="同步统计">
            <div class="metric"><div class="metric-label">源 MT 酒店</div><div class="metric-value" id="sourceCount">0</div></div>
            <div class="metric"><div class="metric-label">Redis MT 酒店</div><div class="metric-value" id="redisCount">0</div></div>
            <div class="metric add"><div class="metric-label">新增 MT</div><div class="metric-value" id="addedCount">0</div></div>
            <div class="metric update"><div class="metric-label">更新 MT</div><div class="metric-value" id="updatedCount">0</div></div>
            <div class="metric delete"><div class="metric-label">删除 MT</div><div class="metric-value" id="deletedCount">0</div></div>
            <div class="metric"><div class="metric-label">变更 MT 总数</div><div class="metric-value" id="operationCount">0</div></div>
        </section>

        <section class="pipeline-grid">
            <div class="pipeline card">
                <div class="pipeline-title">
                    <span>Redis 写入</span>
                    <span class="pipeline-count" id="redisWriteCount">0 / 0</span>
                </div>
                <div class="progress-track" role="progressbar" aria-label="Redis 写入进度" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0">
                    <div class="progress-fill" id="redisWriteProgress"></div>
                </div>
                <div class="pipeline-detail">每批最多 500 条</div>
            </div>
            <div class="pipeline card">
                <div class="pipeline-title">
                    <span>变更队列写入</span>
                    <span class="pipeline-count" id="queueCount">0 本次入队 / 0 当前队列</span>
                </div>
                <div class="progress-track" role="progressbar" aria-label="变更队列写入进度" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0">
                    <div class="progress-fill" id="queueProgress"></div>
                </div>
                <div class="pipeline-detail">List：HotelMatching:MappingSyncPending，消费端使用 RPOP</div>
            </div>
        </section>

        <section class="error-panel card" id="errorPanel" hidden></section>
    </main>

    <script>
        const startButton = document.querySelector('#startButton');
        const statusBadge = document.querySelector('#statusBadge');
        const phaseName = document.querySelector('#phaseName');
        const overallPercent = document.querySelector('#overallPercent');
        const overallProgress = document.querySelector('#overallProgress');
        const errorPanel = document.querySelector('#errorPanel');
        const stages = [
            'preparingQueue',
            'loadingSource',
            'readingRedis',
            'planning',
            'writingRedis'
        ];
        const phaseText = {
            idle: '等待开始',
            starting: '正在启动',
            preparingQueue: '正在准备变更 List 队列',
            loadingSource: '正在读取 HotelMatchResult',
            readingRedis: '正在读取 Redis 映射',
            planning: '正在计算同步差异',
            writingRedis: '正在原子写入 Redis 映射和变更队列',
            completed: '同步完成',
            failed: '同步失败'
        };
        const stateText = {
            idle: '空闲',
            running: '同步中',
            succeeded: '已完成',
            partial: '部分完成',
            failed: '失败'
        };
        let latestStatus = null;

        startButton.addEventListener('click', startSync);
        refreshStatus();
        window.setInterval(refreshStatus, 1000);

        async function startSync() {
            if (!window.confirm('确认以 HotelMatchResult 全量数据同步 Redis 映射吗？多余 MT 分组将被删除。')) return;
            startButton.disabled = true;
            try {
                const response = await fetch('/api/hotel-mapping-redis-sync', { method: 'POST' });
                const status = await response.json();
                if (!response.ok && response.status !== 409) {
                    throw new Error(status.error || `启动失败，HTTP ${response.status}`);
                }
                render(status);
            } catch (error) {
                showError(error.message);
                startButton.disabled = false;
            }
        }

        async function refreshStatus() {
            try {
                const response = await fetch('/api/hotel-mapping-redis-sync/status', { cache: 'no-store' });
                if (!response.ok) throw new Error(`状态查询失败，HTTP ${response.status}`);
                render(await response.json());
            } catch (error) {
                showError(error.message);
            }
        }

        function render(status) {
            latestStatus = status;
            const running = status.state === 'running';
            const operationCount = status.addedCount + status.updatedCount + status.deletedCount;
            const redisRatio = operationCount > 0
                ? Math.min(1, status.appliedCount / operationCount)
                : status.phase === 'completed' ? 1 : 0;
            const queueRatio = operationCount > 0
                ? Math.min(1, status.queuedCount / operationCount)
                : status.phase === 'completed' ? 1 : 0;
            const overallRatio = overallRatioFor(status, redisRatio, queueRatio);

            statusBadge.className = `status-badge ${status.state}`;
            statusBadge.textContent = stateText[status.state] || status.state;
            phaseName.textContent = phaseText[status.phase] || status.phase;
            startButton.disabled = running;
            startButton.textContent = running ? '同步中...' : '开始同步';

            setProgress(overallProgress, overallRatio, status.state);
            overallPercent.textContent = `${Math.round(overallRatio * 100)}%`;
            overallProgress.parentElement.setAttribute('aria-valuenow', String(Math.round(overallRatio * 100)));
            setProgress(document.querySelector('#redisWriteProgress'), redisRatio, status.state);
            setProgress(document.querySelector('#queueProgress'), queueRatio, status.state);

            setNumber('sourceCount', status.sourceCount);
            setNumber('redisCount', status.redisCount);
            setNumber('addedCount', status.addedCount);
            setNumber('updatedCount', status.updatedCount);
            setNumber('deletedCount', status.deletedCount);
            setNumber('operationCount', operationCount);
            document.querySelector('#redisWriteCount').textContent = `${number(status.appliedCount)} / ${number(operationCount)}`;
            document.querySelector('#queueCount').textContent = `${number(status.queuedCount)} 本次入队 / ${number(status.pendingCount)} 当前队列`;
            document.querySelector('#startedAt').textContent = formatTime(status.startedAt);
            document.querySelector('#completedAt').textContent = formatTime(status.completedAt);
            document.querySelector('#duration').textContent = formatDuration(status.startedAt, status.completedAt);

            renderStages(status);
            if (status.error) showError(status.error);
            else if (status.warning) showError(status.warning, 'warning');
            else {
                errorPanel.hidden = true;
                errorPanel.className = 'error-panel card';
                errorPanel.textContent = '';
            }
        }

        function renderStages(status) {
            const currentIndex = stages.indexOf(status.phase);
            document.querySelectorAll('.stage').forEach((element, index) => {
                const completed = status.phase === 'completed' || currentIndex > index;
                element.classList.toggle('done', completed);
                element.classList.toggle('active', status.state === 'running' && currentIndex === index);
            });
        }

        function overallRatioFor(status, redisRatio, queueRatio) {
            if (status.phase === 'completed') return 1;
            const base = {
                idle: 0,
                starting: .02,
                preparingQueue: .08,
                loadingSource: .16,
                readingRedis: .29,
                planning: .40,
                writingRedis: .45,
                failed: 0
            }[status.phase] || 0;
            if (status.phase === 'writingRedis') return base + Math.min(redisRatio, queueRatio) * .55;
            return base;
        }

        function isAtOrAfter(phase, target) {
            const order = ['starting', ...stages, 'completed'];
            return order.indexOf(phase) >= order.indexOf(target);
        }

        function setProgress(element, ratio, state) {
            const percent = Math.max(0, Math.min(100, Math.round(ratio * 100)));
            element.style.width = `${percent}%`;
            element.className = `progress-fill ${state}`;
            element.parentElement.setAttribute('aria-valuenow', String(percent));
        }

        function setNumber(id, value) {
            document.querySelector(`#${id}`).textContent = number(value);
        }

        function number(value) {
            return Number(value || 0).toLocaleString('zh-CN');
        }

        function formatTime(value) {
            return value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '-';
        }

        function formatDuration(startedAt, completedAt) {
            if (!startedAt) return '-';
            const milliseconds = Math.max(0, (completedAt ? new Date(completedAt) : new Date()) - new Date(startedAt));
            const seconds = Math.floor(milliseconds / 1000);
            const hours = Math.floor(seconds / 3600);
            const minutes = Math.floor((seconds % 3600) / 60);
            const remainingSeconds = seconds % 60;
            return [hours > 0 ? `${hours} 小时` : null, minutes > 0 ? `${minutes} 分` : null, `${remainingSeconds} 秒`]
                .filter(Boolean)
                .join(' ');
        }

        function showError(message, tone = 'error') {
            errorPanel.hidden = false;
            errorPanel.className = tone === 'warning'
                ? 'error-panel warning card'
                : 'error-panel card';
            errorPanel.textContent = message;
        }
    </script>
</body>
</html>
""";
}

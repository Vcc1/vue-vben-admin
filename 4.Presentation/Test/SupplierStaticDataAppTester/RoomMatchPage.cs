namespace SupplierStaticDataApp;

internal static class RoomMatchPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>房型匹配测试</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f4f7fb;
            --panel: rgba(255, 255, 255, 0.86);
            --panel-soft: rgba(248, 250, 252, 0.96);
            --text: #0f172a;
            --muted: #64748b;
            --line: rgba(15, 23, 42, 0.08);
            --brand: #2563eb;
            --brand-soft: rgba(37, 99, 235, 0.12);
            --ok: #047857;
            --danger: #b42318;
            --warning: #b54708;
            --shadow: 0 18px 42px rgba(15, 23, 42, 0.08);
            --blur: saturate(180%) blur(18px);
        }

        * { box-sizing: border-box; }

        html, body {
            margin: 0;
            height: 100%;
            font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
            color: var(--text);
            background:
                radial-gradient(circle at top left, rgba(96, 165, 250, 0.14), transparent 22%),
                linear-gradient(180deg, #f8fafc 0%, #eef2f7 100%);
        }

        body {
            padding: 18px;
            overflow: hidden;
        }

        .page {
            height: calc(100vh - 36px);
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 14px;
            overflow: hidden;
        }

        .card {
            background: var(--panel);
            border: 1px solid var(--line);
            border-radius: 22px;
            box-shadow: var(--shadow);
            backdrop-filter: var(--blur);
            -webkit-backdrop-filter: var(--blur);
        }

        .summary {
            padding: 14px 16px;
            display: grid;
            grid-template-columns: repeat(5, minmax(0, 1fr));
            gap: 10px;
        }

        .metric {
            background: var(--panel-soft);
            border: 1px solid var(--line);
            border-radius: 18px;
            padding: 12px 14px;
            min-height: 80px;
        }

        .metric label {
            display: block;
            color: var(--muted);
            font-size: 12px;
            margin-bottom: 8px;
        }

        .metric strong {
            display: block;
            font-size: 18px;
            line-height: 1.2;
        }

        .metric span {
            display: block;
            margin-top: 6px;
            color: var(--muted);
            font-size: 12px;
            line-height: 1.45;
        }

        .content {
            min-height: 0;
            display: grid;
            grid-template-columns: minmax(380px, 430px) minmax(0, 1fr);
            gap: 14px;
        }

        .panel {
            min-height: 0;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            overflow: hidden;
        }

        .panel-head {
            padding: 16px 18px;
            border-bottom: 1px solid var(--line);
            background: var(--panel-soft);
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 12px;
        }

        .panel-head h2 {
            margin: 0 0 4px;
            font-size: 17px;
        }

        .panel-head p {
            margin: 0;
            color: var(--muted);
            font-size: 12px;
            line-height: 1.5;
        }

        .status {
            flex: 0 0 auto;
            color: var(--muted);
            font-size: 13px;
        }

        .status.error { color: var(--danger); }
        .status.success { color: var(--ok); }

        .panel-body {
            min-height: 0;
            overflow: hidden;
            padding: 16px 18px 18px;
        }

        .input-shell {
            height: 100%;
            display: grid;
            grid-template-rows: minmax(0, 1fr) auto auto;
            gap: 14px;
        }

        .form-scroll {
            min-height: 0;
            overflow: auto;
            padding-right: 4px;
        }

        .form-grid {
            min-height: 100%;
            display: grid;
            grid-template-rows: minmax(240px, 1fr) minmax(240px, 1fr) auto;
            gap: 14px;
        }

        .field {
            min-width: 0;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 8px;
        }

        .field label {
            font-size: 13px;
            font-weight: 600;
        }

        textarea,
        select {
            width: 100%;
            border: 1px solid rgba(15, 23, 42, 0.08);
            border-radius: 14px;
            background: rgba(255, 255, 255, 0.95);
            color: var(--text);
            padding: 12px 14px;
            font: inherit;
            outline: none;
        }

        textarea {
            height: 100%;
            min-height: 240px;
            resize: none;
            line-height: 1.55;
        }

        .select-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 12px;
        }

        .button-row {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 12px;
        }

        button {
            border: 0;
            border-radius: 14px;
            padding: 12px 16px;
            font: inherit;
            font-weight: 600;
            cursor: pointer;
            width: 100%;
        }

        button:disabled {
            opacity: 0.65;
            cursor: wait;
        }

        .primary {
            color: white;
            background: linear-gradient(135deg, var(--brand) 0%, #4f46e5 100%);
        }

        .ghost {
            background: rgba(255, 255, 255, 0.94);
            color: var(--text);
            border: 1px solid rgba(15, 23, 42, 0.08);
        }

        .tab-shell {
            height: 100%;
            min-height: 0;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 14px;
            overflow: hidden;
        }

        .tab-bar {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
        }

        .tab-button,
        .relation-mode {
            width: auto;
            padding: 9px 14px;
            border-radius: 999px;
            border: 1px solid var(--line);
            background: rgba(255, 255, 255, 0.96);
            color: var(--text);
            font-size: 12px;
            font-weight: 700;
        }

        .tab-button.active,
        .relation-mode.active {
            background: var(--brand-soft);
            border-color: rgba(37, 99, 235, 0.18);
            color: var(--brand);
        }

        .tab-panels {
            min-height: 0;
            position: relative;
            overflow: hidden;
        }

        .tab-panel {
            display: none;
            height: 100%;
            min-height: 0;
            overflow: hidden;
        }

        .tab-panel.active {
            display: block;
        }

        .tab-scroll {
            height: 100%;
            min-height: 0;
            overflow: auto;
            padding-right: 4px;
            overscroll-behavior: contain;
        }

        .matcher-list,
        .analysis-grid,
        .history-list,
        .relation-list {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .matcher-card,
        .analysis-card,
        .history-item,
        .relation-card {
            border: 1px solid var(--line);
            border-radius: 18px;
            background: rgba(255, 255, 255, 0.90);
        }

        .matcher-card {
            padding: 14px;
        }

        .matcher-card.ok {
            border-color: rgba(4, 120, 87, 0.24);
            background: rgba(4, 120, 87, 0.04);
        }

        .matcher-card.fail {
            border-color: rgba(180, 35, 24, 0.16);
            background: rgba(180, 35, 24, 0.03);
        }

        .matcher-top {
            display: flex;
            justify-content: space-between;
            gap: 12px;
            align-items: center;
        }

        .matcher-name {
            font-size: 14px;
            font-weight: 700;
        }

        .badge {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            min-width: 74px;
            padding: 6px 10px;
            border-radius: 999px;
            font-size: 12px;
            font-weight: 700;
        }

        .badge.ok { background: rgba(4, 120, 87, 0.12); color: var(--ok); }
        .badge.fail { background: rgba(180, 35, 24, 0.10); color: var(--danger); }

        .matcher-meta {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 10px;
            margin-top: 12px;
        }

        .meta-box {
            border: 1px solid var(--line);
            border-radius: 14px;
            padding: 10px 12px;
            background: rgba(255, 255, 255, 0.96);
        }

        .meta-box label {
            display: block;
            color: var(--muted);
            font-size: 11px;
            margin-bottom: 6px;
        }

        .analysis-grid {
            display: grid;
            grid-template-columns: repeat(2, minmax(0, 1fr));
            gap: 14px;
        }

        .analysis-card header,
        .relation-card header {
            padding: 14px 16px;
            background: var(--panel-soft);
            border-bottom: 1px solid var(--line);
        }

        .analysis-card h3,
        .relation-card h3 {
            margin: 0 0 6px;
            font-size: 15px;
        }

        .analysis-card p,
        .relation-card p {
            margin: 0;
            color: var(--muted);
            font-size: 12px;
            line-height: 1.5;
        }

        .analysis-body {
            padding: 14px 16px 16px;
        }

        .analysis-body p {
            margin: 0 0 10px;
            font-size: 13px;
            line-height: 1.6;
        }

        .category-groups {
            display: grid;
            gap: 10px;
            margin-top: 12px;
        }

        .category-group {
            border: 1px solid var(--line);
            border-radius: 14px;
            background: rgba(248, 250, 252, 0.90);
            padding: 10px 12px;
        }

        .category-head {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 10px;
            margin-bottom: 8px;
        }

        .category-title {
            font-size: 12px;
            font-weight: 700;
        }

        .category-count {
            color: var(--muted);
            font-size: 11px;
        }

        .chip-wrap {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
        }

        .chip {
            display: inline-flex;
            align-items: center;
            gap: 6px;
            padding: 6px 10px;
            border-radius: 999px;
            border: 1px solid var(--line);
            background: rgba(255, 255, 255, 0.96);
            font-size: 12px;
        }

        .chip-stack {
            display: inline-flex;
            flex-direction: column;
            align-items: flex-start;
            gap: 6px;
        }

        .chip-main {
            display: inline-flex;
            align-items: center;
            gap: 6px;
        }

        .chip-tags {
            display: flex;
            flex-wrap: wrap;
            gap: 6px;
        }

        .subtag {
            display: inline-flex;
            align-items: center;
            padding: 2px 7px;
            border-radius: 999px;
            font-size: 10px;
            font-weight: 700;
            line-height: 1.4;
            border: 1px solid transparent;
            background: rgba(148, 163, 184, 0.12);
            color: #475569;
        }

        .subtag.must {
            background: rgba(37, 99, 235, 0.10);
            border-color: rgba(37, 99, 235, 0.16);
            color: #1d4ed8;
        }

        .subtag.positive {
            background: rgba(4, 120, 87, 0.10);
            border-color: rgba(4, 120, 87, 0.16);
            color: var(--ok);
        }

        .subtag.negative {
            background: rgba(180, 35, 24, 0.08);
            border-color: rgba(180, 35, 24, 0.16);
            color: var(--danger);
        }

        .subtag.other,
        .subtag.symbol {
            background: rgba(148, 163, 184, 0.10);
            border-color: rgba(148, 163, 184, 0.16);
            color: #475569;
        }

        .chip.must {
            background: rgba(37, 99, 235, 0.10);
            border-color: rgba(37, 99, 235, 0.16);
            color: #1d4ed8;
        }

        .chip.positive {
            background: rgba(4, 120, 87, 0.10);
            border-color: rgba(4, 120, 87, 0.16);
            color: var(--ok);
        }

        .chip.other {
            background: rgba(245, 158, 11, 0.08);
            border-color: rgba(245, 158, 11, 0.16);
            color: var(--warning);
        }

        .chip.negative {
            background: rgba(180, 35, 24, 0.08);
            border-color: rgba(180, 35, 24, 0.16);
            color: var(--danger);
        }

        .chip.symbol {
            background: rgba(148, 163, 184, 0.10);
            color: #475569;
        }

        .relation-toolbar {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            margin-bottom: 14px;
        }

        .relation-row {
            display: grid;
            grid-template-columns: minmax(0, 1fr) 40px minmax(0, 1fr);
            gap: 10px;
            align-items: stretch;
        }

        .relation-cell {
            border: 1px solid var(--line);
            border-radius: 14px;
            background: rgba(248, 250, 252, 0.92);
            padding: 10px 12px;
            min-height: 74px;
        }

        .relation-cell.empty-cell {
            opacity: 0.62;
        }

        .relation-side {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 8px;
            margin-bottom: 8px;
        }

        .relation-side strong {
            font-size: 12px;
        }

        .relation-side span {
            color: var(--muted);
            font-size: 11px;
        }

        .relation-arrow {
            display: flex;
            align-items: center;
            justify-content: center;
            color: var(--muted);
            font-size: 18px;
        }

        .token-meta {
            margin-top: 8px;
            color: var(--muted);
            font-size: 11px;
            line-height: 1.5;
        }

        .mono {
            font-family: Consolas, "Courier New", monospace;
            font-size: 12px;
            background: #0f172a;
            color: #e2e8f0;
            border-radius: 14px;
            padding: 14px;
            line-height: 1.55;
            white-space: pre-wrap;
            word-break: break-word;
            overflow: auto;
        }

        .history-fab {
            position: fixed;
            right: 24px;
            bottom: 24px;
            width: 54px;
            height: 54px;
            border-radius: 999px;
            border: 0;
            background: linear-gradient(135deg, var(--brand) 0%, #4f46e5 100%);
            color: #fff;
            box-shadow: 0 16px 30px rgba(37, 99, 235, 0.28);
            display: inline-flex;
            align-items: center;
            justify-content: center;
            z-index: 40;
        }

        .history-fab svg {
            width: 22px;
            height: 22px;
        }

        .history-modal {
            position: fixed;
            inset: 0;
            background: rgba(15, 23, 42, 0.26);
            display: none;
            align-items: flex-end;
            justify-content: flex-end;
            padding: 24px;
            z-index: 50;
        }

        .history-modal.open {
            display: flex;
        }

        .history-dialog {
            width: min(420px, calc(100vw - 24px));
            height: min(72vh, 760px);
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            overflow: hidden;
        }

        .history-head {
            padding: 16px 18px;
            border-bottom: 1px solid var(--line);
            background: var(--panel-soft);
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 12px;
        }

        .history-head h3 {
            margin: 0 0 4px;
            font-size: 16px;
        }

        .history-head p {
            margin: 0;
            color: var(--muted);
            font-size: 12px;
        }

        .icon-button {
            width: 36px;
            height: 36px;
            padding: 0;
            border-radius: 12px;
            background: rgba(255, 255, 255, 0.92);
            border: 1px solid var(--line);
            color: var(--text);
            display: inline-flex;
            align-items: center;
            justify-content: center;
        }

        .icon-button svg {
            width: 18px;
            height: 18px;
        }

        .history-scroll {
            min-height: 0;
            overflow: auto;
            padding: 16px 18px 18px;
        }

        .history-item {
            padding: 12px;
            cursor: pointer;
        }

        .history-item strong,
        .history-item span,
        .history-item small {
            display: block;
        }

        .history-item strong {
            font-size: 13px;
            margin-bottom: 4px;
        }

        .history-item span,
        .history-item small,
        .empty {
            color: var(--muted);
            font-size: 12px;
            line-height: 1.55;
        }

        @media (max-width: 1360px) {
            .summary {
                grid-template-columns: repeat(2, minmax(0, 1fr));
            }

            .content {
                grid-template-columns: 1fr;
            }

            .analysis-grid {
                grid-template-columns: 1fr;
            }
        }

        @media (max-width: 768px) {
            body {
                padding: 12px;
            }

            .page {
                height: calc(100vh - 24px);
            }

            .summary,
            .select-grid,
            .button-row {
                grid-template-columns: 1fr;
            }

            .history-modal {
                padding: 12px;
                align-items: flex-end;
                justify-content: stretch;
            }

            .history-dialog {
                width: 100%;
                height: 76vh;
            }
        }
    </style>
</head>
<body>
    <main class="page">
        <section class="card summary">
            <article class="metric">
                <label>最终结论</label>
                <strong id="summary-match">-</strong>
                <span id="summary-explain">提交后显示</span>
            </article>
            <article class="metric">
                <label>最佳算法</label>
                <strong id="summary-matcher">-</strong>
                <span id="summary-type">-</span>
            </article>
            <article class="metric">
                <label>最佳得分</label>
                <strong id="summary-score">-</strong>
                <span>分值越低越接近</span>
            </article>
            <article class="metric">
                <label>计算时间</label>
                <strong id="summary-time">-</strong>
                <span id="summary-time-desc">-</span>
            </article>
            <article class="metric">
                <label>历史记录</label>
                <strong id="summary-history">0</strong>
                <span>右下角按钮可打开历史记录</span>
            </article>
        </section>

        <section class="content">
            <section class="card panel">
                <div class="panel-head">
                    <div>
                        <h2>输入</h2>
                        <p>左侧只保留输入，尽量把可视高度让给房型内容本身。</p>
                    </div>
                    <div id="status-text" class="status">等待计算</div>
                </div>
                <div class="panel-body">
                    <div class="input-shell">
                        <form id="match-form" class="form-scroll">
                            <div class="form-grid">
                                <div class="field">
                                    <label for="leftRoomName">供应商房型</label>
                                    <textarea id="leftRoomName" placeholder="例如：Deluxe King Room With Balcony"></textarea>
                                </div>

                                <div class="field">
                                    <label for="rightRoomName">OTA 房型</label>
                                    <textarea id="rightRoomName" placeholder="例如：Deluxe King Balcony Room"></textarea>
                                </div>

                                <div class="select-grid">
                                    <div class="field">
                                        <label for="wordDictSource">词库来源</label>
                                        <select id="wordDictSource">
                                            <option value="official">Official</option>
                                            <option value="contrastOfficial">Contrast Official</option>
                                        </select>
                                    </div>
                                    <div class="field">
                                        <label for="cleanFormatType">清洗强度</label>
                                        <select id="cleanFormatType">
                                            <option value="strongNormalize">Strong Normalize</option>
                                            <option value="basicNormalize">Basic Normalize</option>
                                        </select>
                                    </div>
                                </div>
                            </div>
                        </form>

                        <div class="button-row">
                            <button id="submit-button" class="primary" type="submit" form="match-form">计算匹配</button>
                            <button id="reset-button" class="ghost" type="button">清空输入</button>
                        </div>
                    </div>
                </div>
            </section>

            <section class="card panel">
                <div class="panel-head">
                    <div>
                        <h2>结果</h2>
                        <p>切换 tab 查看算法结果、匹配关系、分词标准化和原始响应。</p>
                    </div>
                </div>
                <div class="panel-body">
                    <div class="tab-shell">
                        <div class="tab-bar">
                            <button type="button" class="tab-button active" data-tab="matcher">算法结果</button>
                            <button type="button" class="tab-button" data-tab="relation">匹配关系</button>
                            <button type="button" class="tab-button" data-tab="analysis">分词标准化</button>
                            <button type="button" class="tab-button" data-tab="raw">原始响应</button>
                        </div>

                        <div class="tab-panels">
                            <section class="tab-panel active" data-panel="matcher">
                                <div class="tab-scroll">
                                    <div id="matcher-list" class="matcher-list">
                                        <div class="empty">等待计算。</div>
                                    </div>
                                </div>
                            </section>

                            <section class="tab-panel" data-panel="relation">
                                <div class="tab-scroll">
                                    <div class="relation-card">
                                        <header>
                                            <h3>匹配关系</h3>
                                            <p>按不同顺序查看两边词的一一对应关系。</p>
                                        </header>
                                        <div class="analysis-body">
                                            <div class="relation-toolbar">
                                                <button type="button" class="relation-mode active" data-mode="ota">OTA 原始顺序</button>
                                                <button type="button" class="relation-mode" data-mode="sup">Sup 原始顺序</button>
                                                <button type="button" class="relation-mode" data-mode="category">按词分类排序</button>
                                            </div>
                                            <div id="relation-content" class="relation-list">
                                                <div class="empty">等待计算。</div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </section>

                            <section class="tab-panel" data-panel="analysis">
                                <div class="tab-scroll">
                                    <div id="analysis-grid" class="analysis-grid">
                                        <div class="empty">等待计算。</div>
                                    </div>
                                </div>
                            </section>

                            <section class="tab-panel" data-panel="raw">
                                <div class="tab-scroll">
                                    <pre id="raw-output" class="mono">等待计算。</pre>
                                </div>
                            </section>
                        </div>
                    </div>
                </div>
            </section>
        </section>
    </main>

    <button id="history-fab" class="history-fab" type="button" aria-label="打开历史记录" title="历史记录">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M3 3v5h5"></path>
            <path d="M3.05 13A9 9 0 1 0 6 5.3L3 8"></path>
            <path d="M12 7v5l3 3"></path>
        </svg>
    </button>

    <div id="history-modal" class="history-modal">
        <section class="card history-dialog" role="dialog" aria-modal="true" aria-labelledby="history-title">
            <div class="history-head">
                <div>
                    <h3 id="history-title">历史记录</h3>
                    <p>点击任意记录可回填输入</p>
                </div>
                <button id="history-close" class="icon-button" type="button" aria-label="关闭历史记录">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M18 6 6 18"></path>
                        <path d="m6 6 12 12"></path>
                    </svg>
                </button>
            </div>
            <div class="history-scroll">
                <div id="history-list" class="history-list">
                    <div class="empty">还没有历史记录。提交一次后会自动保存在当前浏览器。</div>
                </div>
            </div>
        </section>
    </div>

    <script>
        const historyKey = "room-match-tester-history-v1";
        const form = document.getElementById("match-form");
        const submitButton = document.getElementById("submit-button");
        const resetButton = document.getElementById("reset-button");
        const statusText = document.getElementById("status-text");
        const matcherList = document.getElementById("matcher-list");
        const relationContent = document.getElementById("relation-content");
        const analysisGrid = document.getElementById("analysis-grid");
        const rawOutput = document.getElementById("raw-output");
        const historyList = document.getElementById("history-list");
        const summaryHistory = document.getElementById("summary-history");
        const historyFab = document.getElementById("history-fab");
        const historyModal = document.getElementById("history-modal");
        const historyClose = document.getElementById("history-close");
        const tabButtons = Array.from(document.querySelectorAll(".tab-button"));
        const tabPanels = Array.from(document.querySelectorAll(".tab-panel"));
        const relationButtons = Array.from(document.querySelectorAll(".relation-mode"));

        const summaryMatch = document.getElementById("summary-match");
        const summaryExplain = document.getElementById("summary-explain");
        const summaryMatcher = document.getElementById("summary-matcher");
        const summaryType = document.getElementById("summary-type");
        const summaryScore = document.getElementById("summary-score");
        const summaryTime = document.getElementById("summary-time");
        const summaryTimeDesc = document.getElementById("summary-time-desc");

        let latestResponse = null;
        let activeRelationMode = "ota";

        function readHistory() {
            try {
                const raw = localStorage.getItem(historyKey);
                return raw ? JSON.parse(raw) : [];
            } catch {
                return [];
            }
        }

        function writeHistory(items) {
            localStorage.setItem(historyKey, JSON.stringify(items.slice(0, 30)));
        }

        function matchTypeText(value) {
            if (value === 3) return "TextMatch";
            if (value === 2) return "FullMatch";
            if (value === 1) return "Match";
            return "NotMatch";
        }

        function escapeHtml(value) {
            return String(value ?? "")
                .replaceAll("&", "&amp;")
                .replaceAll("<", "&lt;")
                .replaceAll(">", "&gt;")
                .replaceAll("\"", "&quot;")
                .replaceAll("'", "&#39;");
        }

        function setActiveTab(tab) {
            tabButtons.forEach(button => {
                button.classList.toggle("active", button.dataset.tab === tab);
            });
            tabPanels.forEach(panel => {
                panel.classList.toggle("active", panel.dataset.panel === tab);
            });
        }

        function openHistoryModal() {
            historyModal.classList.add("open");
        }

        function closeHistoryModal() {
            historyModal.classList.remove("open");
        }

        function fillForm(entry) {
            document.getElementById("leftRoomName").value = entry.leftRoomName ?? "";
            document.getElementById("rightRoomName").value = entry.rightRoomName ?? "";
            document.getElementById("wordDictSource").value = entry.wordDictSource ?? "official";
            document.getElementById("cleanFormatType").value = entry.cleanFormatType ?? "strongNormalize";
        }

        function renderHistory() {
            const items = readHistory();
            summaryHistory.textContent = String(items.length);

            if (!items.length) {
                historyList.innerHTML = '<div class="empty">还没有历史记录。提交一次后会自动保存在当前浏览器。</div>';
                return;
            }

            historyList.innerHTML = items.map((item, index) => `
                <article class="history-item" data-index="${index}">
                    <strong>${escapeHtml(item.leftRoomName)}</strong>
                    <span>${escapeHtml(item.rightRoomName)}</span>
                    <small>${escapeHtml(item.resultText)} | ${escapeHtml(item.at)}</small>
                </article>
            `).join("");

            historyList.querySelectorAll(".history-item").forEach(element => {
                element.addEventListener("click", () => {
                    const index = Number(element.dataset.index);
                    const entry = readHistory()[index];
                    if (entry) {
                        fillForm(entry);
                        closeHistoryModal();
                    }
                });
            });
        }

        function pushHistory(payload, response) {
            const items = readHistory();
            items.unshift({
                leftRoomName: payload.leftRoomName,
                rightRoomName: payload.rightRoomName,
                wordDictSource: payload.wordDictSource,
                cleanFormatType: payload.cleanFormatType,
                resultText: response.canMatch ? `可匹配 / ${response.bestMatch?.matcherName ?? "-"}` : "不匹配",
                at: new Date().toLocaleString()
            });
            writeHistory(items);
            renderHistory();
        }

        function renderSummary(data) {
            summaryMatch.textContent = data.canMatch ? "可匹配" : "不匹配";
            summaryMatch.style.color = data.canMatch ? "var(--ok)" : "var(--danger)";
            summaryExplain.textContent = data.summary || "-";
            summaryMatcher.textContent = data.bestMatch?.matcherName || "-";
            summaryType.textContent = data.bestMatch ? matchTypeText(data.bestMatch.matchType) : "-";
            summaryScore.textContent = data.bestMatch?.matchScore ?? "-";
            summaryTime.textContent = data.comparedAt ? new Date(data.comparedAt).toLocaleTimeString() : "-";
            summaryTimeDesc.textContent = `供应商: ${data.left.roomNameCleaned || "-"} | OTA: ${data.right.roomNameCleaned || "-"}`;
        }

        function renderMatchers(data) {
            if (!data.matcherResults?.length) {
                matcherList.innerHTML = '<div class="empty">没有可展示的算法结果。</div>';
                return;
            }

            matcherList.innerHTML = data.matcherResults.map(item => `
                <article class="matcher-card ${item.isMatch ? "ok" : "fail"}">
                    <div class="matcher-top">
                        <div class="matcher-name">${escapeHtml(item.matcherName)}</div>
                        <span class="badge ${item.isMatch ? "ok" : "fail"}">${item.isMatch ? "匹配" : "不匹配"}</span>
                    </div>
                    <div class="matcher-meta">
                        <div class="meta-box">
                            <label>MatchType</label>
                            <strong>${escapeHtml(matchTypeText(item.matchType))}</strong>
                        </div>
                        <div class="meta-box">
                            <label>MatchScore</label>
                            <strong>${escapeHtml(item.matchScore ?? "-")}</strong>
                        </div>
                    </div>
                </article>
            `).join("");
        }

        function categoryTitle(category) {
            if (category === "must") return "必须词";
            if (category === "positive") return "增益词";
            if (category === "other") return "其他词";
            if (category === "negative") return "减益词";
            if (category === "symbol") return "符号词";
            return "其他词";
        }

        function categoryOrder(category) {
            if (category === "must") return 1;
            if (category === "positive") return 2;
            if (category === "other") return 3;
            if (category === "negative") return 4;
            if (category === "symbol") return 5;
            return 6;
        }

        function renderWordChip(word) {
            const tags = Array.isArray(word.tags) ? word.tags : [];
            return `
                <span class="chip ${escapeHtml(word.category || "other")}">
                    <span class="chip-stack">
                        <span class="chip-main">
                            <b>${escapeHtml(word.wordText)}</b>
                            <span>#${escapeHtml(word.wordId)}</span>
                            <span>${escapeHtml(word.wordTypeName || "-")}</span>
                        </span>
                        ${tags.length ? `
                            <span class="chip-tags">
                                ${tags.map(tag => `<span class="subtag ${escapeHtml(tag)}">${escapeHtml(categoryTitle(tag))}</span>`).join("")}
                            </span>
                        ` : ""}
                    </span>
                </span>
            `;
        }

        function renderCategoryGroups(words) {
            if (!words?.length) {
                return '<div class="empty">没有命中词。</div>';
            }

            const groups = new Map();
            for (const word of words) {
                const category = word.category || "other";
                if (!groups.has(category)) {
                    groups.set(category, []);
                }
                groups.get(category).push(word);
            }

            return `
                <div class="category-groups">
                    ${Array.from(groups.entries())
                        .sort((a, b) => categoryOrder(a[0]) - categoryOrder(b[0]))
                        .map(([category, items]) => `
                            <section class="category-group">
                                <div class="category-head">
                                    <div class="category-title">${escapeHtml(categoryTitle(category))}</div>
                                    <div class="category-count">${items.length} 个</div>
                                </div>
                                <div class="chip-wrap">
                                    ${items.map(renderWordChip).join("")}
                                </div>
                            </section>
                        `)
                        .join("")}
                </div>
            `;
        }

        function renderAnalysisCard(title, model) {
            return `
                <section class="analysis-card">
                    <header>
                        <h3>${escapeHtml(title)}</h3>
                        <p>原始: ${escapeHtml(model.roomNameOriginal || "-")}</p>
                    </header>
                    <div class="analysis-body">
                        <p><strong>标准化:</strong> ${escapeHtml(model.roomNameCleaned || "-")}</p>
                        <p><strong>错误:</strong> ${escapeHtml(model.errorMsg || "-")}</p>
                        <p><strong>未知字符:</strong> ${model.isUnknownChar ? "是" : "否"}</p>
                        ${renderCategoryGroups(model.words)}
                    </div>
                </section>
            `;
        }

        function renderAnalysis(data) {
            analysisGrid.innerHTML = `
                ${renderAnalysisCard("供应商房型", data.left)}
                ${renderAnalysisCard("OTA 房型", data.right)}
            `;
        }

        function wordKey(word) {
            const wordId = word.wordId ?? 0;
            const wordText = (word.wordText || "").toLowerCase();
            return `${wordId}|${wordText}`;
        }

        function buildWordMap(words) {
            const map = new Map();
            for (const word of words || []) {
                const key = wordKey(word);
                if (!map.has(key)) {
                    map.set(key, []);
                }
                map.get(key).push(word);
            }
            return map;
        }

        function takeMatchedWord(map, key) {
            const bucket = map.get(key);
            if (!bucket?.length) {
                return null;
            }

            const matched = bucket.shift();
            if (!bucket.length) {
                map.delete(key);
            }

            return matched;
        }

        function compareByOriginal(a, b) {
            return (a.index ?? 0) - (b.index ?? 0);
        }

        function compareByCategory(a, b) {
            const categoryCompare = categoryOrder(a.category || "other") - categoryOrder(b.category || "other");
            if (categoryCompare !== 0) {
                return categoryCompare;
            }

            return compareByOriginal(a, b);
        }

        function renderTokenBlock(word, sideLabel) {
            if (!word) {
                return `
                    <div class="relation-cell empty-cell">
                        <div class="relation-side">
                            <strong>${escapeHtml(sideLabel)}</strong>
                            <span>未命中</span>
                        </div>
                        <div class="empty">没有对应词</div>
                    </div>
                `;
            }

            return `
                <div class="relation-cell">
                    <div class="relation-side">
                        <strong>${escapeHtml(sideLabel)}</strong>
                        <span>${escapeHtml(categoryTitle(word.category || "other"))}</span>
                    </div>
                    <div class="chip-wrap">
                        ${renderWordChip(word)}
                    </div>
                    <div class="token-meta">
                        顺序 ${escapeHtml((word.index ?? 0) + 1)} | 类型 ${escapeHtml(word.wordTypeName || "-")}
                    </div>
                </div>
            `;
        }

        function buildRelationRows(leftWords, rightWords, mode) {
            const leftList = [...(leftWords || [])];
            const rightList = [...(rightWords || [])];

            if (mode === "category") {
                leftList.sort(compareByCategory);
                rightList.sort(compareByCategory);
            } else {
                leftList.sort(compareByOriginal);
                rightList.sort(compareByOriginal);
            }

            if (mode === "ota") {
                const leftMap = buildWordMap(leftList);
                const rows = rightList.map(rightWord => ({
                    left: takeMatchedWord(leftMap, wordKey(rightWord)),
                    right: rightWord
                }));

                for (const rest of leftMap.values()) {
                    rest.forEach(word => rows.push({ left: word, right: null }));
                }

                return rows;
            }

            if (mode === "sup") {
                const rightMap = buildWordMap(rightList);
                const rows = leftList.map(leftWord => ({
                    left: leftWord,
                    right: takeMatchedWord(rightMap, wordKey(leftWord))
                }));

                for (const rest of rightMap.values()) {
                    rest.forEach(word => rows.push({ left: null, right: word }));
                }

                return rows;
            }

            const rightMap = buildWordMap(rightList);
            const rows = leftList.map(leftWord => ({
                left: leftWord,
                right: takeMatchedWord(rightMap, wordKey(leftWord))
            }));

            for (const rest of rightMap.values()) {
                rest.forEach(word => rows.push({ left: null, right: word }));
            }

            return rows.sort((a, b) => {
                const leftCategory = categoryOrder(a.left?.category || a.right?.category || "other");
                const rightCategory = categoryOrder(b.left?.category || b.right?.category || "other");
                if (leftCategory !== rightCategory) {
                    return leftCategory - rightCategory;
                }

                const leftIndex = a.left?.index ?? a.right?.index ?? 0;
                const rightIndex = b.left?.index ?? b.right?.index ?? 0;
                return leftIndex - rightIndex;
            });
        }

        function renderRelationRows(rows) {
            if (!rows.length) {
                return '<div class="empty">没有可展示的对应关系。</div>';
            }

            return rows.map(row => `
                <article class="relation-row">
                    ${renderTokenBlock(row.left, "Sup")}
                    <div class="relation-arrow">↔</div>
                    ${renderTokenBlock(row.right, "OTA")}
                </article>
            `).join("");
        }

        function renderRelationView(mode) {
            activeRelationMode = mode;
            relationButtons.forEach(button => {
                button.classList.toggle("active", button.dataset.mode === mode);
            });

            if (!latestResponse) {
                relationContent.innerHTML = '<div class="empty">等待计算。</div>';
                return;
            }

            const rows = buildRelationRows(latestResponse.left?.words, latestResponse.right?.words, mode);
            relationContent.innerHTML = renderRelationRows(rows);
        }

        function renderRaw(data) {
            rawOutput.textContent = JSON.stringify(data, null, 2);
        }

        function setLoading(isLoading, message, isError = false, isSuccess = false) {
            submitButton.disabled = isLoading;
            statusText.textContent = message;
            statusText.className = "status";
            if (isError) statusText.classList.add("error");
            if (isSuccess) statusText.classList.add("success");
        }

        form.addEventListener("submit", async (event) => {
            event.preventDefault();

            const payload = {
                leftRoomName: document.getElementById("leftRoomName").value.trim(),
                rightRoomName: document.getElementById("rightRoomName").value.trim(),
                wordDictSource: document.getElementById("wordDictSource").value,
                cleanFormatType: document.getElementById("cleanFormatType").value
            };

            setLoading(true, "计算中...");

            try {
                const response = await fetch("/api/room-match/evaluate", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });

                const data = await response.json();
                if (!response.ok) {
                    throw new Error(data.message || `HTTP ${response.status}`);
                }

                latestResponse = data;
                renderSummary(data);
                renderMatchers(data);
                renderAnalysis(data);
                renderRelationView(activeRelationMode);
                renderRaw(data);
                pushHistory(payload, data);
                setLoading(false, "计算完成", false, true);
            } catch (error) {
                latestResponse = null;
                summaryMatch.textContent = "-";
                summaryExplain.textContent = error instanceof Error ? error.message : String(error);
                summaryMatcher.textContent = "-";
                summaryType.textContent = "-";
                summaryScore.textContent = "-";
                summaryTime.textContent = "-";
                summaryTimeDesc.textContent = "-";
                matcherList.innerHTML = '<div class="empty">请求失败。</div>';
                analysisGrid.innerHTML = '<div class="empty">请求失败。</div>';
                relationContent.innerHTML = '<div class="empty">请求失败。</div>';
                rawOutput.textContent = JSON.stringify({
                    message: error instanceof Error ? error.message : String(error)
                }, null, 2);
                setLoading(false, "请求失败", true, false);
            }
        });

        resetButton.addEventListener("click", () => {
            form.reset();
            document.getElementById("wordDictSource").value = "official";
            document.getElementById("cleanFormatType").value = "strongNormalize";
        });

        historyFab.addEventListener("click", openHistoryModal);
        historyClose.addEventListener("click", closeHistoryModal);
        historyModal.addEventListener("click", event => {
            if (event.target === historyModal) {
                closeHistoryModal();
            }
        });

        tabButtons.forEach(button => {
            button.addEventListener("click", () => setActiveTab(button.dataset.tab));
        });

        relationButtons.forEach(button => {
            button.addEventListener("click", () => renderRelationView(button.dataset.mode || "ota"));
        });

        renderHistory();
        renderRelationView(activeRelationMode);
    </script>
</body>
</html>
""";
}

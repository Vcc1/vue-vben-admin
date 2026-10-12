namespace Tests.Host.DebugTools;

internal static class SupplierStaticDataPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>静态数据测试</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f5f7fb;
            --panel: rgba(255, 255, 255, 0.82);
            --panel-soft: rgba(248, 250, 252, 0.92);
            --text: #101828;
            --muted: #667085;
            --line: rgba(15, 23, 42, 0.08);
            --brand: #2563eb;
            --danger: #b42318;
            --shadow: 0 18px 40px rgba(15, 23, 42, 0.08);
            --blur: saturate(180%) blur(18px);
        }

        * { box-sizing: border-box; }

        html, body {
            margin: 0;
            min-height: 100%;
            font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
            color: var(--text);
            background:
                radial-gradient(circle at top left, rgba(96, 165, 250, 0.16), transparent 22%),
                linear-gradient(180deg, #f8fafc 0%, #eef2f7 100%);
        }

        body { padding: 20px; }

        .page {
            display: grid;
            grid-template-rows: auto auto minmax(0, 1fr);
            gap: 16px;
            min-height: calc(100vh - 40px);
        }

        .card {
            background: var(--panel);
            border: 1px solid var(--line);
            border-radius: 22px;
            box-shadow: var(--shadow);
            backdrop-filter: var(--blur);
            -webkit-backdrop-filter: var(--blur);
        }

        .hero {
            padding: 20px 22px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 16px;
        }

        .hero h1 {
            margin: 0 0 6px;
            font-size: 28px;
        }

        .hero p {
            margin: 0;
            color: var(--muted);
            font-size: 14px;
            line-height: 1.6;
        }

        .toolbar {
            padding: 18px 20px;
            display: grid;
            grid-template-columns: 140px minmax(0, 1fr) minmax(0, 1fr) 160px 180px auto;
            gap: 12px;
            align-items: stretch;
        }

        .field label {
            display: block;
            margin-bottom: 8px;
            font-size: 13px;
            font-weight: 600;
        }

        input,
        select {
            width: 100%;
            border: 1px solid rgba(15, 23, 42, 0.08);
            border-radius: 14px;
            background: rgba(255, 255, 255, 0.92);
            color: var(--text);
            padding: 12px 14px;
            font: inherit;
            outline: none;
        }

        .field-hint {
            margin-top: 8px;
            color: var(--muted);
            font-size: 12px;
            line-height: 1.5;
        }

        .button-box {
            display: flex;
            align-items: end;
        }

        button {
            width: 100%;
            border: 0;
            border-radius: 14px;
            padding: 12px 16px;
            font: inherit;
            font-weight: 600;
            cursor: pointer;
            color: white;
            background: linear-gradient(135deg, var(--brand) 0%, #4f46e5 100%);
        }

        button:disabled {
            opacity: 0.65;
            cursor: wait;
        }

        .options {
            padding: 0 20px 18px;
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
        }

        .checkbox-row {
            display: inline-flex;
            align-items: center;
            gap: 10px;
            color: var(--text);
            font-size: 14px;
        }

        .checkbox-row input {
            width: 18px;
            height: 18px;
        }

        .result {
            min-height: 0;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
        }

        .result-head {
            display: flex;
            justify-content: space-between;
            gap: 16px;
            padding: 18px 20px;
            border-bottom: 1px solid var(--line);
            background: var(--panel-soft);
        }

        .result-head h2 {
            margin: 0 0 6px;
            font-size: 18px;
        }

        .status {
            color: var(--muted);
            font-size: 13px;
        }

        .status.error { color: var(--danger); }

        pre {
            margin: 0;
            padding: 20px;
            min-height: 0;
            overflow: auto;
            background: rgba(255, 255, 255, 0.64);
            color: #0f172a;
            font-size: 13px;
            line-height: 1.6;
            white-space: pre-wrap;
            word-break: break-word;
        }

        @media (max-width: 1280px) {
            .toolbar {
                grid-template-columns: repeat(2, minmax(0, 1fr));
            }
        }

        @media (max-width: 768px) {
            body { padding: 12px; }
            .toolbar { grid-template-columns: 1fr; }
        }
    </style>
</head>
<body>
    <main class="page">
        <section class="card hero">
            <div>
                <h1>静态数据测试</h1>
                <p>直接测试 <code>ISupplierStaticDataService</code>。当前页只保留正文内容，方便挂到总测试台右侧区域。</p>
            </div>
            <div id="status-text" class="status">等待查询</div>
        </section>

        <section class="card">
            <form id="query-form" class="toolbar">
                <div class="field">
                    <label for="supId">SupId</label>
                    <input id="supId" name="supId" type="number" value="51" min="1" required />
                </div>

                <div class="field">
                    <label for="supplierHotelCode">SHotelCode</label>
                    <input id="supplierHotelCode" name="supplierHotelCode" type="text" placeholder="例如：123456,234567" />
                    <div class="field-hint">和 MTHotelCode 二选一；多个 hotel code 用英文逗号分隔。</div>
                </div>

                <div class="field">
                    <label for="mtHotelCode">MTHotelCode</label>
                    <input id="mtHotelCode" name="mtHotelCode" type="text" placeholder="例如：MT123456,MT234567" />
                </div>

                <div class="field">
                    <label for="language">Language</label>
                    <input id="language" name="language" type="text" placeholder="en" />
                </div>

                <div class="field">
                    <label for="queryKind">查询内容</label>
                    <select id="queryKind" name="queryKind">
                        <option value="hotelAndRooms">酒店 + 房型</option>
                        <option value="hotel">酒店</option>
                        <option value="rooms">房型</option>
                    </select>
                </div>

                <div class="button-box">
                    <button id="submit-button" type="submit">发起查询</button>
                </div>
            </form>

            <div class="options">
                <label class="checkbox-row" for="forceRefresh">
                    <input id="forceRefresh" name="forceRefresh" type="checkbox" />
                    <span>ForceRefresh：跳过缓存直接刷新</span>
                </label>

                <label class="checkbox-row" for="includeRawInfo">
                    <input id="includeRawInfo" name="includeRawInfo" type="checkbox" />
                    <span>输出原始请求和响应</span>
                </label>
            </div>
        </section>

        <section class="card result">
            <div class="result-head">
                <div>
                    <h2>返回结果</h2>
                    <div class="status" id="result-status-text">等待查询</div>
                </div>
            </div>
            <pre id="result-view">{
  "message": "Submit a request to inspect supplier static data."
}</pre>
        </section>
    </main>

    <script>
        const form = document.getElementById("query-form");
        const resultView = document.getElementById("result-view");
        const resultStatusText = document.getElementById("result-status-text");
        const pageStatusText = document.getElementById("status-text");
        const submitButton = document.getElementById("submit-button");

        form.addEventListener("submit", async (event) => {
            event.preventDefault();

            const payload = {
                supId: Number(document.getElementById("supId").value),
                supplierHotelCode: document.getElementById("supplierHotelCode").value.trim(),
                mtHotelCode: document.getElementById("mtHotelCode").value.trim(),
                queryKind: document.getElementById("queryKind").value,
                language: document.getElementById("language").value.trim(),
                forceRefresh: document.getElementById("forceRefresh").checked,
                includeRawInfo: document.getElementById("includeRawInfo").checked
            };

            submitButton.disabled = true;
            resultStatusText.textContent = "查询中...";
            pageStatusText.textContent = "查询中...";
            resultStatusText.classList.remove("error");
            pageStatusText.classList.remove("error");
            resultView.textContent = "Loading...";

            try {
                const response = await fetch("/api/query", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });

                const text = await response.text();
                let data;

                try {
                    data = text ? JSON.parse(text) : {};
                } catch {
                    data = { raw: text };
                }

                resultView.textContent = JSON.stringify(data, null, 2);
                if (!response.ok) {
                    resultStatusText.textContent = `请求失败 (${response.status})`;
                    pageStatusText.textContent = `请求失败 (${response.status})`;
                    resultStatusText.classList.add("error");
                    pageStatusText.classList.add("error");
                } else {
                    resultStatusText.textContent = "查询完成";
                    pageStatusText.textContent = "查询完成";
                }
            } catch (error) {
                resultStatusText.textContent = "请求异常";
                pageStatusText.textContent = "请求异常";
                resultStatusText.classList.add("error");
                pageStatusText.classList.add("error");
                resultView.textContent = JSON.stringify({
                    message: error instanceof Error ? error.message : String(error)
                }, null, 2);
            } finally {
                submitButton.disabled = false;
            }
        });
    </script>
</body>
</html>
""";
}

namespace SupplierStaticDataApp;

internal static class TestShellPage
{
    public const string Html = """
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>测试工具台</title>
    <style>
        :root {
            color-scheme: light;
            --bg: #f4f6fb;
            --sidebar: rgba(248, 250, 252, 0.88);
            --panel: rgba(255, 255, 255, 0.84);
            --text: #0f172a;
            --muted: #64748b;
            --line: rgba(15, 23, 42, 0.08);
            --brand: #2563eb;
            --brand-soft: rgba(37, 99, 235, 0.12);
            --shadow: 0 18px 40px rgba(15, 23, 42, 0.08);
            --blur: saturate(180%) blur(20px);
        }

        * { box-sizing: border-box; }

        html, body {
            margin: 0;
            height: 100%;
            font-family: "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;
            color: var(--text);
            background:
                radial-gradient(circle at top left, rgba(96, 165, 250, 0.14), transparent 24%),
                radial-gradient(circle at top right, rgba(167, 139, 250, 0.10), transparent 20%),
                linear-gradient(180deg, #f8fafc 0%, #eef2f7 100%);
        }

        body {
            height: 100vh;
            overflow: hidden;
        }

        .shell {
            height: 100vh;
            display: flex;
            overflow: hidden;
        }

        .sidebar {
            width: 252px;
            flex: 0 0 252px;
            padding: 28px 18px 24px;
            background: var(--sidebar);
            border-right: 1px solid rgba(255, 255, 255, 0.7);
            backdrop-filter: var(--blur);
            -webkit-backdrop-filter: var(--blur);
            display: flex;
            flex-direction: column;
            gap: 24px;
        }

        .brand {
            padding: 0 8px;
        }

        .brand-tag {
            display: inline-flex;
            align-items: center;
            padding: 6px 10px;
            border-radius: 999px;
            background: var(--brand-soft);
            color: var(--brand);
            font-size: 12px;
            font-weight: 700;
        }

        .brand h1 {
            margin: 12px 0 8px;
            font-size: 24px;
            line-height: 1.1;
        }

        .brand p {
            margin: 0;
            color: var(--muted);
            font-size: 13px;
            line-height: 1.6;
        }

        .nav {
            display: flex;
            flex-direction: column;
            gap: 8px;
        }

        .nav-label {
            padding: 0 8px 6px;
            color: var(--muted);
            font-size: 12px;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: 0;
        }

        .nav button {
            width: 100%;
            border: 0;
            border-radius: 14px;
            background: transparent;
            color: var(--text);
            text-align: left;
            padding: 12px 14px;
            font: inherit;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            transition: background-color 0.18s ease, color 0.18s ease, transform 0.18s ease;
        }

        .nav button:hover {
            background: rgba(255, 255, 255, 0.66);
            transform: translateX(2px);
        }

        .nav button.active {
            background: rgba(255, 255, 255, 0.96);
            color: var(--brand);
            box-shadow: inset 0 0 0 1px rgba(37, 99, 235, 0.10);
        }

        .nav-note {
            padding: 4px 8px 0;
            color: var(--muted);
            font-size: 12px;
            line-height: 1.6;
        }

        .main {
            flex: 1;
            min-width: 0;
            min-height: 0;
            padding: 22px;
            display: grid;
            grid-template-rows: auto minmax(0, 1fr);
            gap: 16px;
            overflow: hidden;
        }

        .header,
        .frame-card {
            background: var(--panel);
            border: 1px solid var(--line);
            border-radius: 22px;
            box-shadow: var(--shadow);
            backdrop-filter: var(--blur);
            -webkit-backdrop-filter: var(--blur);
        }

        .header {
            padding: 18px 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
            gap: 16px;
        }

        .header h2 {
            margin: 0 0 4px;
            font-size: 22px;
        }

        .header p {
            margin: 0;
            color: var(--muted);
            font-size: 14px;
        }

        .status {
            font-size: 13px;
            color: var(--muted);
        }

        .frame-card {
            overflow: hidden;
            min-height: 0;
        }

        iframe {
            width: 100%;
            height: 100%;
            border: 0;
            display: block;
            background: transparent;
        }

        @media (max-width: 960px) {
            .shell {
                flex-direction: column;
            }

            .sidebar {
                width: 100%;
                flex-basis: auto;
            }
        }
    </style>
</head>
<body>
    <div class="shell">
        <aside class="sidebar">
            <section class="brand">
                <span class="brand-tag">Tester Console</span>
                <h1>测试工具台</h1>
                <p>左侧只负责切换测试页面，具体测试内容都保留在右侧主区域。</p>
            </section>

            <nav class="nav" aria-label="测试栏目">
                <div class="nav-label">测试栏目</div>
                <button type="button" class="menu-item active" data-title="房型匹配测试" data-src="/room-match">房型匹配测试</button>
                <button type="button" class="menu-item" data-title="静态数据测试" data-src="/supplier-static-data">静态数据测试</button>
                <div class="nav-note">切页只切换右侧页面，页面自己的输入和历史状态仍由各自页面维护。</div>
            </nav>
        </aside>

        <main class="main">
            <section class="header">
                <div>
                    <h2 id="page-title">房型匹配测试</h2>
                    <p>当前主区域显示所选测试页面的完整内容。</p>
                </div>
                <div class="status" id="status-text">已加载</div>
            </section>

            <section class="frame-card">
                <iframe id="content-frame" src="/room-match" title="测试内容"></iframe>
            </section>
        </main>
    </div>

    <script>
        const buttons = Array.from(document.querySelectorAll(".menu-item"));
        const frame = document.getElementById("content-frame");
        const title = document.getElementById("page-title");
        const status = document.getElementById("status-text");

        function activate(button) {
            buttons.forEach(item => item.classList.toggle("active", item === button));
            title.textContent = button.dataset.title || "";
            status.textContent = "加载中...";
            frame.src = button.dataset.src || "/";
        }

        buttons.forEach(button => {
            button.addEventListener("click", () => activate(button));
        });

        frame.addEventListener("load", () => {
            status.textContent = "已加载";
        });
    </script>
</body>
</html>
""";
}

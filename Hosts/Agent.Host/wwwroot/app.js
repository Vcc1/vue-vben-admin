(() => {
  "use strict";

  const state = {
    mode: "hotelBusiness",
    conversations: [],
    recentHotels: [],
    expandedHotelCodes: new Set(),
    hotelCode: null,
    hotelDetail: null,
    hotelBusinessView: "dashboard",
    hotelBusinessData: null,
    hotelBusinessDataLoading: false,
    hotelBusinessDataError: null,
    hotelBusinessDataRequestId: 0,
    hotelBusinessDataFilter: "all",
    phoneNormalizationResults: new Map(),
    phoneNormalizationLoading: false,
    phoneNormalizationError: null,
    phoneNormalizationRequestId: 0,
    selectedVersion: null,
    current: null,
    viewMessages: [],
    busy: false,
    debugMessage: null,
    lastRaw: null,
    researchResult: null,
    researchLoading: false,
    researchError: null,
    researchViewMode: localStorage.getItem("maf.hotelResearch.viewMode") || "comparison",
    searchTimer: null,
    toastTimer: null
  };

  const elements = {
    conversationList: document.querySelector("#conversationList"),
    conversationCount: document.querySelector("#conversationCount"),
    conversationSearch: document.querySelector("#conversationSearch"),
    conversationTitle: document.querySelector("#conversationTitle"),
    conversationId: document.querySelector("#conversationId"),
    copyConversationIdButton: document.querySelector("#copyConversationIdButton"),
    deleteConversationButton: document.querySelector("#deleteConversationButton"),
    newConversationButton: document.querySelector("#newConversationButton"),
    hotelBusinessSection: document.querySelector("#hotelBusinessSection"),
    hotelBusinessToggle: document.querySelector("#hotelBusinessToggle"),
    hotelBusinessContent: document.querySelector("#hotelBusinessContent"),
    otherConversationsSection: document.querySelector("#otherConversationsSection"),
    otherConversationsToggle: document.querySelector("#otherConversationsToggle"),
    otherConversationsContent: document.querySelector("#otherConversationsContent"),
    hotelLookupForm: document.querySelector("#hotelLookupForm"),
    hotelLookupInput: document.querySelector("#hotelLookupInput"),
    recentHotelList: document.querySelector("#recentHotelList"),
    recentHotelCount: document.querySelector("#recentHotelCount"),
    messages: document.querySelector("#messages"),
    composer: document.querySelector("#composer"),
    hotelResearchView: document.querySelector("#hotelResearchView"),
    hotelResearchForm: document.querySelector("#hotelResearchForm"),
    mtHotelCodeInput: document.querySelector("#mtHotelCodeInput"),
    hotelResearchButton: document.querySelector("#hotelResearchButton"),
    hotelResearchContent: document.querySelector("#hotelResearchContent"),
    messageInput: document.querySelector("#messageInput"),
    sendButton: document.querySelector("#sendButton"),
    agentName: document.querySelector("#agentName"),
    webSearchEnabled: document.querySelector("#webSearchEnabled"),
    runStatus: document.querySelector("#runStatus"),
    serviceStatus: document.querySelector("#serviceStatus"),
    inspectorSubtitle: document.querySelector("#inspectorSubtitle"),
    sourcesPanel: document.querySelector("#sourcesPanel"),
    usagePanel: document.querySelector("#usagePanel"),
    rawPanel: document.querySelector("#rawPanel"),
    toast: document.querySelector("#toast"),
    backToHotelBusinessButton: document.querySelector("#backToHotelBusinessButton"),
    openSidebarButton: document.querySelector("#openSidebarButton"),
    closeSidebarButton: document.querySelector("#closeSidebarButton"),
    openInspectorButton: document.querySelector("#openInspectorButton"),
    closeInspectorButton: document.querySelector("#closeInspectorButton"),
    backdrop: document.querySelector("#backdrop")
  };

  function node(tagName, className, text) {
    const item = document.createElement(tagName);
    if (className) item.className = className;
    if (text !== undefined && text !== null) item.textContent = text;
    return item;
  }

  function refreshIcons() {
    if (window.lucide) window.lucide.createIcons({ attrs: { "stroke-width": 1.8 } });
  }

  function icon(name) {
    const item = document.createElement("i");
    item.dataset.lucide = name;
    item.setAttribute("aria-hidden", "true");
    return item;
  }

  const researchViewComponents = [
    {
      id: "comparison",
      label: "对照表",
      icon: "table-2",
      render: result => createResearchComparisonTable(result.databaseHotel, result.sites)
    },
    {
      id: "sources",
      label: "按来源",
      icon: "panels-top-left",
      render: result => createSourceCardsView(result.databaseHotel, result.sites)
    },
    {
      id: "rooms",
      label: "房型纵览",
      icon: "bed-double",
      render: result => createRoomOverview(result.databaseHotel, result.sites)
    }
  ];

  async function api(url, options) {
    const response = await fetch(url, options);
    if (!response.ok) {
      let detail = `${response.status} ${response.statusText}`;
      try {
        const problem = await response.json();
        detail = problem.detail || problem.title || detail;
      } catch {
        // Keep the HTTP status when the response is not JSON.
      }
      throw new Error(detail);
    }
    return response;
  }

  async function loadConversations(query = elements.conversationSearch.value.trim()) {
    try {
      const response = await api(`/api/agents/conversations?take=20&query=${encodeURIComponent(query)}`);
      state.conversations = await response.json();
      renderConversationList();
    } catch (error) {
      showToast(`无法加载会话：${error.message}`);
      state.conversations = [];
      renderConversationList();
    }
  }

  async function loadRecentHotels() {
    try {
      const response = await api("/api/hotel-business/recent");
      state.recentHotels = await response.json();
    } catch (error) {
      state.recentHotels = [];
      showToast(`无法加载最近酒店：${error.message}`);
    }
    renderRecentHotels();
    refreshIcons();
  }

  async function openHotelByCode(mtHotelCode, closeMobile = true) {
    if (state.busy || state.researchLoading) return;
    const code = String(mtHotelCode || "").trim().toUpperCase();
    if (!code) return;
    const changedHotel = state.hotelCode !== code;
    state.mode = "hotelBusiness";
    state.hotelCode = code;
    if (changedHotel) state.hotelBusinessView = "dashboard";
    state.expandedHotelCodes.add(code);
    state.hotelDetail = null;
    state.hotelBusinessData = null;
    state.hotelBusinessDataError = null;
    state.phoneNormalizationResults = new Map();
    state.phoneNormalizationLoading = false;
    state.phoneNormalizationError = null;
    state.phoneNormalizationRequestId += 1;
    state.researchResult = null;
    state.researchError = null;
    state.selectedVersion = null;
    elements.mtHotelCodeInput.value = code;
    elements.hotelLookupInput.value = code;
    try {
      const response = await fetch(`/api/hotel-business/${encodeURIComponent(code)}`);
      if (response.status !== 404) {
        if (!response.ok) {
          let detail = `${response.status} ${response.statusText}`;
          try {
            const problem = await response.json();
            detail = problem.detail || problem.title || detail;
          } catch {
            // Keep the HTTP status when the response is not JSON.
          }
          throw new Error(detail);
        }
        state.hotelDetail = await response.json();
        const operation = getResearchOperation();
        if (operation?.latestSuccessfulVersion) {
          await loadHotelResult(operation.latestSuccessfulVersion, false);
        }
      }
      await loadHotelBusinessData(false);
    } catch (error) {
      state.researchError = error.message;
      showToast(`无法打开酒店业务：${error.message}`);
    }
    renderAll();
    if (closeMobile) closeDrawers();
  }

  async function loadHotelBusinessData(shouldRender = true) {
    if (!state.hotelCode) return;
    const mtHotelCode = state.hotelCode;
    const requestId = ++state.hotelBusinessDataRequestId;
    state.hotelBusinessDataLoading = true;
    state.hotelBusinessDataError = null;
    try {
      const response = await api(`/api/hotel-research/${encodeURIComponent(mtHotelCode)}/business-data`);
      if (requestId !== state.hotelBusinessDataRequestId || mtHotelCode !== state.hotelCode) return;
      state.hotelBusinessData = await response.json();
      await loadHotelPhoneNormalizations(false);
    } catch (error) {
      if (requestId !== state.hotelBusinessDataRequestId || mtHotelCode !== state.hotelCode) return;
      state.hotelBusinessData = null;
      state.hotelBusinessDataError = error.message;
    } finally {
      if (requestId !== state.hotelBusinessDataRequestId || mtHotelCode !== state.hotelCode) return;
      state.hotelBusinessDataLoading = false;
      if (shouldRender) renderAll();
    }
  }

  async function loadHotelResult(resultVersion, shouldRender = true) {
    if (!state.hotelCode) return;
    const versionPath = resultVersion === "latest" ? "latest" : String(resultVersion);
    try {
      const response = await api(`/api/hotel-business/${encodeURIComponent(state.hotelCode)}/operations/hotel-research/results/${versionPath}`);
      state.researchResult = await response.json();
      state.selectedVersion = state.researchResult.resultVersion;
      setResearchInspector(state.researchResult);
      if (state.hotelBusinessData) await loadHotelPhoneNormalizations(false);
      if (shouldRender) renderAll();
    } catch (error) {
      showToast(`无法读取结果版本：${error.message}`);
    }
  }

  function getResearchOperation() {
    return state.hotelDetail?.operations?.find(operation => operation.operationType === "hotel-research") || null;
  }

  function setResearchInspector(result) {
    state.lastRaw = result;
    state.debugMessage = result ? {
      role: "assistant",
      sources: result.sources || [],
      searchQueries: result.searchQueries || [],
      usage: result.usage || null,
      provider: result.processor?.provider,
      model: result.processor?.model,
      processor: result.processor,
      responseId: null,
      finishReason: null
    } : null;
  }

  async function loadConversation(conversationId, closeMobile = true) {
    if (state.busy) return;
    try {
      const response = await api(`/api/agents/conversations/${encodeURIComponent(conversationId)}`);
      state.current = await response.json();
      state.mode = "chat";
      state.viewMessages = [...state.current.messages];
      state.debugMessage = [...state.current.messages].reverse().find(message => message.role === "assistant") || null;
      state.lastRaw = state.current;
      renderAll();
      setSectionCollapsed("other", false);
      if (closeMobile) closeDrawers();
    } catch (error) {
      showToast(`无法打开会话：${error.message}`);
    }
  }

  function startNewConversation() {
    if (state.busy) return;
    state.current = null;
    state.mode = "chat";
    state.viewMessages = [];
    state.debugMessage = null;
    state.lastRaw = null;
    renderAll();
    closeDrawers();
    elements.messageInput.focus();
  }

  async function deleteCurrentConversation() {
    if (!state.current || state.busy) return;
    const conversationId = state.current.conversationId;
    if (!window.confirm(`确定删除会话“${state.current.title}”？\n会话记录和 MAF session 文件都会被删除。`)) return;

    try {
      await api(`/api/agents/conversations/${encodeURIComponent(conversationId)}`, { method: "DELETE" });
      startNewConversation();
      await loadConversations();
    } catch (error) {
      showToast(`删除失败：${error.message}`);
    }
  }

  function renderAll() {
    renderMode();
    renderHeader();
    renderRecentHotels();
    renderConversationList();
    renderMessages();
    renderInspector();
    renderHotelResearch();
    refreshIcons();
  }

  function renderHeader() {
    elements.backToHotelBusinessButton.hidden = state.mode !== "hotelDebug";
    if (state.mode === "hotelBusiness") {
      const title = state.hotelDetail?.hotelName || state.hotelCode || "酒店业务";
      elements.conversationTitle.textContent = title;
      elements.conversationId.textContent = "";
      elements.copyConversationIdButton.hidden = true;
      elements.deleteConversationButton.disabled = true;
      return;
    }

    if (state.mode === "hotelDebug") {
      elements.conversationTitle.textContent = `调试会话 · ${state.hotelCode}`;
      elements.conversationId.textContent = state.current?.conversationId || "";
      elements.copyConversationIdButton.hidden = !state.current?.conversationId;
      elements.deleteConversationButton.disabled = true;
      return;
    }

    const currentId = state.current?.conversationId;
    elements.conversationTitle.textContent = state.current?.title || "新会话";
    elements.conversationId.textContent = currentId || "";
    elements.copyConversationIdButton.hidden = !currentId;
    elements.deleteConversationButton.disabled = !currentId || state.busy;
  }

  function renderMode() {
    const researchMode = state.mode === "hotelBusiness";
    elements.messages.hidden = researchMode;
    elements.composer.hidden = state.mode !== "chat";
    elements.hotelResearchView.hidden = !researchMode;
  }

  function renderRecentHotels() {
    elements.recentHotelList.replaceChildren();
    elements.recentHotelCount.textContent = `${state.recentHotels.length} / 20`;
    const visibleHotels = [...state.recentHotels];
    if (state.hotelCode && !visibleHotels.some(hotel => (hotel.mtHotelCode || hotel.mthotelCode) === state.hotelCode)) {
      const researchHotel = state.researchResult?.databaseHotel;
      visibleHotels.unshift({
        mtHotelCode: state.hotelCode,
        hotelName: state.hotelDetail?.hotelName || researchHotel?.hotelName || state.hotelCode,
        hotelNameEnglish: state.hotelDetail?.hotelNameEnglish || researchHotel?.hotelNameEnglish,
        updatedAt: state.hotelDetail?.updatedAt || new Date().toISOString(),
        latestSuccessfulVersion: getResearchOperation()?.latestSuccessfulVersion,
        lastRunStatus: getResearchOperation()?.runs?.[0]?.status,
        operations: state.hotelDetail?.operations || []
      });
    }
    if (!visibleHotels.length) {
      elements.recentHotelList.append(node("div", "list-empty", "暂无酒店业务记录"));
      return;
    }
    for (const hotel of visibleHotels) {
      const mtHotelCode = hotel.mtHotelCode || hotel.mthotelCode;
      const expanded = state.expandedHotelCodes.has(mtHotelCode);
      const item = node("div", "hotel-nav-item");
      item.classList.toggle("active", mtHotelCode === state.hotelCode);
      const row = node("div", "hotel-item-row");
      const button = node("button", "hotel-item");
      button.type = "button";
      button.addEventListener("click", () => openHotelByCode(mtHotelCode));
      const identity = node("span", "hotel-item-identity");
      identity.append(node("strong", "", hotel.hotelName || mtHotelCode));
      identity.append(node("span", "hotel-name-english", hotel.hotelNameEnglish || "暂无英文名"));
      identity.append(node("code", "", mtHotelCode));
      button.append(identity);
      const meta = node("span", "hotel-item-meta");
      meta.append(node("span", `run-status-dot ${String(hotel.lastRunStatus || "").toLowerCase()}`));
      meta.append(node("span", "", hotel.latestSuccessfulVersion ? `最新 v${hotel.latestSuccessfulVersion}` : "暂无成功版本"));
      meta.append(node("time", "", formatListTime(hotel.updatedAt)));
      button.append(meta);
      row.append(button);
      const toggle = node("button", "hotel-expand-button");
      toggle.type = "button";
      toggle.title = expanded ? "收起业务功能" : "展开业务功能";
      toggle.setAttribute("aria-label", toggle.title);
      toggle.setAttribute("aria-expanded", String(expanded));
      toggle.append(icon("chevron-down"));
      toggle.addEventListener("click", () => {
        if (expanded) state.expandedHotelCodes.delete(mtHotelCode);
        else state.expandedHotelCodes.add(mtHotelCode);
        renderRecentHotels();
        refreshIcons();
      });
      row.append(toggle);
      item.append(row);

      const operations = hotel.operations?.length
        ? hotel.operations
        : [{
            operationType: hotel.lastOperationType,
            updatedAt: hotel.updatedAt,
            lastRunStatus: hotel.lastRunStatus,
            latestSuccessfulVersion: hotel.latestSuccessfulVersion
          }];
      if (!operations.some(operation => operation.operationType === "hotel-research")) {
        operations.push({ operationType: "hotel-research" });
      }
      const operationList = node("div", "hotel-operation-list");
      operationList.hidden = !expanded;
      const dashboardCounts = mtHotelCode === state.hotelCode ? getHotelBusinessCounts() : null;
      operationList.append(createHotelBusinessViewButton(
        mtHotelCode,
        "dashboard",
        "chart-no-axes-column",
        "数据面板",
        dashboardCounts ? `${dashboardCounts.totalEvidence} 条数据线索` : "计数与数据频次"));
      operationList.append(createHotelBusinessViewButton(
        mtHotelCode,
        "summary",
        "table-properties",
        "整体数据汇总",
        dashboardCounts ? `${dashboardCounts.totalEvidence} 条明细` : "全部来源明细"));
      operations.filter(operation => operation.operationType).forEach(operation => {
        const detailedOperation = mtHotelCode === state.hotelCode
          ? state.hotelDetail?.operations?.find(item => item.operationType === operation.operationType)
          : null;
        const operationButton = node("button", "hotel-operation-item");
        operationButton.type = "button";
        operationButton.classList.toggle(
          "active",
          mtHotelCode === state.hotelCode && state.hotelBusinessView === "research");
        operationButton.addEventListener("click", () => openHotelOperation(mtHotelCode, operation.operationType));
        operationButton.append(icon(operation.operationType === "hotel-research" ? "search" : "workflow"));
        const text = node("span", "");
        text.append(node("strong", "", formatOperationName(operation.operationType)));
        text.append(node("small", "", detailedOperation
          ? `${detailedOperation.runs?.length || 0} 次运行`
          : operation.latestSuccessfulVersion
            ? `${formatRunStatus(operation.lastRunStatus)} · v${operation.latestSuccessfulVersion}`
            : formatRunStatus(operation.lastRunStatus) || "尚未搜索"));
        operationButton.append(text);
        operationList.append(operationButton);
        if (detailedOperation) {
          operationList.append(createSidebarRunList(detailedOperation));
        }
      });
      item.append(operationList);
      elements.recentHotelList.append(item);
    }
  }

  function createHotelBusinessViewButton(mtHotelCode, view, iconName, title, subtitle) {
    const button = node("button", "hotel-operation-item");
    button.type = "button";
    button.classList.toggle(
      "active",
      mtHotelCode === state.hotelCode && state.hotelBusinessView === view);
    button.append(icon(iconName));
    const text = node("span", "");
    text.append(node("strong", "", title));
    text.append(node("small", "", subtitle));
    button.append(text);
    button.addEventListener("click", () => openHotelBusinessView(mtHotelCode, view));
    return button;
  }

  function formatOperationName(operationType) {
    return operationType === "hotel-research" ? "酒店资料搜索" : operationType;
  }

  async function openHotelOperation(mtHotelCode, operationType) {
    await openHotelByCode(mtHotelCode);
    state.hotelBusinessView = operationType === "hotel-research" ? "research" : operationType;
    state.expandedHotelCodes.add(mtHotelCode);
    renderAll();
  }

  async function openHotelBusinessView(mtHotelCode, view) {
    if (state.hotelCode !== mtHotelCode) await openHotelByCode(mtHotelCode, false);
    state.hotelBusinessView = view;
    if (!state.hotelBusinessData && !state.hotelBusinessDataLoading) {
      await loadHotelBusinessData(false);
    }
    state.lastRaw = view === "research"
      ? state.researchResult
      : { businessData: state.hotelBusinessData, researchResult: state.researchResult };
    renderAll();
    closeDrawers();
  }

  function createSidebarRunList(operation) {
    const list = node("div", "hotel-run-list");
    const runs = operation.runs || [];
    if (!runs.length) {
      list.append(node("div", "hotel-run-empty", "尚无运行记录"));
      return list;
    }

    runs.forEach(run => {
      const row = node("div", "hotel-run-item");
      const resultButton = node("button", "hotel-run-result");
      resultButton.type = "button";
      resultButton.classList.toggle("active", run.resultVersion === state.selectedVersion);
      const top = node("span", "hotel-run-top");
      const status = node("span", `run-status ${String(run.status || "").toLowerCase()}`);
      status.append(node("span", "run-status-dot"));
      status.append(node("strong", "", run.resultVersion ? `v${run.resultVersion}` : formatRunStatus(run.status)));
      top.append(status);
      top.append(node("time", "", formatListTime(run.completedAt || run.startedAt)));
      resultButton.append(top);
      resultButton.append(node("span", "hotel-run-meta", `${run.processor?.processorVersion || "—"} · ${run.processor?.model || "未记录模型"}`));
      if (run.errorMessage) resultButton.append(node("span", "hotel-run-error", run.errorMessage));
      resultButton.addEventListener("click", async () => {
        if (run.resultVersion) {
          await loadHotelResult(run.resultVersion);
        } else {
          await loadHotelRunConversation(run.runId);
        }
      });
      row.append(resultButton);

      const debug = node("button", "hotel-run-debug");
      debug.type = "button";
      debug.title = "查看调试会话";
      debug.setAttribute("aria-label", `查看运行 ${run.runId} 的调试会话`);
      debug.append(icon("messages-square"));
      debug.addEventListener("click", () => loadHotelRunConversation(run.runId));
      row.append(debug);
      list.append(row);
    });
    return list;
  }

  function renderConversationList() {
    elements.conversationList.replaceChildren();
    elements.conversationCount.textContent = String(state.conversations.length);

    if (!state.conversations.length) {
      const empty = node("div", "list-empty");
      empty.textContent = elements.conversationSearch.value.trim()
        ? "没有匹配的会话"
        : "暂无会话记录";
      elements.conversationList.append(empty);
      return;
    }

    for (const conversation of state.conversations) {
      const button = node("button", "conversation-item");
      button.type = "button";
      if (conversation.conversationId === state.current?.conversationId) button.classList.add("active");
      button.addEventListener("click", () => loadConversation(conversation.conversationId));

      button.append(node("span", "conversation-item-title", conversation.title));
      button.append(node("span", "conversation-item-preview", conversation.lastMessage || "暂无消息"));
      const meta = node("span", "conversation-item-meta");
      meta.append(node("span", "", `${conversation.agentName} · ${conversation.messageCount} 条`));
      meta.append(node("time", "", formatListTime(conversation.updatedAt)));
      button.append(meta);
      elements.conversationList.append(button);
    }
  }

  function renderMessages() {
    elements.messages.replaceChildren();
    if (!state.viewMessages.length) {
      const empty = node("div", "empty-state");
      const graphic = node("div", "empty-state-icon");
      graphic.append(icon("bot"));
      empty.append(graphic);
      empty.append(node("strong", "", "开始一次 Agent 运行"));
      empty.append(node("span", "", "选择 Agent 和搜索能力，然后发送消息。运行记录会自动保存到独立会话文件夹。"));
      elements.messages.append(empty);
      refreshIcons();
      return;
    }

    state.viewMessages.forEach((message, index) => {
      const article = node("article", `message ${message.role || "system"}`);
      const avatar = node("div", "message-avatar");
      avatar.append(icon(message.role === "assistant" ? "bot" : message.role === "user" ? "user" : "triangle-alert"));
      article.append(avatar);

      const body = node("div", "message-body");
      const meta = node("div", "message-meta");
      meta.append(node("strong", "", message.role === "assistant" ? "MAF Agent" : message.role === "user" ? "你" : "运行错误"));
      if (message.createdAt) {
        const time = node("time", "", formatMessageTime(message.createdAt));
        time.dateTime = message.createdAt;
        meta.append(time);
      }
      if (message.webSearchEnabled) meta.append(node("span", "message-badge", "Web Search"));
      body.append(meta);

      const content = node("div", "message-text", message.text || (message.streaming ? "正在生成" : ""));
      if (message.streaming) content.classList.add("typing-cursor");
      body.append(content);
      article.append(body);
      article.addEventListener("click", () => {
        if (message.role !== "assistant") return;
        state.debugMessage = message;
        state.lastRaw = message;
        renderInspector();
        refreshIcons();
      });
      elements.messages.append(article);

      if (index === state.viewMessages.length - 1) {
        requestAnimationFrame(() => {
          elements.messages.scrollTop = elements.messages.scrollHeight;
        });
      }
    });
    refreshIcons();
  }

  function renderInspector() {
    const message = state.debugMessage;
    elements.inspectorSubtitle.textContent = message ? "已选择 Assistant 响应" : "未选择响应";
    renderSources(message);
    renderUsage(message);
    renderRaw();
  }

  function renderSources(message) {
    elements.sourcesPanel.replaceChildren();
    const sources = message?.sources || [];
    const queries = message?.searchQueries || [];
    if (!sources.length && !queries.length) {
      elements.sourcesPanel.append(node("div", "panel-empty", "该响应没有 Web Search 查询或来源。"));
      return;
    }

    if (queries.length) {
      const section = node("div", "debug-section");
      section.append(node("h3", "", `搜索词 ${queries.length}`));
      queries.forEach(query => section.append(node("div", "query-item", query)));
      elements.sourcesPanel.append(section);
    }

    if (sources.length) {
      const section = node("div", "debug-section");
      section.append(node("h3", "", `引用来源 ${sources.length}`));
      for (const source of sources) {
        const link = node("a", "source-item");
        link.href = source.url;
        link.target = "_blank";
        link.rel = "noreferrer noopener";
        link.append(node("strong", "", source.title || source.url));
        link.append(node("span", "", source.snippet || source.url));
        section.append(link);
      }
      elements.sourcesPanel.append(section);
    }
  }

  function renderUsage(message) {
    elements.usagePanel.replaceChildren();
    const usage = message?.usage;
    if (!usage && !message?.processor) {
      elements.usagePanel.append(node("div", "panel-empty", "该响应没有返回 Token 用量。"));
      return;
    }

    if (usage) {
      const grid = node("div", "usage-grid");
      const metrics = [
        ["输入 Tokens", usage.inputTokens],
        ["输出 Tokens", usage.outputTokens],
        ["总 Tokens", usage.totalTokens],
        ["推理 Tokens", usage.reasoningTokens]
      ];
      for (const [label, value] of metrics) {
        const item = node("div", "usage-item");
        item.append(node("span", "", label));
        item.append(node("strong", "", formatNumber(value)));
        grid.append(item);
      }
      elements.usagePanel.append(grid);
    }

    const metadata = node("div", "debug-section");
    metadata.style.marginTop = "20px";
    metadata.append(node("h3", "", "响应元数据"));
    const list = node("div", "metadata-list");
    const processor = message.processor;
    [
      ["responseId", message.responseId],
      ["finishReason", message.finishReason],
      ["provider", message.provider || processor?.provider],
      ["model", message.model || processor?.model],
      ["processor", processor?.processorVersion],
      ["prompt", processor?.promptVersion],
      ["resultSchema", processor?.resultSchemaVersion]
    ].forEach(([label, value]) => {
      const row = node("div", "metadata-row");
      row.append(node("span", "", label));
      row.append(node("span", "", value || "—"));
      list.append(row);
    });
    metadata.append(list);
    elements.usagePanel.append(metadata);
  }

  function renderRaw() {
    elements.rawPanel.replaceChildren();
    if (!state.lastRaw) {
      elements.rawPanel.append(node("div", "panel-empty", "选择会话或 Assistant 响应后查看原始 JSON。"));
      return;
    }
    elements.rawPanel.append(node("pre", "raw-json", JSON.stringify(state.lastRaw, null, 2)));
  }

  function getHotelBusinessEntries() {
    const data = state.hotelBusinessData || {};
    const entries = [];
    if (data.databaseHotel) {
      const hotel = data.databaseHotel;
      entries.push({
        key: `database-${hotel.hotelId}`,
        sourceType: "database",
        sourceLabel: "DB 酒店",
        sourceDetail: `OTAHotelInfo #${hotel.hotelId}`,
        names: [hotel.hotelName, hotel.hotelNameEnglish].filter(Boolean),
        countryCode: hotel.countryCode,
        phones: hotel.phones || [],
        phoneDetails: hotel.phoneDetails || [],
        addresses: [hotel.address, hotel.addressEnglish].filter(Boolean),
        latitude: hotel.latitude,
        longitude: hotel.longitude,
        starRating: hotel.starRating,
        updatedAt: hotel.updatedAt,
        status: "database"
      });
    }
    const appendDatabaseRecord = (record, sourceType, sourceLabel) => {
      entries.push({
        key: `${sourceType}-${record.supHotelId}-${record.matchEvidence?.matchResultId || "raw"}`,
        sourceType,
        sourceLabel,
        sourceDetail: `Sup ${record.supId} · ${record.sHotelCode}`,
        names: [record.hotelName, record.hotelNameEnglish].filter(Boolean),
        countryCode: record.countryCode,
        phones: record.phones || [],
        phoneDetails: record.phoneDetails || [],
        addresses: [record.address].filter(Boolean),
        latitude: record.latitude,
        longitude: record.longitude,
        starRating: record.starRating,
        updatedAt: record.updatedAt,
        matchEvidence: record.matchEvidence || null,
        status: sourceType === "new" ? "matched" : "mapped"
      });
    };
    (data.originalMatches || []).forEach(record => appendDatabaseRecord(record, "original", "原始匹配"));
    (data.newMatches || []).forEach(record => appendDatabaseRecord(record, "new", "新匹配"));

    (state.researchResult?.sites || []).forEach((site, index) => {
      entries.push({
        key: `search-confirmed-${index}`,
        sourceType: "search-confirmed",
        sourceLabel: "AI 已确认",
        sourceDetail: site.siteName,
        sourceUrl: site.siteUrl,
        names: [site.hotelNameChinese, site.hotelNameEnglish, site.hotelNameLocal, ...(site.aliases || [])].filter(Boolean),
        countryCode: state.researchResult?.databaseHotel?.countryCode,
        phones: [site.hotelPhone].filter(Boolean),
        addresses: [site.addressChinese, site.addressEnglish, site.addressLocal].filter(Boolean),
        latitude: site.latitude,
        longitude: site.longitude,
        starRating: site.starRating,
        status: "confirmed",
        verification: site.identityVerification || null
      });
    });

    (state.researchResult?.rejectedCandidates || []).forEach((candidate, index) => {
      entries.push({
        key: `search-candidate-${index}`,
        sourceType: "search-candidate",
        sourceLabel: "搜索线索",
        sourceDetail: candidate.siteName,
        sourceUrl: candidate.siteUrl,
        names: candidate.candidateNames || [],
        phones: [],
        addresses: [],
        latitude: null,
        longitude: null,
        starRating: null,
        status: candidate.identityVerification?.status || "uncertain",
        verification: candidate.identityVerification || null
      });
    });
    return entries;
  }

  function getPhoneOccurrences(entries = getHotelBusinessEntries()) {
    const occurrences = [];
    entries.forEach(entry => {
      const seen = new Set();
      const phoneValues = entry.phoneDetails?.length
        ? entry.phoneDetails
        : (entry.phones || []).map(value => ({ raw: value, value, isNormalized: false }));
      phoneValues.forEach((phone, index) => {
        const raw = normalizeEvidenceValue(phone.raw ?? phone.value);
        const value = normalizeEvidenceValue(phone.value ?? phone.raw);
        const key = `${raw.toLocaleLowerCase()}|${value.toLocaleLowerCase()}`;
        if (!raw || seen.has(key)) return;
        seen.add(key);
        occurrences.push({
          id: `${entry.key}:phone-${index}`,
          raw,
          value: value || raw,
          isNormalized: Boolean(phone.isNormalized),
          countryCode: entry.countryCode || state.researchResult?.databaseHotel?.countryCode || null,
          needsNormalization: entry.sourceType === "search-confirmed",
          entry
        });
      });
    });
    return occurrences;
  }

  async function loadHotelPhoneNormalizations(shouldRender = true) {
    const mtHotelCode = state.hotelCode;
    const occurrences = getPhoneOccurrences();
    const aiOccurrences = occurrences.filter(item => item.needsNormalization);
    const requestId = ++state.phoneNormalizationRequestId;
    state.phoneNormalizationResults = new Map();
    state.phoneNormalizationError = null;
    if (!aiOccurrences.length) {
      state.phoneNormalizationLoading = false;
      if (shouldRender) renderAll();
      return;
    }

    state.phoneNormalizationLoading = true;
    try {
      const response = await api("/api/hotel-research/phone-normalizations", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          items: aiOccurrences.map(item => ({
            id: item.id,
            raw: item.raw,
            countryCode: item.countryCode
          }))
        })
      });
      const result = await response.json();
      if (requestId !== state.phoneNormalizationRequestId || mtHotelCode !== state.hotelCode) return;
      state.phoneNormalizationResults = new Map((result.items || []).map(item => [item.id, item]));
    } catch (error) {
      if (requestId !== state.phoneNormalizationRequestId || mtHotelCode !== state.hotelCode) return;
      state.phoneNormalizationError = error.message;
    } finally {
      if (requestId !== state.phoneNormalizationRequestId || mtHotelCode !== state.hotelCode) return;
      state.phoneNormalizationLoading = false;
      if (shouldRender) renderAll();
    }
  }

  function getHotelBusinessCounts(entries = getHotelBusinessEntries()) {
    const count = sourceType => entries.filter(entry => entry.sourceType === sourceType).length;
    const searchConfirmed = count("search-confirmed");
    const searchCandidates = count("search-candidate");
    return {
      original: count("original"),
      newlyMatched: count("new"),
      searchClues: searchConfirmed + searchCandidates,
      aiConfirmed: searchConfirmed,
      totalEvidence: entries.length
    };
  }

  function normalizeEvidenceValue(value) {
    return String(value || "").trim().replace(/\s+/g, " ");
  }

  function countEvidenceValues(entries, selector) {
    const values = new Map();
    entries.forEach(entry => {
      const uniqueInEntry = new Set();
      (selector(entry) || []).forEach(rawValue => {
        const value = normalizeEvidenceValue(rawValue);
        const key = value.toLocaleLowerCase();
        if (!value || uniqueInEntry.has(key)) return;
        uniqueInEntry.add(key);
        const current = values.get(key) || { value, count: 0, sources: new Set() };
        current.count += 1;
        current.sources.add(entry.sourceLabel);
        values.set(key, current);
      });
    });
    return [...values.values()]
      .sort((left, right) => right.count - left.count || left.value.localeCompare(right.value));
  }

  function groupPhoneEvidence(entries) {
    const groups = new Map();
    getPhoneOccurrences(entries).forEach(occurrence => {
      const normalized = state.phoneNormalizationResults.get(occurrence.id);
      const values = occurrence.needsNormalization
        ? normalized?.values?.length
          ? normalized.values
          : [{ raw: occurrence.raw, isValid: false, error: "normalization_unavailable" }]
        : [{
            raw: occurrence.raw,
            e164: occurrence.isNormalized ? occurrence.value : null,
            isValid: occurrence.isNormalized,
            fromDatabase: true
          }];
      values.forEach(value => {
        const formatted = value.isValid && value.e164
          ? `${value.e164}${value.extension ? ` ext. ${value.extension}` : ""}`
          : occurrence.raw;
        const key = value.isValid && value.e164
          ? `formatted:${value.e164}:${value.extension || ""}`
          : `raw:${normalizeEvidenceValue(formatted).toLocaleLowerCase()}`;
        const group = groups.get(key) || {
          value: formatted,
          isValid: Boolean(value.isValid && value.e164),
          sources: new Set(),
          rawItems: [],
          seenItems: new Set()
        };
        const rawItemKey = `${occurrence.id}:${key}`;
        if (!group.seenItems.has(rawItemKey)) {
          group.seenItems.add(rawItemKey);
          group.sources.add(occurrence.entry.sourceLabel);
          group.rawItems.push({
            input: occurrence.raw,
            parsedRaw: value.raw,
            sourceLabel: occurrence.entry.sourceLabel,
            sourceDetail: occurrence.entry.sourceDetail,
            sourceUrl: occurrence.entry.sourceUrl,
            hotelName: occurrence.entry.names?.[0],
            isValid: Boolean(value.isValid && value.e164),
            fromDatabase: Boolean(value.fromDatabase),
            error: value.error
          });
        }
        groups.set(key, group);
      });
    });
    return [...groups.values()]
      .map(group => ({ ...group, count: group.rawItems.length }))
      .sort((left, right) => right.count - left.count || left.value.localeCompare(right.value));
  }

  function distanceMeters(left, right) {
    const earthRadius = 6371000;
    const radians = value => value * Math.PI / 180;
    const latitudeDelta = radians(right.latitude - left.latitude);
    const longitudeDelta = radians(right.longitude - left.longitude);
    const a = Math.sin(latitudeDelta / 2) ** 2
      + Math.cos(radians(left.latitude)) * Math.cos(radians(right.latitude))
      * Math.sin(longitudeDelta / 2) ** 2;
    return 2 * earthRadius * Math.asin(Math.sqrt(a));
  }

  function hasValidCoordinates(entry) {
    const latitude = Number(entry.latitude);
    const longitude = Number(entry.longitude);
    return Number.isFinite(latitude)
      && Number.isFinite(longitude)
      && latitude !== 0
      && longitude !== 0;
  }

  function clusterCoordinates(entries) {
    const points = entries
      .filter(hasValidCoordinates)
      .map(entry => ({
        latitude: Number(entry.latitude),
        longitude: Number(entry.longitude),
        sourceLabel: entry.sourceLabel,
        sourceDetail: entry.sourceDetail,
        name: entry.names[0] || entry.sourceDetail
      }));
    const clusters = [];
    points.forEach(point => {
      const cluster = clusters.find(group => group.points.every(existing => distanceMeters(existing, point) <= 200));
      if (cluster) cluster.points.push(point);
      else clusters.push({ points: [point] });
    });
    return clusters
      .map((cluster, index) => ({
        index: index + 1,
        count: cluster.points.length,
        latitude: cluster.points.reduce((sum, point) => sum + point.latitude, 0) / cluster.points.length,
        longitude: cluster.points.reduce((sum, point) => sum + point.longitude, 0) / cluster.points.length,
        points: cluster.points
      }))
      .sort((left, right) => right.count - left.count);
  }

  function createBusinessPageHeader(title, description, iconName) {
    const header = node("header", "business-page-header");
    const heading = node("div", "");
    const titleNode = node("h2", "");
    titleNode.append(icon(iconName));
    titleNode.append(node("span", "", title));
    heading.append(titleNode);
    heading.append(node("p", "", description));
    header.append(heading);
    header.append(node("code", "", state.hotelCode));
    return header;
  }

  function createFrequencySection(title, values, iconName) {
    const section = node("section", "evidence-frequency-section");
    const header = node("header", "evidence-section-heading");
    const heading = node("h3", "");
    heading.append(icon(iconName));
    heading.append(node("span", "", title));
    header.append(heading);
    header.append(node("span", "", `${values.length} 个不同值`));
    section.append(header);
    const list = node("div", "evidence-frequency-list");
    if (!values.length) {
      list.append(node("div", "evidence-list-empty", "暂无数据"));
    } else {
      values.forEach(item => {
        const row = node("div", "evidence-frequency-row");
        const value = node("div", "");
        value.append(node("strong", "", item.value));
        value.append(node("small", "", [...item.sources].join(" · ")));
        row.append(value);
        row.append(node("span", "evidence-count", `${item.count} 次`));
        list.append(row);
      });
    }
    section.append(list);
    return section;
  }

  function createPhoneFrequencySection(entries) {
    const groups = groupPhoneEvidence(entries);
    const section = node("section", "evidence-frequency-section phone-frequency-section");
    const header = node("header", "evidence-section-heading");
    const heading = node("h3", "");
    heading.append(icon("phone"));
    heading.append(node("span", "", "酒店电话"));
    header.append(heading);
    const status = state.phoneNormalizationLoading
      ? "正在格式化 AI 电话"
      : state.phoneNormalizationError
        ? "AI 电话格式化失败，已使用原始值"
        : `${groups.length} 个号码分组`;
    header.append(node("span", state.phoneNormalizationError ? "normalization-error" : "", status));
    section.append(header);

    const list = node("div", "evidence-frequency-list");
    if (!groups.length) {
      list.append(node("div", "evidence-list-empty", "暂无数据"));
    } else {
      groups.forEach(group => {
        const details = node("details", "phone-frequency-group");
        const summary = node("summary", "");
        const value = node("div", "");
        value.append(node("strong", "", group.value));
        value.append(node(
          "small",
          "",
          `${group.isValid ? "标准号码" : "未标准化，使用原始值"} · ${[...group.sources].join(" · ")}`));
        summary.append(value);
        const summaryMeta = node("span", "evidence-summary-meta");
        summaryMeta.append(node("span", "evidence-count", `${group.count} 条`));
        summaryMeta.append(icon("chevron-down"));
        summary.append(summaryMeta);
        details.append(summary);

        const rawList = node("div", "phone-raw-list");
        group.rawItems.forEach(item => {
          const rawItem = node("div", "phone-raw-item");
          const rawValue = node("div", "phone-raw-value");
          rawValue.append(node("strong", "", item.input));
          if (item.parsedRaw && item.parsedRaw !== item.input) {
            rawValue.append(node("small", "", `识别片段：${item.parsedRaw}`));
          }
          rawItem.append(rawValue);

          const source = node("div", "phone-raw-source");
          source.append(node(
            "span",
            `data-source-badge ${item.sourceLabel === "AI 已确认" ? "search-confirmed" : ""}`,
            item.sourceLabel));
          if (item.sourceUrl) {
            const link = node("a", "", item.sourceDetail || item.sourceUrl);
            link.href = item.sourceUrl;
            link.target = "_blank";
            link.rel = "noreferrer noopener";
            source.append(link);
          } else {
            source.append(node("span", "", item.sourceDetail || "来源未记录"));
          }
          if (item.hotelName) source.append(node("small", "", item.hotelName));
          rawItem.append(source);
          rawList.append(rawItem);
        });
        details.append(rawList);
        list.append(details);
      });
    }
    section.append(list);
    return section;
  }

  function renderHotelDataDashboard(entries) {
    const counts = getHotelBusinessCounts(entries);
    elements.hotelResearchContent.append(createBusinessPageHeader(
      "数据面板",
      "按当前数据库记录和所选搜索版本在浏览器中即时统计",
      "chart-no-axes-column"));

    const metrics = node("section", "business-metric-grid");
    [
      ["database", "原始匹配", counts.original, "SupHotelInfo"],
      ["git-merge", "新匹配", counts.newlyMatched, "HotelMatchResult"],
      ["search", "搜索酒店线索", counts.searchClues, `搜索 v${state.selectedVersion || "—"}`],
      ["badge-check", "AI 确认", counts.aiConfirmed, "身份确认通过"]
    ].forEach(([iconName, label, value, note]) => {
      const metric = node("article", "business-metric");
      metric.append(icon(iconName));
      const content = node("div", "");
      content.append(node("span", "", label));
      content.append(node("strong", "", String(value)));
      content.append(node("small", "", note));
      metric.append(content);
      metrics.append(metric);
    });
    elements.hotelResearchContent.append(metrics);

    const frequencyGrid = node("div", "evidence-frequency-grid");
    frequencyGrid.append(createFrequencySection(
      "酒店名称",
      countEvidenceValues(entries, entry => entry.names),
      "building-2"));
    frequencyGrid.append(createPhoneFrequencySection(entries));
    frequencyGrid.append(createFrequencySection(
      "酒店地址",
      countEvidenceValues(entries, entry => entry.addresses),
      "map-pin"));
    frequencyGrid.append(createCoordinateSection(clusterCoordinates(entries)));
    elements.hotelResearchContent.append(frequencyGrid);
  }

  function createCoordinateSection(clusters) {
    const section = node("section", "evidence-frequency-section coordinate-section");
    const header = node("header", "evidence-section-heading");
    const heading = node("h3", "");
    heading.append(icon("locate-fixed"));
    heading.append(node("span", "", "经纬度群组"));
    header.append(heading);
    header.append(node("span", "", `${clusters.length} 群 · 群内距离不超过 200 米`));
    section.append(header);
    const list = node("div", "evidence-frequency-list");
    if (!clusters.length) {
      list.append(node("div", "evidence-list-empty", "暂无经纬度"));
    } else {
      clusters.forEach(cluster => {
        const details = node("details", "coordinate-cluster");
        const summary = node("summary", "");
        const label = node("div", "");
        label.append(node("strong", "", `坐标群 ${cluster.index}`));
        label.append(node("small", "", `${cluster.latitude.toFixed(6)}, ${cluster.longitude.toFixed(6)}`));
        summary.append(label);
        summary.append(node("span", "evidence-count", `${cluster.count} 次`));
        details.append(summary);
        const points = node("div", "coordinate-point-list");
        cluster.points.forEach(point => {
          points.append(node("div", "", `${point.latitude.toFixed(6)}, ${point.longitude.toFixed(6)} · ${point.sourceLabel} · ${point.name}`));
        });
        details.append(points);
        list.append(details);
      });
    }
    section.append(list);
    return section;
  }

  function renderHotelDataSummary(entries) {
    elements.hotelResearchContent.append(createBusinessPageHeader(
      "整体数据汇总",
      "逐条查看原始匹配、新匹配和搜索线索",
      "table-properties"));

    const filters = [
      ["all", "全部"],
      ["database", "DB 酒店"],
      ["original", "原始匹配"],
      ["new", "新匹配"],
      ["search-confirmed", "AI 已确认"],
      ["search-candidate", "其他搜索线索"]
    ];
    const toolbar = node("div", "business-data-toolbar");
    const switcher = node("div", "business-data-filter");
    switcher.setAttribute("role", "tablist");
    filters.forEach(([id, label]) => {
      const count = id === "all" ? entries.length : entries.filter(entry => entry.sourceType === id).length;
      const button = node("button", state.hotelBusinessDataFilter === id ? "active" : "", `${label} ${count}`);
      button.type = "button";
      button.setAttribute("role", "tab");
      button.setAttribute("aria-selected", String(state.hotelBusinessDataFilter === id));
      button.addEventListener("click", () => {
        state.hotelBusinessDataFilter = id;
        renderHotelResearch();
        refreshIcons();
      });
      switcher.append(button);
    });
    toolbar.append(switcher);
    elements.hotelResearchContent.append(toolbar);

    const filtered = state.hotelBusinessDataFilter === "all"
      ? entries
      : entries.filter(entry => entry.sourceType === state.hotelBusinessDataFilter);
    const tableWrap = node("div", "business-data-table-wrap");
    const table = node("table", "business-data-table");
    const thead = document.createElement("thead");
    const header = document.createElement("tr");
    ["来源", "酒店名称", "电话", "地址", "经纬度", "匹配 / 核验信息"].forEach(label => header.append(node("th", "", label)));
    thead.append(header);
    table.append(thead);
    const tbody = document.createElement("tbody");
    filtered.forEach(entry => tbody.append(createBusinessDataRow(entry)));
    if (!filtered.length) {
      const row = document.createElement("tr");
      const cell = node("td", "business-data-empty", "当前分类暂无记录");
      cell.colSpan = 6;
      row.append(cell);
      tbody.append(row);
    }
    table.append(tbody);
    tableWrap.append(table);
    elements.hotelResearchContent.append(tableWrap);
  }

  function createBusinessDataRow(entry) {
    const row = document.createElement("tr");
    const sourceCell = document.createElement("td");
    sourceCell.dataset.label = "来源";
    sourceCell.append(node("span", `data-source-badge ${entry.sourceType}`, entry.sourceLabel));
    if (entry.sourceUrl) {
      const link = node("a", "data-source-link", entry.sourceDetail);
      link.href = entry.sourceUrl;
      link.target = "_blank";
      link.rel = "noreferrer noopener";
      sourceCell.append(link);
    } else {
      sourceCell.append(node("small", "", entry.sourceDetail));
    }
    row.append(sourceCell);
    row.append(createBusinessValuesCell("酒店名称", entry.names));
    row.append(createBusinessValuesCell("电话", entry.phones));
    row.append(createBusinessValuesCell("地址", entry.addresses));
    row.append(createBusinessValuesCell(
      "经纬度",
      hasValidCoordinates(entry)
        ? [`${Number(entry.latitude).toFixed(6)}, ${Number(entry.longitude).toFixed(6)}`]
        : []));
    const evidenceCell = document.createElement("td");
    evidenceCell.dataset.label = "匹配 / 核验信息";
    if (entry.verification) {
      evidenceCell.append(node("strong", "", entry.verification.summary || formatRunStatus(entry.status)));
    } else if (entry.matchEvidence) {
      evidenceCell.append(node("strong", "", `结果 #${entry.matchEvidence.matchResultId}`));
      evidenceCell.append(node("small", "", `坐标距离 ${entry.matchEvidence.distanceMeters} 米`));
      [entry.matchEvidence.nameEvidenceValue, entry.matchEvidence.addressEvidenceValue, entry.matchEvidence.phoneEvidenceValue]
        .filter(Boolean)
        .forEach(value => evidenceCell.append(node("span", "", value)));
    } else {
      evidenceCell.append(node("span", "empty-value", "—"));
    }
    row.append(evidenceCell);
    return row;
  }

  function createBusinessValuesCell(label, values) {
    const cell = document.createElement("td");
    cell.dataset.label = label;
    const unique = [...new Set((values || []).map(normalizeEvidenceValue).filter(Boolean))];
    if (!unique.length) cell.append(node("span", "empty-value", "—"));
    else unique.forEach(value => cell.append(node("span", "business-cell-value", value)));
    return cell;
  }

  function renderHotelResearch() {
    elements.hotelResearchContent.replaceChildren();
    elements.hotelResearchButton.disabled = state.researchLoading;
    elements.hotelResearchForm.hidden = state.hotelBusinessView !== "research";

    if (state.hotelBusinessView !== "research") {
      if (!state.hotelCode) {
        const empty = node("div", "research-empty");
        empty.append(icon("building-2"));
        empty.append(node("strong", "", "输入完整 MTHotelCode 打开酒店工作区"));
        elements.hotelResearchContent.append(empty);
        return;
      }
      if (state.hotelBusinessDataLoading) {
        const loading = node("div", "research-loading");
        loading.append(icon("loader-circle"));
        loading.append(node("strong", "", "正在读取酒店业务数据"));
        elements.hotelResearchContent.append(loading);
        return;
      }
      if (state.hotelBusinessDataError) {
        const error = node("div", "research-error");
        error.append(icon("circle-alert"));
        error.append(node("strong", "", "酒店业务数据读取失败"));
        error.append(node("span", "", state.hotelBusinessDataError));
        elements.hotelResearchContent.append(error);
        return;
      }
      const entries = getHotelBusinessEntries();
      if (state.hotelBusinessView === "summary") renderHotelDataSummary(entries);
      else renderHotelDataDashboard(entries);
      return;
    }

    if (state.researchLoading) {
      const loading = node("div", "research-loading");
      loading.append(icon("loader-circle"));
      loading.append(node("strong", "", "正在读取 RoomMapping 并搜索公开站点"));
      elements.hotelResearchContent.append(loading);
      return;
    }

    if (!state.hotelCode) {
      const empty = node("div", "research-empty");
      empty.append(icon("building-2"));
      empty.append(node("strong", "", "输入完整 MTHotelCode 打开酒店工作区"));
      elements.hotelResearchContent.append(empty);
      return;
    }

    if (state.researchError) {
      const error = node("div", "research-error");
      error.append(icon("circle-alert"));
      error.append(node("strong", "", "最近一次搜索失败"));
      error.append(node("span", "", state.researchError));
      elements.hotelResearchContent.append(error);
    }

    if (!state.researchResult) {
      const empty = node("div", "research-empty compact");
      empty.append(icon("file-search"));
      empty.append(node("strong", "", state.hotelDetail ? "暂无成功搜索结果" : "这是一个新的酒店工作区"));
      elements.hotelResearchContent.append(empty);
      return;
    }

    const result = state.researchResult;
    const rejectedCount = result.rejectedCandidates?.length || 0;
    const header = node("header", "research-result-header");
    const heading = node("div", "");
    heading.append(node("h2", "", result.databaseHotel.hotelName));
    heading.append(node("p", "", `${result.mtHotelCode || result.mthotelCode} · v${result.resultVersion} · ${result.sites.length} 个采用站点 · 拒绝 ${rejectedCount} 个候选 · ${formatMessageTime(result.researchedAt)}`));
    header.append(heading);
    const debugButton = node("button", "debug-conversation-button");
    debugButton.type = "button";
    debugButton.append(icon("messages-square"));
    debugButton.append(node("span", "", "调试会话"));
    debugButton.addEventListener("click", () => loadHotelRunConversation(result.runId));
    header.append(debugButton);
    elements.hotelResearchContent.append(header);

    elements.hotelResearchContent.append(createResearchView(result));

    if (rejectedCount) {
      elements.hotelResearchContent.append(createRejectedCandidates(result.rejectedCandidates));
    }
  }

  async function loadHotelRunConversation(runId) {
    if (!state.hotelCode || !runId) return;
    try {
      const response = await api(`/api/hotel-business/${encodeURIComponent(state.hotelCode)}/operations/hotel-research/runs/${encodeURIComponent(runId)}/conversation`);
      const payload = await response.json();
      state.current = payload.conversation;
      state.mode = "hotelDebug";
      state.viewMessages = [...payload.conversation.messages];
      state.debugMessage = [...state.viewMessages].reverse().find(message => message.role === "assistant") || null;
      state.lastRaw = payload;
      renderAll();
      closeDrawers();
    } catch (error) {
      showToast(`无法读取调试会话：${error.message}`);
    }
  }

  function returnToHotelBusiness() {
    if (!state.hotelCode) return;
    state.mode = "hotelBusiness";
    state.current = null;
    state.viewMessages = [];
    setResearchInspector(state.researchResult);
    renderAll();
    closeDrawers();
  }

  function createResearchView(result) {
    const activeComponent = researchViewComponents.find(component => component.id === state.researchViewMode)
      || researchViewComponents[0];
    state.researchViewMode = activeComponent.id;

    const view = node("section", "research-view-component");
    const toolbar = node("div", "research-view-toolbar");
    toolbar.append(node("span", "research-view-label", "结果视图"));
    const switcher = node("div", "research-view-switcher");
    switcher.setAttribute("role", "tablist");
    researchViewComponents.forEach(component => {
      const button = node("button", component.id === activeComponent.id ? "active" : "");
      button.type = "button";
      button.setAttribute("role", "tab");
      button.setAttribute("aria-selected", String(component.id === activeComponent.id));
      button.title = component.label;
      button.append(icon(component.icon));
      button.append(node("span", "", component.label));
      button.addEventListener("click", () => {
        if (state.researchViewMode === component.id) return;
        state.researchViewMode = component.id;
        localStorage.setItem("maf.hotelResearch.viewMode", component.id);
        renderHotelResearch();
        refreshIcons();
      });
      switcher.append(button);
    });
    toolbar.append(switcher);
    view.append(toolbar);

    const body = node("div", `research-view-body view-${activeComponent.id}`);
    body.append(activeComponent.render(result));
    view.append(body);
    return view;
  }

  function createResearchComparisonTable(databaseHotel, sites) {
    const sources = buildResearchSources(databaseHotel, sites);
    const section = node("section", "research-comparison");
    const heading = node("div", "comparison-heading");
    const title = node("h3", "");
    title.append(icon("table-2"));
    title.append(node("span", "", "酒店与房型对照"));
    heading.append(title);
    heading.append(node("span", "", `${sources.length} 组数据`));
    section.append(heading);

    const table = node("table", "comparison-table");
    const columns = document.createElement("colgroup");
    columns.append(node("col", "comparison-hotel-column"));
    columns.append(node("col", "comparison-room-column"));
    table.append(columns);

    const thead = document.createElement("thead");
    const headerRow = document.createElement("tr");
    headerRow.append(node("th", "", "酒店信息"));
    headerRow.append(node("th", "", "房型信息"));
    thead.append(headerRow);
    table.append(thead);

    const tbody = document.createElement("tbody");
    sources.forEach(source => tbody.append(createComparisonRow(source)));
    table.append(tbody);
    section.append(table);
    return section;
  }

  function buildResearchSources(databaseHotel, sites) {
    return [
      {
        rowKey: "database",
        sourceName: "RoomMapping · OTAHotelInfo",
        sourceKind: "DB",
        sourceUrl: databaseHotel.officialWebsite,
        names: [databaseHotel.hotelName, databaseHotel.hotelNameEnglish],
        fields: [
          ["酒店 ID", databaseHotel.hotelId],
          ["中文/主名称", databaseHotel.hotelName],
          ["英文名称", databaseHotel.hotelNameEnglish],
          ["地址", databaseHotel.address],
          ["英文地址", databaseHotel.addressEnglish],
          ["国家 / 城市", [databaseHotel.countryName, databaseHotel.cityName].filter(Boolean).join(" / ")],
          ["坐标", formatCoordinates(databaseHotel.latitude, databaseHotel.longitude)],
          ["星级", databaseHotel.starRating],
          ["酒店电话", (databaseHotel.hotelPhones || []).join(" / ")],
          ["官网", databaseHotel.officialWebsite]
        ],
        rooms: databaseHotel.rooms || [],
        databaseRooms: true
      },
      ...sites.map((site, index) => ({
        rowKey: `site-${index}`,
        sourceName: site.siteName,
        sourceKind: "搜索",
        sourceUrl: site.siteUrl,
        names: [site.hotelNameChinese, site.hotelNameEnglish, site.hotelNameLocal],
        fields: [
          ["酒店名 · 中文", site.hotelNameChinese],
          ["Hotel name · English", site.hotelNameEnglish],
          ["酒店名 · 当地语言", site.hotelNameLocal],
          ["地址 · 中文", site.addressChinese],
          ["Address · English", site.addressEnglish],
          ["地址 · 当地语言", site.addressLocal],
          ["坐标", formatCoordinates(site.latitude, site.longitude)],
          ["星级", site.starRating],
          ["酒店电话", site.hotelPhone]
        ],
        aliases: site.aliases || [],
        sourceUrls: site.sourceUrls || [],
        verification: site.identityVerification,
        rooms: site.rooms || [],
        databaseRooms: false
      }))
    ];
  }

  function createComparisonRow(data) {
    const row = document.createElement("tr");
    row.className = data.databaseRooms ? "comparison-db-row" : "comparison-search-row";

    const hotelCell = document.createElement("td");
    hotelCell.dataset.label = "酒店信息";
    hotelCell.append(createResearchSourceLabel(data));

    const primaryName = data.names.find(Boolean) || data.sourceName;
    const roomTargetId = `comparison-rooms-${data.rowKey}`;
    const nameButton = node("button", "hotel-name-jump");
    nameButton.type = "button";
    nameButton.title = "定位到对应房型";
    nameButton.append(node("span", "", primaryName));
    nameButton.append(icon("arrow-right"));
    nameButton.addEventListener("click", () => focusRoomTarget(roomTargetId));
    hotelCell.append(nameButton);
    hotelCell.append(createComparisonFields(data.fields));

    if (data.aliases?.length) {
      const aliases = node("div", "site-aliases");
      data.aliases.forEach(alias => aliases.append(node("span", "", alias)));
      hotelCell.append(aliases);
    }
    if (data.verification) hotelCell.append(createIdentityVerification(data.verification));
    if (data.sourceUrls?.length) hotelCell.append(createSourceLinks(data.sourceUrls));
    row.append(hotelCell);

    const roomCell = document.createElement("td");
    roomCell.id = roomTargetId;
    roomCell.className = "comparison-room-cell";
    roomCell.dataset.label = "房型信息";
    const roomHeading = node("div", "comparison-room-heading");
    roomHeading.append(icon("bed-double"));
    roomHeading.append(node("strong", "", `${data.rooms.length} 个房型`));
    roomCell.append(roomHeading);
    if (data.rooms.length) {
      roomCell.append(createRoomList(data.rooms, data.databaseRooms));
    } else {
      roomCell.append(node("div", "comparison-no-rooms", "该来源未取得房型信息"));
    }
    row.append(roomCell);
    return row;
  }

  function createResearchSourceLabel(data) {
    const source = node("div", "comparison-source");
    source.append(node("span", data.databaseRooms ? "source-kind database" : "source-kind search", data.sourceKind));
    if (data.sourceUrl) {
      const sourceLink = node("a", "", data.sourceName);
      sourceLink.href = data.sourceUrl;
      sourceLink.target = "_blank";
      sourceLink.rel = "noreferrer noopener";
      sourceLink.title = data.sourceUrl;
      sourceLink.append(icon("external-link"));
      source.append(sourceLink);
    } else {
      source.append(node("strong", "", data.sourceName));
    }
    if (!data.databaseRooms) {
      source.append(data.verification?.status === "confirmed"
        ? node("span", "identity-badge confirmed", "AI 已确认")
        : node("span", "identity-badge unverified", "旧版未核验"));
    }
    return source;
  }

  function createComparisonFields(fields) {
    const list = node("dl", "comparison-fields");
    fields.forEach(([label, value]) => {
      const item = node("div", "");
      item.append(node("dt", "", label));
      item.append(node("dd", "", formatValue(value)));
      list.append(item);
    });
    return list;
  }

  function createSourceLinks(sourceUrls) {
    const sources = node("div", "source-links");
    sourceUrls.forEach((sourceUrl, index) => {
      const link = node("a", "", `来源 ${index + 1}`);
      link.href = sourceUrl;
      link.target = "_blank";
      link.rel = "noreferrer noopener";
      link.title = sourceUrl;
      sources.append(link);
    });
    return sources;
  }

  function focusRoomTarget(targetId) {
    const target = document.getElementById(targetId);
    if (!target) return;
    target.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
    target.classList.remove("room-target-active");
    requestAnimationFrame(() => target.classList.add("room-target-active"));
    setTimeout(() => target.classList.remove("room-target-active"), 1600);
  }

  function createSourceCardsView(databaseHotel, sites) {
    const view = node("section", "source-cards-view");
    buildResearchSources(databaseHotel, sites).forEach(data => {
      const article = node("article", "site-card source-view-card");
      const header = node("header", "site-card-header");
      header.append(createResearchSourceLabel(data));
      article.append(header);

      const body = node("div", "site-card-body");
      const roomTargetId = `source-rooms-${data.rowKey}`;
      const nameButton = node("button", "hotel-name-jump");
      nameButton.type = "button";
      nameButton.title = "定位到对应房型";
      nameButton.append(node("span", "", data.names.find(Boolean) || data.sourceName));
      nameButton.append(icon("arrow-down"));
      nameButton.addEventListener("click", () => focusRoomTarget(roomTargetId));
      body.append(nameButton);
      body.append(createComparisonFields(data.fields));

      if (data.aliases?.length) {
        const aliases = node("div", "site-aliases");
        data.aliases.forEach(alias => aliases.append(node("span", "", alias)));
        body.append(aliases);
      }
      if (data.verification) body.append(createIdentityVerification(data.verification));
      if (data.sourceUrls?.length) body.append(createSourceLinks(data.sourceUrls));

      const rooms = node("div", "source-card-rooms");
      rooms.id = roomTargetId;
      const roomHeading = node("div", "comparison-room-heading");
      roomHeading.append(icon("bed-double"));
      roomHeading.append(node("strong", "", `${data.rooms.length} 个房型`));
      rooms.append(roomHeading);
      rooms.append(data.rooms.length
        ? createRoomList(data.rooms, data.databaseRooms)
        : node("div", "comparison-no-rooms", "该来源未取得房型信息"));
      body.append(rooms);
      article.append(body);
      view.append(article);
    });
    return view;
  }

  function createRoomOverview(databaseHotel, sites) {
    const view = node("section", "room-overview");
    const sources = buildResearchSources(databaseHotel, sites);
    const heading = node("div", "comparison-heading");
    const title = node("h3", "");
    title.append(icon("bed-double"));
    title.append(node("span", "", "各来源房型纵览"));
    heading.append(title);
    heading.append(node("span", "", `${sources.reduce((count, source) => count + source.rooms.length, 0)} 条房型`));
    view.append(heading);

    sources.forEach(data => {
      const group = node("section", "room-overview-group");
      const groupHeader = node("header", "room-overview-header");
      groupHeader.append(createResearchSourceLabel(data));
      groupHeader.append(node("strong", "", `${data.rooms.length} 个房型`));
      group.append(groupHeader);
      group.append(data.rooms.length
        ? createRoomList(data.rooms, data.databaseRooms)
        : node("div", "comparison-no-rooms", "该来源未取得房型信息"));
      view.append(group);
    });
    return view;
  }

  function createIdentityVerification(verification) {
    const block = node("div", `identity-verification ${verification.status || "uncertain"}`);
    const title = node("div", "identity-verification-title");
    title.append(icon(verification.status === "confirmed" ? "badge-check" : "shield-alert"));
    title.append(node("strong", "", verification.summary || "未提供身份核验摘要"));
    block.append(title);

    const evidence = node("div", "identity-evidence");
    (verification.matchedSignals || []).forEach(signal => {
      evidence.append(node("span", "", formatIdentitySignal(signal, verification.coordinateDistanceMeters)));
    });
    (verification.conflicts || []).forEach(conflict => evidence.append(node("span", "conflict", conflict)));
    if (evidence.childElementCount) block.append(evidence);
    return block;
  }

  function createRejectedCandidates(candidates) {
    const details = node("details", "rejected-candidates");
    const summary = node("summary", "");
    const label = node("span", "");
    label.append(icon("shield-x"));
    label.append(node("strong", "", `未通过身份核验的候选 ${candidates.length}`));
    summary.append(label);
    summary.append(icon("chevron-down"));
    details.append(summary);

    const list = node("div", "rejected-candidate-list");
    candidates.forEach(candidate => {
      const row = node("div", "rejected-candidate-row");
      const top = node("div", "rejected-candidate-top");
      const title = node("div", "");
      title.append(node("strong", "", candidate.siteName));
      title.append(node("span", "", (candidate.candidateNames || []).join(" / ") || "未取得候选名称"));
      top.append(title);
      const link = node("a", "", "查看候选页面");
      link.href = candidate.siteUrl;
      link.target = "_blank";
      link.rel = "noreferrer noopener";
      top.append(link);
      row.append(top);
      row.append(createIdentityVerification(candidate.identityVerification));
      list.append(row);
    });
    details.append(list);
    return details;
  }

  function formatIdentitySignal(signal, distanceMeters) {
    const labels = {
      exactName: "完整名称一致",
      exactAddress: "完整地址一致",
      exactPhone: "酒店电话一致",
      officialWebsiteHost: "官网域名一致",
      coordinatesWithin200m: distanceMeters === null || distanceMeters === undefined
        ? "坐标在 200 米内"
        : `坐标相距 ${distanceMeters} 米`
    };
    return labels[signal] || signal;
  }

  function createRoomList(rooms, databaseRooms) {
    const list = node("div", "room-list");
    rooms.forEach(room => {
      const row = node("div", "room-row");
      const nameFields = [
        room.roomNameChinese,
        room.roomNameEnglish,
        databaseRooms ? null : room.roomNameLocal
      ];
      if (!nameFields.some(value => value?.trim()) && room.roomName?.trim()) {
        nameFields.push(room.roomName);
      }

      const names = node("div", "room-name-fields");
      const seenNames = new Set();
      nameFields.forEach(value => {
        const normalized = value?.trim();
        const key = normalized?.toLocaleLowerCase();
        if (!normalized || seenNames.has(key)) return;
        seenNames.add(key);
        names.append(node("div", "room-name-value", normalized));
      });
      if (!names.childElementCount) names.append(node("div", "room-name-value empty", "—"));
      row.append(names);

      const attributeFields = [
        ["面积", formatRoomArea(room.area)],
        ["床型", formatRoomBedType(room.bedType)],
        ["禁烟", formatSmoking(room.isNonSmoking)],
        ["景观", databaseRooms ? room.view : room.viewEnglish]
      ];
      const attributes = node("div", "room-attribute-fields");
      attributeFields.forEach(([label, value]) => {
        const cell = node("div", "room-cell");
        cell.append(node("span", "", label));
        cell.append(node("span", "", formatValue(value)));
        attributes.append(cell);
      });
      row.append(attributes);

      if (!databaseRooms && (room.facilities?.length || room.offersAndServices?.length)) {
        const details = node("div", "room-cell room-details");
        details.append(node("span", "", "设施 / 优惠服务"));
        const tags = node("div", "room-tags");
        (room.facilities || []).forEach(value => tags.append(node("span", "room-tag", value)));
        (room.offersAndServices || []).forEach(value => tags.append(node("span", "room-tag offer", value)));
        details.append(tags);
        row.append(details);
      }
      list.append(row);
    });
    return list;
  }

  async function runHotelResearch(event) {
    event.preventDefault();
    const mtHotelCode = elements.mtHotelCodeInput.value.trim().toUpperCase();
    if (!mtHotelCode || state.researchLoading) return;

    state.researchLoading = true;
    state.researchError = null;
    state.researchResult = null;
    state.phoneNormalizationResults = new Map();
    state.phoneNormalizationError = null;
    state.phoneNormalizationRequestId += 1;
    state.hotelCode = mtHotelCode;
    elements.hotelLookupInput.value = mtHotelCode;
    elements.runStatus.textContent = "正在搜索酒店资料";
    renderHotelResearch();
    refreshIcons();

    try {
      const response = await api("/api/hotel-research", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mtHotelCode })
      });
      state.researchResult = await response.json();
      state.selectedVersion = state.researchResult.resultVersion;
      setResearchInspector(state.researchResult);
      if (state.hotelBusinessData) await loadHotelPhoneNormalizations(false);
      elements.runStatus.textContent = "酒店资料搜索完成";
    } catch (error) {
      state.researchError = error.message;
      elements.runStatus.textContent = "酒店资料搜索失败";
      showToast(error.message);
    } finally {
      await reloadHotelDetail();
      const latestVersion = getResearchOperation()?.latestSuccessfulVersion;
      if (!state.researchResult && latestVersion) await loadHotelResult(latestVersion, false);
      await loadRecentHotels();
      state.researchLoading = false;
      renderAll();
    }
  }

  async function reloadHotelDetail() {
    if (!state.hotelCode) return;
    try {
      const response = await fetch(`/api/hotel-business/${encodeURIComponent(state.hotelCode)}`);
      state.hotelDetail = response.ok ? await response.json() : null;
    } catch {
      // Keep the current result visible when only the summary refresh fails.
    }
  }

  function formatRunStatus(status) {
    return ({ running: "运行中", completed: "成功", failed: "失败", cancelled: "已取消" })[String(status || "").toLowerCase()] || status;
  }

  function setSectionCollapsed(section, collapsed) {
    const isHotel = section === "hotel";
    const container = isHotel ? elements.hotelBusinessSection : elements.otherConversationsSection;
    const toggle = isHotel ? elements.hotelBusinessToggle : elements.otherConversationsToggle;
    const content = isHotel ? elements.hotelBusinessContent : elements.otherConversationsContent;
    container.classList.toggle("collapsed", collapsed);
    toggle.setAttribute("aria-expanded", String(!collapsed));
    content.hidden = collapsed;
    localStorage.setItem(`maf.${section}.collapsed`, String(collapsed));
  }

  function formatCoordinates(latitude, longitude) {
    return latitude === null || latitude === undefined || longitude === null || longitude === undefined
      ? null
      : `${latitude}, ${longitude}`;
  }

  function formatSmoking(value) {
    return value === true ? "是" : value === false ? "否" : null;
  }

  function formatRoomArea(value) {
    if (value === null || value === undefined || value === "") return null;
    if (typeof value === "string") {
      const parsed = tryParseStructuredValue(value);
      if (parsed !== value) return formatRoomArea(parsed);
      return value;
    }
    if (typeof value !== "object") return value;

    const squareMeters = value.squareMeters ?? value.squareMetres;
    const squareFeet = value.squareFeet;
    const parts = [];
    if (squareMeters !== null && squareMeters !== undefined) parts.push(`${squareMeters} m²`);
    if (squareFeet !== null && squareFeet !== undefined) parts.push(`${squareFeet} ft²`);
    return parts.length ? parts.join(" / ") : null;
  }

  function formatRoomBedType(value) {
    if (value === null || value === undefined || value === "") return null;
    if (typeof value === "string") {
      const parsed = tryParseStructuredValue(value);
      if (parsed !== value) return formatRoomBedType(parsed);
      return value;
    }
    if (Array.isArray(value)) return value.filter(Boolean).join(" / ") || null;
    if (typeof value === "object") {
      const values = Object.values(value).flat().filter(Boolean);
      return values.join(" / ") || null;
    }
    return value;
  }

  function tryParseStructuredValue(value) {
    const trimmed = value.trim();
    if ((!trimmed.startsWith("{") || !trimmed.endsWith("}"))
      && (!trimmed.startsWith("[") || !trimmed.endsWith("]"))) {
      return value;
    }

    try {
      return JSON.parse(trimmed);
    } catch {
      return value;
    }
  }

  function formatValue(value) {
    return value === null || value === undefined || value === "" ? "—" : String(value);
  }

  async function sendMessage(event) {
    event?.preventDefault();
    const text = elements.messageInput.value.trim();
    if (!text || state.busy) return;

    state.busy = true;
    elements.sendButton.disabled = true;
    elements.deleteConversationButton.disabled = true;
    elements.runStatus.textContent = "正在连接 MAF";
    const now = new Date().toISOString();
    const transientUser = {
      messageId: `local-${Date.now()}`,
      role: "user",
      text,
      createdAt: now,
      webSearchEnabled: elements.webSearchEnabled.checked
    };
    const transientAssistant = {
      messageId: `local-${Date.now()}-assistant`,
      role: "assistant",
      text: "",
      createdAt: now,
      sources: [],
      searchQueries: [],
      usage: null,
      streaming: true
    };
    state.viewMessages.push(transientUser, transientAssistant);
    state.debugMessage = transientAssistant;
    state.lastRaw = transientAssistant;
    elements.messageInput.value = "";
    resizeTextarea();
    renderMessages();
    renderInspector();

    const request = {
      message: text,
      conversationId: state.current?.conversationId || null,
      enableWebSearch: elements.webSearchEnabled.checked
    };
    let activeConversationId = request.conversationId;
    let runError = null;

    try {
      const agentName = elements.agentName.value.trim() || "operations";
      const response = await api(`/api/agents/${encodeURIComponent(agentName)}/runs/stream`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request)
      });

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      while (true) {
        const { value, done } = await reader.read();
        buffer += decoder.decode(value || new Uint8Array(), { stream: !done });
        const lines = buffer.split("\n");
        buffer = lines.pop() || "";
        for (const line of lines) {
          if (line.trim()) {
            const eventData = JSON.parse(line);
            activeConversationId = handleStreamEvent(eventData, transientAssistant, activeConversationId);
            if (eventData.kind === "error") runError = eventData.error || "Agent 执行失败";
          }
        }
        if (done) break;
      }
      if (buffer.trim()) {
        const eventData = JSON.parse(buffer);
        activeConversationId = handleStreamEvent(eventData, transientAssistant, activeConversationId);
        if (eventData.kind === "error") runError = eventData.error || "Agent 执行失败";
      }
    } catch (error) {
      runError = error.message;
    }

    transientAssistant.streaming = false;
    if (runError) {
      transientAssistant.role = "system";
      transientAssistant.text = runError;
      showToast(runError);
    }
    state.busy = false;
    elements.sendButton.disabled = false;
    elements.runStatus.textContent = runError ? "运行失败" : "运行完成";

    if (activeConversationId && !runError) {
      await loadConversation(activeConversationId, false);
      await loadConversations();
    } else {
      renderAll();
    }
    elements.messageInput.focus();
  }

  function handleStreamEvent(eventData, assistant, activeConversationId) {
    state.lastRaw = eventData;
    if (eventData.kind === "started") {
      activeConversationId = eventData.conversationId;
      elements.runStatus.textContent = "MAF 正在运行";
    } else if (eventData.kind === "text") {
      assistant.text += eventData.text || "";
    } else if (eventData.kind === "source" && eventData.source) {
      if (!assistant.sources.some(source => source.url === eventData.source.url)) assistant.sources.push(eventData.source);
    } else if (eventData.kind === "completed") {
      assistant.usage = eventData.usage || null;
      assistant.streaming = false;
    }
    state.debugMessage = assistant;
    renderMessages();
    renderInspector();
    return activeConversationId;
  }

  async function checkHealth() {
    try {
      await api("/health");
      elements.serviceStatus.className = "service-status online";
      elements.serviceStatus.lastChild.textContent = "服务正常";
    } catch {
      elements.serviceStatus.className = "service-status offline";
      elements.serviceStatus.lastChild.textContent = "服务异常";
    }
  }

  function selectTab(tabName) {
    document.querySelectorAll(".tab").forEach(tab => {
      const active = tab.dataset.tab === tabName;
      tab.classList.toggle("active", active);
      tab.setAttribute("aria-selected", String(active));
    });
    document.querySelectorAll(".tab-panel").forEach(panel => {
      panel.classList.toggle("active", panel.dataset.panel === tabName);
    });
  }

  function showToast(message) {
    clearTimeout(state.toastTimer);
    elements.toast.textContent = message;
    elements.toast.classList.add("visible");
    state.toastTimer = setTimeout(() => elements.toast.classList.remove("visible"), 4500);
  }

  function formatListTime(value) {
    const date = new Date(value);
    const today = new Date();
    if (date.toDateString() === today.toDateString()) {
      return new Intl.DateTimeFormat("zh-CN", { hour: "2-digit", minute: "2-digit", hour12: false }).format(date);
    }
    return new Intl.DateTimeFormat("zh-CN", { month: "2-digit", day: "2-digit" }).format(date);
  }

  function formatMessageTime(value) {
    return new Intl.DateTimeFormat("zh-CN", {
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false
    }).format(new Date(value));
  }

  function formatNumber(value) {
    return value === null || value === undefined ? "—" : new Intl.NumberFormat("zh-CN").format(value);
  }

  function resizeTextarea() {
    elements.messageInput.style.height = "auto";
    elements.messageInput.style.height = `${Math.min(elements.messageInput.scrollHeight, 160)}px`;
  }

  function closeDrawers() {
    document.body.classList.remove("sidebar-open", "inspector-open");
  }

  elements.composer.addEventListener("submit", sendMessage);
  elements.messageInput.addEventListener("input", resizeTextarea);
  elements.messageInput.addEventListener("keydown", event => {
    if (event.key === "Enter" && event.ctrlKey) sendMessage(event);
  });
  elements.newConversationButton.addEventListener("click", startNewConversation);
  elements.backToHotelBusinessButton.addEventListener("click", returnToHotelBusiness);
  elements.hotelLookupForm.addEventListener("submit", event => {
    event.preventDefault();
    openHotelByCode(elements.hotelLookupInput.value);
  });
  elements.hotelResearchForm.addEventListener("submit", runHotelResearch);
  elements.deleteConversationButton.addEventListener("click", deleteCurrentConversation);
  elements.copyConversationIdButton.addEventListener("click", async () => {
    if (!state.current) return;
    await navigator.clipboard.writeText(state.current.conversationId);
    elements.runStatus.textContent = "会话 ID 已复制";
  });
  elements.conversationSearch.addEventListener("input", () => {
    clearTimeout(state.searchTimer);
    state.searchTimer = setTimeout(() => loadConversations(), 220);
  });
  elements.hotelBusinessToggle.addEventListener("click", () => {
    setSectionCollapsed("hotel", !elements.hotelBusinessSection.classList.contains("collapsed"));
  });
  elements.otherConversationsToggle.addEventListener("click", () => {
    setSectionCollapsed("other", !elements.otherConversationsSection.classList.contains("collapsed"));
  });
  document.querySelectorAll(".tab").forEach(tab => tab.addEventListener("click", () => selectTab(tab.dataset.tab)));
  elements.openSidebarButton.addEventListener("click", () => document.body.classList.add("sidebar-open"));
  elements.closeSidebarButton.addEventListener("click", closeDrawers);
  elements.openInspectorButton.addEventListener("click", () => document.body.classList.add("inspector-open"));
  elements.closeInspectorButton.addEventListener("click", closeDrawers);
  elements.backdrop.addEventListener("click", closeDrawers);
  document.addEventListener("keydown", event => {
    if (event.key === "Escape") closeDrawers();
  });

  setSectionCollapsed("hotel", localStorage.getItem("maf.hotel.collapsed") === "true");
  setSectionCollapsed("other", localStorage.getItem("maf.other.collapsed") !== "false");
  renderAll();
  checkHealth();
  loadRecentHotels();
  loadConversations();
})();

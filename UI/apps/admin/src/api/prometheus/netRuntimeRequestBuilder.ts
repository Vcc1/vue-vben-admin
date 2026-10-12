export interface NetRuntimeRequestBuilderOptions {
  baseUrl?: string;
  endTime: Date | number | string;
  includeProcessInstrumentation?: boolean;
  instanceName: string;
  jobName?: string;
  startTime: Date | number | string;
  step?: number;
}

export interface PrometheusRequestDescriptor {
  key: string;
  kind: 'instant' | 'range';
  params: Record<string, string>;
  path: '/api/v1/query' | '/api/v1/query_range';
  query: string;
  title: string;
  url: string;
}

export interface NetRuntimeRequestBuildResult {
  end: number;
  instantRequests: PrometheusRequestDescriptor[];
  rangeRequests: PrometheusRequestDescriptor[];
  resolvedWindow: string;
  start: number;
  step: number;
  targetLabels: Record<string, string>;
}

interface QueryContext {
  end: number;
  processLabels: Record<string, string>;
  runtimeLabels: Record<string, string>;
  start: number;
  step: number;
  window: string;
}

interface RequestTemplate {
  key: string;
  kind: 'instant' | 'range';
  query: (ctx: QueryContext) => string;
  title: string;
}

function normalizeUnixSeconds(value: Date | number | string, fieldName: string) {
  if (value instanceof Date) {
    const seconds = Math.floor(value.getTime() / 1000);
    if (Number.isFinite(seconds)) {
      return seconds;
    }
    throw new Error(`${fieldName} is invalid`);
  }

  if (typeof value === 'number') {
    if (!Number.isFinite(value)) {
      throw new Error(`${fieldName} is invalid`);
    }
    return value > 1_000_000_000_000 ? Math.floor(value / 1000) : Math.floor(value);
  }

  const time = Date.parse(value);
  if (!Number.isFinite(time)) {
    throw new Error(`${fieldName} is invalid`);
  }
  return Math.floor(time / 1000);
}

function escapeLabelValue(value: string) {
  return value.replaceAll('\\', '\\\\').replaceAll('"', '\\"');
}

function buildSelector(labels: Record<string, string>) {
  const parts = Object.entries(labels)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `${key}="${escapeLabelValue(value)}"`);
  return `{${parts.join(',')}}`;
}

function buildMetricSelector(metricName: string, labels: Record<string, string>) {
  return `${metricName}${buildSelector(labels)}`;
}

function buildOrExpr(leftExpr: string, rightExpr: string) {
  return `(${leftExpr}) or (${rightExpr})`;
}

function toPrometheusDuration(totalSeconds: number) {
  const seconds = Math.max(60, Math.floor(totalSeconds));
  const units = [
    { label: 'd', seconds: 24 * 60 * 60 },
    { label: 'h', seconds: 60 * 60 },
    { label: 'm', seconds: 60 },
    { label: 's', seconds: 1 },
  ];

  for (const unit of units) {
    if (seconds % unit.seconds === 0) {
      return `${seconds / unit.seconds}${unit.label}`;
    }
  }

  return `${seconds}s`;
}

function resolveStep(durationSeconds: number) {
  if (durationSeconds <= 15 * 60) {
    return 15;
  }
  if (durationSeconds <= 60 * 60) {
    return 30;
  }
  if (durationSeconds <= 6 * 60 * 60) {
    return 120;
  }
  return 300;
}

function buildUrl(baseUrl: string, path: string, params: Record<string, string>) {
  const normalizedBaseUrl = baseUrl.replace(/\/$/, '');
  const search = new URLSearchParams(params).toString();
  return `${normalizedBaseUrl}${path}?${search}`;
}

const baseTemplates: RequestTemplate[] = [
  {
    key: 'cpuCountDotnet',
    kind: 'instant',
    query: (ctx) => buildMetricSelector('dotnet_process_cpu_count', ctx.runtimeLabels),
    title: 'dotnet process cpu count',
  },
  {
    key: 'cpuTimeIncDotnet',
    kind: 'instant',
    query: (ctx) =>
      `increase(${buildMetricSelector('dotnet_process_cpu_time_seconds_total', ctx.runtimeLabels)}[${ctx.window}])`,
    title: 'dotnet process cpu time increase',
  },
  {
    key: 'workingSet',
    kind: 'instant',
    query: (ctx) => buildMetricSelector('dotnet_process_memory_working_set_bytes', ctx.runtimeLabels),
    title: 'dotnet working set',
  },
  {
    key: 'virtualMemory',
    kind: 'instant',
    query: (ctx) => buildMetricSelector('process_memory_virtual_bytes', ctx.processLabels),
    title: 'process virtual memory',
  },
  {
    key: 'processThreadCount',
    kind: 'instant',
    query: (ctx) => buildMetricSelector('process_thread_count', ctx.processLabels),
    title: 'process thread count',
  },
  {
    key: 'gcGen0Inc',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_gc_collections_total', { ...ctx.runtimeLabels, gc_heap_generation: 'gen0' })}[${ctx.window}])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', { ...ctx.runtimeLabels, generation: 'gen0' })}[${ctx.window}])`,
      ),
    title: 'gc gen0 increase',
  },
  {
    key: 'gcGen1Inc',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_gc_collections_total', { ...ctx.runtimeLabels, gc_heap_generation: 'gen1' })}[${ctx.window}])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', { ...ctx.runtimeLabels, generation: 'gen1' })}[${ctx.window}])`,
      ),
    title: 'gc gen1 increase',
  },
  {
    key: 'gcGen2Inc',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_gc_collections_total', { ...ctx.runtimeLabels, gc_heap_generation: 'gen2' })}[${ctx.window}])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', { ...ctx.runtimeLabels, generation: 'gen2' })}[${ctx.window}])`,
      ),
    title: 'gc gen2 increase',
  },
  {
    key: 'gcPauseInc',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_gc_pause_time_seconds_total', ctx.runtimeLabels)}[${ctx.window}])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_gc_duration_nanoseconds_total', ctx.runtimeLabels)}[${ctx.window}]) / 1000000000`,
      ),
    title: 'gc pause increase',
  },
  {
    key: 'gcAllocatedInc',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_gc_heap_allocated_bytes_total', ctx.runtimeLabels)}[${ctx.window}])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_gc_allocations_size_bytes_total', ctx.runtimeLabels)}[${ctx.window}])`,
      ),
    title: 'gc allocated bytes increase',
  },
  {
    key: 'lockContentionsInc',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_monitor_lock_contentions_total', ctx.runtimeLabels)}[${ctx.window}])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_monitor_lock_contention_count_total', ctx.runtimeLabels)}[${ctx.window}])`,
      ),
    title: 'monitor lock contentions increase',
  },
  {
    key: 'tpQueueLength',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_thread_pool_queue_length_total', ctx.runtimeLabels),
        buildMetricSelector('process_runtime_dotnet_thread_pool_queue_length', ctx.runtimeLabels),
      ),
    title: 'thread pool queue length',
  },
  {
    key: 'tpThreadCount',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_thread_pool_thread_count_total', ctx.runtimeLabels),
        buildMetricSelector('process_runtime_dotnet_thread_pool_threads_count', ctx.runtimeLabels),
      ),
    title: 'thread pool thread count',
  },
  {
    key: 'tpWorkItemInc',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_thread_pool_work_item_count_total', ctx.runtimeLabels)}[${ctx.window}])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_thread_pool_completed_items_count_total', ctx.runtimeLabels)}[${ctx.window}])`,
      ),
    title: 'thread pool work item increase',
  },
  {
    key: 'timerCount',
    kind: 'instant',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_timer_count', ctx.runtimeLabels),
        buildMetricSelector('process_runtime_dotnet_timer_count', ctx.runtimeLabels),
      ),
    title: 'timer count',
  },
  {
    key: 'heapSizeRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_gc_last_collection_heap_size_bytes', ctx.runtimeLabels),
        buildMetricSelector('process_runtime_dotnet_gc_heap_size_bytes', ctx.runtimeLabels),
      ),
    title: 'gc heap size range',
  },
  {
    key: 'fragSizeRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_gc_last_collection_heap_fragmentation_size_bytes', ctx.runtimeLabels),
        buildMetricSelector('process_runtime_dotnet_gc_heap_fragmentation_size_bytes', ctx.runtimeLabels),
      ),
    title: 'gc heap fragmentation range',
  },
  {
    key: 'gcCollectionsIncRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_gc_collections_total', ctx.runtimeLabels)}[1m])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_gc_collections_count_total', ctx.runtimeLabels)}[1m])`,
      ),
    title: 'gc collections increase range',
  },
  {
    key: 'gcPauseIncRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_gc_pause_time_seconds_total', ctx.runtimeLabels)}[1m])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_gc_duration_nanoseconds_total', ctx.runtimeLabels)}[1m]) / 1000000000`,
      ),
    title: 'gc pause increase range',
  },
  {
    key: 'jitMethodsIncRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_jit_compiled_methods_total', ctx.runtimeLabels)}[1m])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_jit_methods_compiled_count_total', ctx.runtimeLabels)}[1m])`,
      ),
    title: 'jit methods increase range',
  },
  {
    key: 'jitIlBytesIncRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_jit_compiled_il_size_bytes_total', ctx.runtimeLabels)}[1m])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_jit_il_compiled_size_bytes_total', ctx.runtimeLabels)}[1m])`,
      ),
    title: 'jit il bytes increase range',
  },
  {
    key: 'jitTimeIncRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_jit_compilation_time_seconds_total', ctx.runtimeLabels)}[1m])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_jit_compilation_time_nanoseconds_total', ctx.runtimeLabels)}[1m]) / 1000000000`,
      ),
    title: 'jit time increase range',
  },
  {
    key: 'tpQueueRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_thread_pool_queue_length_total', ctx.runtimeLabels),
        buildMetricSelector('process_runtime_dotnet_thread_pool_queue_length', ctx.runtimeLabels),
      ),
    title: 'thread pool queue range',
  },
  {
    key: 'tpThreadRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_thread_pool_thread_count_total', ctx.runtimeLabels),
        buildMetricSelector('process_runtime_dotnet_thread_pool_threads_count', ctx.runtimeLabels),
      ),
    title: 'thread pool thread range',
  },
  {
    key: 'tpWorkIncRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_thread_pool_work_item_count_total', ctx.runtimeLabels)}[1m])`,
        `increase(${buildMetricSelector('process_runtime_dotnet_thread_pool_completed_items_count_total', ctx.runtimeLabels)}[1m])`,
      ),
    title: 'thread pool work item increase range',
  },
];

const processTemplates: RequestTemplate[] = [
  {
    key: 'processCpuCount',
    kind: 'instant',
    query: (ctx) => buildMetricSelector('process_cpu_count', ctx.processLabels),
    title: 'process cpu count',
  },
  {
    key: 'processCpuTimeInc',
    kind: 'instant',
    query: (ctx) =>
      `increase(${buildMetricSelector('process_cpu_time_seconds_total', ctx.processLabels)}[${ctx.window}])`,
    title: 'process cpu time increase',
  },
  {
    key: 'processMemoryUsage',
    kind: 'instant',
    query: (ctx) => buildMetricSelector('process_memory_usage_bytes', ctx.processLabels),
    title: 'process memory usage',
  },
  {
    key: 'cpuTimeIncRangeDotnet',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        `increase(${buildMetricSelector('dotnet_process_cpu_time_seconds_total', ctx.runtimeLabels)}[1m])`,
        `increase(${buildMetricSelector('process_cpu_time_seconds_total', ctx.processLabels)}[1m])`,
      ),
    title: 'cpu time increase range',
  },
  {
    key: 'workingSetRange',
    kind: 'range',
    query: (ctx) =>
      buildOrExpr(
        buildMetricSelector('dotnet_process_memory_working_set_bytes', ctx.runtimeLabels),
        buildMetricSelector('process_memory_usage_bytes', ctx.processLabels),
      ),
    title: 'working set range',
  },
  {
    key: 'processThreadCountRange',
    kind: 'range',
    query: (ctx) =>
      buildMetricSelector('process_thread_count', ctx.processLabels),
    title: 'process thread count range',
  },
];

function buildDescriptor(
  template: RequestTemplate,
  ctx: QueryContext,
  baseUrl: string,
): PrometheusRequestDescriptor {
  const query = template.query(ctx);
  if (template.kind === 'instant') {
    const params = {
      query,
      time: String(ctx.end),
    };
    return {
      key: template.key,
      kind: template.kind,
      params,
      path: '/api/v1/query',
      query,
      title: template.title,
      url: buildUrl(baseUrl, '/api/v1/query', params),
    };
  }

  const params = {
    end: String(ctx.end),
    query,
    start: String(ctx.start),
    step: String(ctx.step),
  };
  return {
    key: template.key,
    kind: template.kind,
    params,
    path: '/api/v1/query_range',
    query,
    title: template.title,
    url: buildUrl(baseUrl, '/api/v1/query_range', params),
  };
}

export function buildNetRuntimeRequestSet(
  options: NetRuntimeRequestBuilderOptions,
): NetRuntimeRequestBuildResult {
  const start = normalizeUnixSeconds(options.startTime, 'startTime');
  const end = normalizeUnixSeconds(options.endTime, 'endTime');

  if (start >= end) {
    throw new Error('startTime must be earlier than endTime');
  }

  const durationSeconds = end - start;
  const step = options.step && options.step > 0 ? Math.floor(options.step) : resolveStep(durationSeconds);
  const targetLabels: Record<string, string> = {
    exported_instance: options.instanceName,
  };

  if (options.jobName) {
    targetLabels.exported_job = options.jobName;
  }

  const ctx: QueryContext = {
    end,
    processLabels: { ...targetLabels },
    runtimeLabels: { ...targetLabels },
    start,
    step,
    window: toPrometheusDuration(durationSeconds),
  };

  const templates = options.includeProcessInstrumentation
    ? [...baseTemplates, ...processTemplates]
    : baseTemplates;

  const descriptors = templates.map((template) =>
    buildDescriptor(template, ctx, options.baseUrl || ''),
  );

  return {
    end,
    instantRequests: descriptors.filter((item) => item.kind === 'instant'),
    rangeRequests: descriptors.filter((item) => item.kind === 'range'),
    resolvedWindow: ctx.window,
    start,
    step,
    targetLabels,
  };
}

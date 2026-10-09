import dayjs from 'dayjs';

const compactNumberFormatter = new Intl.NumberFormat('zh-CN', {
  maximumFractionDigits: 1,
  notation: 'compact',
});

const integerFormatter = new Intl.NumberFormat('zh-CN', {
  maximumFractionDigits: 0,
});

export function normalizeNumber(value: number | string | undefined) {
  const num = Number(value);
  return Number.isFinite(num) ? num : 0;
}

export function formatInteger(value: number) {
  return integerFormatter.format(normalizeNumber(value));
}

export function formatCompactNumber(value: number) {
  const normalized = normalizeNumber(value);
  if (Math.abs(normalized) < 1000) {
    return normalized.toFixed(normalized % 1 === 0 ? 0 : 2);
  }
  return compactNumberFormatter.format(normalized);
}

export function formatDurationMs(value: number) {
  const normalized = normalizeNumber(value);
  if (normalized >= 1000) {
    const seconds = normalized / 1000;
    return `${seconds.toFixed(seconds >= 10 ? 1 : 2)} s`;
  }
  return `${normalized.toFixed(normalized >= 100 ? 0 : 2)} ms`;
}

export function formatDateTime(timestampMs: number) {
  if (!timestampMs) {
    return '--';
  }
  return dayjs(timestampMs).format('YYYY-MM-DD HH:mm:ss');
}

export function buildQueryStepSeconds(start: number, end: number) {
  const duration = Math.max(end - start, 60);
  if (duration <= 15 * 60) {
    return 15;
  }
  if (duration <= 60 * 60) {
    return 30;
  }
  if (duration <= 6 * 60 * 60) {
    return 120;
  }
  if (duration <= 24 * 60 * 60) {
    return 300;
  }
  return Math.max(600, Math.ceil(duration / 240));
}

export function buildTimelineMillis(start: number, end: number, step: number) {
  const safeStep = Math.max(step, 1);
  const points: number[] = [];
  let cursor = start;

  while (cursor <= end) {
    points.push(cursor * 1000);
    cursor += safeStep;
  }

  const lastPoint = points.at(-1);
  if (lastPoint !== end * 1000) {
    points.push(end * 1000);
  }

  return points;
}

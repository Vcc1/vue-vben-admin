export type BodyFormat = 'form' | 'json' | 'ndjson' | 'text' | 'xml';

export interface KeyValue {
  key: string;
  value: string;
}

function isBlank(value: null | string | undefined): boolean {
  return value === null || value === undefined || value.trim() === '';
}

function tryParseJson(text: string): any {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

function looksLikeXml(text: string): boolean {
  const t = text.trim();
  return t.startsWith('<') && t.includes('>');
}

function looksLikeNdjson(text: string): boolean {
  const lines = text
    .split('\n')
    .map((l) => l.trim())
    .filter((l) => l.length > 0);
  if (lines.length < 2) return false;
  return lines.every((line) => {
    const parsed = tryParseJson(line);
    return parsed !== undefined && typeof parsed === 'object';
  });
}

function looksLikeForm(text: string): boolean {
  if (text.includes('\n')) return false;
  const t = text.trim().replace(/^\?/, '');
  if (!t.includes('=')) return false;
  const segments = t.split('&').filter((s) => s.length > 0);
  if (segments.length === 0) return false;
  return segments.every((seg) => seg.includes('='));
}

/** 自动识别 body / query 文本的常见格式。 */
export function detectFormat(raw: null | string | undefined): BodyFormat {
  if (isBlank(raw)) return 'text';
  const text = raw as string;
  const trimmed = text.trim();

  if (looksLikeXml(trimmed)) return 'xml';
  if (tryParseJson(trimmed) !== undefined) return 'json';
  if (looksLikeNdjson(text)) return 'ndjson';
  if (looksLikeForm(trimmed)) return 'form';
  return 'text';
}

export function formatLabel(format: BodyFormat): string {
  switch (format) {
    case 'form': {
      return 'Form';
    }
    case 'json': {
      return 'JSON';
    }
    case 'ndjson': {
      return 'NDJSON';
    }
    case 'xml': {
      return 'XML';
    }
    default: {
      return '纯文本';
    }
  }
}

/** 美化 JSON，失败时返回原文。 */
export function prettyJson(raw: string): string {
  const parsed = tryParseJson(raw);
  return parsed === undefined ? raw : JSON.stringify(parsed, null, 2);
}

/** 将 NDJSON 每行美化后拼接，非法行原样保留。 */
export function prettyNdjson(raw: string): string {
  return raw
    .split('\n')
    .filter((l) => l.trim().length > 0)
    .map((line) => {
      const parsed = tryParseJson(line);
      return parsed === undefined ? line : JSON.stringify(parsed, null, 2);
    })
    .join('\n');
}

/** 简易 XML 缩进美化，失败时返回原文。 */
export function prettyXml(raw: string): string {
  try {
    const normalized = raw.replace(/>\s+</g, '><').trim();
    const tokens = normalized.replace(/(>)(<)/g, '$1\n$2').split('\n');
    let pad = 0;
    const lines = tokens.map((token) => {
      const line = token.trim();
      if (/^<\//.test(line)) {
        pad = Math.max(pad - 1, 0);
      }
      const out = `${'  '.repeat(pad)}${line}`;
      const isSelfClosing = /\/>$/.test(line);
      const isDeclaration = /^<\?/.test(line);
      const isOpening = /^<[^!?/][^>]*[^/]>$/.test(line) || /^<[^!?/][^>]*>$/.test(line);
      if (!isSelfClosing && !isDeclaration && isOpening) {
        pad += 1;
      }
      return out;
    });
    return lines.join('\n');
  } catch {
    return raw;
  }
}

/** 解析 form / query 文本为键值对。 */
export function parseKeyValue(raw: string): KeyValue[] {
  const text = raw.trim().replace(/^\?/, '');
  const params = new URLSearchParams(text);
  const result: KeyValue[] = [];
  for (const [key, value] of params.entries()) {
    result.push({ key, value });
  }
  return result;
}

/** 根据识别出的格式返回美化后的纯文本（form 类型返回空，由键值表渲染）。 */
export function prettyByFormat(
  raw: null | string | undefined,
  format: BodyFormat,
): string {
  if (isBlank(raw)) return '';
  const text = raw as string;
  switch (format) {
    case 'form': {
      return text;
    }
    case 'json': {
      return prettyJson(text);
    }
    case 'ndjson': {
      return prettyNdjson(text);
    }
    case 'xml': {
      return prettyXml(text);
    }
    default: {
      return text;
    }
  }
}

import { describe, expect, it } from 'vitest';

import { grantsPermission } from '../permission-grants';

describe('grantsPermission', () => {
  it.each([
    ['paycardhub:cards:read', 'paycardhub:cards:read', true],
    ['*', 'paycardhub:cards:read', true],
    ['paycardhub:*', 'paycardhub:cards:read:detail', true],
    ['paycardhub:cards:*', 'paycardhub:cards:read', true],
    ['paycardhub:*', 'paycardhub', false],
    ['paycardhub:*', 'paycardhub2:cards:read', false],
    ['paycardhub:*', 'other:cards:read', false],
    ['paycardhub:*', 'paycardhub:cards:*', false],
    ['paycardhub:*:read', 'paycardhub:cards:read', false],
  ])('%s grants %s = %s', (grant, required, expected) => {
    expect(grantsPermission(grant, required)).toBe(expected);
  });
});

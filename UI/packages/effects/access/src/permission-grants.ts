function grantsPermission(grant: string, required: string): boolean {
  if (!required || required.includes('*')) return false;
  if (grant === '*' || grant === required) return true;
  return (
    grant.endsWith(':*') &&
    !grant.slice(0, -1).includes('*') &&
    required.startsWith(grant.slice(0, -1)) &&
    required.length > grant.length - 1
  );
}

export { grantsPermission };

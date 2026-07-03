/** Normaliza respuestas paginadas `{ items, totalCount }` o arrays planos del API. */
export function parseListResponse<T>(data: unknown): T[] {
  if (Array.isArray(data)) return data as T[];
  if (data && typeof data === 'object' && 'items' in data && Array.isArray((data as { items: unknown }).items)) {
    return (data as { items: T[] }).items;
  }
  return [];
}

export function parseTotalCount(data: unknown, fallbackLength: number): number {
  if (data && typeof data === 'object' && 'totalCount' in data && typeof (data as { totalCount: unknown }).totalCount === 'number') {
    return (data as { totalCount: number }).totalCount;
  }
  return fallbackLength;
}

/** Actualiza una línea de formulario sin `any` (ADR-0018 #44). */
export function updateLineAt<T extends object>(
  lines: T[],
  index: number,
  field: keyof T,
  value: T[keyof T],
): T[] {
  const next = [...lines];
  next[index] = { ...next[index], [field]: value };
  return next;
}

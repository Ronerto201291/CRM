/** Marca temporal para cálculos fuera del render (snooze, vencimientos). */
export function timestampMs(): number {
    return Date.now();
}

export function addMinutesIso(minutes: number): string {
    return new Date(timestampMs() + minutes * 60_000).toISOString();
}

export function isScheduledOverdue(scheduledAt: string, graceMs = 5 * 60_000): boolean {
    return new Date(scheduledAt).getTime() < timestampMs() - graceMs;
}

export function daysUntil(isoDate: string): number {
    return Math.ceil((new Date(isoDate).getTime() - timestampMs()) / (1000 * 60 * 60 * 24));
}

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { addMinutesIso, daysUntil, isScheduledOverdue } from '../time';

describe('time utilities', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date('2026-03-15T12:00:00.000Z'));
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('addMinutesIso suma minutos correctamente', () => {
        expect(addMinutesIso(30)).toBe('2026-03-15T12:30:00.000Z');
    });

    it('isScheduledOverdue detecta vencimiento', () => {
        expect(isScheduledOverdue('2026-03-15T11:00:00.000Z')).toBe(true);
        expect(isScheduledOverdue('2026-03-15T12:00:00.000Z')).toBe(false);
    });

    it('daysUntil calcula días restantes', () => {
        expect(daysUntil('2026-03-17T12:00:00.000Z')).toBe(2);
    });
});

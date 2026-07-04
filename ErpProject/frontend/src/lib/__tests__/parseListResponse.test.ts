import { describe, expect, it } from 'vitest';
import { parseListResponse, parseTotalCount } from '@/lib/parseListResponse';

describe('parseListResponse', () => {
    it('devuelve array plano tal cual', () => {
        const data = [{ id: '1' }, { id: '2' }];
        expect(parseListResponse(data)).toEqual(data);
    });

    it('extrae items de respuesta paginada', () => {
        const data = { items: [{ id: 'a' }], totalCount: 1 };
        expect(parseListResponse(data)).toEqual([{ id: 'a' }]);
    });

    it('devuelve array vacío para datos desconocidos', () => {
        expect(parseListResponse(null)).toEqual([]);
        expect(parseListResponse({ foo: 'bar' })).toEqual([]);
    });
});

describe('parseTotalCount', () => {
    it('lee totalCount de respuesta paginada', () => {
        expect(parseTotalCount({ totalCount: 42, items: [] }, 0)).toBe(42);
    });

    it('usa fallback si no hay totalCount', () => {
        expect(parseTotalCount([1, 2, 3], 3)).toBe(3);
    });
});

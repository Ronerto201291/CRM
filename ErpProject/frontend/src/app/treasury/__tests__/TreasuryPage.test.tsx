import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import TreasuryPage from '@/app/treasury/page';
import { serverFetchList } from '@/lib/serverFetch';

vi.mock('@/lib/serverFetch', () => ({
    serverFetch: vi.fn(),
    serverFetchList: vi.fn(),
}));

describe('TreasuryPage forecast tab', () => {
    beforeEach(() => {
        vi.mocked(serverFetchList).mockResolvedValue([
            {
                id: 'acc-1',
                name: 'Cuenta principal',
                iban: 'ES9121000418450200051332',
                bankName: 'BBVA',
                currentBalance: 5000,
                currencyCode: 'EUR',
                isActive: true,
            },
        ]);
        vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string) => {
            if (url.includes('/forecasts')) {
                return Promise.resolve({
                    ok: true,
                    json: async () => [
                        {
                            id: 'fc-1',
                            forecastDate: '2026-07-15T00:00:00Z',
                            expectedInflow: 2000,
                            expectedOutflow: 500,
                            expectedBalance: 6500,
                            source: 'Effects',
                            isActual: false,
                        },
                    ],
                });
            }
            return Promise.resolve({ ok: true, json: async () => ({ items: [], totalCount: 0 }) });
        }));
    });

    it('renderiza módulo tesorería con KPIs', async () => {
        render(await TreasuryPage());

        expect(screen.getByRole('heading', { name: /tesorería/i })).toBeInTheDocument();
        await waitFor(() => {
            expect(screen.getByText('Cuenta principal')).toBeInTheDocument();
        });
    });

    it('pestaña previsión de caja carga forecasts', async () => {
        const fetchMock = vi.mocked(global.fetch);
        render(await TreasuryPage());

        fireEvent.click(screen.getByRole('button', { name: /previsión de caja/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                expect.stringContaining('/api/proxy/treasury/forecasts'),
            );
        });

        await waitFor(() => {
            expect(screen.getByText('Effects')).toBeInTheDocument();
        });
    });
});

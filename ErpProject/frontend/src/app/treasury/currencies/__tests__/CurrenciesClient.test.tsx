import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import CurrenciesClient from '@/app/treasury/currencies/CurrenciesClient';

describe('CurrenciesClient', () => {
    it('renderiza listado de divisas inicial', () => {
        render(
            <CurrenciesClient
                initialCurrencies={[
                    {
                        id: '1',
                        code: 'USD',
                        name: 'Dólar USA',
                        exchangeRateToEur: 0.92,
                        trend: 'stable',
                        lastUpdated: '2026-07-01T00:00:00Z',
                        isActive: true,
                    },
                ]}
            />,
        );

        expect(screen.getByText('USD')).toBeInTheDocument();
        expect(screen.getByText('Dólar USA')).toBeInTheDocument();
    });

    it('abre modal crear divisa y envía POST', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 'cur-1' }) });
        vi.stubGlobal('fetch', fetchMock);

        render(<CurrenciesClient initialCurrencies={[]} />);

        fireEvent.click(screen.getByRole('button', { name: /nueva divisa/i }));

        await waitFor(() => {
            expect(screen.getByRole('dialog')).toBeInTheDocument();
        });

        fireEvent.change(screen.getByPlaceholderText('USD'), { target: { value: 'GBP' } });
        fireEvent.change(screen.getByPlaceholderText('Dólar estadounidense'), { target: { value: 'Libra esterlina' } });
        fireEvent.click(screen.getByRole('button', { name: /^crear$/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/v1/treasury/currencies',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

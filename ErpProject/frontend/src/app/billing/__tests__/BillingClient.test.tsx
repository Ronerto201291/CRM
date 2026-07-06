import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import BillingClient from '@/app/billing/BillingClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('BillingClient', () => {
    it('renderiza listado de facturas inicial', () => {
        render(
            <BillingClient
                initialInvoices={[
                    {
                        id: '1',
                        number: 'A-2026-000001',
                        clientName: 'Acme SL',
                        issueDate: '2026-07-01',
                        subtotal: 100,
                        taxAmount: 21,
                        irpfAmount: 0,
                        total: 121,
                        status: 'Draft',
                        isLocked: false,
                        series: 'A',
                    },
                ]}
                initialClients={[]}
            />,
        );
        expect(screen.getByRole('heading', { name: 'Facturación' })).toBeInTheDocument();
        expect(screen.getByText('A-2026-000001')).toBeInTheDocument();
        expect(screen.getByText('Acme SL')).toBeInTheDocument();
    });

    it('marca factura como pagada vía POST', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true });
        vi.stubGlobal('fetch', fetchMock);
        vi.stubGlobal('confirm', vi.fn(() => true));

        render(
            <BillingClient
                initialInvoices={[
                    {
                        id: 'inv-1',
                        number: 'A-2026-000099',
                        clientName: 'Cliente Pago',
                        issueDate: '2026-07-01',
                        subtotal: 100,
                        taxAmount: 21,
                        irpfAmount: 0,
                        total: 121,
                        status: 'Issued',
                        isLocked: false,
                        series: 'A',
                    },
                ]}
                initialClients={[]}
            />,
        );

        fireEvent.click(screen.getByRole('button', { name: /pagar/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/invoices/inv-1/pay',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

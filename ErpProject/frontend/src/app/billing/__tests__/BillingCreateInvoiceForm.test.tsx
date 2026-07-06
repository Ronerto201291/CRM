import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import BillingClient from '@/app/billing/BillingClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('BillingClient create invoice form', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
            ok: true,
            json: async () => [{ code: 'EUR', name: 'Euro', exchangeRate: 1 }],
        }));
        vi.stubGlobal('confirm', vi.fn(() => true));
    });

    it('muestra error de validación si el formulario está incompleto', async () => {
        render(<BillingClient initialInvoices={[]} initialClients={[]} />);

        fireEvent.click(screen.getByRole('button', { name: /nueva factura/i }));

        await waitFor(() => {
            expect(screen.getByRole('dialog')).toBeInTheDocument();
        });

        fireEvent.click(screen.getByRole('button', { name: /b2c/i }));
        fireEvent.click(screen.getByRole('button', { name: /crear factura/i }));

        await waitFor(() => {
            const dialog = screen.getByRole('dialog');
            expect(dialog.textContent).toMatch(/cliente|línea|descripción/i);
        });
    });

    it('envía POST al crear factura manual válida', async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({ id: 'new-inv-1' }),
        });
        vi.stubGlobal('fetch', fetchMock);

        render(<BillingClient initialInvoices={[]} initialClients={[]} />);

        fireEvent.click(screen.getByRole('button', { name: /nueva factura/i }));

        await waitFor(() => {
            expect(screen.getByRole('dialog')).toBeInTheDocument();
        });

        fireEvent.click(screen.getByRole('button', { name: /cliente manual/i }));
        fireEvent.change(screen.getByPlaceholderText('Nombre del cliente'), { target: { value: 'Cliente Test' } });
        fireEvent.change(screen.getByPlaceholderText('12345678A'), { target: { value: '12345678Z' } });
        fireEvent.change(screen.getByPlaceholderText(/descripción/i), { target: { value: 'Servicio consultoría' } });

        const priceInputs = screen.getAllByRole('spinbutton');
        fireEvent.change(priceInputs[1], { target: { value: '100' } });

        fireEvent.click(screen.getByRole('button', { name: /crear factura/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/invoices',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

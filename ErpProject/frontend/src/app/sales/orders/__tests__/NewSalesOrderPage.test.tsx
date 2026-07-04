import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import NewSalesOrderPage from '@/app/sales/orders/new/page';

describe('NewSalesOrderPage form', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({ items: [{ id: 'c1', name: 'Acme SL', taxId: 'B12345674' }] }),
        }));
        delete (window as { location?: Location }).location;
        (window as { location: { href: string } }).location = { href: '' };
    });

    it('renderiza formulario de nuevo pedido', () => {
        render(<NewSalesOrderPage />);

        expect(screen.getByRole('heading', { name: /nuevo pedido de venta/i })).toBeInTheDocument();
        expect(screen.getByPlaceholderText('PV-2026-0001')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /crear pedido/i })).toBeInTheDocument();
    });

    it('muestra error si falta cliente o número', async () => {
        render(<NewSalesOrderPage />);

        fireEvent.click(screen.getByRole('button', { name: /crear pedido/i }));

        await waitFor(() => {
            expect(screen.getByRole('alert')).toBeInTheDocument();
        });
    });

    it('envía POST al crear pedido válido', async () => {
        const fetchMock = vi.fn()
            .mockResolvedValueOnce({
                ok: true,
                json: async () => ({ items: [{ id: 'c1', name: 'Acme SL', taxId: 'B12345674' }] }),
            })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 'order-1' }) });
        vi.stubGlobal('fetch', fetchMock);

        render(<NewSalesOrderPage />);

        await waitFor(() => {
            expect(screen.getByRole('option', { name: /Acme SL/i })).toBeInTheDocument();
        });

        const clientSelect = screen.getAllByRole('combobox')[0];
        fireEvent.change(clientSelect, { target: { value: 'c1' } });
        fireEvent.change(screen.getByPlaceholderText('PV-2026-0001'), { target: { value: 'PV-2026-0001' } });
        fireEvent.change(screen.getByPlaceholderText(/descripción/i), { target: { value: 'Producto A' } });

        const spinbuttons = screen.getAllByRole('spinbutton');
        fireEvent.change(spinbuttons[0], { target: { value: '2' } });
        fireEvent.change(spinbuttons[1], { target: { value: '50' } });

        fireEvent.click(screen.getByRole('button', { name: /crear pedido/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/v1/sales/orders',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

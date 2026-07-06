import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import NewPurchaseOrderPage from '@/app/purchasing/orders/new/page';

describe('NewPurchaseOrderPage form', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({ items: [{ id: 's1', name: 'Proveedor SL', taxId: 'B12345674' }] }),
        }));
        delete (window as { location?: Location }).location;
        (window as { location: { href: string } }).location = { href: '' };
    });

    it('renderiza formulario de nuevo pedido de compra', () => {
        render(<NewPurchaseOrderPage />);

        expect(screen.getByRole('heading', { name: /nuevo pedido de compra/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /crear pedido/i })).toBeInTheDocument();
    });

    it('muestra error si falta proveedor o número', async () => {
        render(<NewPurchaseOrderPage />);

        fireEvent.click(screen.getByRole('button', { name: /crear pedido/i }));

        await waitFor(() => {
            expect(screen.getByRole('alert')).toBeInTheDocument();
        });
    });

    it('envía POST al crear pedido válido', async () => {
        const fetchMock = vi.fn()
            .mockResolvedValueOnce({
                ok: true,
                json: async () => ({ items: [{ id: 's1', name: 'Proveedor SL', taxId: 'B12345674' }] }),
            })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 'po-1' }) });
        vi.stubGlobal('fetch', fetchMock);

        render(<NewPurchaseOrderPage />);

        await waitFor(() => {
            expect(screen.getByRole('option', { name: /Proveedor SL/i })).toBeInTheDocument();
        });

        const supplierSelect = screen.getAllByRole('combobox')[0];
        fireEvent.change(supplierSelect, { target: { value: 's1' } });
        fireEvent.change(screen.getByPlaceholderText('PC-2026-0001'), { target: { value: 'PC-2026-0001' } });
        fireEvent.change(screen.getByPlaceholderText(/descripción/i), { target: { value: 'Material A' } });

        const spinbuttons = screen.getAllByRole('spinbutton');
        fireEvent.change(spinbuttons[0], { target: { value: '3' } });
        fireEvent.change(spinbuttons[1], { target: { value: '25' } });

        fireEvent.click(screen.getByRole('button', { name: /crear pedido/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/v1/purchasing/orders',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

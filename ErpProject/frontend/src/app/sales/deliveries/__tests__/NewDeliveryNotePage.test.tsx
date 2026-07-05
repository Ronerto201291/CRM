import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import NewDeliveryNotePage from '@/app/sales/deliveries/new/page';

describe('NewDeliveryNotePage form', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({ items: [{ id: 'o1', number: 'PV-2026-0001', customerName: 'Cliente SL' }] }),
        }));
        delete (window as { location?: Location }).location;
        (window as { location: { href: string } }).location = { href: '' };
    });

    it('renderiza formulario de nuevo albarán', () => {
        render(<NewDeliveryNotePage />);

        expect(screen.getByRole('heading', { name: /nuevo albarán/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /crear albarán/i })).toBeInTheDocument();
    });

    it('muestra error si falta pedido o número', async () => {
        render(<NewDeliveryNotePage />);

        fireEvent.click(screen.getByRole('button', { name: /crear albarán/i }));

        await waitFor(() => {
            expect(screen.getByRole('alert')).toBeInTheDocument();
        });
    });

    it('envía POST al crear albarán válido', async () => {
        const fetchMock = vi.fn()
            .mockResolvedValueOnce({
                ok: true,
                json: async () => ({ items: [{ id: 'o1', number: 'PV-2026-0001', customerName: 'Cliente SL' }] }),
            })
            .mockResolvedValueOnce({ ok: true, json: async () => ({ id: 'dn-1' }) });
        vi.stubGlobal('fetch', fetchMock);

        render(<NewDeliveryNotePage />);

        await waitFor(() => {
            expect(screen.getByRole('option', { name: /PV-2026-0001/i })).toBeInTheDocument();
        });

        const orderSelect = screen.getAllByRole('combobox')[0];
        fireEvent.change(orderSelect, { target: { value: 'o1' } });
        fireEvent.change(screen.getByPlaceholderText('ALB-2026-0001'), { target: { value: 'ALB-2026-0001' } });
        fireEvent.change(screen.getByPlaceholderText(/descripción/i), { target: { value: 'Entrega parcial' } });

        const spinbuttons = screen.getAllByRole('spinbutton');
        fireEvent.change(spinbuttons[0], { target: { value: '2' } });

        fireEvent.click(screen.getByRole('button', { name: /crear albarán/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/v1/sales/deliveries',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

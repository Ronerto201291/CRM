import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import SuppliersClient from '@/app/crm/suppliers/SuppliersClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('SuppliersClient', () => {
    const initialSuppliers = [
        {
            id: 'sup-1',
            name: 'Proveedor Alpha',
            taxId: 'B12345674',
            email: 'alpha@prov.com',
            phone: '600111222',
            address: 'Calle 1',
            isActive: true,
            createdAt: '2026-07-01T00:00:00Z',
        },
    ];

    beforeEach(() => {
        vi.restoreAllMocks();
    });

    it('renderiza tabla de proveedores', () => {
        render(<SuppliersClient initialSuppliers={initialSuppliers} />);
        expect(screen.getByText('Proveedor Alpha')).toBeInTheDocument();
        expect(screen.getByText('B12345674')).toBeInTheDocument();
    });

    it('abre modal crear proveedor y envía POST', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true });
        vi.stubGlobal('fetch', fetchMock);

        render(<SuppliersClient initialSuppliers={initialSuppliers} />);

        fireEvent.click(screen.getByRole('button', { name: /nuevo proveedor/i }));

        await waitFor(() => {
            expect(screen.getByRole('dialog')).toBeInTheDocument();
        });

        fireEvent.change(screen.getByLabelText(/nombre/i), { target: { value: 'Nuevo SL' } });
        fireEvent.change(screen.getByLabelText(/cif/i), { target: { value: 'B98765432' } });
        fireEvent.click(screen.getByRole('button', { name: /guardar proveedor/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/suppliers',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import InventoryClient from '@/app/inventory/InventoryClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('InventoryClient', () => {
    it('renderiza listado de productos inicial', () => {
        render(
            <InventoryClient
                initialProducts={[
                    {
                        id: 'p1',
                        name: 'Producto A',
                        sku: 'SKU-001',
                        costPrice: 10,
                        salePrice: 15,
                        vatPercent: 21,
                        totalStock: 5,
                        reorderPoint: 2,
                        isActive: true,
                        type: 'Product',
                    },
                ]}
                initialWarehouses={[{ id: 'w1', name: 'Almacén central' }]}
            />,
        );
        expect(screen.getByText('Producto A')).toBeInTheDocument();
        expect(screen.getByText('SKU-001')).toBeInTheDocument();
    });

    it('abre modal de nuevo producto y envía POST', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 'p-new' }) });
        vi.stubGlobal('fetch', fetchMock);

        render(
            <InventoryClient initialProducts={[]} initialWarehouses={[{ id: 'w1', name: 'Central' }]} />,
        );

        fireEvent.click(screen.getByRole('button', { name: /nuevo producto/i }));

        await waitFor(() => {
            expect(screen.getByRole('heading', { name: /nuevo producto/i })).toBeInTheDocument();
        });

        fireEvent.change(screen.getByPlaceholderText(/nombre del producto/i), { target: { value: 'Nuevo SKU test' } });
        fireEvent.change(screen.getByPlaceholderText(/ref-001/i), { target: { value: 'SKU-NEW' } });
        fireEvent.click(screen.getByRole('button', { name: /crear producto/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/inventory/products',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });
});

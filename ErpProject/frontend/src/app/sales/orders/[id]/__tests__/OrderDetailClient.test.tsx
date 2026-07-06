import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import OrderDetailClient from '@/app/sales/orders/[id]/OrderDetailClient';

vi.mock('next/navigation', () => ({
    useRouter: () => ({ push: vi.fn() }),
}));

const baseOrder = {
    id: 'order-1',
    number: 'PV-2026-00001',
    orderDate: '2026-07-01T00:00:00Z',
    customerName: 'Cliente Ventas',
    customerTaxId: 'B12345674',
    status: 'Open',
    subtotal: 500,
    taxAmount: 105,
    total: 605,
    lines: [
        {
            id: 'line-1',
            description: 'Producto A',
            quantity: 2,
            unitPrice: 250,
            taxRate: 21,
            total: 500,
        },
    ],
};

describe('OrderDetailClient', () => {
    it('renderiza detalle del pedido', () => {
        render(<OrderDetailClient id="order-1" initialOrder={baseOrder} />);
        expect(screen.getByRole('heading', { name: /pedido pv-2026-00001/i })).toBeInTheDocument();
        expect(screen.getByText('Producto A')).toBeInTheDocument();
    });

    it('muestra estado abierto', () => {
        render(<OrderDetailClient id="order-1" initialOrder={baseOrder} />);
        expect(screen.getByText(/abierto/i)).toBeInTheDocument();
    });
});

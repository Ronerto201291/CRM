import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import DeliveriesClient from '@/app/sales/deliveries/DeliveriesClient';

describe('DeliveriesClient', () => {
    const initialDeliveries = [
        {
            id: 'd-1',
            number: 'ALB-2026-001',
            deliveryDate: '2026-07-01',
            lineCount: 3,
            salesOrderId: 'so-1',
            createdAt: '2026-07-01',
        },
        {
            id: 'd-2',
            number: 'ALB-2026-002',
            deliveryDate: '2026-07-02',
            lineCount: 1,
            createdAt: '2026-07-02',
        },
    ];

    it('renderiza listado de albaranes', () => {
        render(<DeliveriesClient initialDeliveries={initialDeliveries} />);
        expect(screen.getByRole('heading', { name: /albaranes de entrega/i })).toBeInTheDocument();
        expect(screen.getByText('ALB-2026-001')).toBeInTheDocument();
        expect(screen.getByText('ALB-2026-002')).toBeInTheDocument();
    });

    it('enlace a pedido cuando hay salesOrderId', () => {
        render(<DeliveriesClient initialDeliveries={initialDeliveries} />);
        const links = screen.getAllByRole('link', { name: /ver pedido/i });
        expect(links[0]).toHaveAttribute('href', '/sales/orders/so-1');
    });

    it('enlace a nuevo albarán', () => {
        render(<DeliveriesClient initialDeliveries={[]} />);
        const link = screen.getByRole('link', { name: /nuevo albarán/i });
        expect(link).toHaveAttribute('href', '/sales/deliveries/new');
    });
});

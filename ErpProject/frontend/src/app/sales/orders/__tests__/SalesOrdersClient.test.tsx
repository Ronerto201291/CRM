import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import SalesOrdersClient from '@/app/sales/orders/SalesOrdersClient';

describe('SalesOrdersClient', () => {
    const initialOrders = [
        {
            id: 'so-1',
            number: 'PV-2026-000001',
            orderDate: '2026-03-01T10:00:00Z',
            customerName: 'Cliente Alpha',
            status: 'Open',
            total: 1500,
            lineCount: 3,
            createdAt: '2026-03-01T10:00:00Z',
        },
        {
            id: 'so-2',
            number: 'PV-2026-000002',
            orderDate: '2026-03-05T10:00:00Z',
            customerName: 'Cliente Beta',
            status: 'Completed',
            total: 800,
            lineCount: 1,
            createdAt: '2026-03-05T10:00:00Z',
        },
    ];

    it('renderiza listado de pedidos de venta', () => {
        render(<SalesOrdersClient initialOrders={initialOrders} />);
        expect(screen.getByRole('heading', { name: /pedidos de venta/i })).toBeInTheDocument();
        expect(screen.getByText('Cliente Alpha')).toBeInTheDocument();
        expect(screen.getByText('Cliente Beta')).toBeInTheDocument();
    });

    it('filtra pedidos por estado Completado', () => {
        render(<SalesOrdersClient initialOrders={initialOrders} />);
        fireEvent.click(screen.getByRole('button', { name: /completado/i }));
        expect(screen.queryByText('Cliente Alpha')).not.toBeInTheDocument();
        expect(screen.getByText('Cliente Beta')).toBeInTheDocument();
    });
});

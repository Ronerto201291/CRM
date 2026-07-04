import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import DeliveriesClient from '@/app/sales/deliveries/DeliveriesClient';

describe('DeliveriesClient', () => {
    const initialDeliveries = [
        {
            id: 'd-1',
            number: 'ALB-2026-001',
            deliveryDate: '2026-07-01',
            customerName: 'Cliente Entrega SL',
            status: 'Pending',
            lineCount: 3,
            createdAt: '2026-07-01',
        },
        {
            id: 'd-2',
            number: 'ALB-2026-002',
            deliveryDate: '2026-07-02',
            customerName: 'Otro Cliente',
            status: 'Delivered',
            lineCount: 1,
            createdAt: '2026-07-02',
        },
    ];

    it('renderiza listado de albaranes', () => {
        render(<DeliveriesClient initialDeliveries={initialDeliveries} />);
        expect(screen.getByRole('heading', { name: /albaranes de entrega/i })).toBeInTheDocument();
        expect(screen.getByText('Cliente Entrega SL')).toBeInTheDocument();
        expect(screen.getByText('ALB-2026-001')).toBeInTheDocument();
    });

    it('filtra albaranes por estado Pending', () => {
        render(<DeliveriesClient initialDeliveries={initialDeliveries} />);
        fireEvent.click(screen.getByRole('button', { name: /pendiente/i }));
        expect(screen.getByText('Cliente Entrega SL')).toBeInTheDocument();
        expect(screen.queryByText('Otro Cliente')).not.toBeInTheDocument();
    });

    it('enlace a nuevo albarán', () => {
        render(<DeliveriesClient initialDeliveries={[]} />);
        const link = screen.getByRole('link', { name: /nuevo albarán/i });
        expect(link).toHaveAttribute('href', '/sales/deliveries/new');
    });
});

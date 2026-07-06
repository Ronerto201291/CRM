import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import FacturaEClient from '@/app/billing/facturae/FacturaEClient';

describe('FacturaEClient', () => {
    const initialInvoices = [
        {
            id: 'inv-1',
            number: 'A-2026-000001',
            issueDate: '2026-07-01T00:00:00Z',
            status: 'Locked',
            isLocked: true,
            verifactuHuella: 'huella-test',
            clientName: 'Cliente FacturaE',
            total: 1210,
        },
    ];

    it('renderiza listado de facturas bloqueadas', () => {
        render(<FacturaEClient initialInvoices={initialInvoices} />);
        expect(screen.getByRole('heading', { name: /facturae \/ veri\*factu/i })).toBeInTheDocument();
        expect(screen.getByText('A-2026-000001')).toBeInTheDocument();
        expect(screen.getByText('Cliente FacturaE')).toBeInTheDocument();
    });

    it('muestra enlace a facturación', () => {
        render(<FacturaEClient initialInvoices={[]} />);
        expect(screen.getByRole('link', { name: /ir a facturación/i })).toBeInTheDocument();
    });
});

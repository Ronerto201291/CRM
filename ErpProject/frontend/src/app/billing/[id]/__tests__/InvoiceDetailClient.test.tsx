import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import InvoiceDetailClient from '@/app/billing/[id]/InvoiceDetailClient';

vi.mock('next/navigation', () => ({
    useRouter: () => ({ push: vi.fn() }),
}));

const baseInvoice = {
    id: 'inv-1',
    number: 'A-2026-000010',
    series: 'A',
    fiscalYear: 2026,
    invoiceType: 'Normal',
    issueDate: '2026-07-01T00:00:00Z',
    dueDate: '2026-08-01T00:00:00Z',
    subtotal: 1000,
    taxAmount: 210,
    irpfRate: 0,
    irpfAmount: 0,
    surchargeAmount: 0,
    total: 1210,
    status: 'Issued',
    isLocked: false,
    clientId: 'client-1',
    clientName: 'Cliente Demo',
    clientTaxId: '12345678Z',
    companyName: 'Mi Empresa SL',
    companyNif: 'B87654321',
    invoiceLines: [
        {
            id: 'line-1',
            description: 'Servicio consultoría',
            quantity: 1,
            unitPrice: 1000,
            taxRate: 21,
            surchargeRate: 0,
            lineTotal: 1000,
        },
    ],
};

describe('InvoiceDetailClient', () => {
    it('renderiza detalle de factura', () => {
        render(<InvoiceDetailClient id="inv-1" initialInvoice={baseInvoice} />);
        expect(screen.getByText('A-2026-000010')).toBeInTheDocument();
        expect(screen.getByText('Cliente Demo')).toBeInTheDocument();
        expect(screen.getByText('Servicio consultoría')).toBeInTheDocument();
    });

    it('muestra acciones cuando no está bloqueada', () => {
        render(<InvoiceDetailClient id="inv-1" initialInvoice={baseInvoice} />);
        expect(screen.getByRole('button', { name: /marcar como pagada/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /bloquear y contabilizar/i })).toBeInTheDocument();
    });
});

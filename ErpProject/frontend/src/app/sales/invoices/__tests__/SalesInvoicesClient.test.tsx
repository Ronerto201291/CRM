import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import SalesInvoicesClient from '@/app/sales/invoices/SalesInvoicesClient';

describe('SalesInvoicesClient', () => {
    const initialInvoices = [
        {
            id: 'si-1',
            number: 'FV-2026-00001',
            billingInvoiceNumber: 'A-2026-000010',
            invoiceDate: '2026-07-01T00:00:00Z',
            subTotal: 500,
            taxAmount: 105,
            total: 605,
            lineCount: 1,
            createdAt: '2026-07-01T00:00:00Z',
        },
    ];

    it('renderiza listado de facturas de venta', () => {
        render(<SalesInvoicesClient initialInvoices={initialInvoices} />);
        expect(screen.getByRole('heading', { name: /facturas de venta/i })).toBeInTheDocument();
        expect(screen.getByText('FV-2026-00001')).toBeInTheDocument();
    });
});

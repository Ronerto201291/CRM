import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import QuoteDetailClient from '@/app/billing/quotes/[id]/QuoteDetailClient';

const baseQuote = {
    id: 'quote-1',
    number: 'P-2026-001',
    seriesPrefix: 'P',
    fiscalYear: 2026,
    version: 1,
    status: 'Sent',
    clientName: 'Cliente Demo',
    clientType: 'Client',
    issueDate: '2026-07-01T00:00:00Z',
    validUntil: '2026-08-01T00:00:00Z',
    globalDiscountPct: 0,
    globalDiscountAmount: 0,
    subtotalBeforeDisc: 1000,
    subtotalAfterDisc: 1000,
    taxBaseAmount: 1000,
    taxAmount: 210,
    totalAmount: 1210,
    currency: 'EUR',
    createdAt: '2026-07-01T00:00:00Z',
    lines: [
        {
            id: 'line-1',
            sortOrder: 1,
            description: 'Servicio consultoría',
            quantity: 1,
            unitPrice: 1000,
            discountPct: 0,
            discountAmount: 0,
            taxRate: 21,
            lineSubtotal: 1000,
            lineTaxBase: 1000,
            lineTaxAmount: 210,
            lineTotalAmount: 1210,
        },
    ],
    taxBreakdown: [{ taxRate: 21, baseAmount: 1000, taxAmount: 210 }],
    statusHistory: [
        { id: 'h-1', toStatus: 'Sent', changedAt: '2026-07-02T00:00:00Z' },
    ],
};

describe('QuoteDetailClient', () => {
    it('renderiza detalle del presupuesto', () => {
        render(<QuoteDetailClient id="quote-1" initialQuote={baseQuote} />);
        expect(screen.getByText('P-2026-001')).toBeInTheDocument();
        expect(screen.getByText('Cliente Demo')).toBeInTheDocument();
        expect(screen.getByText(/servicio consultoría/i)).toBeInTheDocument();
    });

    it('muestra estado enviado', () => {
        render(<QuoteDetailClient id="quote-1" initialQuote={baseQuote} />);
        expect(screen.getAllByText(/enviado/i).length).toBeGreaterThan(0);
    });
});

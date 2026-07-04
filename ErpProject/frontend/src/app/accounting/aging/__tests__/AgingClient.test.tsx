import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import AgingClient, { type AgingReport } from '@/app/accounting/aging/AgingClient';

describe('AgingClient', () => {
    const sampleReport: AgingReport = {
        reportDate: '2026-07-01T00:00:00Z',
        dso: 45,
        dpo: 30,
        note: 'Informe de prueba P16',
        receivables: {
            type: 'Receivable',
            totalAmount: 10000,
            current: 6000,
            days31To60: 2000,
            days61To90: 1000,
            days91Plus: 1000,
            lines: [
                {
                    id: 'r-1',
                    reference: 'FAC-001',
                    counterpartyName: 'Cliente Alpha',
                    referenceDate: '2026-06-01',
                    daysOutstanding: 30,
                    amount: 6000,
                    status: 'Open',
                },
            ],
        },
        payables: {
            type: 'Payable',
            totalAmount: 5000,
            current: 5000,
            days31To60: 0,
            days61To90: 0,
            days91Plus: 0,
            lines: [],
        },
    };

    it('muestra mensaje cuando no hay informe', () => {
        render(<AgingClient initialReport={null} />);
        expect(screen.getByText(/inicia sesión/i)).toBeInTheDocument();
    });

    it('renderiza buckets de cobros y pagos', () => {
        render(<AgingClient initialReport={sampleReport} />);
        expect(screen.getByRole('heading', { name: /antigüedad de saldos/i })).toBeInTheDocument();
        expect(screen.getByText(/cobros \(clientes\)/i)).toBeInTheDocument();
        expect(screen.getByText(/pagos \(proveedores\)/i)).toBeInTheDocument();
        expect(screen.getByText('Cliente Alpha')).toBeInTheDocument();
        expect(screen.getByText(/informe de prueba p16/i)).toBeInTheDocument();
    });

    it('muestra DSO y DPO en subtítulo', () => {
        render(<AgingClient initialReport={sampleReport} />);
        expect(screen.getByText(/dso 45/i)).toBeInTheDocument();
        expect(screen.getByText(/dpo 30/i)).toBeInTheDocument();
    });
});

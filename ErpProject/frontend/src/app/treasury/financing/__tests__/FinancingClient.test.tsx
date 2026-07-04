import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import FinancingClient from '@/app/treasury/financing/FinancingClient';

describe('FinancingClient', () => {
    const initialConfirming = [
        {
            id: 'c-1',
            bank: 'CaixaBank',
            amount: 5000,
            fee: 50,
            status: 'Pending' as const,
            maturityDate: '2026-09-01',
            clientName: 'Cliente A',
        },
    ];

    it('renderiza pestaña confirming con operaciones', () => {
        render(
            <FinancingClient
                initialConfirming={initialConfirming}
                initialFactoring={[]}
                initialCreditLines={[]}
            />,
        );
        expect(screen.getByRole('heading', { name: /financiación/i })).toBeInTheDocument();
        expect(screen.getByText('CaixaBank')).toBeInTheDocument();
        expect(screen.getByText('Cliente A')).toBeInTheDocument();
    });

    it('cambia a pestaña líneas de crédito', () => {
        render(
            <FinancingClient
                initialConfirming={[]}
                initialFactoring={[]}
                initialCreditLines={[{ id: 'cl-1', bank: 'Santander', limit: 100000, drawn: 20000, available: 80000 }]}
            />,
        );
        fireEvent.click(screen.getByRole('button', { name: /líneas de crédito/i }));
        expect(screen.getByText('Santander')).toBeInTheDocument();
    });
});

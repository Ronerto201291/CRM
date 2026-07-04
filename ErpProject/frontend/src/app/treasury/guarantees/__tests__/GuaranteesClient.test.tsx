import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import GuaranteesClient from '@/app/treasury/guarantees/GuaranteesClient';

describe('GuaranteesClient', () => {
    const initialGuarantees = [
        {
            id: 'g-1',
            type: 'Aval' as const,
            beneficiary: 'Proveedor XYZ',
            amount: 25000,
            currency: 'EUR',
            startDate: '2026-01-01',
            endDate: '2026-12-31',
            status: 'Active' as const,
            autoRenew: false,
            bank: 'CaixaBank',
        },
    ];

    it('renderiza listado de garantías', () => {
        render(
            <GuaranteesClient
                initialGuarantees={initialGuarantees}
                initialCollaterals={[]}
            />,
        );
        expect(screen.getByRole('heading', { name: /garantías y avales/i })).toBeInTheDocument();
        expect(screen.getByText('Proveedor XYZ')).toBeInTheDocument();
    });

    it('cambia a pestaña colateral', () => {
        render(
            <GuaranteesClient
                initialGuarantees={[]}
                initialCollaterals={[{ id: 'c-1', type: 'Inmueble', description: 'Nave industrial', value: 500000 }]}
            />,
        );
        fireEvent.click(screen.getByRole('button', { name: /colaterales/i }));
        expect(screen.getByText('Nave industrial')).toBeInTheDocument();
    });
});

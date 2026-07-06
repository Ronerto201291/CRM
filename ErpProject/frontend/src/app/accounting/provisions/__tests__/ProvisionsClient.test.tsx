import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import ProvisionsClient from '@/app/accounting/provisions/ProvisionsClient';

describe('ProvisionsClient', () => {
    const initialProvisions = [
        {
            id: 'p-1',
            code: '490',
            description: 'Deterioro créditos',
            amount: 5000,
            dueDate: '2026-12-31',
            status: 'Active',
            createdAt: '2026-01-01',
        },
    ];

    it('renderiza listado de provisiones', () => {
        render(
            <ProvisionsClient initialProvisions={initialProvisions} initialStatusFilter="Active" />,
        );
        expect(screen.getByRole('heading', { name: /provisiones contables/i })).toBeInTheDocument();
        expect(screen.getByText('Deterioro créditos')).toBeInTheDocument();
    });

    it('abre modal de nueva provisión', () => {
        render(<ProvisionsClient initialProvisions={[]} />);
        fireEvent.click(screen.getByRole('button', { name: /nueva provisión/i }));
        expect(screen.getByRole('dialog')).toBeInTheDocument();
        expect(screen.getByText(/descripción \*/i)).toBeInTheDocument();
    });

    it('muestra filtros de estado', () => {
        render(<ProvisionsClient initialProvisions={initialProvisions} />);
        expect(screen.getByRole('button', { name: /activa/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /liberada/i })).toBeInTheDocument();
    });
});

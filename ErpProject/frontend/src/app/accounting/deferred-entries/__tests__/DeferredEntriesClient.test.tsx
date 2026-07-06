import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import DeferredEntriesClient from '@/app/accounting/deferred-entries/DeferredEntriesClient';

describe('DeferredEntriesClient', () => {
    const initialEntries = [
        {
            id: 'de-1',
            entryType: 'PrepaidExpense',
            description: 'Seguro anual',
            totalAmount: 1200,
            periodStart: '2026-01-01',
            periodEnd: '2026-12-31',
            recognizedAmount: 100,
            remainingAmount: 1100,
            monthlyAmount: 100,
            totalMonths: 12,
            status: 'Active',
            deferralAccountCode: '480',
            counterpartAccountCode: '600',
            createdAt: '2026-01-01',
        },
    ];

    it('renderiza listado de partidas diferidas', () => {
        render(<DeferredEntriesClient initialEntries={initialEntries} />);
        expect(screen.getByRole('heading', { name: /periodificaciones/i })).toBeInTheDocument();
        expect(screen.getByText('Seguro anual')).toBeInTheDocument();
    });
});

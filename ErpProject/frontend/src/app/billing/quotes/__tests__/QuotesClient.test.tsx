import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import QuotesClient from '@/app/billing/quotes/QuotesClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('QuotesClient', () => {
    const initialQuotes = [
        {
            id: 'q-1',
            number: 'P-2026-000001',
            seriesPrefix: 'P',
            fiscalYear: 2026,
            version: 1,
            status: 'Draft',
            clientName: 'Cliente Alpha',
            clientType: 'Registered',
            issueDate: '2026-03-01T10:00:00Z',
            validUntil: '2026-04-01T10:00:00Z',
            totalAmount: 1210,
            taxAmount: 210,
            taxBaseAmount: 1000,
            createdAt: '2026-03-01T10:00:00Z',
        },
        {
            id: 'q-2',
            number: 'P-2026-000002',
            seriesPrefix: 'P',
            fiscalYear: 2026,
            version: 1,
            status: 'Accepted',
            clientName: 'Cliente Beta',
            clientType: 'Registered',
            issueDate: '2026-03-05T10:00:00Z',
            validUntil: '2026-04-05T10:00:00Z',
            totalAmount: 605,
            taxAmount: 105,
            taxBaseAmount: 500,
            createdAt: '2026-03-05T10:00:00Z',
        },
    ];

    it('renderiza listado de presupuestos', () => {
        render(
            <QuotesClient
                initialQuotes={initialQuotes}
                initialClients={[]}
                initialProspects={[]}
            />,
        );
        expect(screen.getByRole('heading', { name: /presupuestos/i })).toBeInTheDocument();
        expect(screen.getByText('Cliente Alpha')).toBeInTheDocument();
        expect(screen.getByText('Cliente Beta')).toBeInTheDocument();
    });

    it('filtra presupuestos por estado', () => {
        render(
            <QuotesClient
                initialQuotes={initialQuotes}
                initialClients={[]}
                initialProspects={[]}
            />,
        );
        fireEvent.click(screen.getByRole('button', { name: /aceptado/i }));
        expect(screen.queryByText('Cliente Alpha')).not.toBeInTheDocument();
        expect(screen.getByText('Cliente Beta')).toBeInTheDocument();
    });
});

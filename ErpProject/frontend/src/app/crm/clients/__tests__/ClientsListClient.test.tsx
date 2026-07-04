import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import ClientsListClient from '@/app/crm/clients/ClientsListClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('ClientsListClient', () => {
    it('renderiza listado con clientes iniciales', () => {
        render(
            <ClientsListClient
                initialClients={[
                    { id: '1', name: 'Acme SL', email: 'a@test.com', phone: '', taxId: 'B12345678', address: '' },
                ]}
            />
        );
        expect(screen.getByRole('heading', { name: 'Clientes' })).toBeInTheDocument();
        expect(screen.getByText('Acme SL')).toBeInTheDocument();
        expect(screen.getByText('1 clientes registrados')).toBeInTheDocument();
    });
});

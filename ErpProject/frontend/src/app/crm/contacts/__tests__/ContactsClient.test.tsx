import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import ContactsClient from '@/app/crm/contacts/ContactsClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('ContactsClient', () => {
    const initialContacts = [
        {
            id: 'c-1',
            name: 'Laura Pérez',
            email: 'laura@test.com',
            phone: '600111222',
            position: 'Directora',
            clientName: 'Cliente Alpha',
        },
        {
            id: 'c-2',
            name: 'Pedro Ruiz',
            email: 'pedro@test.com',
            phone: '600333444',
            position: 'Compras',
            supplierName: 'Proveedor Beta',
        },
    ];

    it('renderiza listado de contactos', () => {
        render(<ContactsClient initialContacts={initialContacts} />);
        expect(screen.getByRole('heading', { name: /contactos/i })).toBeInTheDocument();
        expect(screen.getByText('Laura Pérez')).toBeInTheDocument();
        expect(screen.getByText('Pedro Ruiz')).toBeInTheDocument();
    });

    it('filtra contactos por búsqueda', () => {
        render(<ContactsClient initialContacts={initialContacts} />);
        fireEvent.change(screen.getByPlaceholderText(/buscar contacto/i), { target: { value: 'laura' } });
        expect(screen.getByText('Laura Pérez')).toBeInTheDocument();
        expect(screen.queryByText('Pedro Ruiz')).not.toBeInTheDocument();
    });
});

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import ClientEditorClient from '@/app/crm/clients/[id]/ClientEditorClient';

vi.mock('next/navigation', () => ({
    useRouter: () => ({ push: vi.fn() }),
}));

describe('ClientEditorClient', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn());
    });

    it('renderiza formulario de edición con datos iniciales', () => {
        render(
            <ClientEditorClient
                clientId="c1"
                initialClient={{
                    id: 'c1',
                    name: 'Acme SL',
                    taxId: 'B12345674',
                    email: 'acme@test.local',
                }}
                initialContractedServices={[]}
                serviceCatalog={[]}
            />,
        );

        expect(screen.getByRole('heading', { name: /editar cliente/i })).toBeInTheDocument();
        expect(screen.getByDisplayValue('Acme SL')).toBeInTheDocument();
        expect(screen.getByDisplayValue('B12345674')).toBeInTheDocument();
    });

    it('envía PATCH al guardar cliente existente', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({}) });
        vi.stubGlobal('fetch', fetchMock);

        render(
            <ClientEditorClient
                clientId="c1"
                initialClient={{
                    id: 'c1',
                    name: 'Acme SL',
                    taxId: 'B12345674',
                }}
                initialContractedServices={[]}
                serviceCatalog={[]}
            />,
        );

        fireEvent.change(screen.getByLabelText(/nombre/i), { target: { value: 'Acme Actualizada SL' } });
        fireEvent.click(screen.getByRole('button', { name: /guardar cliente/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/clients/c1',
                expect.objectContaining({ method: 'PATCH' }),
            );
        });
    });

    it('muestra sección de servicios contratados cuando hay datos', () => {
        render(
            <ClientEditorClient
                clientId="c1"
                initialClient={{
                    id: 'c1',
                    name: 'Acme SL',
                    taxId: 'B12345674',
                }}
                initialContractedServices={[
                    {
                        id: 'cs1',
                        clientId: 'c1',
                        serviceCatalogItemId: 'sc1',
                        serviceName: 'Mantenimiento web',
                        price: 99,
                        taxRate: 21,
                        periodicity: 'Monthly',
                        startDate: '2026-01-01',
                        nextBillingDate: '2026-08-01',
                        status: 'Active',
                    },
                ]}
                serviceCatalog={[]}
            />,
        );

        expect(screen.getByText(/servicios contratados/i)).toBeInTheDocument();
        expect(screen.getByText('Mantenimiento web')).toBeInTheDocument();
    });
});

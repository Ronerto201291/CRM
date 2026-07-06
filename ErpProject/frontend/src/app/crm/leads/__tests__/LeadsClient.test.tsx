import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import LeadsClient from '@/app/crm/leads/LeadsClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('LeadsClient', () => {
    const initialLeads = [
        {
            id: 'lead-1',
            name: 'Prospecto Alpha',
            email: 'alpha@test.com',
            phone: '600111222',
            status: 'New',
            source: 'Web',
            notes: 'Interesado',
            createdAt: '2026-07-01T00:00:00Z',
        },
    ];

    it('renderiza kanban de leads', () => {
        render(<LeadsClient initialLeads={initialLeads} />);
        expect(screen.getByRole('heading', { name: /pipeline de leads/i })).toBeInTheDocument();
        expect(screen.getByText('Prospecto Alpha')).toBeInTheDocument();
    });

    it('abre modal editar lead y envía PUT', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true });
        vi.stubGlobal('fetch', fetchMock);

        render(<LeadsClient initialLeads={initialLeads} />);

        fireEvent.click(screen.getByRole('button', { name: /^editar$/i }));

        await waitFor(() => {
            expect(screen.getByRole('dialog')).toBeInTheDocument();
        });

        fireEvent.change(screen.getByLabelText(/^nombre/i), { target: { value: 'Prospecto Editado' } });
        fireEvent.click(screen.getByRole('button', { name: /guardar/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/leads/lead-1',
                expect.objectContaining({ method: 'PUT' }),
            );
        });
    });
});

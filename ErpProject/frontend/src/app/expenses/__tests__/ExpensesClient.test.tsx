import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import ExpensesClient from '@/app/expenses/ExpensesClient';

vi.mock('@/hooks/useCachedApi', () => ({
    useCachedApi: () => ({
        fetchCached: vi.fn(),
        invalidateCached: vi.fn(),
    }),
}));

describe('ExpensesClient', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string) => {
            if (url.includes('/anomalies')) {
                return Promise.resolve({ ok: true, json: async () => ({ outliers: [], duplicates: [] }) });
            }
            return Promise.resolve({ ok: true, json: async () => ({}) });
        }));
    });

    it('renderiza listado de gastos inicial', () => {
        render(
            <ExpensesClient
                initialDocs={[
                    {
                        id: '1',
                        invoiceNumber: 'G-001',
                        supplierName: 'Proveedor SA',
                        status: 'Draft',
                        isValidated: false,
                        createdAt: '2026-07-01T00:00:00Z',
                    },
                ]}
                initialStats={{ pending: 1, approved: 0, totalVATSoportado: 0, totalBase: 0 }}
            />,
        );
        expect(screen.getByRole('heading', { name: 'Gastos' })).toBeInTheDocument();
        expect(screen.getByText('Proveedor SA')).toBeInTheDocument();
    });

    it('abre modal de crear gasto y envía POST', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ id: 'exp-1' }) });
        vi.stubGlobal('fetch', fetchMock);

        render(
            <ExpensesClient
                initialDocs={[]}
                initialStats={{ pending: 0, approved: 0, totalVATSoportado: 0, totalBase: 0 }}
            />,
        );

        fireEvent.click(screen.getByRole('button', { name: /registrar gasto/i }));

        await waitFor(() => {
            expect(screen.getByRole('dialog')).toBeInTheDocument();
        });

        fireEvent.change(screen.getByLabelText(/^proveedor$/i), { target: { value: 'Proveedor OCR' } });
        fireEvent.change(screen.getByLabelText(/base imponible/i), { target: { value: '100' } });
        fireEvent.click(screen.getByRole('button', { name: /✓ registrar gasto/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/expenses',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });

    it('abre modal editar y envía PUT', async () => {
        const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => ({}) });
        vi.stubGlobal('fetch', fetchMock);

        render(
            <ExpensesClient
                initialDocs={[
                    {
                        id: 'exp-edit-1',
                        invoiceNumber: 'G-002',
                        supplierName: 'Proveedor edit',
                        status: 'Draft',
                        isValidated: false,
                        taxBase: 80,
                        createdAt: '2026-07-02T00:00:00Z',
                    },
                ]}
                initialStats={{ pending: 1, approved: 0, totalVATSoportado: 0, totalBase: 80 }}
            />,
        );

        fireEvent.click(screen.getByRole('button', { name: /revisar/i }));

        await waitFor(() => {
            expect(screen.getByDisplayValue('Proveedor edit')).toBeInTheDocument();
        });

        fireEvent.change(screen.getByDisplayValue('Proveedor edit'), { target: { value: 'Proveedor actualizado' } });
        fireEvent.click(screen.getByRole('button', { name: /guardar revisión/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/expenses/exp-edit-1',
                expect.objectContaining({ method: 'PUT' }),
            );
        });
    });
});
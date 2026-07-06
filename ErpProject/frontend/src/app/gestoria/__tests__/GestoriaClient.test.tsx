import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import GestoriaClient from '@/app/gestoria/GestoriaClient';

describe('GestoriaClient', () => {
    const dashboard = [
        {
            companyId: 'co-1',
            companyName: 'Cliente SA',
            roleId: 'role-contable',
            roleName: 'Contable',
            adminRoleId: 'role-admin',
            contableRoleId: 'role-contable',
            monthlyBilling: 1200,
            monthlyExpenses: 300,
            overdueInvoices: 1,
            lowStockProducts: 0,
            pendingApprovals: 2,
            alertCount: 3,
        },
    ];

    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string, init?: RequestInit) => {
            if (url.includes('/gestoria/dashboard')) {
                return Promise.resolve({ ok: true, json: async () => dashboard });
            }
            if (url.includes('/gestoria/memberships/') && init?.method === 'PUT') {
                return Promise.resolve({ ok: true, json: async () => ({ message: 'Rol actualizado.' }) });
            }
            return Promise.resolve({ ok: false, json: async () => ({}) });
        }));
    });

    it('renderiza KPIs y selector de rol por empresa', async () => {
        render(<GestoriaClient />);
        expect(await screen.findByText('Cliente SA')).toBeInTheDocument();
        expect(screen.getByLabelText(/rol en esta empresa/i)).toBeInTheDocument();
        expect(screen.getByText(/facturación mes/i)).toBeInTheDocument();
    });

    it('envía PUT al cambiar rol', async () => {
        render(<GestoriaClient />);
        const select = await screen.findByLabelText(/rol en esta empresa/i);
        fireEvent.change(select, { target: { value: 'role-admin' } });
        await waitFor(() => {
            expect(fetch).toHaveBeenCalledWith(
                '/api/proxy/gestoria/memberships/co-1/role',
                expect.objectContaining({
                    method: 'PUT',
                    body: JSON.stringify({ roleId: 'role-admin' }),
                }),
            );
        });
        expect(await screen.findByText(/rol actualizado/i)).toBeInTheDocument();
    });
});

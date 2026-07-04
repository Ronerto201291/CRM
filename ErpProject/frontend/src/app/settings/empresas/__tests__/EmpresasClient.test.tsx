import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import EmpresasClient from '@/app/settings/empresas/EmpresasClient';

describe('EmpresasClient', () => {
    const initialCompanies = [
        {
            id: 'co-1',
            name: 'Empresa Demo SL',
            taxId: 'B12345674',
            isActive: true,
            country: 'ES',
            createdAt: '2026-01-01T00:00:00Z',
            planName: 'Pro',
            subscriptionActive: true,
        },
    ];

    const initialInvitations = [
        {
            id: 'inv-1',
            email: 'nuevo@test.com',
            token: 'token-abc',
            isUsed: false,
            expiresAt: '2026-12-31T00:00:00Z',
            companyId: 'co-2',
            companyName: 'Pendiente SL',
        },
    ];

    it('renderiza listado de empresas e invitaciones', () => {
        render(
            <EmpresasClient
                initialCompanies={initialCompanies}
                initialInvitations={initialInvitations}
                initialForbidden={false}
            />,
        );
        expect(screen.getByRole('heading', { name: /gestión de empresas/i })).toBeInTheDocument();
        expect(screen.getByText('Empresa Demo SL')).toBeInTheDocument();
        expect(screen.getByText('nuevo@test.com')).toBeInTheDocument();
    });

    it('abre modal de invitar empresa', () => {
        render(
            <EmpresasClient
                initialCompanies={initialCompanies}
                initialInvitations={[]}
                initialForbidden={false}
            />,
        );
        fireEvent.click(screen.getByRole('button', { name: /invitar empresa/i }));
        expect(screen.getByRole('dialog')).toBeInTheDocument();
        expect(screen.getByPlaceholderText(/empresa s\.l\./i)).toBeInTheDocument();
    });
});

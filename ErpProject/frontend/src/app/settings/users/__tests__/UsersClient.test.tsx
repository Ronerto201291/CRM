import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import UsersClient from '@/app/settings/users/UsersClient';

describe('UsersClient', () => {
    const initialRoles = [
        { id: 'role-admin', name: 'Admin' },
        { id: 'role-viewer', name: 'Viewer' },
    ];

    const initialUsers = [
        {
            id: 'u-1',
            firstName: 'Ana',
            lastName: 'García',
            email: 'ana@test.com',
            roleId: 'role-admin',
            roleName: 'Admin',
            isActive: true,
            twoFactorEnabled: false,
            createdAt: '2026-01-15T10:00:00Z',
        },
        {
            id: 'u-2',
            firstName: 'Luis',
            lastName: 'Ruiz',
            email: 'luis@test.com',
            roleId: 'role-viewer',
            roleName: 'Viewer',
            isActive: false,
            twoFactorEnabled: true,
            createdAt: '2026-02-01T10:00:00Z',
        },
    ];

    it('renderiza listado de usuarios y roles', () => {
        render(<UsersClient initialUsers={initialUsers} initialRoles={initialRoles} />);
        expect(screen.getByRole('heading', { name: /usuarios y roles/i })).toBeInTheDocument();
        expect(screen.getByText('Ana García')).toBeInTheDocument();
        expect(screen.getByText('Luis Ruiz')).toBeInTheDocument();
        expect(screen.getByText('ana@test.com')).toBeInTheDocument();
    });

    it('abre modal de nuevo usuario', () => {
        render(<UsersClient initialUsers={initialUsers} initialRoles={initialRoles} />);
        fireEvent.click(screen.getByRole('button', { name: /nuevo usuario/i }));
        expect(screen.getByLabelText(/nombre/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    });
});

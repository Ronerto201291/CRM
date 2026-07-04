import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/app/actions/auth', () => ({
    loginAction: vi.fn(),
    verifyTotpAction: vi.fn(),
}));

vi.mock('@/components/Logo', () => ({
    default: () => <div data-testid="logo">Logo</div>,
}));

describe('AdminPage (login)', () => {
    it('renderiza formulario de login', async () => {
        const AdminPage = (await import('@/app/admin/page')).default;
        render(<AdminPage />);
        expect(screen.getByTestId('logo')).toBeInTheDocument();
        expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/contraseña/i)).toBeInTheDocument();
    });
});

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import ApiKeysClient from '@/app/settings/api-keys/ApiKeysClient';

describe('ApiKeysClient', () => {
    const initialApiKeys = [
        {
            id: 'key-1',
            name: 'Integración ERP',
            keyPrefix: 'erp_abc',
            createdAt: '2026-01-10T10:00:00Z',
            lastUsed: '2026-03-01T12:00:00Z',
            rateLimit: 1000,
            isActive: true,
        },
        {
            id: 'key-2',
            name: 'Webhook externo',
            keyPrefix: 'wh_xyz',
            createdAt: '2026-02-05T10:00:00Z',
            rateLimit: 500,
            isActive: false,
        },
    ];

    it('renderiza listado de API keys', () => {
        render(<ApiKeysClient initialApiKeys={initialApiKeys} />);
        expect(screen.getByRole('heading', { name: /api keys/i })).toBeInTheDocument();
        expect(screen.getByText('Integración ERP')).toBeInTheDocument();
        expect(screen.getByText('Webhook externo')).toBeInTheDocument();
    });

    it('muestra botón crear nueva API key', () => {
        render(<ApiKeysClient initialApiKeys={[]} />);
        expect(screen.getByRole('button', { name: /crear nueva api key/i })).toBeInTheDocument();
        expect(screen.getByText(/no hay api keys creadas/i)).toBeInTheDocument();
    });
});

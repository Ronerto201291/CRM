import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import AuditLogsClient from '@/app/settings/audit-logs/AuditLogsClient';

describe('AuditLogsClient', () => {
    const initialLogs = [
        {
            id: 'log-1',
            entityType: 'Client',
            entityId: 'c-1',
            action: 'Created',
            userId: 'u-1',
            userName: 'Admin Test',
            changes: '{"name":"Cliente Alpha"}',
            timestamp: '2026-03-01T10:00:00Z',
            ipAddress: '127.0.0.1',
        },
    ];

    it('renderiza título y filtros con datos iniciales', () => {
        render(
            <AuditLogsClient
                initialLogs={initialLogs}
                initialDateFrom="2026-01-01"
                initialDateTo="2026-12-31"
            />,
        );
        expect(screen.getByRole('heading', { name: /registro de auditoría/i })).toBeInTheDocument();
        expect(screen.getByLabelText(/^acción$/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/^tipo de entidad$/i)).toBeInTheDocument();
        expect(screen.getByText('Admin Test')).toBeInTheDocument();
    });
});

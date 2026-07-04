import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import AutomationClient from '@/app/settings/automation/AutomationClient';

describe('AutomationClient', () => {
    const initialRules = [
        {
            id: 'rule-1',
            name: 'Alerta facturas',
            description: 'Recordatorio cobro',
            triggerEvent: 'OnInvoiceCreated',
            isActive: true,
            conditionsSummary: 'Total > 1000',
            actionsSummary: 'SendEmail',
            createdAt: '2026-07-01T00:00:00Z',
        },
    ];

    it('renderiza reglas del sistema y personalizadas', () => {
        render(<AutomationClient initialRules={initialRules} />);
        expect(screen.getByText('Alerta facturas')).toBeInTheDocument();
        expect(screen.getByText(/facturas vencidas/i)).toBeInTheDocument();
    });

    it('valida formulario antes de crear regla', async () => {
        const fetchMock = vi.fn();
        vi.stubGlobal('fetch', fetchMock);

        render(<AutomationClient initialRules={[]} />);

        fireEvent.click(screen.getByRole('button', { name: /crear regla/i }));
        fireEvent.click(screen.getByRole('button', { name: /guardar regla/i }));

        await waitFor(() => {
            expect(screen.getByText(/el nombre de la regla es obligatorio/i)).toBeInTheDocument();
        });
        expect(fetchMock).not.toHaveBeenCalled();
    });
});

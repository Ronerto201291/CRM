import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import FiscalClient from '@/app/fiscal/FiscalClient';

describe('FiscalClient', () => {
    it('renderiza calendario fiscal con eventos iniciales', () => {
        render(
            <FiscalClient
                initialEvents={[
                    {
                        id: '1',
                        modelCode: '303',
                        modelName: 'Modelo 303',
                        year: 2026,
                        quarter: 2,
                        deadlineDate: '2026-07-20',
                        reminderDate: '2026-07-15',
                        status: 'Pending',
                        diasRestantes: 16,
                        isOverdue: false,
                    },
                ]}
                initialYear={2026}
            />,
        );

        expect(screen.getByRole('heading', { name: /calendario fiscal/i })).toBeInTheDocument();
        expect(screen.getByText('303')).toBeInTheDocument();
        expect(screen.getByText('Pendiente')).toBeInTheDocument();
    });
});

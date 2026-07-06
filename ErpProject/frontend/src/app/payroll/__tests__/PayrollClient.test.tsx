import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import PayrollClient from '@/app/payroll/PayrollClient';

describe('PayrollClient', () => {
    it('renderiza empleados y liquidaciones iniciales', () => {
        render(
            <PayrollClient
                initialEmployees={[
                    {
                        id: 'e1',
                        taxId: '12345678Z',
                        fullName: 'Ana García',
                        hireDate: '2024-01-01',
                        contractType: 'Indefinido',
                        weeklyHours: 40,
                    },
                ]}
                initialSettlements={[
                    {
                        id: 's1',
                        year: 2026,
                        month: 6,
                        status: 'Draft',
                        lineCount: 0,
                        totalGross: 0,
                        totalIrpf: 0,
                        totalEmployerSs: 0,
                    },
                ]}
                initialTemplates={[]}
                initialYear={2026}
            />,
        );

        expect(screen.getByRole('heading', { name: /nóminas/i })).toBeInTheDocument();
        expect(screen.getByTestId('legal-disclaimer-payroll-module')).toBeInTheDocument();
        expect(screen.getByTestId('legal-disclaimer-payroll-export')).toBeInTheDocument();
        expect(screen.getAllByText('Ana García').length).toBeGreaterThanOrEqual(1);
        expect(screen.getByText('6/2026')).toBeInTheDocument();
    });

    it('crea empleado vía POST', async () => {
        const fetchMock = vi.fn().mockImplementation((url: string) => {
            if (url.includes('/employees') || url.includes('/templates')) {
                return Promise.resolve({ ok: true, json: async () => [] });
            }
            if (url.includes('/settlements')) {
                return Promise.resolve({ ok: true, json: async () => [] });
            }
            return Promise.resolve({ ok: true, json: async () => ({}) });
        });
        vi.stubGlobal('fetch', fetchMock);

        render(
            <PayrollClient initialEmployees={[]} initialSettlements={[]} initialTemplates={[]} initialYear={2026} />,
        );

        fireEvent.change(screen.getByPlaceholderText('NIF'), { target: { value: '87654321X' } });
        fireEvent.change(screen.getByPlaceholderText('Nombre completo'), { target: { value: 'Nuevo Empleado' } });
        fireEvent.click(screen.getByRole('button', { name: /^añadir$/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                '/api/proxy/payroll/employees',
                expect.objectContaining({ method: 'POST' }),
            );
        });
    });

    it('descarga RED vía GET export/red', async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: true,
            headers: { get: (k: string) => k.includes('disclaimer') ? 'Export orientativo TGSS' : null },
            blob: async () => new Blob(['01'], { type: 'text/plain' }),
        });
        vi.stubGlobal('fetch', fetchMock);
        vi.stubGlobal('URL', {
            createObjectURL: vi.fn(() => 'blob:mock'),
            revokeObjectURL: vi.fn(),
        });

        render(
            <PayrollClient initialEmployees={[]} initialSettlements={[]} initialTemplates={[]} initialYear={2026} />,
        );

        fireEvent.click(screen.getByRole('button', { name: /RED\/SILTRA/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                expect.stringContaining('/api/proxy/payroll/export/red?year=2026'),
            );
        });
    });
});

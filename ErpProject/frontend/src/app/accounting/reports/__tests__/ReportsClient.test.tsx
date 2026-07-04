import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import ReportsClient from '@/app/accounting/reports/ReportsClient';

const initialDateRange = { inicio: '2026-01-01', fin: '2026-06-30' };

describe('ReportsClient', () => {
    it('renderiza libro diario inicial', () => {
        render(
            <ReportsClient
                initialDiario={{
                    lineas: [
                        {
                            fecha: '2026-03-01',
                            numero: 'AS-001',
                            cuenta: '572',
                            descripcion: 'Ingreso',
                            debe: 1000,
                            haber: 0,
                            saldo: 1000,
                        },
                    ],
                    totalDebe: 1000,
                    totalHaber: 1000,
                    totalRegistros: 1,
                }}
                initialDateRange={initialDateRange}
            />,
        );

        expect(screen.getByRole('heading', { name: /libro diario/i })).toBeInTheDocument();
    });

    it('cambia a pestaña balance y hace fetch', async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({
                activo: { nombre: 'Activo', lineas: [], total: 0 },
                pasivo: { nombre: 'Pasivo', lineas: [], total: 0 },
                patrimonio: { nombre: 'Patrimonio', lineas: [], total: 0 },
                totalActivo: 0,
                totalPasivoPatrimonio: 0,
                estaBalanceado: true,
            }),
        });
        vi.stubGlobal('fetch', fetchMock);

        render(
            <ReportsClient initialDiario={null} initialDateRange={initialDateRange} />,
        );

        fireEvent.click(screen.getByRole('button', { name: /balance/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                expect.stringContaining('/api/proxy/reports/balance'),
            );
        });
    });

    it('cambia a pestaña PyG y hace fetch', async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({
                ingresos: { nombre: 'Ingresos', lineas: [], subTotal: 500 },
                gastos: { nombre: 'Gastos', lineas: [], subTotal: 200 },
                totalIngresos: 500,
                totalGastos: 200,
                resultadoBruto: 300,
                resultadoNeto: 300,
            }),
        });
        vi.stubGlobal('fetch', fetchMock);

        render(
            <ReportsClient initialDiario={null} initialDateRange={initialDateRange} />,
        );

        fireEvent.click(screen.getByRole('button', { name: /profit\s*&\s*loss/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(
                expect.stringContaining('/api/proxy/reports/pyg'),
            );
        });
    });
});

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import DashboardClient from '@/app/dashboard/DashboardClient';
import type { DashboardStats } from '@/lib/buildDashboardStats';

const sampleStats: DashboardStats = {
    revenue: 1000,
    totalExpenses: 400,
    profit: 600,
    pendingExpenses: 1,
    ivaRepercutido: 210,
    ivaSoportado: 84,
    recentInvoices: [],
    recentExpenses: [],
};

describe('DashboardClient', () => {
    it('renderiza KPIs del dashboard', () => {
        render(<DashboardClient initialData={sampleStats} />);
        expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
        expect(screen.getByText('Ingresos Cobrados')).toBeInTheDocument();
        expect(screen.getByText('Gastos Aprobados')).toBeInTheDocument();
        expect(screen.getByText('Resultado Neto')).toBeInTheDocument();
    });
});

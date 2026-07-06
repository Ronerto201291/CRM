import { parseListResponse } from './parseListResponse';

export interface DashboardInvoice {
    id: string;
    number: string;
    clientName?: string;
    total?: number;
    status: string;
}

export interface DashboardStats {
    revenue: number;
    totalExpenses: number;
    profit: number;
    ivaRepercutido: number;
    ivaSoportado: number;
    pendingExpenses: number;
    recentInvoices: DashboardInvoice[];
    recentExpenses: never[];
}

const emptyStats = (): DashboardStats => ({
    revenue: 0,
    totalExpenses: 0,
    profit: 0,
    ivaRepercutido: 0,
    ivaSoportado: 0,
    pendingExpenses: 0,
    recentInvoices: [],
    recentExpenses: [],
});

export function buildDashboardStats(
    invoicesRaw: unknown,
    expStatsRaw: unknown,
    ivaRaw: unknown,
): DashboardStats {
    try {
        const invoices = parseListResponse<DashboardInvoice>(invoicesRaw);
        const expStats = (expStatsRaw && typeof expStatsRaw === 'object')
            ? expStatsRaw as { totalBase?: number; pending?: number }
            : { totalBase: 0, pending: 0 };
        const iva = (ivaRaw && typeof ivaRaw === 'object')
            ? ivaRaw as { ivaRepercutido?: number; ivaSoportado?: number }
            : { ivaRepercutido: 0, ivaSoportado: 0 };

        const revenue = invoices
            .filter((i) => i.status === 'Paid' || i.status === 'Locked')
            .reduce((sum, i) => sum + (i.total || 0), 0);

        return {
            revenue,
            totalExpenses: expStats.totalBase || 0,
            profit: revenue - (expStats.totalBase || 0),
            ivaRepercutido: iva.ivaRepercutido || 0,
            ivaSoportado: iva.ivaSoportado || 0,
            pendingExpenses: expStats.pending || 0,
            recentInvoices: invoices.slice(0, 6),
            recentExpenses: [],
        };
    } catch {
        return emptyStats();
    }
}

import { serverFetch } from '@/lib/serverFetch';
import ReportsClient from './ReportsClient';

export default async function ReportsPage() {
    const inicio = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];
    const fin = new Date().toISOString().split('T')[0];
    const initialDiario = await serverFetch<{
        lineas: unknown[];
        totalDebe: number;
        totalHaber: number;
        totalRegistros: number;
    }>(`reports/diario?fechaInicio=${inicio}&fechaFin=${fin}`);
    return (
        <ReportsClient
            initialDiario={initialDiario}
            initialDateRange={{ inicio, fin }}
        />
    );
}

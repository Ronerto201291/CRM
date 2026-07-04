import { serverFetch, serverFetchList } from '@/lib/serverFetch';
import AccountingClient from './AccountingClient';

export interface JournalEntry {
    id: string; date: string; reference: string; description: string;
    sourceType?: string; isPosted: boolean;
    lines: { accountCode: string; accountName: string; debit: number; credit: number; }[];
    totalDebit: number; totalCredit: number;
}
export interface BalanceRow { accountCode: string; accountName: string; totalDebit: number; totalCredit: number; balance: number; }
export interface IvaDetailRow { taxRate?: number; base?: number; amount?: number; label?: string; }
export interface IvaSummary { total: number; details: IvaDetailRow[]; }
export interface LiquidacionIva { ivaRepercutido?: number; ivaSoportado?: number; resultado?: number; }

export default async function AccountingPage() {
    const year = new Date().getFullYear();

    const [journal, balance, ivaSoportado, ivaRepercutido, liquidacion] = await Promise.all([
        serverFetchList<JournalEntry>(`accounting/journal?year=${year}&pageSize=500`),
        serverFetchList<BalanceRow>(`accounting/balance?year=${year}`),
        serverFetch<IvaSummary>(`accounting/iva-soportado?year=${year}`),
        serverFetch<IvaSummary>(`accounting/iva-repercutido?year=${year}`),
        serverFetch<LiquidacionIva>(`accounting/liquidacion-iva?year=${year}`),
    ]);

    return (
        <AccountingClient
            initialJournal={journal}
            initialBalance={balance}
            initialIvaSoportado={ivaSoportado ?? { total: 0, details: [] }}
            initialIvaRepercutido={ivaRepercutido ?? { total: 0, details: [] }}
            initialLiquidacion={liquidacion}
        />
    );
}

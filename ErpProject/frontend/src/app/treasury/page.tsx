import { serverFetchList } from '@/lib/serverFetch';
import TreasuryClient from './TreasuryClient';

export interface BankAccount {
    id: string; name: string; iban: string; bic?: string;
    bankName: string; currentBalance: number; currencyCode: string; isActive: boolean; notes?: string;
}

export default async function TreasuryPage() {
    const initialAccounts = await serverFetchList<BankAccount>('treasury/bank-accounts');
    return <TreasuryClient initialAccounts={initialAccounts} />;
}

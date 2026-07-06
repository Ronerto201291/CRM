import { serverFetchList } from '@/lib/serverFetch';
import FinancingClient from './FinancingClient';

interface ConfirmingOp {
    id: string;
    bank: string;
    amount: number;
    fee: number;
    status: string;
    maturityDate: string;
    clientName?: string;
}

interface FactoringOp {
    id: string;
    clientName: string;
    amount: number;
    fee: number;
    status: string;
    maturityDate?: string;
}

interface CreditLine {
    id: string;
    bank: string;
    limit: number;
    drawn: number;
    available: number;
    interestRate?: number;
}

export default async function FinancingPage() {
    const [initialConfirming, initialFactoring, initialCreditLines] = await Promise.all([
        serverFetchList<ConfirmingOp>('v1/treasury/financing/confirming'),
        serverFetchList<FactoringOp>('v1/treasury/financing/factoring'),
        serverFetchList<CreditLine>('v1/treasury/financing/credit-lines'),
    ]);
    return (
        <FinancingClient
            initialConfirming={initialConfirming}
            initialFactoring={initialFactoring}
            initialCreditLines={initialCreditLines}
        />
    );
}

import { serverFetch } from '@/lib/serverFetch';
import ExpensesClient from './ExpensesClient';

interface ExpenseDoc {
    id: string;
    status: string;
    createdAt: string;
}

interface Stats {
    pending: number;
    approved: number;
    totalVATSoportado: number;
    totalBase: number;
}

export default async function ExpensesPage() {
    const [docs, stats] = await Promise.all([
        serverFetch<ExpenseDoc[]>('expenses'),
        serverFetch<Stats>('expenses/stats'),
    ]);
    return (
        <ExpensesClient
            initialDocs={Array.isArray(docs) ? docs : []}
            initialStats={stats}
        />
    );
}

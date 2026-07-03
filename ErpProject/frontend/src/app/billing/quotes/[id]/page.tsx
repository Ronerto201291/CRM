import { serverFetch } from '@/lib/serverFetch';
import QuoteDetailClient from './QuoteDetailClient';

export default async function QuoteDetailPage({
    params,
}: {
    params: Promise<{ id: string }>;
}) {
    const { id } = await params;
    const initialQuote = await serverFetch(`quotes/${id}`);
    return <QuoteDetailClient id={id} initialQuote={initialQuote as never} />;
}

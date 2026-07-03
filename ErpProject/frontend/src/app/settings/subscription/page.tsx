import { serverFetch } from '@/lib/serverFetch';
import SubscriptionClient from './SubscriptionClient';

interface Plan { id: string; name: string; monthlyPrice: number; yearlyPrice: number; maxUsers: number; maxInvoicesPerMonth: number; }
interface Subscription { id: string; planId: string; planName: string; status: string; stripeStatus?: string; currentPeriodStart?: string; currentPeriodEnd?: string; }
interface TenantModule { id: string; moduleName: string; isEnabled: boolean; }
interface StripeInvoice { id: string; amount: number; currency: string; status: string; created: string; invoiceUrl?: string; }

export default async function SubscriptionPage() {
    const [plans, subscription, modules, stripeInvoices] = await Promise.all([
        serverFetch<Plan[]>('subscription/plans'),
        serverFetch<Subscription>('subscription'),
        serverFetch<TenantModule[]>('tenant/modules'),
        serverFetch<StripeInvoice[]>('subscription/invoices'),
    ]);

    return (
        <SubscriptionClient
            initialPlans={Array.isArray(plans) ? plans : []}
            initialSubscription={subscription ?? null}
            initialModules={Array.isArray(modules) ? modules : []}
            initialStripeInvoices={Array.isArray(stripeInvoices) ? stripeInvoices : []}
        />
    );
}

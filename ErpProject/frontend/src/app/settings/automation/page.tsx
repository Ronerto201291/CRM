import { serverFetchList } from '@/lib/serverFetch';
import AutomationClient from './AutomationClient';

interface Rule {
    id: string;
    name: string;
    description: string;
    triggerEvent: string;
    isActive: boolean;
    conditionsSummary: string;
    actionsSummary: string;
    createdAt: string;
}

export default async function AutomationSettingsPage() {
    const initialRules = await serverFetchList<Rule>('automation/rules');
    return <AutomationClient initialRules={initialRules} />;
}

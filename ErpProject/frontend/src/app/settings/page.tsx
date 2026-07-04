import { serverFetch } from '@/lib/serverFetch';
import SettingsClient from './SettingsClient';

export interface Company {
    id: string; name: string; taxId: string; address?: string;
    phone?: string; email?: string; publicUploadToken: string; qrUploadEnabled: boolean;
}

export default async function SettingsPage() {
    const initialCompany = await serverFetch<Company>('company');
    return <SettingsClient initialCompany={initialCompany} />;
}

import { serverFetchList } from '@/lib/serverFetch';
import IspClient from './IspClient';

interface IspRecord {
    id: string;
    supplierCountryCode: string;
    vatableBase: number;
    vatRate: number;
    vatAmount: number;
    isReverseCharge: boolean;
}

export default async function ISPPage() {
    const initialIspList = await serverFetchList<IspRecord>('v1/accounting/isp');
    return <IspClient initialIspList={initialIspList} />;
}

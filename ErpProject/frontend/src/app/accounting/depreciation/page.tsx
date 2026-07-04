import { serverFetchList } from '@/lib/serverFetch';
import DepreciationClient from './DepreciationClient';

export interface FixedAsset {
  id: string;
  assetCode: string;
  name: string;
  description?: string;
  acquisitionDate: string;
  commissioningDate: string;
  acquisitionCost: number;
  residualValue: number;
  usefulLifeYears: number;
  amortizationMethod: string;
  assetAccountCode: string;
  depreciationAccountCode: string;
  accumDepreciationAccountCode: string;
  accumulatedDepreciation: number;
  netBookValue: number;
  monthlyDepreciation: number;
  lastAmortizationDate?: string;
  status: string;
  disposedAt?: string;
  notes?: string;
  createdAt: string;
}

export default async function DepreciationPage() {
  const initialAssets = await serverFetchList<FixedAsset>('v1/accounting/fixed-assets?status=Active');
  return (
    <DepreciationClient
      initialAssets={initialAssets}
      initialStatusFilter="Active"
    />
  );
}

import { serverFetchList } from '@/lib/serverFetch';
import BudgetsClient from './BudgetsClient';

export interface BudgetSummary {
  id: string;
  name: string;
  fiscalYear: number;
  startDate: string;
  endDate: string;
  status: string;
  linesCount: number;
  totalBudgeted: number;
  createdAt: string;
}

export interface BudgetDetail {
  id: string;
  name: string;
  fiscalYear: number;
  startDate: string;
  endDate: string;
  status: string;
  lines: BudgetLine[];
}

export interface BudgetLine {
  id: string;
  budgetId: string;
  accountId?: string;
  accountCode?: string;
  costCenterId?: string;
  type: string;
  budgetedAmount: number;
}

export interface BudgetLineAnalysis {
  lineId: string;
  accountCode?: string;
  type: string;
  budgeted: number;
  actual: number;
  variance: number;
  variancePercent: number;
  status: string;  // OnTrack | OverBudget | UnderBudget
}

export default async function BudgetsPage() {
  const initialYearFilter = new Date().getFullYear();
  const initialBudgets = await serverFetchList<BudgetSummary>(`v1/accounting/budgets?fiscalYear=${initialYearFilter}`);
  return (
    <BudgetsClient
      initialBudgets={initialBudgets}
      initialYearFilter={initialYearFilter}
    />
  );
}

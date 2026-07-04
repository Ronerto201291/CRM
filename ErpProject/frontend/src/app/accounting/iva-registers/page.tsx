import { serverFetchList } from '@/lib/serverFetch';
import IvaRegistersClient from './IvaRegistersClient';

export interface InvoiceRow {
  id: string;
  clientName?: string;
  subtotal?: number;
  taxAmount?: number;
  total?: number;
  invoiceLines?: { tipoOperacion?: string }[];
  issueDate?: string;
}

export interface ExpenseDoc {
  id: string;
  supplierName?: string;
  taxBase?: number;
  vatAmount?: number;
  total?: number;
  issueDate?: string;
  documentDate?: string;
}

export interface RegistersSummary {
  purchaseTotal: number;
  salesTotal: number;
  purchaseRecords: number;
  salesRecords: number;
  intraEU: number;
}

function computeForYear(invoices: InvoiceRow[], expenses: ExpenseDoc[], year: number) {
  const yearInvoices = invoices.filter((i) => {
    const d = i.issueDate;
    return d ? new Date(d).getFullYear() === year : false;
  });
  const yearExpenses = expenses.filter((e) => {
    const d = e.issueDate ?? e.documentDate;
    return d ? new Date(d).getFullYear() === year : false;
  });

  const intraEU = yearInvoices.filter((i) =>
    (i.invoiceLines ?? []).some((l) => l.tipoOperacion === "IntraComunitario")
  ).length;

  const registers: RegistersSummary = {
    purchaseTotal: yearExpenses.reduce((s, e) => s + (e.taxBase ?? e.total ?? 0), 0),
    salesTotal: yearInvoices.reduce((s, i) => s + (i.subtotal ?? i.total ?? 0), 0),
    purchaseRecords: yearExpenses.length,
    salesRecords: yearInvoices.length,
    intraEU,
  };

  return {
    registers,
    purchaseRows: yearExpenses.slice(0, 5),
    salesRows: yearInvoices.slice(0, 5),
  };
}

export default async function IvaRegistersPage() {
  const year = new Date().getFullYear();

  const [invoices, expenses] = await Promise.all([
    serverFetchList<InvoiceRow>('invoices?pageSize=500'),
    serverFetchList<ExpenseDoc>('expenses/documents'),
  ]);

  const { registers, purchaseRows, salesRows } = computeForYear(invoices, expenses, year);

  return (
    <IvaRegistersClient
      initialYear={year}
      initialRegisters={registers}
      initialPurchaseRows={purchaseRows}
      initialSalesRows={salesRows}
    />
  );
}

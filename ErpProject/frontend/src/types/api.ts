// Tipos de API comunes
export interface ApiError {
    error?: string;
    message?: string;
    details?: string;
}

export interface AuthResponse {
    id: string;
    email: string;
    firstName?: string;
    lastName?: string;
    token: string;
    refreshToken?: string;
    companyId: string;
    companyName: string;
    role: string;
}

// Facturas (Invoices)
export interface Invoice {
    id: string;
    number: string;
    series: string;
    fiscalYear: number;
    customerId: string;
    customerName?: string;
    issueDate: string;
    dueDate?: string;
    subtotal: number;
    taxAmount: number;
    irpfAmount: number;
    total: number;
    status: 'Draft' | 'Issued' | 'Paid' | 'Locked';
    isLocked: boolean;
    hash?: string;
    createdAt: string;
}

export interface InvoiceLine {
    id: string;
    invoiceId: string;
    description: string;
    quantity: number;
    unitPrice: number;
    vatPercent: number;
    total: number;
}

export interface CreateInvoiceRequest {
    customerId: string;
    series: string;
    issueDate: string;
    dueDate?: string;
    lines: {
        description: string;
        quantity: number;
        unitPrice: number;
        vatPercent: number;
    }[];
}

// Gastos (Expenses)
export interface ExpenseDocument {
    id: string;
    invoiceNumber?: string;
    supplierName?: string;
    supplierTaxId?: string;
    issueDate?: string;
    taxBase?: number;
    vatRate?: number;
    vatAmount?: number;
    irpfRate?: number;
    irpfAmount?: number;
    total?: number;
    status: 'Draft' | 'Reviewed' | 'Approved';
    isValidated: boolean;
    isLocked: boolean;
    hashSignature?: string;
    ocrData?: Record<string, unknown>;
    createdAt: string;
}

export interface ExpenseUpload {
    id: string;
    fileName: string;
    filePath: string;
    contentType: string;
    status: 'Pending' | 'Processed' | 'Error';
    comment?: string;
    uploadedAt: string;
    expenseDocumentId?: string;
}

export interface ExpenseStats {
    pending: number;
    approved: number;
    totalVATSoportado: number;
    totalBase: number;
}

// Contabilidad (Accounting)
export interface Account {
    id: string;
    code: string;
    name: string;
    type: 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense';
}

export interface JournalEntry {
    id: string;
    entryDate: string;
    reference: string;
    sourceType: 'Invoice' | 'Expense' | 'Manual';
    isPosted: boolean;
    lines: JournalEntryLine[];
    createdAt: string;
}

export interface JournalEntryLine {
    id: string;
    journalEntryId: string;
    accountId: string;
    accountCode?: string;
    accountName?: string;
    debit?: number;
    credit?: number;
}

// CRM
export interface Customer {
    id: string;
    name: string;
    taxId?: string;
    email?: string;
    phone?: string;
    address?: string;
    createdAt: string;
}

export interface Supplier {
    id: string;
    name: string;
    taxId?: string;
    email?: string;
    phone?: string;
    address?: string;
    createdAt: string;
}

export interface Contact {
    id: string;
    name?: string;
    email?: string;
    phone?: string;
    customerId?: string;
    supplierId?: string;
    createdAt: string;
}

// Inventario
export interface Product {
    id: string;
    sku: string;
    name: string;
    description?: string;
    type: string;
    costPrice?: number;
    salePrice?: number;
    vatPercent: number;
    trackStock: boolean;
    isActive: boolean;
    createdAt: string;
}

export interface Stock {
    id: string;
    productId: string;
    warehouseId: string;
    quantity: number;
    lastUpdated?: string;
}

export interface StockMovement {
    id: string;
    productId: string;
    warehouseId: string;
    movementType: 'In' | 'Out' | 'Adjustment' | 'Return';
    quantity: number;
    unitCost: number;
    referenceType?: 'Invoice' | 'Expense';
    referenceId?: string;
    createdAt: string;
}

// Dashboard
export interface DashboardStats {
    revenue: number;
    totalExpenses: number;
    profit: number;
    ivaRepercutido: number;
    ivaSoportado: number;
    pendingExpenses: number;
    recentInvoices: Invoice[];
    recentExpenses: ExpenseDocument[];
}

// Presupuestos (Quotes)
export type QuoteStatus = 'Draft' | 'Sent' | 'Accepted' | 'Rejected' | 'Expired' | 'Converted' | 'Superseded';

export interface QuoteSummary {
    id: string;
    number: string;
    seriesPrefix: string;
    fiscalYear: number;
    version: number;
    status: QuoteStatus;
    clientName?: string;
    clientType: string;
    issueDate: string;
    validUntil: string;
    totalAmount: number;
    taxAmount: number;
    taxBaseAmount: number;
    createdAt: string;
    convertedToInvoiceId?: string;
}

export interface QuoteLineDetail {
    id: string;
    sortOrder: number;
    description: string;
    productCode?: string;
    unit?: string;
    quantity: number;
    unitPrice: number;
    discountPct: number;
    discountAmount: number;
    taxRate: number;
    lineSubtotal: number;
    lineTaxBase: number;
    lineTaxAmount: number;
    lineTotalAmount: number;
}

export interface QuoteTaxBreakdown {
    taxRate: number;
    baseAmount: number;
    taxAmount: number;
}

export interface QuoteStatusHistory {
    id: string;
    fromStatus?: string;
    toStatus: string;
    changedAt: string;
    reason?: string;
}

export interface QuoteDetail extends QuoteSummary {
    clientId?: string;
    clientTaxId?: string;
    clientEmail?: string;
    clientPhone?: string;
    clientAddress?: string;
    globalDiscountPct: number;
    globalDiscountAmount: number;
    subtotalBeforeDisc: number;
    subtotalAfterDisc: number;
    notes?: string;
    internalNotes?: string;
    currency: string;
    sentAt?: string;
    acceptedAt?: string;
    rejectedAt?: string;
    convertedAt?: string;
    lines: QuoteLineDetail[];
    taxBreakdown: QuoteTaxBreakdown[];
    statusHistory: QuoteStatusHistory[];
}

export interface QuotePublicView {
    number: string;
    seriesPrefix: string;
    fiscalYear: number;
    version: number;
    status: QuoteStatus;
    clientName?: string;
    clientTaxId?: string;
    clientEmail?: string;
    issueDate: string;
    validUntil: string;
    globalDiscountPct: number;
    globalDiscountAmount: number;
    subtotalBeforeDisc: number;
    taxBaseAmount: number;
    taxAmount: number;
    totalAmount: number;
    notes?: string;
    currency: string;
    companyName: string;
    companyTaxId?: string;
    companyEmail?: string;
    companyPhone?: string;
    companyAddress?: string;
    lines: QuoteLineDetail[];
    taxBreakdown: QuoteTaxBreakdown[];
}

export interface QuoteLineInput {
    description: string;
    productCode?: string;
    unit?: string;
    quantity: number;
    unitPrice: number;
    discountPct: number;
    taxRate: number;
    sortOrder: number;
}

export interface CreateQuoteRequest {
    clientId?: string;
    clientType: string;
    clientName?: string;
    clientTaxId?: string;
    clientEmail?: string;
    clientPhone?: string;
    clientAddress?: string;
    issueDate: string;
    validUntil: string;
    globalDiscountPct: number;
    notes?: string;
    internalNotes?: string;
    lines: QuoteLineInput[];
}

// API Responses
export interface PaginatedResponse<T> {
    items: T[];
    total: number;
    page: number;
    pageSize: number;
}

export interface ApiResponse<T> {
    success: boolean;
    data?: T;
    error?: string;
    message?: string;
}

// Purchasing
export interface PurchaseOrder {
    id: string;
    number: string;
    orderDate: string;
    supplierName: string;
    status: 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';
    subtotal: number;
    taxAmount: number;
    total: number;
    createdAt: string;
}

export interface PurchaseOrderLine {
    id?: string;
    productId?: string;
    productName?: string;
    quantity: number;
    unitPrice: number;
    deliveredQuantity: number;
    billedQuantity: number;
}

export interface SupplierInvoice {
    id: string;
    number: string;
    invoiceDate: string;
    supplierId?: string;
    supplierName?: string;
    supplierTaxId?: string;
    subtotal: number;
    taxAmount: number;
    total: number;
    status: 'Draft' | 'Approved' | 'Paid';
    createdAt: string;
}

export interface GoodsReceipt {
    id: string;
    number: string;
    receiptDate: string;
    purchaseOrderId?: string;
    supplierName?: string;
    status: 'Draft' | 'Completed';
    lineCount: number;
    createdAt: string;
}

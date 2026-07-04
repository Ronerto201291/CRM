import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import CreditNotesClient from '@/app/billing/credit-notes/CreditNotesClient';

vi.mock('next/navigation', () => ({
    useRouter: () => ({ push: vi.fn() }),
}));

describe('CreditNotesClient', () => {
    const lockedInvoice = {
        id: 'inv-1',
        number: 'A-2026-000001',
        clientName: 'Cliente Rect',
        issueDate: '2026-07-01T00:00:00Z',
        total: 1210,
        status: 'Locked',
        isLocked: true,
    };

    it('renderiza abonos y facturas origen', () => {
        render(
            <CreditNotesClient
                initialInvoices={[lockedInvoice]}
                initialCreditNotes={[]}
            />,
        );
        expect(screen.getByRole('heading', { name: /abonos y rectificativas/i })).toBeInTheDocument();
        expect(screen.getByText('#A-2026-000001')).toBeInTheDocument();
    });
});

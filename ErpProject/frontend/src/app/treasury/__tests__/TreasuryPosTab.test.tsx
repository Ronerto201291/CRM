import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import TreasuryClient from '@/app/treasury/TreasuryClient';

describe('TreasuryClient TPV tab', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockImplementation((url: string) => {
            if (url.includes('/pos-terminals')) {
                return Promise.resolve({
                    ok: true,
                    json: async () => [{ id: 'tpv-1', name: 'Mostrador', terminalCode: 'TPV-01', isActive: true }],
                });
            }
            return Promise.resolve({ ok: true, json: async () => [] });
        }));
    });

    it('pestaña TPV carga terminales', async () => {
        const fetchMock = vi.mocked(global.fetch);
        render(<TreasuryClient initialAccounts={[]} />);

        fireEvent.click(screen.getByRole('button', { name: /tpv/i }));

        await waitFor(() => {
            expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/api/proxy/treasury/pos-terminals'));
        });

        await waitFor(() => {
            expect(screen.getByText('Mostrador')).toBeInTheDocument();
        });
    });
});

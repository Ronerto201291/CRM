import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import SettingsPage from '@/app/settings/page';
import TreasuryPage from '@/app/treasury/page';
import PayrollClient from '@/app/payroll/PayrollClient';
import InventoryClient from '@/app/inventory/InventoryClient';
import PurchasingOrdersClient from '@/app/purchasing/orders/PurchasingOrdersClient';
import SalesOrdersClient from '@/app/sales/orders/SalesOrdersClient';
import AccountingPage from '@/app/accounting/page';
import { serverFetch, serverFetchList } from '@/lib/serverFetch';

vi.mock('@/lib/serverFetch', () => ({
    serverFetch: vi.fn(),
    serverFetchList: vi.fn(),
}));

describe('SettingsPage smoke', () => {
    beforeEach(() => {
        vi.mocked(serverFetch).mockResolvedValue({
            id: '1',
            name: 'Empresa Test',
            taxId: 'B12345674',
            publicUploadToken: 'token123',
            qrUploadEnabled: true,
        });
    });

    it('renderiza título de configuración', async () => {
        render(await SettingsPage());
        await waitFor(() => {
            expect(screen.getByRole('heading', { name: 'Configuración' })).toBeInTheDocument();
        });
        expect(screen.getByRole('heading', { name: /Datos de la Empresa/i })).toBeInTheDocument();
    });
});

describe('TreasuryPage smoke', () => {
    beforeEach(() => {
        vi.mocked(serverFetchList).mockResolvedValue([]);
    });

    it('renderiza módulo de tesorería', async () => {
        render(await TreasuryPage());
        await waitFor(() => {
            expect(screen.getByRole('heading', { name: 'Tesorería' })).toBeInTheDocument();
        });
        expect(screen.getByText('Cuentas Bancarias')).toBeInTheDocument();
    });
});

describe('PayrollClient smoke', () => {
    it('renderiza listado de nóminas', () => {
        render(
            <PayrollClient
                initialEmployees={[]}
                initialSettlements={[]}
                initialYear={2026}
            />,
        );
        expect(screen.getByRole('heading', { name: 'Nóminas (Fase 0)' })).toBeInTheDocument();
    });
});

describe('InventoryClient smoke', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({ items: [], totalCount: 0 }),
        }));
    });

    it('renderiza módulo de inventario', () => {
        render(<InventoryClient initialProducts={[]} initialWarehouses={[]} />);
        expect(screen.getByRole('heading', { name: 'Inventario' })).toBeInTheDocument();
    });
});

describe('PurchasingOrdersClient smoke', () => {
    it('renderiza pedidos de compra', () => {
        render(<PurchasingOrdersClient initialOrders={[]} />);
        expect(screen.getByRole('heading', { name: 'Pedidos de Compra' })).toBeInTheDocument();
    });
});

describe('SalesOrdersClient smoke', () => {
    it('renderiza pedidos de venta', () => {
        render(<SalesOrdersClient initialOrders={[]} />);
        expect(screen.getByRole('heading', { name: 'Pedidos de Venta' })).toBeInTheDocument();
    });
});

describe('AccountingPage smoke', () => {
    beforeEach(() => {
        vi.mocked(serverFetchList).mockResolvedValue([]);
        vi.mocked(serverFetch).mockResolvedValue({ total: 0, details: [] });
    });

    it('renderiza módulo de contabilidad', async () => {
        render(await AccountingPage());
        await waitFor(() => {
            expect(screen.getByRole('heading', { name: 'Contabilidad' })).toBeInTheDocument();
        });
    });
});

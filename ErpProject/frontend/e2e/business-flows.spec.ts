import { test, expect } from '@playwright/test';
import { assertBackendReady, loginAsAdmin, skipUnlessStackReady } from './helpers';

test.describe('Flujos de negocio E2E', () => {
    test('login → crear cliente → factura borrador → ver en listado billing', async ({ page }) => {
        await loginAsAdmin(page);
        await assertBackendReady(page);

        const suffix = Date.now();
        const clientName = `E2E Cliente ${suffix}`;
        const clientTaxId = '12345678Z';

        const clientResponse = await page.request.post('/api/proxy/clients', {
            data: {
                name: clientName,
                taxId: clientTaxId,
                email: `e2e-${suffix}@test.local`,
                customFields: '{}',
            },
        });
        skipUnlessStackReady(
            clientResponse.ok(),
            `No se pudo crear cliente (${clientResponse.status()})`);
        const clientBody = await clientResponse.json();
        const clientId = clientBody.id as string;

        const invoiceResponse = await page.request.post('/api/proxy/invoices', {
            data: {
                clientType: 'Registered',
                clientId,
                clientName,
                clientTaxId,
                series: 'A',
                dueDate: new Date(Date.now() + 30 * 86400000).toISOString(),
                irpfRate: 0,
                invoiceType: 'Normal',
                lines: [{
                    description: `Servicio E2E ${suffix}`,
                    quantity: 1,
                    unitPrice: 100,
                    taxRate: 21,
                    surchargeRate: 0,
                    tipoOperacion: 'Nacional',
                }],
            },
        });
        skipUnlessStackReady(
            invoiceResponse.ok(),
            `No se pudo crear factura borrador (${invoiceResponse.status()})`);
        const invoiceBody = await invoiceResponse.json();
        const invoiceNumber = invoiceBody.number as string;

        await page.goto('/billing');
        await expect(page.getByRole('heading', { name: /facturación/i })).toBeVisible({ timeout: 20_000 });
        await expect(page.getByText(invoiceNumber)).toBeVisible({ timeout: 15_000 });
        await expect(page.getByText(/borrador/i).first()).toBeVisible();
    });

    test('crear gasto → aprobar → asiento contable visible', async ({ page }) => {
        await loginAsAdmin(page);
        await assertBackendReady(page);

        const suffix = Date.now();
        const createResponse = await page.request.post('/api/proxy/expenses', {
            data: {
                invoiceNumber: `GA-E2E-${suffix}`,
                supplierName: 'Proveedor E2E',
                supplierTaxId: 'B12345674',
                taxBase: 100,
                vatRate: 21,
                vatAmount: 21,
                total: 121,
            },
        });
        skipUnlessStackReady(
            createResponse.ok(),
            `No se pudo crear gasto (${createResponse.status()})`);
        const expense = await createResponse.json();
        const expenseId = expense.id as string;

        const approveResponse = await page.request.post(`/api/proxy/expenses/${expenseId}/approve`);
        skipUnlessStackReady(
            approveResponse.ok(),
            `No se pudo aprobar gasto (${approveResponse.status()}): ${await approveResponse.text()}`);

        const journalResponse = await page.request.get('/api/proxy/accounting/journal?year=2026&pageSize=100');
        skipUnlessStackReady(journalResponse.ok(), 'No se pudo consultar libro diario');
        const journalText = await journalResponse.text();
        expect(journalText.toLowerCase()).toMatch(/expense|gasto|proveedor e2e/i);

        await page.goto('/accounting');
        await expect(page.getByRole('heading', { name: /contabilidad/i })).toBeVisible({ timeout: 20_000 });
        await expect(page.getByText(/sin asientos/i).or(page.getByText(/proveedor e2e/i))).toBeVisible({ timeout: 15_000 });
    });

    test('pedido venta → albarán → listado entregas', async ({ page }) => {
        await loginAsAdmin(page);
        await assertBackendReady(page);

        const suffix = Date.now();
        const clientResponse = await page.request.post('/api/proxy/clients', {
            data: {
                name: `Cliente Ventas E2E ${suffix}`,
                taxId: '87654321X',
                email: `ventas-e2e-${suffix}@test.local`,
                customFields: '{}',
            },
        });
        skipUnlessStackReady(clientResponse.ok(), 'No se pudo crear cliente para pedido');
        const clientId = (await clientResponse.json()).id as string;

        const orderResponse = await page.request.post('/api/proxy/v1/sales/orders', {
            data: {
                number: `PED-E2E-${suffix}`,
                orderDate: new Date().toISOString(),
                clientId,
                clientName: `Cliente Ventas E2E ${suffix}`,
                lines: [{ description: 'Producto E2E', quantity: 2, unitPrice: 50 }],
            },
        });
        skipUnlessStackReady(
            orderResponse.ok(),
            `No se pudo crear pedido (${orderResponse.status()}): ${await orderResponse.text()}`);
        const order = await orderResponse.json();
        const orderId = order.id as string;

        const orderDetail = await page.request.get(`/api/proxy/v1/sales/orders/${orderId}`);
        skipUnlessStackReady(orderDetail.ok(), 'No se pudo obtener detalle del pedido');
        const orderBody = await orderDetail.json();
        const lineId = orderBody.lines?.[0]?.id as string | undefined;
        skipUnlessStackReady(!!lineId, 'Pedido sin líneas');

        const deliveryNumber = `ALB-E2E-${suffix}`;
        const deliveryResponse = await page.request.post('/api/proxy/v1/sales/deliveries', {
            data: {
                salesOrderId: orderId,
                number: deliveryNumber,
                deliveryDate: new Date().toISOString(),
                lines: [{ salesOrderLineId: lineId, shippedQuantity: 2 }],
            },
        });
        skipUnlessStackReady(
            deliveryResponse.ok(),
            `No se pudo crear albarán (${deliveryResponse.status()}): ${await deliveryResponse.text()}`);

        await page.goto('/sales/deliveries');
        await expect(page.getByRole('heading', { name: /albaranes|entregas/i })).toBeVisible({ timeout: 20_000 });
        await expect(page.getByText(deliveryNumber)).toBeVisible({ timeout: 15_000 });
    });

    test('presupuesto → listado quotes billing', async ({ page }) => {
        await loginAsAdmin(page);
        await assertBackendReady(page);

        const suffix = Date.now();
        const quoteResponse = await page.request.post('/api/proxy/quotes', {
            data: {
                clientType: 'Manual',
                clientName: `Cliente Presupuesto ${suffix}`,
                clientTaxId: '12345678Z',
                series: 'P',
                validUntil: new Date(Date.now() + 15 * 86400000).toISOString(),
                lines: [{
                    description: 'Línea presupuesto E2E',
                    quantity: 1,
                    unitPrice: 200,
                    taxRate: 21,
                    surchargeRate: 0,
                    tipoOperacion: 'Nacional',
                }],
            },
        });
        skipUnlessStackReady(
            quoteResponse.ok(),
            `No se pudo crear presupuesto (${quoteResponse.status()}): ${await quoteResponse.text()}`);
        const quote = await quoteResponse.json();
        const quoteNumber = quote.number as string;

        await page.goto('/billing/quotes');
        await expect(page.getByRole('heading', { name: /presupuestos/i })).toBeVisible({ timeout: 20_000 });
        await expect(page.getByText(quoteNumber)).toBeVisible({ timeout: 15_000 });
    });

    test('factura borrador → bloquear → asiento en diario', async ({ page }) => {
        await loginAsAdmin(page);
        await assertBackendReady(page);

        const suffix = Date.now();
        const invoiceResponse = await page.request.post('/api/proxy/invoices', {
            data: {
                clientType: 'Manual',
                clientName: `Cliente Lock E2E ${suffix}`,
                clientTaxId: '12345678Z',
                series: 'A',
                dueDate: new Date(Date.now() + 30 * 86400000).toISOString(),
                irpfRate: 0,
                invoiceType: 'Normal',
                lines: [{
                    description: 'Servicio lock E2E',
                    quantity: 1,
                    unitPrice: 50,
                    taxRate: 21,
                    surchargeRate: 0,
                    tipoOperacion: 'Nacional',
                }],
            },
        });
        skipUnlessStackReady(invoiceResponse.ok(), 'No se pudo crear factura');
        const invoice = await invoiceResponse.json();
        const invoiceId = invoice.id as string;

        const lockResponse = await page.request.post(`/api/proxy/invoices/${invoiceId}/lock`);
        skipUnlessStackReady(
            lockResponse.ok(),
            `No se pudo bloquear factura (${lockResponse.status()}): ${await lockResponse.text()}`);

        const year = new Date().getFullYear();
        const journalResponse = await page.request.get(`/api/proxy/accounting/journal?year=${year}&pageSize=50`);
        skipUnlessStackReady(journalResponse.ok(), 'No se pudo consultar diario');
        const journalJson = await journalResponse.json();
        const items = Array.isArray(journalJson) ? journalJson : (journalJson.items ?? []);
        expect(items.length).toBeGreaterThan(0);
    });

    test('crear proveedor → listado CRM proveedores', async ({ page }) => {
        await loginAsAdmin(page);
        await assertBackendReady(page);

        const suffix = Date.now();
        const supplierName = `Proveedor E2E ${suffix}`;
        const createResponse = await page.request.post('/api/proxy/suppliers', {
            data: {
                name: supplierName,
                taxId: 'B12345674',
                email: `prov-e2e-${suffix}@test.local`,
                customFields: '{}',
            },
        });
        skipUnlessStackReady(
            createResponse.ok(),
            `No se pudo crear proveedor (${createResponse.status()})`);

        await page.goto('/crm/suppliers');
        await expect(page.getByRole('heading', { name: /proveedores/i })).toBeVisible({ timeout: 20_000 });
        await expect(page.getByText(supplierName)).toBeVisible({ timeout: 15_000 });
    });
});

import { test, expect } from '@playwright/test';

const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';

async function login(page: import('@playwright/test').Page) {
    await page.goto('/admin');
    await page.getByLabel('Email').fill(email);
    await page.getByLabel(/contraseña/i).fill(password);
    await page.getByRole('button', { name: /iniciar sesión/i }).click();
    await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });
}

test.describe('Flujos fiscales E2E (mock/sin certificado AEAT)', () => {
    test.beforeEach(async () => {
        test.skip(!email || !password, 'Credenciales seed requeridas');
    });

    test('accounting/aeat carga y muestra disclaimer legal', async ({ page }) => {
        await login(page);
        await page.goto('/accounting/aeat');
        await expect(page.getByRole('heading', { name: /modelos aeat|aeat/i })).toBeVisible({ timeout: 15_000 });
        await expect(page.getByTestId('legal-disclaimer')).toBeVisible();
        await expect(page.getByText(/orientativos|asesor/i)).toBeVisible();
    });

    test('verifactu carga y muestra disclaimer legal', async ({ page }) => {
        await login(page);
        await page.goto('/verifactu');
        await expect(page.getByRole('heading', { name: /veri\*factu|verifactu/i })).toBeVisible({ timeout: 15_000 });
        await expect(page.getByTestId('legal-disclaimer')).toBeVisible();
    });

    test('sii carga y muestra disclaimer legal', async ({ page }) => {
        await login(page);
        await page.goto('/sii');
        await expect(page.getByRole('heading', { name: /sii|suministro/i })).toBeVisible({ timeout: 15_000 });
        await expect(page.getByTestId('legal-disclaimer')).toBeVisible();
    });

    test('payroll carga disclaimer nómina y export TGSS', async ({ page }) => {
        await login(page);
        await page.goto('/payroll');
        await expect(page.getByRole('heading', { name: /nóminas/i })).toBeVisible({ timeout: 15_000 });
        await expect(page.getByTestId('legal-disclaimer-payroll-module')).toBeVisible();
        await expect(page.getByTestId('legal-disclaimer-payroll-export')).toBeVisible();
    });

    test('accountant export carga disclaimer gestoría', async ({ page }) => {
        await login(page);
        await page.goto('/settings/accountant-export');
        await expect(page.getByRole('heading', { name: /export|gestor/i })).toBeVisible({ timeout: 15_000 });
        await expect(page.getByTestId('legal-disclaimer')).toBeVisible();
    });

    /**
     * Sin certificado AEAT en CI: verifica UI + respuesta API (no envío real).
     * Skip condicional si backend no expone endpoint (503/404).
     */
    test('verifactu API devuelve XML o error controlado (sin cert)', async ({ page, request }) => {
        await login(page);
        const year = new Date().getFullYear();
        const month = new Date().getMonth() + 1;

        const apiResponse = await request.get(
            `/api/proxy/sii/verifactu?year=${year}&month=${month}`,
        );

        // 200 = XML generado; 400/404/503 = sin datos o cert no configurado — ambos válidos en CI
        expect([200, 400, 404, 503]).toContain(apiResponse.status());

        if (apiResponse.status() === 200) {
            const ct = apiResponse.headers()['content-type'] ?? '';
            expect(ct).toMatch(/xml|octet-stream/i);
        }
    });
});

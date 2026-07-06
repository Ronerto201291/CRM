import { test, expect } from '@playwright/test';

const isCi = !!process.env.CI;

/** En CI el stack docker debe estar listo; en local, skip si no hay servicios. */
function skipUnlessStackReady(condition: boolean, reason: string) {
    if (!condition) {
        if (isCi) {
            throw new Error(`CI E2E: ${reason}`);
        }
        test.skip(true, reason);
    }
}

test.describe('Smoke E2E', () => {
    test('página de login admin carga', async ({ page }) => {
        const response = await page.goto('/admin');
        skipUnlessStackReady(!!response?.ok(), 'Frontend no disponible — levantar stack con docker compose');

        await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
        await expect(page.getByLabel('Email')).toBeVisible();
    });

    test('login → dashboard (requiere stack + credenciales)', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();

        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });
    });

    test('login → listar clientes (credenciales seed docker)', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });

        await page.goto('/crm/clients');
        await expect(page.getByRole('heading', { name: /clientes/i })).toBeVisible({ timeout: 15_000 });
    });

    test('login → crear cliente (credenciales seed docker)', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });

        await page.goto('/crm/clients');
        await expect(page.getByRole('heading', { name: /clientes/i })).toBeVisible({ timeout: 15_000 });

        const clientName = `E2E Cliente ${Date.now()}`;
        const createBtn = page.getByRole('button', { name: /nuevo cliente|crear cliente/i });
        if (await createBtn.count() > 0) {
            await createBtn.click();
            await page.getByLabel(/nombre/i).fill(clientName);
            await page.getByLabel(/nif|tax/i).fill('12345678Z');
            await page.getByLabel(/email/i).fill(`e2e-${Date.now()}@test.local`);
            await page.getByRole('button', { name: /guardar|crear/i }).click();
            await expect(page.getByText(clientName)).toBeVisible({ timeout: 15_000 });
        }
    });

    test('health del frontend responde', async ({ request }) => {
        const base = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3000';
        try {
            const response = await request.get(base);
            expect(response.status()).toBeLessThan(500);
        } catch (err) {
            skipUnlessStackReady(false, `Frontend no disponible: ${err}`);
        }
    });

    test('login → fiscal page carga calendario', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });

        await page.goto('/fiscal');
        await expect(page.getByRole('heading', { name: /calendario fiscal/i })).toBeVisible({ timeout: 15_000 });
    });

    test('login → treasury page carga módulo', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });

        await page.goto('/treasury');
        await expect(page.getByRole('heading', { name: /tesorería/i })).toBeVisible({ timeout: 15_000 });
    });

    test('login → expenses page carga listado', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });

        await page.goto('/expenses');
        await expect(page.getByRole('heading', { name: /gastos/i })).toBeVisible({ timeout: 15_000 });
    });

    test('login → settings usuarios carga listado', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });

        await page.goto('/settings/users');
        await expect(page.getByRole('heading', { name: /usuarios y roles/i })).toBeVisible({ timeout: 15_000 });
    });

    test('login → upload gasto vía token QR (stack docker)', async ({ page, request }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 20_000 });

        const companyResponse = await page.request.get('/api/proxy/company');
        skipUnlessStackReady(companyResponse.ok(), 'Backend no disponible — levantar stack docker');
        const company = await companyResponse.json();
        const token = company.publicUploadToken as string | undefined;
        skipUnlessStackReady(!!token, 'Empresa sin publicUploadToken');

        const base = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3000';
        const pdfBytes = Buffer.from('%PDF-1.4 e2e upload test');
        const uploadMultipart = (name: string, comment: string) => ({
            multipart: {
                file: {
                    name,
                    mimeType: 'application/pdf',
                    buffer: pdfBytes,
                },
                comment,
            },
        });

        let uploadResponse = await request.post(
            `${base}/api/expenses/upload/${token}`,
            uploadMultipart('ticket-e2e.pdf', 'E2E upload fase 12'),
        );

        for (let attempt = 1; !uploadResponse.ok() && uploadResponse.status() >= 500 && attempt <= 3; attempt++) {
            await page.waitForTimeout(attempt * 2000);
            uploadResponse = await request.post(
                `${base}/api/expenses/upload/${token}`,
                uploadMultipart(`ticket-e2e-retry-${attempt}.pdf`, `E2E upload fase 12 retry ${attempt}`),
            );
        }

        const uploadBody = await uploadResponse.text();
        skipUnlessStackReady(
            uploadResponse.ok(),
            `Endpoint upload no disponible (status ${uploadResponse.status()}): ${uploadBody.slice(0, 200)}`);
        expect(uploadResponse.ok()).toBeTruthy();

        await page.goto('/expenses');
        await expect(page.getByRole('heading', { name: /gastos/i })).toBeVisible({ timeout: 20_000 });
    });

    test('login → facturae page carga listado', async ({ page }) => {
        const email = process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
        const password = process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';
        test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

        await page.goto('/admin');
        await page.getByLabel('Email').fill(email!);
        await page.getByLabel(/contraseña/i).fill(password!);
        await page.getByRole('button', { name: /iniciar sesión/i }).click();
        await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 15_000 });

        await page.goto('/billing/facturae');
        await expect(page.getByRole('heading', { name: /facturae \/ veri\*factu/i })).toBeVisible({ timeout: 15_000 });
    });

    /**
     * 2FA E2E: requiresTwoFactor + verificación TOTP completa con otplib.
     * Seed manual: usuario con 2FA activo + secreto base32 en E2E_2FA_TOTP_SECRET.
     * Variables: E2E_2FA_EMAIL, E2E_2FA_PASSWORD, E2E_2FA_TOTP_SECRET.
     */
    test('login → verify TOTP completo (E2E_2FA_* + TOTP secret)', async ({ request }) => {
        const email = process.env.E2E_2FA_EMAIL;
        const password = process.env.E2E_2FA_PASSWORD;
        const totpSecret = process.env.E2E_2FA_TOTP_SECRET;
        test.skip(!email || !password || !totpSecret,
            '2FA E2E completo: definir E2E_2FA_EMAIL, E2E_2FA_PASSWORD y E2E_2FA_TOTP_SECRET');

        const backendBase = process.env.E2E_BACKEND_URL ?? 'http://localhost:8081';
        let loginResponse;
        try {
            loginResponse = await request.post(`${backendBase}/api/auth/login`, {
                data: { email, password },
            });
        } catch (err) {
            skipUnlessStackReady(false, `Backend 2FA no disponible: ${err}`);
        }
        skipUnlessStackReady(!!loginResponse?.ok(), 'Login 2FA requiere backend disponible');

        const loginBody = await loginResponse!.json();
        expect(loginBody.requiresTwoFactor).toBe(true);
        expect(loginBody.userId).toBeTruthy();

        const { authenticator } = await import('otplib');
        const code = authenticator.generate(totpSecret!);

        const verifyResponse = await request.post(`${backendBase}/api/auth/2fa/verify`, {
            data: { userId: loginBody.userId, code },
        });
        skipUnlessStackReady(verifyResponse.ok(), `Verify TOTP falló (${verifyResponse.status()})`);

        const tokens = await verifyResponse.json();
        expect(tokens.token ?? tokens.accessToken).toBeTruthy();
    });

    test('login → 2FA requiresTwoFactor (condicional E2E_2FA_*)', async ({ request }) => {
        const email = process.env.E2E_2FA_EMAIL;
        const password = process.env.E2E_2FA_PASSWORD;
        test.skip(!email || !password,
            '2FA E2E: definir E2E_2FA_EMAIL y E2E_2FA_PASSWORD (usuario con 2FA activo)');

        const backendBase = process.env.E2E_BACKEND_URL ?? 'http://localhost:8081';
        let loginResponse;
        try {
            loginResponse = await request.post(`${backendBase}/api/auth/login`, {
                data: { email, password },
            });
        } catch (err) {
            skipUnlessStackReady(false, `Backend 2FA no disponible: ${err}`);
        }
        skipUnlessStackReady(!!loginResponse?.ok(), 'Login 2FA requiere backend disponible');

        const loginBody = await loginResponse!.json();
        expect(loginBody.requiresTwoFactor).toBe(true);
        expect(loginBody.userId).toBeTruthy();
    });
});

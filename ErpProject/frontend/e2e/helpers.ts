import { expect, Page, test } from '@playwright/test';

export const isCi = !!process.env.CI;

export const adminEmail = () => process.env.E2E_ADMIN_EMAIL ?? 'admin@devcorp.com';
export const adminPassword = () => process.env.E2E_ADMIN_PASSWORD ?? 'DevChangeMe2026!!';

/** En CI el stack docker debe estar listo; en local, skip si no hay servicios. */
export function skipUnlessStackReady(condition: boolean, reason: string) {
    if (!condition) {
        if (isCi) {
            throw new Error(`CI E2E: ${reason}`);
        }
        test.skip(true, reason);
    }
}

/** Login seed docker y espera redirección al área autenticada. */
export async function loginAsAdmin(page: Page) {
    const email = adminEmail();
    const password = adminPassword();
    test.skip(!email || !password, 'Definir E2E_ADMIN_EMAIL y E2E_ADMIN_PASSWORD');

    await page.goto('/admin');
    await page.getByLabel('Email').fill(email!);
    await page.getByLabel(/contraseña/i).fill(password!);
    await page.getByRole('button', { name: /iniciar sesión/i }).click();
    await expect(page).toHaveURL(/\/(dashboard|crm)/, { timeout: 20_000 });
}

/** Comprueba backend vía proxy autenticado (cookies de sesión). */
export async function assertBackendReady(page: Page) {
    const companyResponse = await page.request.get('/api/proxy/company');
    skipUnlessStackReady(companyResponse.ok(), 'Backend no disponible — levantar stack docker');
}

/** Establece cookies de sesión tras login o cambio de empresa (fuera del árbol React). */
export function setSessionCookies(token: string, companyId: string, companyName: string) {
    const maxAge = 60 * 60 * 8;
    const base = `path=/; max-age=${maxAge}; SameSite=Lax`;
    document.cookie = `erp_token=${token}; ${base}`;
    document.cookie = `tenantId=${companyId}; ${base}`;
    document.cookie = `tenantName=${encodeURIComponent(companyName)}; ${base}`;
}

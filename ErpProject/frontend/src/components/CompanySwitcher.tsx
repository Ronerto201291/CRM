'use client';
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useTenant } from '@/context/TenantContext';

interface CompanyOption {
    companyId: string;
    companyName: string;
    isDefault?: boolean;
}

export default function CompanySwitcher() {
    const { tenantId, tenantName, setTenant } = useTenant();
    const router = useRouter();
    const [loading, setLoading] = useState(false);
    const [companies, setCompanies] = useState<CompanyOption[]>([]);
    const [loaded, setLoaded] = useState(false);

    const loadCompanies = async () => {
        if (loaded) return;
        const res = await fetch('/api/proxy/auth/companies');
        if (res.ok) {
            const data = await res.json();
            setCompanies(Array.isArray(data) ? data : []);
            setLoaded(true);
        }
    };

    const switchCompany = async (companyId: string, companyName: string) => {
        if (companyId === tenantId || loading) return;
        setLoading(true);
        try {
            const res = await fetch('/api/proxy/auth/switch-company', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ companyId }),
            });
            if (res.ok) {
                const data = await res.json();
                document.cookie = `erp_token=${data.token}; path=/; max-age=${60 * 60 * 8}; SameSite=Lax`;
                document.cookie = `tenantId=${data.companyId}; path=/; max-age=${60 * 60 * 8}; SameSite=Lax`;
                document.cookie = `tenantName=${encodeURIComponent(data.companyName || companyName)}; path=/; max-age=${60 * 60 * 8}; SameSite=Lax`;
                setTenant(data.companyId, data.companyName || companyName);
                router.refresh();
            }
        } finally {
            setLoading(false);
        }
    };

    if (!tenantId) return null;

    return (
        <div style={{ padding: '12px 16px', borderBottom: '1px solid rgba(255,255,255,0.08)' }}>
            <label htmlFor="company-switcher" style={{ display: 'block', fontSize: '10px', fontWeight: 600, color: 'rgba(255,255,255,0.5)', marginBottom: '6px', letterSpacing: '0.06em' }}>
                EMPRESA ACTIVA
            </label>
            <select
                id="company-switcher"
                className="erp-input"
                value={tenantId}
                disabled={loading}
                onFocus={loadCompanies}
                onChange={e => {
                    const opt = companies.find(c => c.companyId === e.target.value);
                    switchCompany(e.target.value, opt?.companyName || tenantName || '');
                }}
                style={{ width: '100%', fontSize: '12px', padding: '8px 10px', margin: 0 }}
            >
                <option value={tenantId}>{tenantName || 'Empresa actual'}</option>
                {companies.filter(c => c.companyId !== tenantId).map(c => (
                    <option key={c.companyId} value={c.companyId}>{c.companyName}</option>
                ))}
            </select>
            <a
                href="/settings/add-company"
                style={{ display: 'block', marginTop: 8, fontSize: 11, color: 'rgba(255,255,255,0.65)', textDecoration: 'none' }}
            >
                + Añadir empresa
            </a>
        </div>
    );
}

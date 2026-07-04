'use client';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { logoutAction } from '@/app/actions/auth';
import Logo from '@/components/Logo';
import CompanySwitcher from '@/components/CompanySwitcher';

const navGroups = [
    {
        label: 'Principal',
        items: [
            { href: '/dashboard', label: 'Dashboard', icon: <IconDashboard /> },
        ]
    },
    {
        label: 'Ventas',
        items: [
            { href: '/crm', label: 'Clientes', icon: <IconClients /> },
            { href: '/crm/prospects', label: 'Posibles Clientes', icon: <IconProspects /> },
            { href: '/crm/alerts', label: 'Alertas', icon: <IconAlerts /> },
            { href: '/crm/suppliers', label: 'Proveedores', icon: <IconSuppliers /> },
            { href: '/crm/services', label: 'Catálogo de Servicios', icon: <IconServices /> },
            { href: '/billing', label: 'Facturación', icon: <IconBilling /> },
            { href: '/billing/quotes', label: 'Presupuestos', icon: <IconQuotes /> },
            { href: '/billing/credit-notes', label: '↩ Rectificativas', icon: <IconCreditNotes /> },
        ]
    },
    {
        label: 'Compras y Gastos',
        items: [
            { href: '/expenses', label: 'Gastos (OCR)', icon: <IconExpenses /> },
            { href: '/purchasing/supplier-uploads', label: 'Facturas de Proveedores', icon: <IconExpenses /> },
        ]
    },
    {
        label: 'Finanzas',
        items: [
            { href: '/accounting', label: 'Contabilidad', icon: <IconAccounting /> },
            { href: '/accounting/reports', label: '📊 Reportes', icon: <IconReports /> },
            { href: '/accounting/cierre', label: '🔒 Cierre Contable', icon: <IconCierre /> },
            { href: '/treasury', label: 'Tesorería', icon: <IconTreasury /> },
            { href: '/treasury/currencies', label: '💱 Divisas (BCE)', icon: <IconCurrency /> },
            { href: '/treasury/financing', label: '💰 Confirming & Factoring', icon: <IconFinancing /> },
            { href: '/treasury/guarantees', label: '🔐 Cauciones & Avales', icon: <IconGuarantees /> },
            { href: '/treasury/consolidation', label: '📈 Consolidación Grupos', icon: <IconConsolidation /> },
            { href: '/payroll', label: 'Nóminas', icon: <IconPayroll /> },
            { href: '/fiscal', label: 'Calendario Fiscal', icon: <IconFiscal /> },
            { href: '/sii', label: '🏛 SII — AEAT', icon: <IconSii /> },
            { href: '/verifactu', label: '🛡️ VERI*FACTU', icon: <IconVerifactu /> },
            { href: '/accounting/aeat', label: '📋 Modelos AEAT', icon: <IconSii /> },
        ]
    },
    {
        label: 'Operaciones',
        items: [
            { href: '/inventory', label: 'Inventario', icon: <IconInventory /> },
        ]
    },
    {
        label: 'Sistema',
        items: [
            { href: '/settings', label: 'Configuración', icon: <IconSettings /> },
            { href: '/settings/subscription', label: '📋 Suscripción', icon: <IconSubscription /> },
            { href: '/settings/api-keys', label: '🔑 API Keys', icon: <IconApiKeys /> },
            { href: '/settings/users', label: '👥 Usuarios', icon: <IconUsers /> },
            { href: '/settings/audit-logs', label: '📋 Auditoría', icon: <IconAudit /> },
            { href: '/settings/documents', label: '📁 Documentos', icon: <IconDocuments /> },
            { href: '/settings/empresas', label: '🏢 Empresas', icon: <IconCompanies /> },
        ]
    },
];

export default function Sidebar() {
    const pathname = usePathname();

    return (
        <aside style={{
            width: '220px',
            minWidth: '220px',
            background: 'var(--brand-sidebar)',
            minHeight: '100vh',
            display: 'flex',
            flexDirection: 'column',
            fontFamily: 'Inter, sans-serif',
        }}>
            {/* Brand */}
            <div style={{ padding: '24px 16px', borderBottom: '1px solid rgba(255,255,255,0.06)', textAlign: 'center' }}>
                <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '8px' }}>
                    <Logo size={64} />
                </div>
                <div style={{ fontSize: '11px', color: 'rgba(255,255,255,0.4)', fontWeight: 500, letterSpacing: '0.02em' }}>Gestión Empresarial</div>
            </div>

            <CompanySwitcher />

            {/* Nav Groups */}
            <nav style={{ flex: 1, padding: '12px 8px', overflowY: 'auto' }}>
                {navGroups.map((group) => (
                    <div key={group.label} style={{ marginBottom: '8px' }}>
                        <div style={{
                            fontSize: '9px', fontWeight: 700, color: 'rgba(255,255,255,0.28)',
                            textTransform: 'uppercase', letterSpacing: '0.10em',
                            padding: '6px 8px 4px',
                        }}>{group.label}</div>
                        {group.items.map(item => {
                            // Use exact match for paths that are prefixes of sibling paths to avoid false highlights
                            const exactOnly = ['/dashboard', '/crm', '/billing', '/accounting', '/expenses', '/inventory', '/treasury', '/fiscal'];
                            const isActive = pathname === item.href || (!exactOnly.includes(item.href) && pathname?.startsWith(item.href));
                            return (
                                <Link key={item.href} href={item.href} style={{
                                    display: 'flex', alignItems: 'center', gap: '9px',
                                    padding: '7px 10px', borderRadius: '7px',
                                    textDecoration: 'none',
                                    fontSize: '13px', fontWeight: isActive ? 600 : 400,
                                    color: isActive ? 'white' : 'rgba(255,255,255,0.55)',
                                    background: isActive ? 'rgba(37,99,235,0.55)' : 'transparent',
                                    transition: 'all 0.15s',
                                    marginBottom: '1px',
                                }}
                                    onMouseEnter={(e) => { if (!isActive) { (e.currentTarget as HTMLElement).style.background = 'rgba(255,255,255,0.06)'; (e.currentTarget as HTMLElement).style.color = 'white'; } }}
                                    onMouseLeave={(e) => { if (!isActive) { (e.currentTarget as HTMLElement).style.background = 'transparent'; (e.currentTarget as HTMLElement).style.color = 'rgba(255,255,255,0.55)'; } }}
                                >
                                    <span style={{ width: '16px', height: '16px', flexShrink: 0, opacity: isActive ? 1 : 0.7 }}>{item.icon}</span>
                                    <span>{item.label}</span>
                                    {isActive && <span style={{ marginLeft: 'auto', width: '5px', height: '5px', borderRadius: '50%', background: '#60a5fa', flexShrink: 0 }} />}
                                </Link>
                            );
                        })}
                    </div>
                ))}
            </nav>

            {/* Footer */}
            <div style={{ padding: '12px 8px', borderTop: '1px solid rgba(255,255,255,0.06)' }}>
                <form action={logoutAction}>
                    <button type="submit" style={{
                        width: '100%', display: 'flex', alignItems: 'center', gap: '8px',
                        padding: '7px 10px', borderRadius: '7px', border: 'none',
                        background: 'transparent', color: 'rgba(255,255,255,0.4)',
                        cursor: 'pointer', fontSize: '12px', fontWeight: 500,
                        transition: 'all 0.15s',
                    }}
                        onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.background = 'rgba(239,68,68,0.15)'; (e.currentTarget as HTMLElement).style.color = '#fca5a5'; }}
                        onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.background = 'transparent'; (e.currentTarget as HTMLElement).style.color = 'rgba(255,255,255,0.4)'; }}
                    >
                        <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}><path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" /></svg>
                        Cerrar sesión
                    </button>
                </form>
            </div>
        </aside>
    );
}

// ─── SVG ICONS ───
function IconDashboard() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><rect x="3" y="3" width="7" height="7" rx="1.5" /><rect x="14" y="3" width="7" height="7" rx="1.5" /><rect x="3" y="14" width="7" height="7" rx="1.5" /><rect x="14" y="14" width="7" height="7" rx="1.5" /></svg>; }
function IconClients() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a4 4 0 00-4-4h-1M9 20H4v-2a4 4 0 014-4h1m4 6v-1a3 3 0 00-6 0v1m3-9a4 4 0 100-8 4 4 0 000 8z" /></svg>; }
function IconSuppliers() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" /></svg>; }
function IconServices() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M11 4a4 4 0 10-4.899 3.899L3 11l3 3 3.101-3.101A4 4 0 0011 4zm5.657 2.343l1.414-1.414 2.828 2.828-1.414 1.414m-2.828-2.828L9.172 13.83m7.485-7.487L21 10.686 10.686 21 6.343 16.657 16.657 6.343z" /></svg>; }
function IconBilling() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>; }
function IconCreditNotes() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m-9 8h18a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v14a2 2 0 002 2z" /></svg>; }
function IconExpenses() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M3 9a2 2 0 012-2h.93a2 2 0 001.664-.89l.812-1.22A2 2 0 0110.07 4h3.86a2 2 0 011.664.89l.812 1.22A2 2 0 0018.07 7H19a2 2 0 012 2v9a2 2 0 01-2 2H5a2 2 0 01-2-2V9z" /><path strokeLinecap="round" strokeLinejoin="round" d="M15 13a3 3 0 11-6 0 3 3 0 016 0z" /></svg>; }
function IconAccounting() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z" /></svg>; }
function IconReports() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" /></svg>; }
function IconInventory() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" /></svg>; }
function IconSettings() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" /><path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" /></svg>; }
function IconSubscription() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>; }
function IconApiKeys() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z" /></svg>; }
function IconUsers() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4.354a4 4 0 110 8.048M9 19H3v-2a6 6 0 0112 0v2h-6zm6-12a4 4 0 100-8 4 4 0 000 8z" /></svg>; }
function IconAudit() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" /></svg>; }
function IconDocuments() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M3 7a2 2 0 012-2h3.586a1 1 0 01.707.293l1.414 1.414a1 1 0 00.707.293H19a2 2 0 012 2v8a2 2 0 01-2 2H5a2 2 0 01-2-2V7z" /></svg>; }
function IconQuotes() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" /></svg>; }
function IconAlerts() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" /></svg>; }
function IconProspects() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" /></svg>; }
function IconSii() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M3 10h18M3 10V6a2 2 0 012-2h14a2 2 0 012 2v4M3 10v8a2 2 0 002 2h14a2 2 0 002-2v-8M8 14h.01M12 14h.01M16 14h.01" /></svg>; }
function IconCierre() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" /></svg>; }
function IconVerifactu() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" /></svg>; }
function IconCompanies() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" /></svg>; }
function IconTreasury() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3" /></svg>; }
function IconPayroll() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" /></svg>; }
function IconFiscal() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" /></svg>; }
function IconCurrency() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>; }
function IconFinancing() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M12 8c-1.657 0-3 .895-3 2v4c0 1.105 1.343 2 3 2s3-.895 3-2v-4c0-1.105-1.343-2-3-2z" /><path strokeLinecap="round" strokeLinejoin="round" d="M7 14h10m-5-7v10" /></svg>; }
function IconGuarantees() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m7 0a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>; }
function IconConsolidation() { return <svg fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.8}><path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>; }

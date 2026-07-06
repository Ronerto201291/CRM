'use client';
import { useState } from 'react';
import PageContainer from '@/components/PageContainer';
import type { Company } from './page';
import { companySettingsSchema } from '@/lib/schemas/settingsFiscalFormSchemas';

interface TwoFaSetupResult { qrCodeUri?: string; secret?: string; backupCodes?: string[]; }
interface TwoFaState { step: 'idle' | 'setup' | 'enabled'; setup?: TwoFaSetupResult; }

interface SettingsClientProps {
    initialCompany: Company | null;
}

export default function SettingsClient({ initialCompany }: SettingsClientProps) {
    const [company, setCompany] = useState<Company | null>(initialCompany);
    const [editing, setEditing] = useState(false);
    const [form, setForm] = useState<Partial<Company>>(initialCompany || {});
    const [copied, setCopied] = useState(false);
    const [regenerating, setRegenerating] = useState(false);
    const [twoFa, setTwoFa] = useState<TwoFaState>({ step: 'idle' });
    const [twoFaCode, setTwoFaCode] = useState('');
    const [twoFaMsg, setTwoFaMsg] = useState<{ text: string; ok: boolean } | null>(null);

    const load = async () => {
        const r = await fetch('/api/proxy/company');
        if (r.ok) { const d = await r.json(); setCompany(d); setForm(d); }
    };

    const saveCompany = async () => {
        const parsed = companySettingsSchema.safeParse(form);
        if (!parsed.success) {
            alert(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        await fetch('/api/proxy/company', {
            method: 'PUT', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ...form, qrUploadEnabled: company?.qrUploadEnabled }),
        });
        setEditing(false); load();
    };

    const regenerateToken = async () => {
        if (!confirm('¿Regenerar token QR? El código QR actual dejará de funcionar inmediatamente.')) return;
        setRegenerating(true);
        const r = await fetch('/api/proxy/company/regenerate-token', { method: 'POST' });
        if (r.ok) { load(); }
        setRegenerating(false);
    };

    const copyUrl = (url: string) => {
        navigator.clipboard.writeText(url);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    };

    const setup2FA = async () => {
        setTwoFaMsg(null);
        const r = await fetch('/api/proxy/auth/2fa/setup', { method: 'POST' });
        if (r.ok) {
            const d = await r.json();
            setTwoFa({ step: 'setup', setup: d });
        } else {
            setTwoFaMsg({ text: 'Error al iniciar configuración 2FA', ok: false });
        }
    };

    const confirm2FA = async () => {
        setTwoFaMsg(null);
        const r = await fetch('/api/proxy/auth/2fa/confirm', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code: twoFaCode }),
        });
        if (r.ok) {
            setTwoFa({ step: 'enabled' });
            setTwoFaMsg({ text: '2FA activado correctamente. Guarda los códigos de respaldo.', ok: true });
        } else {
            setTwoFaMsg({ text: 'Código incorrecto', ok: false });
        }
        setTwoFaCode('');
    };

    const disable2FA = async () => {
        if (!twoFaCode) { setTwoFaMsg({ text: 'Introduce el código 2FA para desactivarlo', ok: false }); return; }
        setTwoFaMsg(null);
        const r = await fetch('/api/proxy/auth/2fa/disable', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ code: twoFaCode }),
        });
        if (r.ok) {
            setTwoFa({ step: 'idle' });
            setTwoFaMsg({ text: '2FA desactivado', ok: true });
        } else {
            setTwoFaMsg({ text: 'Código incorrecto', ok: false });
        }
        setTwoFaCode('');
    };

    const uploadUrl = company ? `${typeof window !== 'undefined' ? window.location.origin : ''}/api/expenses/upload/${company.publicUploadToken}` : '';

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Configuración</h1>
                    <p className="page-subtitle">Empresa · QR de captura · Sistema</p>
                </div>
            </div>

            {/* Company Profile */}
            <div className="erp-card" style={{ padding: '24px', marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                    <div>
                        <h2 style={{ fontSize: '15px', fontWeight: 700 }}>🏢 Datos de la Empresa</h2>
                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>Información fiscal y de contacto</p>
                    </div>
                    {!editing && <button className="btn btn-secondary btn-sm" onClick={() => setEditing(true)}>Editar</button>}
                </div>

                {!editing ? (
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '20px' }}>
                        <InfoField label="Nombre / Razón Social" value={company?.name} />
                        <InfoField label="CIF / NIF" value={company?.taxId} mono />
                        <InfoField label="Email" value={company?.email} />
                        <InfoField label="Teléfono" value={company?.phone} />
                        <InfoField label="Dirección" value={company?.address} />
                    </div>
                ) : (
                    <div>
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px', marginBottom: '16px' }}>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}><label className="erp-label">RAZÓN SOCIAL</label><input className="erp-input" value={form.name || ''} onChange={e => setForm({ ...form, name: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">CIF/NIF</label><input className="erp-input" value={form.taxId || ''} onChange={e => setForm({ ...form, taxId: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">EMAIL</label><input className="erp-input" type="email" value={form.email || ''} onChange={e => setForm({ ...form, email: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">TELÉFONO</label><input className="erp-input" value={form.phone || ''} onChange={e => setForm({ ...form, phone: e.target.value })} /></div>
                            <div className="form-group"><label className="erp-label">DIRECCIÓN</label><input className="erp-input" value={form.address || ''} onChange={e => setForm({ ...form, address: e.target.value })} /></div>
                        </div>
                        <div style={{ display: 'flex', gap: '10px' }}>
                            <button className="btn btn-primary" onClick={saveCompany}>✓ Guardar cambios</button>
                            <button className="btn btn-secondary" onClick={() => { setEditing(false); setForm(company || {}); }}>Cancelar</button>
                        </div>
                    </div>
                )}
            </div>

            {/* QR Upload */}
            <div className="erp-card" style={{ padding: '24px', marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '16px' }}>
                    <div>
                        <h2 style={{ fontSize: '15px', fontWeight: 700 }}>📱 QR de Captura de Gastos</h2>
                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>
                            Cualquier persona con este QR puede subir fotos de tickets que el OCR procesará automáticamente
                        </p>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>QR activo</span>
                        <div style={{
                            width: '40px', height: '22px', borderRadius: '99px',
                            background: company?.qrUploadEnabled ? 'var(--success)' : 'var(--border)',
                            cursor: 'pointer', position: 'relative', transition: 'background 0.2s',
                        }} onClick={async () => {
                            await fetch('/api/proxy/company', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...company, qrUploadEnabled: !company?.qrUploadEnabled }) });
                            load();
                        }}>
                            <div style={{
                                width: '16px', height: '16px', borderRadius: '50%', background: 'white',
                                position: 'absolute', top: '3px', transition: 'left 0.2s',
                                left: company?.qrUploadEnabled ? '21px' : '3px',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.2)',
                            }} />
                        </div>
                    </div>
                </div>

                <div style={{ background: 'var(--surface-2)', border: '1px solid var(--border)', borderRadius: '8px', padding: '14px 16px', marginBottom: '14px' }}>
                    <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '6px' }}>URL DE SUBIDA (pública)</div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                        <code style={{ flex: 1, fontSize: '12px', color: 'var(--brand-primary)', wordBreak: 'break-all' }}>{uploadUrl}</code>
                        <button className="btn btn-secondary btn-sm" onClick={() => copyUrl(uploadUrl)}>
                            {copied ? '✓ Copiado' : 'Copiar'}
                        </button>
                    </div>
                </div>

                {/* QR Code image */}
                {company?.publicUploadToken && uploadUrl && (
                    <div style={{ display: 'flex', gap: '24px', alignItems: 'flex-start', marginBottom: '14px' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '10px' }}>
                            <div style={{
                                padding: '12px', background: 'white', borderRadius: '12px',
                                border: '2px solid var(--border)', boxShadow: 'var(--shadow-md)',
                                display: 'inline-block',
                            }}>
                                {/* eslint-disable-next-line @next/next/no-img-element */}
                                <img
                                    src={`https://api.qrserver.com/v1/create-qr-code/?size=160x160&ecc=M&data=${encodeURIComponent(uploadUrl)}`}
                                    alt="QR de captura de gastos"
                                    width={160} height={160}
                                    style={{ display: 'block', borderRadius: '4px' }}
                                />
                            </div>
                            <a
                                href={`https://api.qrserver.com/v1/create-qr-code/?size=400x400&ecc=H&data=${encodeURIComponent(uploadUrl)}&format=png`}
                                download="qr-gastos-erp.png"
                                target="_blank" rel="noopener noreferrer"
                                className="btn btn-secondary btn-sm"
                                style={{ textDecoration: 'none' }}
                            >
                                ↓ Descargar QR (400x400)
                            </a>
                        </div>
                        <div style={{ flex: 1, fontSize: '13px', color: 'var(--text-secondary)', display: 'flex', flexDirection: 'column', gap: '10px', paddingTop: '8px' }}>
                            <div style={{ background: 'var(--info-bg)', border: '1px solid rgba(59,130,246,0.2)', borderRadius: '8px', padding: '12px', color: 'var(--info)', fontSize: '13px' }}>
                                <strong>📱 Cómo usarlo:</strong><br />
                                1. Imprime o muestra este QR a tu equipo<br />
                                2. Cualquiera que lo escanee puede subir una foto del ticket<br />
                                3. El OCR procesa la imagen en hasta 30 segundos<br />
                                4. El gasto aparece en borrador para que lo revises y apruebes
                            </div>
                            <div style={{ background: 'var(--warning-bg)', border: '1px solid rgba(245,158,11,0.2)', borderRadius: '8px', padding: '10px', color: '#92400e', fontSize: '12px' }}>
                                ⚠️ Este QR da acceso de subida sin autenticación. Compártelo solo con personas de confianza. Si se compromete, usa <strong>Regenerar Token</strong>.
                            </div>
                        </div>
                    </div>
                )}


                <div style={{ display: 'flex', gap: '10px' }}>
                    <button className="btn btn-secondary" onClick={regenerateToken} disabled={regenerating}>
                        {regenerating ? 'Regenerando...' : '🔄 Regenerar Token QR'}
                    </button>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)', alignSelf: 'center' }}>
                        Token: <code style={{ fontFamily: 'monospace', color: 'var(--text-primary)' }}>{company?.publicUploadToken?.slice(0, 8)}...</code>
                    </div>
                </div>
            </div>

            {/* 2FA Security */}
            <div className="erp-card" style={{ padding: '24px', marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '16px' }}>
                    <div>
                        <h2 style={{ fontSize: '15px', fontWeight: 700 }}>🔐 Autenticación en Dos Factores (2FA)</h2>
                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '2px' }}>TOTP compatible con Google Authenticator, Authy, etc.</p>
                    </div>
                    <span className={`badge ${twoFa.step === 'enabled' ? 'badge-success' : 'badge-gray'}`}>
                        {twoFa.step === 'enabled' ? 'Activo' : 'Inactivo'}
                    </span>
                </div>

                {twoFaMsg && (
                    <div style={{ padding: '10px 14px', borderRadius: '8px', marginBottom: '14px', background: twoFaMsg.ok ? 'var(--success-bg)' : 'var(--danger-bg)', color: twoFaMsg.ok ? 'var(--success)' : 'var(--danger)', fontSize: '13px' }}>
                        {twoFaMsg.ok ? '✓ ' : '✗ '}{twoFaMsg.text}
                    </div>
                )}

                {/* IDLE — not setup */}
                {twoFa.step === 'idle' && (
                    <button className="btn btn-primary" onClick={setup2FA}>Activar 2FA</button>
                )}

                {/* SETUP — show QR + confirm */}
                {twoFa.step === 'setup' && twoFa.setup && (
                    <div style={{ display: 'flex', gap: '24px', flexWrap: 'wrap', alignItems: 'flex-start' }}>
                        <div>
                            {/* eslint-disable-next-line @next/next/no-img-element */}
                            <img
                                src={`https://api.qrserver.com/v1/create-qr-code/?size=160x160&data=${encodeURIComponent(twoFa.setup.qrCodeUri ?? '')}`}
                                alt="QR 2FA"
                                width={160} height={160}
                                style={{ background: 'white', padding: '8px', borderRadius: '8px', display: 'block' }}
                            />
                        </div>
                        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '12px' }}>
                            <div>
                                <div style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '4px' }}>O introduce manualmente este secreto:</div>
                                <code style={{ fontFamily: 'monospace', fontSize: '13px', color: 'var(--brand-primary)', letterSpacing: '0.1em' }}>{twoFa.setup.secret}</code>
                            </div>
                            {twoFa.setup.backupCodes && (
                                <div style={{ background: 'var(--warning-bg)', padding: '10px 12px', borderRadius: '8px', border: '1px solid rgba(245,158,11,0.2)' }}>
                                    <div style={{ fontSize: '11px', fontWeight: 700, color: '#92400e', marginBottom: '6px' }}>⚠️ CÓDIGOS DE RESPALDO — guárdalos en un lugar seguro</div>
                                    <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                                        {twoFa.setup.backupCodes.map((c, i) => (
                                            <code key={i} style={{ fontFamily: 'monospace', fontSize: '12px', background: 'white', padding: '2px 6px', borderRadius: '4px' }}>{c}</code>
                                        ))}
                                    </div>
                                </div>
                            )}
                            <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                                <input className="erp-input" style={{ width: '140px', textAlign: 'center', letterSpacing: '0.2em', fontSize: '18px' }}
                                    placeholder="000000" maxLength={6} value={twoFaCode}
                                    onChange={e => setTwoFaCode(e.target.value.replace(/\D/g, ''))} />
                                <button className="btn btn-primary" onClick={confirm2FA} disabled={twoFaCode.length !== 6}>✓ Confirmar y activar</button>
                                <button className="btn btn-secondary" onClick={() => setTwoFa({ step: 'idle' })}>Cancelar</button>
                            </div>
                        </div>
                    </div>
                )}

                {/* ENABLED — disable option */}
                {twoFa.step === 'enabled' && (
                    <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                        <input className="erp-input" style={{ width: '140px', textAlign: 'center', letterSpacing: '0.2em', fontSize: '18px' }}
                            placeholder="000000" maxLength={6} value={twoFaCode}
                            onChange={e => setTwoFaCode(e.target.value.replace(/\D/g, ''))} />
                        <button className="btn btn-secondary" style={{ borderColor: 'var(--danger)', color: 'var(--danger)' }} onClick={disable2FA} disabled={twoFaCode.length !== 6}>
                            Desactivar 2FA
                        </button>
                    </div>
                )}
            </div>

            {/* Roles */}
            <div className="erp-card" style={{ padding: '24px', marginBottom: '20px' }}>
                <h2 style={{ fontSize: '15px', fontWeight: 700, marginBottom: '16px' }}>🔐 Roles del Sistema</h2>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '12px' }}>
                    {[
                        { role: 'Admin', desc: 'Acceso completo al sistema', color: 'var(--danger)', bg: 'var(--danger-bg)' },
                        { role: 'Manager', desc: 'CRM + Facturación + Gastos', color: 'var(--warning)', bg: 'var(--warning-bg)' },
                        { role: 'Contable', desc: 'Contabilidad + Gastos + Reportes', color: 'var(--info)', bg: 'var(--info-bg)' },
                    ].map(r => (
                        <div key={r.role} style={{ background: r.bg, border: `1px solid ${r.color}20`, borderRadius: '8px', padding: '14px' }}>
                            <div style={{ fontWeight: 700, color: r.color, marginBottom: '4px' }}>{r.role}</div>
                            <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>{r.desc}</div>
                        </div>
                    ))}
                </div>
            </div>

            {/* Technical info */}
            <div className="erp-card" style={{ padding: '24px' }}>
                <h2 style={{ fontSize: '15px', fontWeight: 700, marginBottom: '16px' }}>⚙️ Sistema</h2>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '10px', fontSize: '13px' }}>
                    <TechRow label="OCR Engine" value="Tesseract (local, sin servicios externos)" />
                    <TechRow label="Procesado OCR" value="Cada 30 segundos (BackgroundService)" />
                    <TechRow label="Normativa fiscal" value="RD 1619/2012 · Ley 11/2021 Antifraude" />
                    <TechRow label="Contabilidad" value="Doble partida · PGC España" />
                    <TechRow label="Multi-tenant" value="Aislamiento por TenantId (CompanyId)" />
                    <TechRow label="Hash documentos" value="SHA-256 (cumplimiento Ley Antifraude)" />
                </div>
            </div>
        </PageContainer>
    );
}

function InfoField({ label, value, mono }: { label: string; value?: string; mono?: boolean }) {
    return (
        <div>
            <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>{label}</div>
            <div style={{ fontSize: '14px', fontWeight: 500, color: value ? 'var(--text-primary)' : 'var(--text-muted)', fontFamily: mono ? 'monospace' : 'inherit' }}>
                {value || '—'}
            </div>
        </div>
    );
}

function TechRow({ label, value }: { label: string; value: string }) {
    return (
        <div style={{ padding: '10px 14px', background: 'var(--surface-2)', borderRadius: '6px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>{label}</span>
            <span style={{ fontWeight: 600, fontSize: '12px' }}>{value}</span>
        </div>
    );
}

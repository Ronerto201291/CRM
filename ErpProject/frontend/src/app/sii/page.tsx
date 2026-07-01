'use client';
import { useState } from 'react';

type SiiType = 'emitidas' | 'recibidas';

const MONTHS = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
];

export default function SiiPage() {
    const now = new Date();
    const [year, setYear] = useState(now.getFullYear());
    const [month, setMonth] = useState(now.getMonth() + 1);
    const [type, setType] = useState<SiiType>('emitidas');
    const [loading, setLoading] = useState<string | null>(null);
    const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
    const [previewXml, setPreviewXml] = useState<string | null>(null);
    const [showPreview, setShowPreview] = useState(false);

    const reset = () => setMessage(null);

    const handleDownload = async () => {
        setLoading('download'); reset();
        try {
            const endpoint = type === 'emitidas' ? 'sii/emitidas' : 'sii/recibidas';
            const r = await fetch(`/api/proxy/${endpoint}?year=${year}&month=${month}`);
            if (r.ok) {
                const blob = await r.blob();
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `SII_${type}_${year}_${String(month).padStart(2, '0')}.xml`;
                document.body.appendChild(a); a.click(); document.body.removeChild(a);
                URL.revokeObjectURL(url);
                setMessage({ text: `XML generado: SII_${type}_${year}_${String(month).padStart(2, '0')}.xml`, ok: true });
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error al generar el XML', ok: false });
            }
        } finally { setLoading(null); }
    };

    const handlePreview = async () => {
        setLoading('preview'); reset();
        try {
            const endpoint = type === 'emitidas' ? 'sii/emitidas' : 'sii/recibidas';
            const r = await fetch(`/api/proxy/${endpoint}?year=${year}&month=${month}`);
            if (r.ok) {
                const text = await r.text();
                setPreviewXml(text);
                setShowPreview(true);
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error al previsualizar', ok: false });
            }
        } finally { setLoading(null); }
    };

    const handleSubmit = async () => {
        const label = type === 'emitidas' ? 'Facturas Emitidas' : 'Facturas Recibidas';
        if (!confirm(`¿Firmar y enviar ${label} de ${MONTHS[month - 1]} ${year} a la AEAT?\n\nEsta acción requiere certificado digital configurado en el servidor.`)) return;
        setLoading('submit'); reset();
        try {
            const r = await fetch('/api/proxy/sii/submit', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ type, year, month }),
            });
            if (r.ok) {
                const data = await r.json() as { estado?: string; period?: string };
                setMessage({ text: `✓ Enviado correctamente a AEAT. Estado: ${data.estado ?? 'OK'} · Período: ${data.period}`, ok: true });
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error en el envío a AEAT', ok: false });
            }
        } finally { setLoading(null); }
    };

    const deadlineLabel = (() => {
        // SII: 4 días naturales desde expedición para facturas emitidas
        const d = new Date(year, month - 1, 1);
        d.setMonth(d.getMonth() + 1);
        return d.toLocaleDateString('es-ES', { month: 'long', year: 'numeric' });
    })();

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
            {/* Header */}
            <div className="page-header" style={{ marginBottom: '24px' }}>
                <div>
                    <h1 className="page-title">SII — Suministro Inmediato de Información</h1>
                    <p className="page-subtitle">
                        Libros de IVA para AEAT · Plazo máximo 4 días desde expedición ·{' '}
                        <span style={{ color: 'var(--text-muted)', fontSize: '12px' }}>
                            ≠ Verifactu (RD 1007/2023 — hash/QR en PDF, configurado en Facturación)
                        </span>
                    </p>
                </div>
            </div>

            {/* Info banner — differentiating SII vs Verifactu */}
            <div style={{
                display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '24px'
            }}>
                <InfoCard
                    color="var(--brand-primary)"
                    title="🏛 SII — Este módulo"
                    lines={[
                        'Envío de libros de IVA a AEAT (SOAP)',
                        'Facturas Emitidas + Recibidas',
                        'Plazo: 4 días naturales desde expedición',
                        'Requiere certificado digital configurado',
                    ]}
                />
                <InfoCard
                    color="var(--text-muted)"
                    title="🔒 Verifactu — Facturación"
                    lines={[
                        'Hash SHA-256 + QR AEAT en el PDF',
                        'Cadena de integridad (Ley 11/2021)',
                        'Se activa al bloquear cada factura',
                        'Configurado en el módulo Facturación',
                    ]}
                    muted
                />
            </div>

            {/* Main card */}
            <div className="erp-card" style={{ padding: '28px', marginBottom: '20px' }}>
                <h2 style={{ fontSize: '13px', fontWeight: 700, marginBottom: '20px', textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-muted)' }}>
                    Parámetros de Exportación
                </h2>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '16px', marginBottom: '24px' }}>
                    <div className="form-group" style={{ gridColumn: 'span 3' }}>
                        <label className="erp-label">TIPO DE LIBRO</label>
                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                            {(['emitidas', 'recibidas'] as SiiType[]).map(t => (
                                <button
                                    key={t}
                                    onClick={() => setType(t)}
                                    style={{
                                        padding: '12px 16px', borderRadius: '8px', border: '2px solid',
                                        borderColor: type === t ? 'var(--brand-primary)' : 'var(--border)',
                                        background: type === t ? 'var(--primary-bg, #eff6ff)' : 'var(--surface)',
                                        color: type === t ? 'var(--brand-primary)' : 'var(--text-secondary)',
                                        fontWeight: type === t ? 700 : 500, fontSize: '13px',
                                        cursor: 'pointer', transition: 'all 0.15s', textAlign: 'left',
                                    }}
                                >
                                    <div>{t === 'emitidas' ? '📤 Facturas Emitidas' : '📥 Facturas Recibidas'}</div>
                                    <div style={{ fontSize: '11px', marginTop: '2px', opacity: 0.7 }}>
                                        {t === 'emitidas' ? 'Libro de Ventas (ventas a clientes)' : 'Libro de Compras (gastos de proveedores)'}
                                    </div>
                                </button>
                            ))}
                        </div>
                    </div>

                    <div className="form-group">
                        <label className="erp-label">AÑO</label>
                        <select className="erp-input" value={year} onChange={e => setYear(+e.target.value)}>
                            {[2026, 2025, 2024].map(y => <option key={y} value={y}>{y}</option>)}
                        </select>
                    </div>
                    <div className="form-group" style={{ gridColumn: 'span 2' }}>
                        <label className="erp-label">MES</label>
                        <select className="erp-input" value={month} onChange={e => setMonth(+e.target.value)}>
                            {MONTHS.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                        </select>
                    </div>
                </div>

                <div style={{
                    padding: '10px 14px', borderRadius: '8px', background: 'var(--surface-2)',
                    fontSize: '12px', color: 'var(--text-muted)', marginBottom: '20px',
                }}>
                    📅 Solo se incluyen facturas <strong>bloqueadas (contabilizadas)</strong> del período seleccionado.
                    Plazo AEAT: antes del inicio de {deadlineLabel}.
                </div>

                {message && (
                    <div style={{
                        padding: '12px 16px', borderRadius: '8px', marginBottom: '20px',
                        background: message.ok ? 'var(--success-bg)' : 'var(--danger-bg)',
                        color: message.ok ? 'var(--success)' : 'var(--danger)',
                        fontSize: '13px', fontWeight: 500,
                    }}>
                        {message.text}
                    </div>
                )}

                <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                    <button className="btn btn-secondary" onClick={handlePreview} disabled={!!loading}>
                        {loading === 'preview' ? '...' : '👁 Ver XML'}
                    </button>
                    <button
                        className="btn btn-secondary" onClick={handleDownload} disabled={!!loading}
                        style={{ borderColor: 'var(--brand-primary)', color: 'var(--brand-primary)' }}
                    >
                        {loading === 'download' ? '...' : '⬇ Descargar XML'}
                    </button>
                    <button className="btn btn-primary" onClick={handleSubmit} disabled={!!loading}
                        style={{ marginLeft: 'auto' }}
                    >
                        {loading === 'submit' ? '⏳ Enviando a AEAT...' : '📡 Firmar y Enviar a AEAT'}
                    </button>
                </div>
            </div>

            {/* Requirements */}
            <div style={{
                display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px'
            }}>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                        Requisitos para envío
                    </div>
                    <ul style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: 1.8, paddingLeft: '16px', margin: 0 }}>
                        <li>Certificado digital (FNMT/AC) en <code>Sii:CertPath</code></li>
                        <li>Contraseña en <code>Sii:CertPass</code></li>
                        <li>Entorno: <code>Sii:Environment=test|prod</code></li>
                        <li>Facturas con estado <strong>Locked</strong> (bloqueadas)</li>
                    </ul>
                </div>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-muted)', marginBottom: '8px', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                        Endpoints AEAT utilizados
                    </div>
                    <ul style={{ fontSize: '12px', color: 'var(--text-secondary)', lineHeight: 1.8, paddingLeft: '16px', margin: 0 }}>
                        <li><strong>Test:</strong> www1.agenciatributaria.gob.es</li>
                        <li><strong>Prod:</strong> www2.agenciatributaria.gob.es</li>
                        <li>Protocolo: <strong>SOAP + XAdES-BES</strong></li>
                        <li>Versión SII: <strong>1.1</strong></li>
                    </ul>
                </div>
            </div>

            {/* XML Preview Modal */}
            {showPreview && previewXml && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setShowPreview(false); }}>
                    <div className="modal-box" style={{ maxWidth: '800px', maxHeight: '85vh', display: 'flex', flexDirection: 'column' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                            <h2 style={{ fontSize: '15px', fontWeight: 700 }}>
                                XML SII — {type === 'emitidas' ? 'Facturas Emitidas' : 'Facturas Recibidas'} · {MONTHS[month - 1]} {year}
                            </h2>
                            <button onClick={() => setShowPreview(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>
                        <div style={{ overflowY: 'auto', flex: 1 }}>
                            <pre style={{
                                fontSize: '11px', fontFamily: 'monospace',
                                background: 'var(--surface-2)', padding: '16px',
                                borderRadius: '8px', whiteSpace: 'pre-wrap', wordBreak: 'break-word',
                                margin: 0,
                            }}>
                                {previewXml}
                            </pre>
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                            <button className="btn btn-secondary" onClick={() => setShowPreview(false)}>Cerrar</button>
                            <button className="btn btn-primary" onClick={() => { handleDownload(); setShowPreview(false); }}>
                                ⬇ Descargar XML
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

function InfoCard({ title, lines, color, muted }: { title: string; lines: string[]; color: string; muted?: boolean }) {
    return (
        <div style={{
            padding: '16px 20px', borderRadius: '10px',
            background: muted ? 'var(--surface)' : 'var(--surface)',
            border: `1.5px solid ${muted ? 'var(--border)' : color}`,
            opacity: muted ? 0.7 : 1,
        }}>
            <div style={{ fontSize: '13px', fontWeight: 700, color, marginBottom: '10px' }}>{title}</div>
            <ul style={{ margin: 0, paddingLeft: '16px', fontSize: '12px', color: 'var(--text-secondary)', lineHeight: 1.8 }}>
                {lines.map((l, i) => <li key={i}>{l}</li>)}
            </ul>
        </div>
    );
}

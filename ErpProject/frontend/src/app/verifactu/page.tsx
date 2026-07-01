'use client';
import { useState } from 'react';
import PageContainer from '@/components/PageContainer';

const MONTHS = [
    'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
];

export default function VerifactuPage() {
    const now = new Date();
    const [year, setYear] = useState(now.getFullYear());
    const [month, setMonth] = useState(now.getMonth() + 1);
    const [useProd, setUseProd] = useState(false);
    const [loading, setLoading] = useState<string | null>(null);
    const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

    const reset = () => setMessage(null);

    const handleDownload = async () => {
        setLoading('download'); reset();
        try {
            const r = await fetch(`/api/proxy/sii/verifactu?year=${year}&month=${month}`);
            if (r.ok) {
                const blob = await r.blob();
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `Verifactu_${year}_${String(month).padStart(2, '0')}.xml`;
                document.body.appendChild(a); a.click(); document.body.removeChild(a);
                URL.revokeObjectURL(url);
                setMessage({ text: `XML generado: Verifactu_${year}_${String(month).padStart(2, '0')}.xml`, ok: true });
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error al generar el XML', ok: false });
            }
        } finally { setLoading(null); }
    };

    const handleSubmit = async () => {
        if (!confirm(`¿Firmar y enviar VERI*FACTU de ${MONTHS[month - 1]} ${year} a la AEAT?\n\nEntorno: ${useProd ? 'PRODUCCIÓN (Real)' : 'PRUEBAS (Test)'}`)) return;
        setLoading('submit'); reset();
        try {
            const r = await fetch('/api/proxy/sii/verifactu/submit', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ year, month, useProd }),
            });
            if (r.ok) {
                const data = await r.json() as { estadoEnvio?: string; period?: string };
                setMessage({ text: `✓ Enviado correctamente a AEAT. Estado: ${data.estadoEnvio ?? 'OK'} · Período: ${data.period}`, ok: true });
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error en el envío a AEAT', ok: false });
            }
        } finally { setLoading(null); }
    };

    return (
        <PageContainer>
            <div className="page-header" style={{ marginBottom: '24px' }}>
                <div>
                    <h1 className="page-title">VERI*FACTU</h1>
                    <p className="page-subtitle">
                        Reglamento de Facturación (RD 1007/2023). Integridad, conservación y envío de registros a la AEAT.
                    </p>
                </div>
            </div>

            {/* Info banner */}
            <div style={{
                display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '24px'
            }}>
                <InfoCard
                    color="var(--brand-primary)"
                    title="🔒 Sistema de Emisión (Facturación)"
                    lines={[
                        'Las facturas bloqueadas generan automáticamente una Huella (Hash SHA-256).',
                        'Cada factura está encadenada criptográficamente con la anterior.',
                        'El PDF incluye un código QR verificable por la AEAT.',
                    ]}
                />
                <InfoCard
                    color="var(--success)"
                    title="📡 Envío Teórico / VERI*FACTU"
                    lines={[
                        'Envío voluntario del XML "Registro de Alta" a la Agencia Tributaria.',
                        'Diferente del SII. Solo reporta la Huella y datos básicos.',
                        'Se firma con certificado de empresa (XAdES).',
                    ]}
                />
            </div>

            {/* Main card */}
            <div className="erp-card" style={{ padding: '28px', marginBottom: '20px' }}>
                <h2 style={{ fontSize: '13px', fontWeight: 700, marginBottom: '20px', textTransform: 'uppercase', letterSpacing: '0.06em', color: 'var(--text-muted)' }}>
                    Exportación y Envío de Registros
                </h2>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '24px' }}>
                    <div className="form-group">
                        <label className="erp-label">AÑO FISCAL</label>
                        <select className="erp-input" value={year} onChange={e => setYear(+e.target.value)}>
                            {[2026, 2025, 2024].map(y => <option key={y} value={y}>{y}</option>)}
                        </select>
                    </div>
                    <div className="form-group">
                        <label className="erp-label">MES</label>
                        <select className="erp-input" value={month} onChange={e => setMonth(+e.target.value)}>
                            {MONTHS.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                        </select>
                    </div>
                    
                    <div className="form-group" style={{ gridColumn: 'span 2' }}>
                        <label className="erp-label" style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                            <input 
                                type="checkbox" 
                                checked={useProd} 
                                onChange={e => setUseProd(e.target.checked)} 
                                style={{ transform: 'scale(1.2)' }}
                            />
                            <span>Enviar al entorno de <strong>PRODUCCIÓN (AEAT Real)</strong></span>
                        </label>
                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '4px', marginLeft: '24px' }}>
                            Si no se marca, los envíos irán al entorno de PRUEBAS de la Agencia Tributaria.
                        </p>
                    </div>
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
                    <button
                        className="btn btn-secondary" onClick={handleDownload} disabled={!!loading}
                        style={{ borderColor: 'var(--brand-primary)', color: 'var(--brand-primary)' }}
                    >
                        {loading === 'download' ? '...' : '⬇ Generar y Descargar XML'}
                    </button>
                    <button className="btn btn-primary" onClick={handleSubmit} disabled={!!loading}
                        style={{ marginLeft: 'auto', background: useProd ? 'var(--danger)' : 'var(--brand-primary)' }}
                    >
                        {loading === 'submit' ? '⏳ Enviando...' : `📡 Firmar y Enviar (${useProd ? 'PROD' : 'TEST'})`}
                    </button>
                </div>
            </div>
        </PageContainer>
    );
}

function InfoCard({ title, lines, color }: { title: string; lines: string[]; color: string }) {
    return (
        <div style={{
            padding: '16px 20px', borderRadius: '10px',
            background: 'var(--surface)',
            border: `1.5px solid ${color}`,
        }}>
            <div style={{ fontSize: '13px', fontWeight: 700, color, marginBottom: '10px' }}>{title}</div>
            <ul style={{ margin: 0, paddingLeft: '16px', fontSize: '12px', color: 'var(--text-secondary)', lineHeight: 1.8 }}>
                {lines.map((l, i) => <li key={i}>{l}</li>)}
            </ul>
        </div>
    );
}

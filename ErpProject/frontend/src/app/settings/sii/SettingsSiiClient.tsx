'use client';
import { useState } from 'react';
import AccessibleModal from '@/components/AccessibleModal';
import LegalDisclaimer, { FISCAL_EXPORT_DISCLAIMER_TEXT } from '@/components/LegalDisclaimer';
import { siiSubmitSchema } from '@/lib/schemas/settingsFiscalFormSchemas';

type SiiType = 'emitidas' | 'recibidas';

export default function SettingsSiiClient() {
    const [year, setYear] = useState(new Date().getFullYear());
    const [month, setMonth] = useState(new Date().getMonth() + 1);
    const [type, setType] = useState<SiiType>('emitidas');
    const [preview, setPreview] = useState<object | null>(null);
    const [showPreview, setShowPreview] = useState(false);
    const [loading, setLoading] = useState<string | null>(null);
    const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

    const handlePreview = async () => {
        setLoading('preview');
        setMessage(null);
        try {
            const r = await fetch(`/api/proxy/sii/preview?year=${year}&month=${month}&type=${type}`);
            if (r.ok) {
                setPreview(await r.json());
                setShowPreview(true);
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error al obtener el preview', ok: false });
            }
        } finally {
            setLoading(null);
        }
    };

    const handleDownload = async () => {
        setLoading('download');
        setMessage(null);
        try {
            const endpoint = type === 'emitidas' ? 'sii/emitidas' : 'sii/recibidas';
            const r = await fetch(`/api/proxy/${endpoint}?year=${year}&month=${month}`);
            if (r.ok) {
                const blob = await r.blob();
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `SII_${type}_${year}_${String(month).padStart(2, '0')}.xml`;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error al generar el XML', ok: false });
            }
        } finally {
            setLoading(null);
        }
    };

    const handleSubmit = async () => {
        const parsed = siiSubmitSchema.safeParse({ type, year, month });
        if (!parsed.success) {
            setMessage({ text: parsed.error.issues[0]?.message ?? 'Revisa el formulario', ok: false });
            return;
        }
        if (!confirm(`¿Enviar las facturas ${type} de ${String(month).padStart(2, '0')}/${year} a la AEAT? Esta acción no se puede deshacer.`)) return;
        setLoading('submit');
        setMessage(null);
        try {
            const r = await fetch('/api/proxy/sii/submit', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ type, year, month }),
            });
            if (r.ok) {
                const data = await r.json();
                setMessage({ text: (data as { message?: string }).message || 'Envío realizado correctamente', ok: true });
            } else {
                const e = await r.json().catch(() => ({}));
                setMessage({ text: (e as { error?: string }).error || 'Error en el envío a AEAT', ok: false });
            }
        } finally {
            setLoading(null);
        }
    };

    const months = [
        'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
        'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'
    ];

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
            <div className="page-header">
                <div>
                    <h1 className="page-title">SII — Suministro Inmediato de Información</h1>
                    <p className="page-subtitle">Generación y envío de libros de IVA a la AEAT</p>
                </div>
            </div>

            <LegalDisclaimer title="Aviso legal — SII / AEAT">
                {FISCAL_EXPORT_DISCLAIMER_TEXT}
            </LegalDisclaimer>

            <div className="erp-card" style={{ padding: '28px', marginBottom: '20px' }}>
                <h2 style={{ fontSize: '14px', fontWeight: 700, marginBottom: '20px', textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-muted)' }}>Parámetros de Exportación</h2>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '16px', marginBottom: '24px' }}>
                    <div className="form-group" style={{ gridColumn: 'span 3' }}>
                        <label className="erp-label">TIPO DE LIBRO</label>
                        <select className="erp-input" value={type} onChange={e => setType(e.target.value as SiiType)}>
                            <option value="emitidas">Facturas Emitidas (Libro de Ventas)</option>
                            <option value="recibidas">Facturas Recibidas (Libro de Compras)</option>
                        </select>
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
                            {months.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                        </select>
                    </div>
                </div>

                {message && (
                    <div style={{ padding: '12px 16px', borderRadius: '8px', marginBottom: '20px', background: message.ok ? 'var(--success-bg)' : 'var(--danger-bg)', color: message.ok ? 'var(--success)' : 'var(--danger)', fontSize: '13px', fontWeight: 500 }}>
                        {message.ok ? '✓ ' : '✗ '}{message.text}
                    </div>
                )}

                <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
                    <button className="btn btn-secondary" onClick={handlePreview} disabled={!!loading}>
                        {loading === 'preview' ? '...' : '👁 Ver Preview JSON'}
                    </button>
                    <button className="btn btn-secondary" onClick={handleDownload} disabled={!!loading}
                        style={{ borderColor: 'var(--brand-primary)', color: 'var(--brand-primary)' }}>
                        {loading === 'download' ? '...' : '⬇ Descargar XML'}
                    </button>
                    <button className="btn btn-primary" onClick={handleSubmit} disabled={!!loading}>
                        {loading === 'submit' ? 'Enviando...' : '📡 Enviar a AEAT'}
                    </button>
                </div>
            </div>

            <div className="erp-card" style={{ padding: '20px', background: 'var(--surface-2)', border: '1px solid var(--border)' }}>
                <div style={{ fontSize: '12px', color: 'var(--text-muted)', lineHeight: 1.7 }}>
                    <strong style={{ color: 'var(--text-secondary)' }}>Información importante:</strong><br />
                    • El SII requiere presentación de facturas en el plazo máximo de 4 días naturales desde la expedición.<br />
                    • Las facturas deben estar <strong>bloqueadas (contabilizadas)</strong> para aparecer en el XML.<br />
                    • El envío a AEAT requiere certificado digital configurado en el servidor.
                </div>
            </div>

            {/* Preview Modal */}
            <AccessibleModal
                open={showPreview && !!preview}
                onClose={() => setShowPreview(false)}
                title={`Preview SII — ${type === 'emitidas' ? 'Facturas Emitidas' : 'Facturas Recibidas'} ${String(month).padStart(2, '0')}/${year}`}
                maxWidth="700px"
            >
                <div style={{ overflowY: 'auto', flex: 1 }}>
                    <pre style={{ fontSize: '11px', fontFamily: 'monospace', background: 'var(--surface-2)', padding: '16px', borderRadius: '8px', whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
                        {JSON.stringify(preview, null, 2)}
                    </pre>
                </div>
            </AccessibleModal>
        </div>
    );
}

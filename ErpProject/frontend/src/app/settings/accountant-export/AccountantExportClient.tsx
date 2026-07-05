'use client';
import { useCallback, useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';

export default function AccountantExportClient() {
    const [email, setEmail] = useState('');
    const [frequency, setFrequency] = useState('disabled');
    const [lastRun, setLastRun] = useState<string | null>(null);
    const [saving, setSaving] = useState(false);
    const [message, setMessage] = useState<string | null>(null);

    const load = useCallback(async () => {
        const r = await fetch('/api/proxy/accountant-export/settings');
        if (r.ok) {
            const d = await r.json();
            setEmail(d.accountantEmail ?? '');
            setFrequency(d.frequency ?? 'disabled');
            setLastRun(d.lastRunAt ?? null);
        }
    }, []);

    useEffect(() => { void load(); }, [load]);

    const save = async () => {
        setSaving(true);
        setMessage(null);
        const r = await fetch('/api/proxy/accountant-export/settings', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ accountantEmail: email || null, frequency }),
        });
        setSaving(false);
        if (r.ok) {
            setMessage('Configuración guardada');
            load();
        } else {
            setMessage('Error al guardar');
        }
    };

    const sendNow = async () => {
        setMessage(null);
        const r = await fetch('/api/proxy/accountant-export/send', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({}),
        });
        if (r.ok) {
            const d = await r.json();
            setMessage(d.message ?? 'Paquete enviado');
            load();
        } else {
            setMessage('Error al enviar (¿email configurado?)');
        }
    };

    const download = async () => {
        const r = await fetch('/api/proxy/accountant-export/export', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({}),
        });
        if (!r.ok) { setMessage('Error al generar ZIP'); return; }
        const blob = await r.blob();
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = 'paquete-gestoria.zip';
        a.click();
        URL.revokeObjectURL(a.href);
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Export a gestoría</h1>
                    <p className="page-subtitle">ZIP periódico: libro IVA emitidas + recibidas (ADR-0018 #42e)</p>
                </div>
            </div>

            <div className="erp-card" style={{ padding: '24px', maxWidth: '560px' }}>
                {message && <div style={{ marginBottom: 12, fontSize: 13, color: 'var(--success)' }}>{message}</div>}
                <div className="form-group" style={{ marginBottom: 14 }}>
                    <label className="erp-label">Email gestoría</label>
                    <input className="erp-input" type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="contabilidad@gestoria.com" />
                </div>
                <div className="form-group" style={{ marginBottom: 14 }}>
                    <label className="erp-label">Frecuencia envío automático</label>
                    <select className="erp-input" value={frequency} onChange={e => setFrequency(e.target.value)}>
                        <option value="disabled">Desactivado</option>
                        <option value="monthly">Mensual (día 3)</option>
                        <option value="quarterly">Trimestral</option>
                    </select>
                </div>
                {lastRun && <p style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 16 }}>Último envío: {new Date(lastRun).toLocaleString('es-ES')}</p>}
                <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
                    <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</button>
                    <button className="btn btn-secondary" onClick={download}>Descargar ZIP ahora</button>
                    <button className="btn btn-secondary" onClick={sendNow} disabled={!email}>Enviar por email</button>
                </div>
            </div>
        </PageContainer>
    );
}

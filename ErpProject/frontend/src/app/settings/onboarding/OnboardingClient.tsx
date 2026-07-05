'use client';
import { useEffect, useRef, useState } from 'react';
import PageContainer from '@/components/PageContainer';

interface Sector { id: string; name: string; description: string; extraAccounts: string[]; }

export default function OnboardingClient() {
    const [sectors, setSectors] = useState<Sector[]>([]);
    const [selected, setSelected] = useState<string>('');
    const [csv, setCsv] = useState('nombre;cif;email\n');
    const [message, setMessage] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const fileRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        fetch('/api/proxy/onboarding/sectors').then(r => r.ok ? r.json() : []).then(setSectors);
    }, []);

    const applySector = async () => {
        if (!selected) return;
        setLoading(true);
        const r = await fetch(`/api/proxy/onboarding/sectors/${selected}/apply`, { method: 'POST' });
        setLoading(false);
        const data = await r.json().catch(() => ({}));
        setMessage(r.ok ? data.message : data.error || 'Error');
    };

    const importClients = async () => {
        setLoading(true);
        const file = fileRef.current?.files?.[0];
        let body: Record<string, string>;

        if (file) {
            const buffer = await file.arrayBuffer();
            const bytes = new Uint8Array(buffer);
            let binary = '';
            bytes.forEach(b => { binary += String.fromCharCode(b); });
            body = { fileBase64: btoa(binary), fileName: file.name };
        } else {
            body = { csvContent: csv };
        }

        const r = await fetch('/api/proxy/onboarding/import-clients', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        setLoading(false);
        const data = await r.json().catch(() => ({}));
        setMessage(r.ok ? `Importados: ${data.imported}, omitidos: ${data.skipped}` : data.error || 'Error');
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Onboarding guiado</h1>
                    <p className="page-subtitle">Plantillas sectoriales e importación CSV/Excel (#41)</p>
                </div>
            </div>
            {message && <div className="erp-card" style={{ padding: '12px', marginBottom: '16px' }}>{message}</div>}

            <div className="erp-card" style={{ padding: '20px', marginBottom: '20px' }}>
                <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '12px' }}>1. Sector de actividad</h2>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: '12px', marginBottom: '16px' }}>
                    {sectors.map(s => (
                        <button key={s.id} type="button" onClick={() => setSelected(s.id)} style={{
                            padding: '14px', borderRadius: '8px', textAlign: 'left', cursor: 'pointer',
                            border: selected === s.id ? '2px solid var(--brand-primary)' : '1px solid var(--border)',
                            background: selected === s.id ? 'rgba(37,99,235,0.06)' : 'var(--surface)',
                        }}>
                            <div style={{ fontWeight: 700 }}>{s.name}</div>
                            <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{s.description}</div>
                        </button>
                    ))}
                </div>
                <button className="btn btn-primary" disabled={!selected || loading} onClick={applySector}>
                    Confirmar plan contable sectorial
                </button>
            </div>

            <div className="erp-card" style={{ padding: '20px' }}>
                <h2 style={{ fontSize: '16px', fontWeight: 700, marginBottom: '12px' }}>2. Importar clientes (CSV o Excel)</h2>
                <p style={{ fontSize: '13px', color: 'var(--text-muted)', marginBottom: '8px' }}>
                    Columnas: nombre, NIF (obligatorios); email, teléfono, dirección (opcionales)
                </p>
                <input ref={fileRef} type="file" accept=".csv,.xlsx" className="erp-input" style={{ marginBottom: '12px' }} />
                <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '8px' }}>O pegue CSV:</p>
                <textarea className="erp-input" rows={6} value={csv} onChange={e => setCsv(e.target.value)} style={{ width: '100%', fontFamily: 'monospace', fontSize: '12px' }} />
                <button className="btn btn-secondary" style={{ marginTop: '12px' }} disabled={loading} onClick={importClients}>
                    Importar clientes
                </button>
            </div>
        </PageContainer>
    );
}

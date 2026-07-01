'use client';
import { useEffect, useState } from 'react';
import PageContainer from '@/components/PageContainer';

interface ApiKey {
    id: string;
    name: string;
    keyPrefix: string;
    createdAt: string;
    lastUsed?: string;
    rateLimit: number;
    isActive: boolean;
}

export default function ApiKeysPage() {
    const [apiKeys, setApiKeys] = useState<ApiKey[]>([]);
    const [loading, setLoading] = useState(true);
    const [showCreateModal, setShowCreateModal] = useState(false);
    const [showViewModal, setShowViewModal] = useState(false);
    const [selectedKey, setSelectedKey] = useState<ApiKey | null>(null);
    const [fullKey, setFullKey] = useState<string>('');
    const [form, setForm] = useState({ name: '', rateLimit: 1000 });
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    useEffect(() => {
        const load = async () => {
            try {
                const res = await fetch('/api/proxy/api-keys');
                if (res.ok) setApiKeys(await res.json());
            } catch (err) {
                console.error('Error cargando API Keys:', err);
            } finally {
                setLoading(false);
            }
        };
        load();
    }, []);

    const handleCreate = async () => {
        if (!form.name) {
            setMessage({ type: 'error', text: 'El nombre es requerido' });
            return;
        }

        try {
            const response = await fetch('/api/proxy/api-keys', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(form),
            });

            if (response.ok) {
                const newKey = await response.json();
                setFullKey(newKey.fullKey); // Mostrar la key completa una sola vez
                setMessage({ type: 'success', text: 'API Key creada' });
                setShowCreateModal(false);
                setForm({ name: '', rateLimit: 1000 });
                // Recargar
                const res = await fetch('/api/proxy/api-keys');
                if (res.ok) setApiKeys(await res.json());
            } else {
                setMessage({ type: 'error', text: 'Error creando API Key' });
            }
        } catch (err) {
            setMessage({ type: 'error', text: 'Error en la solicitud' });
            console.error(err);
        }
    };

    const handleRevoke = async (id: string) => {
        if (!confirm('¿Revocar esta API Key? Esta acción es irreversible.')) return;

        try {
            const response = await fetch(`/api/proxy/api-keys/${id}`, {
                method: 'DELETE',
            });

            if (response.ok) {
                setMessage({ type: 'success', text: 'API Key revocada' });
                setApiKeys(apiKeys.filter(k => k.id !== id));
            } else {
                setMessage({ type: 'error', text: 'Error revocando API Key' });
            }
        } catch (err) {
            setMessage({ type: 'error', text: 'Error en la solicitud' });
            console.error(err);
        }
    };

    const copyToClipboard = (text: string) => {
        navigator.clipboard.writeText(text);
        setMessage({ type: 'success', text: 'Copiado al portapapeles' });
    };

    return (
        <PageContainer>
            {/* Header */}
            <div className="page-header">
                <div>
                    <h1 className="page-title">API Keys</h1>
                    <p className="page-subtitle">Gestión de claves para acceso a la API pública</p>
                </div>
            </div>

            {/* Mensaje */}
            {message && (
                <div style={{
                    marginBottom: '16px', padding: '12px 16px', borderRadius: '6px',
                    background: message.type === 'success' ? 'rgba(34,197,94,0.1)' : 'rgba(239,68,68,0.1)',
                    border: `1px solid ${message.type === 'success' ? '#22c55e' : '#ef4444'}`,
                    color: message.type === 'success' ? '#15803d' : '#991b1b',
                    fontSize: '13px',
                }}>
                    {message.type === 'success' ? '?' : '?'} {message.text}
                </div>
            )}

            {/* Botón Crear */}
            <button
                onClick={() => setShowCreateModal(true)}
                style={{
                    marginBottom: '20px', padding: '10px 16px', borderRadius: '6px',
                    background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                    color: 'white', border: 'none', fontSize: '13px', fontWeight: 600,
                    cursor: 'pointer', transition: 'all 0.2s',
                }}
                onMouseEnter={(e) => e.currentTarget.style.transform = 'translateY(-2px)'}
                onMouseLeave={(e) => e.currentTarget.style.transform = 'translateY(0)'}
            >
                + Crear Nueva API Key
            </button>

            {/* Tabla */}
            {loading ? (
                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    <p>? Cargando API Keys...</p>
                </div>
            ) : apiKeys.length === 0 ? (
                <div className="erp-card" style={{ padding: '40px', textAlign: 'center' }}>
                    <p style={{ fontSize: '14px', color: 'var(--text-muted)', marginBottom: '12px' }}>
                        ?? No hay API Keys creadas
                    </p>
                    <p style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        Crea la primera para empezar a usar la API pública
                    </p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                        <thead>
                            <tr style={{ background: 'var(--bg-secondary)', borderBottom: '2px solid var(--border)' }}>
                                <th style={{ padding: '10px', textAlign: 'left', fontWeight: 600, color: 'var(--text-muted)' }}>Nombre</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontWeight: 600, color: 'var(--text-muted)' }}>Prefijo</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontWeight: 600, color: 'var(--text-muted)' }}>Rate Limit</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontWeight: 600, color: 'var(--text-muted)' }}>Creada</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontWeight: 600, color: 'var(--text-muted)' }}>Última Uso</th>
                                <th style={{ padding: '10px', textAlign: 'left', fontWeight: 600, color: 'var(--text-muted)' }}>Estado</th>
                                <th style={{ padding: '10px', textAlign: 'center', fontWeight: 600, color: 'var(--text-muted)' }}>Acciones</th>
                            </tr>
                        </thead>
                        <tbody>
                            {apiKeys.map((key) => (
                                <tr key={key.id} style={{ borderBottom: '1px solid var(--border)' }}>
                                    <td style={{ padding: '10px', fontWeight: 600 }}>{key.name}</td>
                                    <td style={{ padding: '10px', fontFamily: 'monospace', fontSize: '11px' }}>
                                        <code style={{ background: 'var(--bg-secondary)', padding: '4px 6px', borderRadius: '3px' }}>
                                            {key.keyPrefix}***
                                        </code>
                                    </td>
                                    <td style={{ padding: '10px' }}>{key.rateLimit} req/min</td>
                                    <td style={{ padding: '10px' }}>{new Date(key.createdAt).toLocaleDateString('es-ES')}</td>
                                    <td style={{ padding: '10px', color: 'var(--text-muted)' }}>
                                        {key.lastUsed ? new Date(key.lastUsed).toLocaleDateString('es-ES') : '-'}
                                    </td>
                                    <td style={{ padding: '10px' }}>
                                        <span style={{
                                            padding: '4px 8px', borderRadius: '4px',
                                            background: key.isActive ? '#ecfdf5' : '#fef2f2',
                                            color: key.isActive ? '#065f46' : '#991b1b',
                                            fontSize: '11px', fontWeight: 600,
                                        }}>
                                            {key.isActive ? '? Activa' : '? Revocada'}
                                        </span>
                                    </td>
                                    <td style={{ padding: '10px', textAlign: 'center' }}>
                                        <button
                                            onClick={() => {
                                                setSelectedKey(key);
                                                setShowViewModal(true);
                                            }}
                                            style={{
                                                padding: '4px 8px', borderRadius: '4px',
                                                background: '#eff6ff', border: '1px solid #bfdbfe',
                                                color: '#1e40af', fontSize: '11px', fontWeight: 600,
                                                cursor: 'pointer', marginRight: '6px',
                                            }}
                                        >
                                            Ver
                                        </button>
                                        {key.isActive && (
                                            <button
                                                onClick={() => handleRevoke(key.id)}
                                                style={{
                                                    padding: '4px 8px', borderRadius: '4px',
                                                    background: '#fef2f2', border: '1px solid #fca5a5',
                                                    color: '#991b1b', fontSize: '11px', fontWeight: 600,
                                                    cursor: 'pointer',
                                                }}
                                            >
                                                Revocar
                                            </button>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Info API */}
            <div style={{ marginTop: '28px' }}>
                <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '12px' }}>
                    Documentación de API
                </h2>
                <div className="erp-card" style={{ padding: '16px 20px' }}>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                        <p>
                            <strong style={{ color: 'var(--text-primary)' }}>Endpoint Base:</strong><br/>
                            <code style={{ background: 'var(--bg-secondary)', padding: '4px 6px', borderRadius: '3px' }}>https://api.tudominio.com/api/v1</code>
                        </p>
                        <p style={{ marginTop: '12px' }}>
                            <strong style={{ color: 'var(--text-primary)' }}>Autenticación:</strong><br/>
                            Incluye el header: <code style={{ background: 'var(--bg-secondary)', padding: '4px 6px', borderRadius: '3px' }}>X-API-Key: tu_clave</code>
                        </p>
                        <p style={{ marginTop: '12px' }}>
                            <strong style={{ color: 'var(--text-primary)' }}>Rate Limiting:</strong><br/>
                            Las requests se limitan según el rate limit de tu API Key
                        </p>
                    </div>
                </div>
            </div>

            {/* Modal Crear */}
            {showCreateModal && (
                <div style={{
                    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    zIndex: 1000, fontFamily: 'Inter, sans-serif',
                }}>
                    <div style={{
                        background: 'var(--bg-primary)', borderRadius: '8px', padding: '24px',
                        maxWidth: '500px', width: '90%', boxShadow: '0 20px 60px rgba(0,0,0,0.3)',
                    }}>
                        <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '16px' }}>
                            Crear Nueva API Key
                        </h2>

                        <div style={{ marginBottom: '16px' }}>
                            <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                                NOMBRE *
                            </label>
                            <input
                                type="text"
                                value={form.name}
                                onChange={(e) => setForm({ ...form, name: e.target.value })}
                                placeholder="Ej: Integración Shopify"
                                style={{
                                    width: '100%', padding: '10px', borderRadius: '6px',
                                    border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                    color: 'var(--text-primary)', fontSize: '13px',
                                    fontFamily: 'Inter, sans-serif', outline: 'none',
                                }}
                            />
                        </div>

                        <div style={{ marginBottom: '16px' }}>
                            <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                                RATE LIMIT (req/minuto)
                            </label>
                            <input
                                type="number"
                                value={form.rateLimit}
                                onChange={(e) => setForm({ ...form, rateLimit: parseInt(e.target.value) })}
                                min="100"
                                max="10000"
                                style={{
                                    width: '100%', padding: '10px', borderRadius: '6px',
                                    border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                    color: 'var(--text-primary)', fontSize: '13px',
                                    fontFamily: 'Inter, sans-serif', outline: 'none',
                                }}
                            />
                            <p style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '4px' }}>
                                Entre 100 y 10000 requests por minuto
                            </p>
                        </div>

                        <div style={{ display: 'flex', gap: '12px' }}>
                            <button
                                onClick={() => {
                                    setShowCreateModal(false);
                                    setForm({ name: '', rateLimit: 1000 });
                                }}
                                style={{
                                    flex: 1, padding: '10px', borderRadius: '6px', border: '1px solid var(--border)',
                                    background: 'var(--bg-secondary)', color: 'var(--text-primary)',
                                    fontSize: '13px', fontWeight: 600, cursor: 'pointer',
                                }}
                            >
                                Cancelar
                            </button>
                            <button
                                onClick={handleCreate}
                                style={{
                                    flex: 1, padding: '10px', borderRadius: '6px', border: 'none',
                                    background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                                    color: 'white', fontSize: '13px', fontWeight: 600,
                                    cursor: 'pointer', transition: 'all 0.2s',
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.transform = 'translateY(-2px)'}
                                onMouseLeave={(e) => e.currentTarget.style.transform = 'translateY(0)'}
                            >
                                Crear API Key
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Modal Ver */}
            {showViewModal && selectedKey && (
                <div style={{
                    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)',
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                    zIndex: 1000, fontFamily: 'Inter, sans-serif',
                }}>
                    <div style={{
                        background: 'var(--bg-primary)', borderRadius: '8px', padding: '24px',
                        maxWidth: '500px', width: '90%', boxShadow: '0 20px 60px rgba(0,0,0,0.3)',
                    }}>
                        <h2 style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '16px' }}>
                            Detalles de API Key
                        </h2>

                        <div style={{ marginBottom: '16px' }}>
                            <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', letterSpacing: '0.04em' }}>
                                NOMBRE
                            </div>
                            <div style={{ fontSize: '13px', color: 'var(--text-primary)' }}>{selectedKey.name}</div>
                        </div>

                        {fullKey && (
                            <div style={{ marginBottom: '16px', padding: '12px', background: 'var(--bg-secondary)', borderRadius: '6px', border: '1px solid var(--border)' }}>
                                <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', letterSpacing: '0.04em' }}>
                                    ?? CLAVE COMPLETA (mostrada una sola vez)
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                    <code style={{ flex: 1, padding: '8px', background: 'var(--bg-primary)', borderRadius: '4px', fontSize: '11px', fontFamily: 'monospace', overflow: 'auto', color: 'var(--text-primary)' }}>
                                        {fullKey}
                                    </code>
                                    <button
                                        onClick={() => copyToClipboard(fullKey)}
                                        style={{
                                            padding: '6px 10px', borderRadius: '4px', border: '1px solid var(--border)',
                                            background: 'var(--bg-primary)', color: 'var(--text-primary)',
                                            fontSize: '11px', fontWeight: 600, cursor: 'pointer',
                                        }}
                                    >
                                        Copiar
                                    </button>
                                </div>
                            </div>
                        )}

                        <div style={{ marginBottom: '16px' }}>
                            <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '4px', letterSpacing: '0.04em' }}>
                                RATE LIMIT
                            </div>
                            <div style={{ fontSize: '13px', color: 'var(--text-primary)' }}>{selectedKey.rateLimit} requests/minuto</div>
                        </div>

                        <div style={{ display: 'flex', gap: '12px' }}>
                            <button
                                onClick={() => setShowViewModal(false)}
                                style={{
                                    flex: 1, padding: '10px', borderRadius: '6px', border: 'none',
                                    background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                                    color: 'white', fontSize: '13px', fontWeight: 600,
                                    cursor: 'pointer',
                                }}
                            >
                                Cerrar
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </PageContainer>
    );
}

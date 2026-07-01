'use client';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import PageContainer from '@/components/PageContainer';

interface Client {
    id: string;
    name: string;
    taxId: string;
    email?: string;
    phone?: string;
    address?: string;
    city?: string;
    zipCode?: string;
    country?: string;
}

export default function ClientEditorPage({ params }: { params: { id: string } }) {
    const router = useRouter();
    const [client, setClient] = useState<Client | null>(null);
    const [loading, setLoading] = useState(!!params.id);
    const [saving, setSaving] = useState(false);
    const [form, setForm] = useState<Client>({
        id: '',
        name: '',
        taxId: '',
        email: '',
        phone: '',
        address: '',
        city: '',
        zipCode: '',
        country: 'ES',
    });
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    useEffect(() => {
        if (params.id && params.id !== 'new') {
            const loadClient = async () => {
                try {
                    const res = await fetch(`/api/proxy/clients/${params.id}`);
                    if (res.ok) {
                        const data = await res.json();
                        setClient(data);
                        setForm(data);
                    }
                } catch (err) {
                    console.error('Error cargando cliente:', err);
                } finally {
                    setLoading(false);
                }
            };
            loadClient();
        }
    }, [params.id]);

    const handleSave = async () => {
        if (!form.name || !form.taxId) {
            setMessage({ type: 'error', text: 'Nombre y CIF/NIF son requeridos' });
            return;
        }

        setSaving(true);
        try {
            const method = client ? 'PATCH' : 'POST';
            const url = client ? `/api/proxy/clients/${client.id}` : '/api/proxy/clients';
            
            const response = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(form),
            });

            if (response.ok) {
                setMessage({ type: 'success', text: client ? 'Cliente actualizado' : 'Cliente creado' });
                setTimeout(() => router.push('/crm'), 1500);
            } else {
                setMessage({ type: 'error', text: 'Error guardando cliente' });
            }
        } catch (err) {
            setMessage({ type: 'error', text: 'Error en la solicitud' });
            console.error(err);
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return (
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh', color: 'var(--text-muted)' }}>
                <p>? Cargando cliente...</p>
            </div>
        );
    }

    return (
        <PageContainer>
            {/* Header */}
            <div style={{ marginBottom: '28px' }}>
                <h1 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-primary)', marginBottom: '4px' }}>
                    {client ? 'Editar Cliente' : 'Nuevo Cliente'}
                </h1>
                <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    {client ? `${client.name} (${client.taxId})` : 'Crear un nuevo cliente'}
                </p>
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

            {/* Formulario */}
            <div className="erp-card" style={{ padding: '20px' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                    {/* Nombre */}
                    <div>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            NOMBRE *
                        </label>
                        <input
                            type="text"
                            value={form.name}
                            onChange={(e) => setForm({ ...form, name: e.target.value })}
                            placeholder="Ej: Empresa SL"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>

                    {/* CIF/NIF */}
                    <div>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            CIF/NIF *
                        </label>
                        <input
                            type="text"
                            value={form.taxId}
                            onChange={(e) => setForm({ ...form, taxId: e.target.value })}
                            placeholder="Ej: A12345678"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>

                    {/* Email */}
                    <div>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            EMAIL
                        </label>
                        <input
                            type="email"
                            value={form.email || ''}
                            onChange={(e) => setForm({ ...form, email: e.target.value })}
                            placeholder="cliente@empresa.com"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>

                    {/* Teléfono */}
                    <div>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            TELÉFONO
                        </label>
                        <input
                            type="tel"
                            value={form.phone || ''}
                            onChange={(e) => setForm({ ...form, phone: e.target.value })}
                            placeholder="+34 xxx xx xx xx"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>

                    {/* Dirección */}
                    <div style={{ gridColumn: '1 / -1' }}>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            DIRECCIÓN
                        </label>
                        <input
                            type="text"
                            value={form.address || ''}
                            onChange={(e) => setForm({ ...form, address: e.target.value })}
                            placeholder="Calle, número, piso"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>

                    {/* Ciudad */}
                    <div>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            CIUDAD
                        </label>
                        <input
                            type="text"
                            value={form.city || ''}
                            onChange={(e) => setForm({ ...form, city: e.target.value })}
                            placeholder="Madrid"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>

                    {/* Código Postal */}
                    <div>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            CÓDIGO POSTAL
                        </label>
                        <input
                            type="text"
                            value={form.zipCode || ''}
                            onChange={(e) => setForm({ ...form, zipCode: e.target.value })}
                            placeholder="28001"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>

                    {/* País */}
                    <div>
                        <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '6px', letterSpacing: '0.04em' }}>
                            PAÍS
                        </label>
                        <input
                            type="text"
                            value={form.country || 'ES'}
                            onChange={(e) => setForm({ ...form, country: e.target.value })}
                            placeholder="ES"
                            style={{
                                width: '100%', padding: '10px', borderRadius: '6px',
                                border: '1px solid var(--border)', background: 'var(--bg-secondary)',
                                color: 'var(--text-primary)', fontSize: '13px',
                                fontFamily: 'Inter, sans-serif', outline: 'none',
                            }}
                        />
                    </div>
                </div>

                {/* Botones */}
                <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '20px', paddingTop: '20px', borderTop: '1px solid var(--border)' }}>
                    <button
                        onClick={() => router.push('/crm')}
                        style={{
                            padding: '10px 20px', borderRadius: '6px', border: '1px solid var(--border)',
                            background: 'var(--bg-secondary)', color: 'var(--text-primary)',
                            fontSize: '13px', fontWeight: 600, cursor: 'pointer',
                        }}
                    >
                        Cancelar
                    </button>
                    <button
                        onClick={handleSave}
                        disabled={saving}
                        style={{
                            padding: '10px 20px', borderRadius: '6px', border: 'none',
                            background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                            color: 'white', fontSize: '13px', fontWeight: 600,
                            cursor: saving ? 'not-allowed' : 'pointer',
                            opacity: saving ? 0.6 : 1,
                        }}
                    >
                        {saving ? '? Guardando...' : 'Guardar Cliente'}
                    </button>
                </div>
            </div>
        </PageContainer>
    );
}

'use client';
import { useState } from 'react';
import Link from 'next/link';
import { parseListResponse } from '@/lib/parseListResponse';
import { useCachedApi } from '@/hooks/useCachedApi';
import PageContainer from '@/components/PageContainer';

export interface Client {
    id: string;
    name: string;
    email: string;
    phone: string;
    taxId: string;
    address: string;
}

interface ClientsListClientProps {
    initialClients: Client[];
}

export default function ClientsListClient({ initialClients }: ClientsListClientProps) {
    const [clients, setClients] = useState<Client[]>(initialClients);
    const [search, setSearch] = useState('');
    const { fetchCached, invalidateCached } = useCachedApi();

    const refresh = async () => {
        invalidateCached('clients');
        const data = await fetchCached<unknown>('clients?pageSize=500');
        if (data) setClients(parseListResponse<Client>(data));
    };

    const filtered = clients.filter(c =>
        c.name.toLowerCase().includes(search.toLowerCase()) ||
        c.taxId?.toLowerCase().includes(search.toLowerCase()) ||
        c.email?.toLowerCase().includes(search.toLowerCase())
    );

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Clientes</h1>
                    <p className="page-subtitle">{clients.length} clientes registrados</p>
                </div>
                <Link href="/crm/clients/new" className="btn btn-primary">
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                    </svg>
                    Nuevo Cliente
                </Link>
            </div>

            <div style={{ marginBottom: '16px', maxWidth: '320px' }}>
                <input className="erp-input" placeholder="Buscar por nombre, CIF o email..." value={search}
                    onChange={e => setSearch(e.target.value)} style={{ margin: 0 }} />
            </div>

            {filtered.length === 0 ? (
                <div className="erp-card" style={{ padding: '56px', textAlign: 'center' }}>
                    <div style={{ fontSize: '36px', marginBottom: '12px' }}>👤</div>
                    <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>
                        {search ? 'Sin resultados para esa búsqueda' : 'No hay clientes registrados'}
                    </p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Nombre</th>
                                <th>CIF/NIF</th>
                                <th>Email</th>
                                <th>Teléfono</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody>
                            {filtered.map(c => (
                                <tr key={c.id}>
                                    <td style={{ fontWeight: 600 }}>{c.name}</td>
                                    <td style={{ fontFamily: 'monospace', fontSize: '13px' }}>{c.taxId || '—'}</td>
                                    <td>{c.email || '—'}</td>
                                    <td>{c.phone || '—'}</td>
                                    <td>
                                        <Link href={`/crm/clients/${c.id}`} className="btn btn-secondary btn-sm">
                                            Editar
                                        </Link>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            <button className="btn btn-secondary btn-sm" style={{ marginTop: '12px' }} onClick={refresh}>
                Actualizar listado
            </button>
        </PageContainer>
    );
}

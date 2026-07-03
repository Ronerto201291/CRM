'use client';

import { useState } from 'react';
import Link from 'next/link';
import PageContainer from '@/components/PageContainer';
import { useCachedApi } from '@/hooks/useCachedApi';
import { parseListResponse } from '@/lib/parseListResponse';

export interface Contact {
    id: string;
    name: string;
    email: string;
    phone: string;
    position: string;
    clientId?: string;
    supplierId?: string;
    clientName?: string;
    supplierName?: string;
}

export default function ContactsClient({ initialContacts }: { initialContacts: Contact[] }) {
    const [contacts, setContacts] = useState<Contact[]>(initialContacts);
    const [search, setSearch] = useState('');
    const { fetchCached, invalidateCached } = useCachedApi();

    const refresh = async () => {
        invalidateCached('contacts');
        const data = await fetchCached<unknown>('contacts?pageSize=500');
        if (data) setContacts(parseListResponse<Contact>(data));
    };

    const filtered = contacts.filter(c =>
        c.name.toLowerCase().includes(search.toLowerCase()) ||
        c.email?.toLowerCase().includes(search.toLowerCase()) ||
        c.clientName?.toLowerCase().includes(search.toLowerCase()) ||
        c.supplierName?.toLowerCase().includes(search.toLowerCase()),
    );

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Contactos</h1>
                    <p className="page-subtitle">{contacts.length} contactos registrados</p>
                </div>
                <Link href="/crm" className="btn btn-secondary">Gestionar en CRM</Link>
            </div>

            <div style={{ marginBottom: '16px', maxWidth: '320px' }}>
                <input
                    className="erp-input"
                    placeholder="Buscar contacto..."
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    style={{ margin: 0 }}
                />
            </div>

            <div className="erp-card" style={{ overflow: 'hidden' }}>
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Nombre</th>
                            <th>Email</th>
                            <th>Teléfono</th>
                            <th>Cargo</th>
                            <th>Vinculado a</th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.length === 0 && (
                            <tr><td colSpan={5} style={{ textAlign: 'center', padding: '24px', color: 'var(--text-muted)' }}>
                                {search ? 'Sin resultados' : 'No hay contactos'}
                            </td></tr>
                        )}
                        {filtered.map(c => (
                            <tr key={c.id}>
                                <td style={{ fontWeight: 600 }}>{c.name}</td>
                                <td>{c.email || '—'}</td>
                                <td>{c.phone || '—'}</td>
                                <td>{c.position || '—'}</td>
                                <td style={{ color: 'var(--text-secondary)', fontSize: '12px' }}>
                                    {c.clientName ? `Cliente: ${c.clientName}` : c.supplierName ? `Proveedor: ${c.supplierName}` : '—'}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
            <button type="button" className="btn btn-secondary btn-sm" style={{ marginTop: 12 }} onClick={refresh}>
                Actualizar
            </button>
        </PageContainer>
    );
}

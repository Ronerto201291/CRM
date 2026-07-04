'use client';
import { useState } from 'react';
import PageListLayout from '@/components/PageListLayout';
import EmptyState from '@/components/EmptyState';
import type { SupplierInvoiceUpload } from './page';

interface SupplierUploadsClientProps {
    initialUploads: SupplierInvoiceUpload[];
}

export default function SupplierUploadsClient({ initialUploads }: SupplierUploadsClientProps) {
    const [uploads, setUploads] = useState<SupplierInvoiceUpload[]>(initialUploads);

    const reload = async () => {
        const res = await fetch('/api/proxy/suppliers/uploads');
        if (res.ok) setUploads(await res.json());
    };

    const handleDownload = async (id: string) => {
        const res = await fetch(`/api/proxy/suppliers/uploads/${id}/download-url`);
        if (res.ok) {
            const { url } = await res.json();
            window.open(url, '_blank', 'noopener,noreferrer');
        }
    };

    const handleMarkReviewed = async (id: string) => {
        const res = await fetch(`/api/proxy/suppliers/uploads/${id}/mark-reviewed`, { method: 'POST' });
        if (res.ok) await reload();
    };

    return (
        <PageListLayout
            title="Facturas de proveedores recibidas"
            subtitle="Facturas subidas por proveedores vía su enlace público de subida (crm/proveedores)"
        >
            {uploads.length === 0 ? (
                <EmptyState icon="📥" title="Sin facturas recibidas" description="Todavía no se ha subido ninguna factura de proveedor." />
            ) : (
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Proveedor</th>
                            <th>Archivo</th>
                            <th>Comentario</th>
                            <th>Fecha</th>
                            <th>Estado</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        {uploads.map(u => (
                            <tr key={u.id}>
                                <td>{u.supplierName}</td>
                                <td>{u.fileName}</td>
                                <td>{u.comment || '—'}</td>
                                <td>{new Date(u.uploadedAt).toLocaleDateString('es-ES')}</td>
                                <td>
                                    <span style={{
                                        padding: '2px 8px', borderRadius: '10px', fontSize: '12px', fontWeight: 600,
                                        background: u.status === 'Reviewed' ? '#dcfce7' : '#fef3c7',
                                        color: u.status === 'Reviewed' ? '#15803d' : '#b45309',
                                    }}>
                                        {u.status === 'Reviewed' ? 'Revisada' : 'Pendiente'}
                                    </span>
                                </td>
                                <td>
                                    <button
                                        onClick={() => handleDownload(u.id)}
                                        style={{ background: 'none', border: 'none', color: '#2563eb', cursor: 'pointer', padding: 0, fontSize: '13px' }}
                                    >
                                        Descargar
                                    </button>
                                    {u.status !== 'Reviewed' && (
                                        <>
                                            {' · '}
                                            <button
                                                onClick={() => handleMarkReviewed(u.id)}
                                                style={{ background: 'none', border: 'none', color: 'var(--success, #15803d)', cursor: 'pointer', padding: 0, fontSize: '13px' }}
                                            >
                                                Marcar revisada
                                            </button>
                                        </>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </PageListLayout>
    );
}

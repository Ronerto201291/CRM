'use client';
import { useRef, useState } from 'react';
import PageListLayout from '@/components/PageListLayout';
import EmptyState from '@/components/EmptyState';
import FormLabel from '@/components/FormLabel';
import type { DocumentItem } from './page';

interface DocumentsClientProps {
    initialDocuments: DocumentItem[];
}

function formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function DocumentsClient({ initialDocuments }: DocumentsClientProps) {
    const [documents, setDocuments] = useState<DocumentItem[]>(initialDocuments);
    const [description, setDescription] = useState('');
    const [uploading, setUploading] = useState(false);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
    const fileInputRef = useRef<HTMLInputElement>(null);

    const reload = async () => {
        const res = await fetch('/api/proxy/documents');
        if (res.ok) setDocuments(await res.json());
    };

    const handleUpload = async () => {
        const file = fileInputRef.current?.files?.[0];
        if (!file) {
            setMessage({ type: 'error', text: 'Selecciona un archivo.' });
            return;
        }

        setUploading(true);
        setMessage(null);
        try {
            const form = new FormData();
            form.append('file', file);
            if (description) form.append('description', description);

            const res = await fetch('/api/proxy/documents', { method: 'POST', body: form });
            if (res.ok) {
                setMessage({ type: 'success', text: 'Documento subido.' });
                setDescription('');
                if (fileInputRef.current) fileInputRef.current.value = '';
                await reload();
            } else {
                const err = await res.json().catch(() => ({}));
                setMessage({ type: 'error', text: err.error || 'Error al subir el documento.' });
            }
        } catch {
            setMessage({ type: 'error', text: 'Error de conexión.' });
        } finally {
            setUploading(false);
        }
    };

    const handleDownload = async (id: string) => {
        const res = await fetch(`/api/proxy/documents/${id}/download-url`);
        if (res.ok) {
            const { url } = await res.json();
            window.open(url, '_blank', 'noopener,noreferrer');
        }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('¿Eliminar este documento?')) return;
        const res = await fetch(`/api/proxy/documents/${id}`, { method: 'DELETE' });
        if (res.ok) await reload();
    };

    return (
        <PageListLayout
            title="Biblioteca de documentos"
            subtitle="Documentos internos de la empresa (contratos, DNIs, adjuntos varios)"
        >
            <div className="erp-card" style={{ marginBottom: '16px', padding: '16px' }}>
                <FormLabel htmlFor="doc-file">Archivo</FormLabel>
                <input
                    id="doc-file"
                    ref={fileInputRef}
                    type="file"
                    accept=".pdf,.jpg,.jpeg,.png,.webp,.doc,.docx,.xls,.xlsx"
                    style={{ marginBottom: '12px', display: 'block' }}
                />
                <FormLabel htmlFor="doc-description">Descripción (opcional)</FormLabel>
                <input
                    id="doc-description"
                    className="erp-input"
                    value={description}
                    onChange={e => setDescription(e.target.value)}
                    placeholder="p. ej. Contrato de mantenimiento anual"
                    style={{ marginBottom: '12px' }}
                />
                <button
                    onClick={handleUpload}
                    disabled={uploading}
                    style={{
                        padding: '10px 16px', borderRadius: '6px',
                        background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                        color: 'white', border: 'none', fontSize: '13px', fontWeight: 600,
                        cursor: uploading ? 'default' : 'pointer', opacity: uploading ? 0.7 : 1,
                    }}
                >
                    {uploading ? 'Subiendo...' : 'Subir documento'}
                </button>
                {message && (
                    <p style={{ color: message.type === 'error' ? 'var(--danger)' : 'var(--success)', marginTop: '8px' }}>
                        {message.text}
                    </p>
                )}
            </div>

            {documents.length === 0 ? (
                <EmptyState icon="📁" title="Sin documentos" description="Todavía no se ha subido ningún documento." />
            ) : (
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Nombre</th>
                            <th>Tamaño</th>
                            <th>Descripción</th>
                            <th>Fecha</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        {documents.map(doc => (
                            <tr key={doc.id}>
                                <td>{doc.fileName}</td>
                                <td>{formatSize(doc.sizeBytes)}</td>
                                <td>{doc.description || '—'}</td>
                                <td>{new Date(doc.uploadedAt).toLocaleDateString('es-ES')}</td>
                                <td>
                                    <button
                                        onClick={() => handleDownload(doc.id)}
                                        style={{ background: 'none', border: 'none', color: '#2563eb', cursor: 'pointer', padding: 0, fontSize: '13px' }}
                                    >
                                        Descargar
                                    </button>
                                    {' · '}
                                    <button
                                        onClick={() => handleDelete(doc.id)}
                                        style={{ background: 'none', border: 'none', color: 'var(--danger)', cursor: 'pointer', padding: 0, fontSize: '13px' }}
                                    >
                                        Eliminar
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}
        </PageListLayout>
    );
}

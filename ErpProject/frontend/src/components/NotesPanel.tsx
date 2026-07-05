'use client';
import { useEffect, useState, useCallback } from 'react';

interface CrmNote {
    id: string;
    entityType: 'Client' | 'Lead';
    entityId: string;
    title?: string;
    content: string;
    createdAt: string;
    updatedAt?: string;
}

interface Props {
    entityType: 'Client' | 'Lead';
    entityId: string;
}

export default function NotesPanel({ entityType, entityId }: Props) {
    const [notes, setNotes] = useState<CrmNote[]>([]);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [editingId, setEditingId] = useState<string | null>(null);

    // New note form
    const [newTitle, setNewTitle] = useState('');
    const [newContent, setNewContent] = useState('');

    // Edit form
    const [editTitle, setEditTitle] = useState('');
    const [editContent, setEditContent] = useState('');

    const load = useCallback(async () => {
        setLoading(true);
        const res = await fetch(`/api/proxy/crm/notes?entityType=${entityType}&entityId=${entityId}`);
        if (res.ok) setNotes(await res.json());
        setLoading(false);
    }, [entityType, entityId]);

    useEffect(() => {
        queueMicrotask(() => { void load(); });
    }, [load]);

    const handleAdd = async () => {
        if (!newContent.trim()) return;
        setSaving(true);
        const res = await fetch('/api/proxy/crm/notes', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ entityType, entityId, title: newTitle || null, content: newContent }),
        });
        setSaving(false);
        if (res.ok) {
            setNewTitle('');
            setNewContent('');
            load();
        }
    };

    const startEdit = (note: CrmNote) => {
        setEditingId(note.id);
        setEditTitle(note.title ?? '');
        setEditContent(note.content);
    };

    const cancelEdit = () => { setEditingId(null); };

    const handleUpdate = async (id: string) => {
        if (!editContent.trim()) return;
        setSaving(true);
        const res = await fetch(`/api/proxy/crm/notes/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ title: editTitle || null, content: editContent }),
        });
        setSaving(false);
        if (res.ok) { setEditingId(null); load(); }
    };

    const handleDelete = async (id: string) => {
        if (!confirm('¿Eliminar esta nota?')) return;
        await fetch(`/api/proxy/crm/notes/${id}`, { method: 'DELETE' });
        load();
    };

    const fmt = (iso: string) =>
        new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' });

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {/* Existing notes */}
            {loading ? (
                <p style={{ fontSize: '13px', color: 'var(--text-muted)', textAlign: 'center', padding: '16px 0' }}>Cargando...</p>
            ) : notes.length === 0 ? (
                <p style={{ fontSize: '13px', color: 'var(--text-muted)', textAlign: 'center', padding: '16px 0' }}>
                    Sin notas. Añade la primera nota abajo.
                </p>
            ) : (
                notes.map(note => (
                    <div key={note.id} style={{
                        background: 'var(--bg-secondary)',
                        borderRadius: '8px',
                        padding: '12px 14px',
                        border: '1px solid var(--border)',
                    }}>
                        {editingId === note.id ? (
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                                <input
                                    className="erp-input"
                                    placeholder="Título (opcional)"
                                    value={editTitle}
                                    onChange={e => setEditTitle(e.target.value)}
                                    style={{ marginBottom: 0 }}
                                />
                                <textarea
                                    className="erp-input"
                                    placeholder="Contenido *"
                                    value={editContent}
                                    onChange={e => setEditContent(e.target.value)}
                                    rows={3}
                                    style={{ marginBottom: 0, resize: 'vertical' }}
                                />
                                <div style={{ display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                                    <button className="btn btn-secondary" style={{ fontSize: '12px', padding: '4px 12px' }} onClick={cancelEdit}>Cancelar</button>
                                    <button className="btn btn-primary" style={{ fontSize: '12px', padding: '4px 12px' }} onClick={() => handleUpdate(note.id)} disabled={saving}>
                                        {saving ? 'Guardando...' : 'Guardar'}
                                    </button>
                                </div>
                            </div>
                        ) : (
                            <>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: note.title ? '6px' : 0 }}>
                                    <div style={{ flex: 1 }}>
                                        {note.title && (
                                            <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '4px' }}>
                                                {note.title}
                                            </div>
                                        )}
                                        <p style={{ fontSize: '13px', color: 'var(--text-primary)', margin: 0, whiteSpace: 'pre-wrap' }}>
                                            {note.content}
                                        </p>
                                    </div>
                                    <div style={{ display: 'flex', gap: '6px', marginLeft: '10px', flexShrink: 0 }}>
                                        <button
                                            onClick={() => startEdit(note)}
                                            title="Editar"
                                            style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '2px', color: 'var(--text-muted)' }}
                                        >
                                            <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                                                <path strokeLinecap="round" strokeLinejoin="round" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                                            </svg>
                                        </button>
                                        <button
                                            onClick={() => handleDelete(note.id)}
                                            title="Eliminar"
                                            style={{ background: 'none', border: 'none', cursor: 'pointer', padding: '2px', color: 'var(--danger)' }}
                                        >
                                            <svg width="13" height="13" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                                                <path strokeLinecap="round" strokeLinejoin="round" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                                            </svg>
                                        </button>
                                    </div>
                                </div>
                                <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginTop: '6px' }}>
                                    {note.updatedAt ? `Editado ${fmt(note.updatedAt)}` : fmt(note.createdAt)}
                                </div>
                            </>
                        )}
                    </div>
                ))
            )}

            {/* Add new note */}
            <div style={{ borderTop: '1px solid var(--border)', paddingTop: '12px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
                <input
                    className="erp-input"
                    placeholder="Título (opcional)"
                    value={newTitle}
                    onChange={e => setNewTitle(e.target.value)}
                    style={{ marginBottom: 0 }}
                />
                <textarea
                    className="erp-input"
                    placeholder="Contenido de la nota *"
                    value={newContent}
                    onChange={e => setNewContent(e.target.value)}
                    rows={3}
                    style={{ marginBottom: 0, resize: 'vertical' }}
                />
                <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                    <button
                        className="btn btn-primary"
                        style={{ fontSize: '12px' }}
                        onClick={handleAdd}
                        disabled={saving || !newContent.trim()}
                    >
                        {saving ? 'Guardando...' : '+ Añadir nota'}
                    </button>
                </div>
            </div>
        </div>
    );
}

'use client';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import PageContainer from '@/components/PageContainer';
import AccessibleModal from '@/components/AccessibleModal';
import FormLabel from '@/components/FormLabel';
import { parseListResponse } from '@/lib/parseListResponse';
import { leadFormSchema, type LeadFormValues } from '@/lib/schemas/leadSchema';
import { useCachedApi } from '@/hooks/useCachedApi';

const STAGES = [
    { key: 'New', label: 'Nuevo', color: '#6b7280', bg: '#f3f4f6' },
    { key: 'Contacted', label: 'Contactado', color: '#2563eb', bg: '#eff6ff' },
    { key: 'Qualified', label: 'Cualificado', color: '#8b5cf6', bg: '#f5f3ff' },
    { key: 'Negotiation', label: 'Negociación', color: '#f59e0b', bg: '#fffbeb' },
    { key: 'Won', label: 'Ganado', color: '#10b981', bg: '#ecfdf5' },
    { key: 'Lost', label: 'Perdido', color: '#ef4444', bg: '#fef2f2' },
];

interface Lead { id: string; name: string; email: string; phone: string; status: string; source: string; notes: string; createdAt: string; }

export default function LeadsClient({ initialLeads }: { initialLeads: Lead[] }) {
    const [leads, setLeads] = useState<Lead[]>(initialLeads);
    const [dragging, setDragging] = useState<Lead | null>(null);
    const [showModal, setShowModal] = useState(false);
    const [editing, setEditing] = useState<Lead | null>(null);
    const { register, handleSubmit, reset, formState: { errors } } = useForm<LeadFormValues>({
        resolver: zodResolver(leadFormSchema),
        defaultValues: { name: '', email: '', phone: '', status: 'New', source: '', notes: '' },
    });

    const { fetchCached, invalidateCached } = useCachedApi();

    const refresh = async () => {
        invalidateCached('leads');
        const data = await fetchCached<unknown>('leads?pageSize=500');
        if (data) setLeads(parseListResponse<Lead>(data));
    };
    // Datos iniciales vía RSC; refresh() solo tras mutaciones

    const moveLead = async (lead: Lead, newStatus: string) => {
        if (lead.status === newStatus) return;
        await fetch(`/api/proxy/leads/${lead.id}`, {
            method: 'PUT', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ ...lead, status: newStatus }),
        });
        refresh();
    };

    const handleDrop = async (e: React.DragEvent, status: string) => {
        e.preventDefault();
        if (dragging) { await moveLead(dragging, status); setDragging(null); }
    };

    const onSave = handleSubmit(async (data) => {
        const method = editing ? 'PUT' : 'POST';
        const url = editing ? `/api/proxy/leads/${editing.id}` : '/api/proxy/leads';
        await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
        setShowModal(false);
        setEditing(null);
        reset({ name: '', email: '', phone: '', status: 'New', source: '', notes: '' });
        refresh();
    });

    const openEdit = (lead: Lead) => {
        setEditing(lead);
        reset({ name: lead.name, email: lead.email, phone: lead.phone, status: lead.status, source: lead.source, notes: lead.notes });
        setShowModal(true);
    };
    const openNew = () => {
        setEditing(null);
        reset({ name: '', email: '', phone: '', status: 'New', source: '', notes: '' });
        setShowModal(true);
    };
    const deleteLead = async (id: string) => {
        if (!confirm('¿Eliminar este lead?')) return;
        await fetch(`/api/proxy/leads/${id}`, { method: 'DELETE' }); refresh();
    };

    const byStage = (key: string) => leads.filter(l => l.status === key);
    const won = leads.filter(l => l.status === 'Won').length;
    const total = leads.filter(l => l.status !== 'Lost').length;
    const convRate = total > 0 ? Math.round((won / total) * 100) : 0;

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Pipeline de Leads</h1>
                    <p className="page-subtitle">
                        {leads.length} leads · {won} ganados · {convRate}% conversión
                    </p>
                </div>
                <button className="btn btn-primary" onClick={openNew}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}><path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" /></svg>
                    Nuevo Lead
                </button>
            </div>

            {/* Kanban */}
            <div style={{ display: 'grid', gridTemplateColumns: `repeat(${STAGES.length}, 1fr)`, gap: '12px', overflowX: 'auto', minWidth: '900px' }}>
                {STAGES.map(stage => (
                    <div key={stage.key}
                        style={{ minHeight: '400px', borderRadius: '10px', background: stage.bg, border: `1px solid ${stage.color}30`, display: 'flex', flexDirection: 'column' }}
                        onDragOver={e => e.preventDefault()}
                        onDrop={e => handleDrop(e, stage.key)}>
                        {/* Column header */}
                        <div style={{ padding: '12px 14px', borderBottom: `2px solid ${stage.color}40`, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <div style={{ fontWeight: 700, fontSize: '12px', color: stage.color, textTransform: 'uppercase', letterSpacing: '0.05em' }}>{stage.label}</div>
                            <span style={{ background: stage.color, color: 'white', borderRadius: '99px', padding: '1px 8px', fontSize: '11px', fontWeight: 700 }}>
                                {byStage(stage.key).length}
                            </span>
                        </div>

                        {/* Cards */}
                        <div style={{ padding: '10px', display: 'flex', flexDirection: 'column', gap: '8px', flex: 1 }}>
                            {byStage(stage.key).map(lead => (
                                <div key={lead.id}
                                    draggable
                                    onDragStart={() => setDragging(lead)}
                                    onDragEnd={() => setDragging(null)}
                                    style={{
                                        background: 'white', borderRadius: '8px', padding: '12px 14px',
                                        boxShadow: '0 1px 4px rgba(0,0,0,0.06)', cursor: 'grab',
                                        border: `1px solid ${stage.color}20`,
                                        opacity: dragging?.id === lead.id ? 0.5 : 1, transition: 'opacity 0.15s',
                                    }}>
                                    <div style={{ fontWeight: 700, fontSize: '13px', marginBottom: '4px' }}>{lead.name}</div>
                                    {lead.email && <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '2px' }}>✉ {lead.email}</div>}
                                    {lead.phone && <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '6px' }}>📞 {lead.phone}</div>}
                                    {lead.source && (
                                        <span style={{ fontSize: '10px', background: `${stage.color}15`, color: stage.color, padding: '2px 8px', borderRadius: '99px', fontWeight: 600 }}>
                                            {lead.source}
                                        </span>
                                    )}
                                    <div style={{ display: 'flex', gap: '6px', marginTop: '10px' }}>
                                        <button className="btn btn-secondary btn-sm" style={{ fontSize: '11px', padding: '3px 8px' }} onClick={() => openEdit(lead)}>Editar</button>
                                        <button className="btn btn-sm" style={{ fontSize: '11px', padding: '3px 8px', background: 'var(--danger-bg)', color: 'var(--danger)' }} onClick={() => deleteLead(lead.id)}>✕</button>
                                        {/* Quick advance */}
                                        {stage.key !== 'Won' && stage.key !== 'Lost' && (
                                            <button className="btn btn-sm" style={{ fontSize: '11px', padding: '3px 8px', background: 'var(--success-bg)', color: 'var(--success)', marginLeft: 'auto' }}
                                                onClick={() => {
                                                    const idx = STAGES.findIndex(s => s.key === stage.key);
                                                    if (idx < STAGES.length - 1) moveLead(lead, STAGES[idx + 1].key);
                                                }}>→</button>
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                ))}
            </div>

            {/* Modal */}
            <AccessibleModal
                open={showModal}
                onClose={() => setShowModal(false)}
                title={`${editing ? 'Editar' : 'Nuevo'} Lead`}
                maxWidth="480px"
                footer={(
                    <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                        <button type="button" className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                        <button type="submit" form="lead-form" className="btn btn-primary">✓ Guardar</button>
                    </div>
                )}
            >
                        <form id="lead-form" onSubmit={onSave} style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <FormLabel htmlFor="lead-name" required>Nombre</FormLabel>
                                <input id="lead-name" className="erp-input" {...register('name')} placeholder="Empresa o persona" />
                                {errors.name && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.name.message}</p>}
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="lead-email">Email</FormLabel>
                                <input id="lead-email" className="erp-input" type="email" {...register('email')} />
                                {errors.email && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.email.message}</p>}
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="lead-phone">Teléfono</FormLabel>
                                <input id="lead-phone" className="erp-input" {...register('phone')} />
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="lead-status">Estado</FormLabel>
                                <select id="lead-status" className="erp-input" {...register('status')}>
                                    {STAGES.map(s => <option key={s.key} value={s.key}>{s.label}</option>)}
                                </select>
                            </div>
                            <div className="form-group">
                                <FormLabel htmlFor="lead-source">Canal / origen</FormLabel>
                                <select id="lead-source" className="erp-input" {...register('source')}>
                                    <option value="">Desconocido</option>
                                    <option value="Web">Página Web</option>
                                    <option value="Referral">Referido</option>
                                    <option value="Event">Evento</option>
                                    <option value="Cold Call">Puerta Fría</option>
                                </select>
                            </div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <FormLabel htmlFor="lead-notes">Notas</FormLabel>
                                <textarea id="lead-notes" className="erp-input" rows={3} {...register('notes')} />
                            </div>
                        </form>
            </AccessibleModal>
        </PageContainer>
    );
}

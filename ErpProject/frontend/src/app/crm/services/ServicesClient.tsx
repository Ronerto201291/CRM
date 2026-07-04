'use client';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import PageListLayout from '@/components/PageListLayout';
import EmptyState from '@/components/EmptyState';
import FormLabel from '@/components/FormLabel';
import AccessibleModal from '@/components/AccessibleModal';
import { serviceCatalogFormSchema, type ServiceCatalogFormValues } from '@/lib/schemas/serviceCatalogSchema';
import type { ServiceCatalogItem } from './page';

const periodicityLabel: Record<string, string> = {
    Monthly: 'Mensual',
    Quarterly: 'Trimestral',
    Yearly: 'Anual',
};

interface ServicesClientProps {
    initialServices: ServiceCatalogItem[];
}

export default function ServicesClient({ initialServices }: ServicesClientProps) {
    const [services, setServices] = useState<ServiceCatalogItem[]>(initialServices);
    const [modalOpen, setModalOpen] = useState(false);
    const [editing, setEditing] = useState<ServiceCatalogItem | null>(null);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const { register, handleSubmit, reset, formState: { errors } } = useForm<ServiceCatalogFormValues>({
        resolver: zodResolver(serviceCatalogFormSchema),
        defaultValues: { name: '', description: '', defaultPrice: 0, defaultTaxRate: 21, defaultPeriodicity: 'Monthly' },
    });

    const reload = async () => {
        const res = await fetch('/api/proxy/service-catalog?includeInactive=true');
        if (res.ok) setServices(await res.json());
    };

    const openCreate = () => {
        setEditing(null);
        reset({ name: '', description: '', defaultPrice: 0, defaultTaxRate: 21, defaultPeriodicity: 'Monthly' });
        setModalOpen(true);
    };

    const openEdit = (item: ServiceCatalogItem) => {
        setEditing(item);
        reset({
            name: item.name,
            description: item.description ?? '',
            defaultPrice: item.defaultPrice,
            defaultTaxRate: item.defaultTaxRate,
            defaultPeriodicity: item.defaultPeriodicity,
        });
        setModalOpen(true);
    };

    const onSubmit = async (data: ServiceCatalogFormValues) => {
        setMessage(null);
        try {
            const url = editing ? `/api/proxy/service-catalog/${editing.id}` : '/api/proxy/service-catalog';
            const method = editing ? 'PUT' : 'POST';
            const body = editing ? { ...data, isActive: editing.isActive } : data;
            const res = await fetch(url, { method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
            if (res.ok) {
                setModalOpen(false);
                await reload();
            } else {
                setMessage({ type: 'error', text: 'Error al guardar el servicio.' });
            }
        } catch {
            setMessage({ type: 'error', text: 'Error de conexión.' });
        }
    };

    const toggleActive = async (item: ServiceCatalogItem) => {
        const res = await fetch(`/api/proxy/service-catalog/${item.id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                name: item.name,
                description: item.description,
                defaultPrice: item.defaultPrice,
                defaultTaxRate: item.defaultTaxRate,
                defaultPeriodicity: item.defaultPeriodicity,
                isActive: !item.isActive,
            }),
        });
        if (res.ok) await reload();
    };

    return (
        <PageListLayout
            title="Catálogo de servicios"
            subtitle="Servicios que ofrece tu empresa a clientes (mantenimientos, cuotas recurrentes, etc.)"
        >
            <div style={{ marginBottom: '16px', display: 'flex', justifyContent: 'flex-end' }}>
                <button
                    onClick={openCreate}
                    style={{
                        padding: '10px 16px', borderRadius: '6px',
                        background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                        color: 'white', border: 'none', fontSize: '13px', fontWeight: 600, cursor: 'pointer',
                    }}
                >
                    Nuevo servicio
                </button>
            </div>

            {services.length === 0 ? (
                <EmptyState icon="🛠️" title="Sin servicios" description="Todavía no se ha dado de alta ningún servicio en el catálogo." />
            ) : (
                <table className="erp-table">
                    <thead>
                        <tr>
                            <th>Nombre</th>
                            <th>Precio</th>
                            <th>IVA</th>
                            <th>Periodicidad</th>
                            <th>Estado</th>
                            <th>Acciones</th>
                        </tr>
                    </thead>
                    <tbody>
                        {services.map(item => (
                            <tr key={item.id}>
                                <td>{item.name}</td>
                                <td>{item.defaultPrice.toFixed(2)} €</td>
                                <td>{item.defaultTaxRate}%</td>
                                <td>{periodicityLabel[item.defaultPeriodicity]}</td>
                                <td>{item.isActive ? 'Activo' : 'Inactivo'}</td>
                                <td>
                                    <button
                                        onClick={() => openEdit(item)}
                                        style={{ background: 'none', border: 'none', color: '#2563eb', cursor: 'pointer', padding: 0, fontSize: '13px' }}
                                    >
                                        Editar
                                    </button>
                                    {' · '}
                                    <button
                                        onClick={() => toggleActive(item)}
                                        style={{ background: 'none', border: 'none', color: item.isActive ? 'var(--danger)' : 'var(--success)', cursor: 'pointer', padding: 0, fontSize: '13px' }}
                                    >
                                        {item.isActive ? 'Desactivar' : 'Activar'}
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            )}

            <AccessibleModal
                open={modalOpen}
                onClose={() => setModalOpen(false)}
                title={editing ? 'Editar servicio' : 'Nuevo servicio'}
                footer={
                    <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '20px' }}>
                        <button type="button" onClick={() => setModalOpen(false)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', fontSize: '13px' }}>
                            Cancelar
                        </button>
                        <button
                            type="submit"
                            form="service-catalog-form"
                            style={{
                                padding: '10px 16px', borderRadius: '6px',
                                background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                                color: 'white', border: 'none', fontSize: '13px', fontWeight: 600, cursor: 'pointer',
                            }}
                        >
                            Guardar
                        </button>
                    </div>
                }
            >
                <form id="service-catalog-form" onSubmit={handleSubmit(onSubmit)}>
                    <FormLabel htmlFor="svc-name" required>Nombre</FormLabel>
                    <input id="svc-name" className="erp-input" {...register('name')} style={{ marginBottom: '4px' }} />
                    {errors.name && <p style={{ color: 'var(--danger)', fontSize: 12, marginBottom: '8px' }}>{errors.name.message}</p>}

                    <FormLabel htmlFor="svc-description">Descripción</FormLabel>
                    <input id="svc-description" className="erp-input" {...register('description')} style={{ marginBottom: '12px' }} />

                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                        <div>
                            <FormLabel htmlFor="svc-price" required>Precio</FormLabel>
                            <input id="svc-price" type="number" step="0.01" className="erp-input" {...register('defaultPrice')} />
                            {errors.defaultPrice && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.defaultPrice.message}</p>}
                        </div>
                        <div>
                            <FormLabel htmlFor="svc-tax" required>IVA (%)</FormLabel>
                            <input id="svc-tax" type="number" step="0.01" className="erp-input" {...register('defaultTaxRate')} />
                            {errors.defaultTaxRate && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.defaultTaxRate.message}</p>}
                        </div>
                    </div>

                    <FormLabel htmlFor="svc-periodicity" required>Periodicidad</FormLabel>
                    <select id="svc-periodicity" className="erp-input" {...register('defaultPeriodicity')}>
                        <option value="Monthly">Mensual</option>
                        <option value="Quarterly">Trimestral</option>
                        <option value="Yearly">Anual</option>
                    </select>
                </form>
            </AccessibleModal>

            {message && (
                <p style={{ color: message.type === 'error' ? 'var(--danger)' : 'var(--success)', marginTop: '8px' }}>
                    {message.text}
                </p>
            )}
        </PageListLayout>
    );
}

'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import PageContainer from '@/components/PageContainer';
import FormLabel from '@/components/FormLabel';
import AccessibleModal from '@/components/AccessibleModal';
import { clientFormSchema, type ClientFormValues } from '@/lib/schemas/clientSchema';
import { clientContractedServiceFormSchema, type ClientContractedServiceFormValues } from '@/lib/schemas/clientContractedServiceSchema';
import type { ClientContractedServiceItem } from './page';
import type { ServiceCatalogItem } from '../../services/page';

export interface Client {
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

const periodicityLabel: Record<string, string> = {
    Monthly: 'Mensual',
    Quarterly: 'Trimestral',
    Yearly: 'Anual',
};

export default function ClientEditorClient({
    clientId,
    initialClient,
    initialContractedServices,
    serviceCatalog,
}: {
    clientId: string;
    initialClient: Client | null;
    initialContractedServices: ClientContractedServiceItem[];
    serviceCatalog: ServiceCatalogItem[];
}) {
    const router = useRouter();
    const isNew = clientId === 'new';
    const [saving, setSaving] = useState(false);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
    const [contractedServices, setContractedServices] = useState<ClientContractedServiceItem[]>(initialContractedServices);
    const [serviceModalOpen, setServiceModalOpen] = useState(false);
    const [serviceMessage, setServiceMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const {
        register: registerService,
        handleSubmit: handleServiceSubmit,
        reset: resetServiceForm,
        formState: { errors: serviceErrors },
    } = useForm<ClientContractedServiceFormValues>({
        resolver: zodResolver(clientContractedServiceFormSchema),
        defaultValues: { serviceCatalogItemId: '', startDate: new Date().toISOString().slice(0, 10) },
    });

    const { register, handleSubmit, formState: { errors } } = useForm<ClientFormValues>({
        resolver: zodResolver(clientFormSchema),
        defaultValues: {
            name: initialClient?.name ?? '',
            taxId: initialClient?.taxId ?? '',
            email: initialClient?.email ?? '',
            phone: initialClient?.phone ?? '',
            address: initialClient?.address ?? '',
            city: initialClient?.city ?? '',
            zipCode: initialClient?.zipCode ?? '',
            country: initialClient?.country ?? 'ES',
        },
    });

    const onSubmit = async (data: ClientFormValues) => {
        setSaving(true);
        setMessage(null);
        try {
            const method = initialClient ? 'PATCH' : 'POST';
            const url = initialClient ? `/api/proxy/clients/${initialClient.id}` : '/api/proxy/clients';
            const response = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data),
            });
            if (response.ok) {
                setMessage({ type: 'success', text: initialClient ? 'Cliente actualizado' : 'Cliente creado' });
                setTimeout(() => router.push('/crm/clients'), 1500);
            } else {
                setMessage({ type: 'error', text: 'Error guardando cliente' });
            }
        } catch {
            setMessage({ type: 'error', text: 'Error en la solicitud' });
        } finally {
            setSaving(false);
        }
    };

    const reloadContractedServices = async () => {
        const res = await fetch(`/api/proxy/clients/${clientId}/contracted-services`);
        if (res.ok) setContractedServices(await res.json());
    };

    const onAddService = async (data: ClientContractedServiceFormValues) => {
        setServiceMessage(null);
        try {
            const res = await fetch(`/api/proxy/clients/${clientId}/contracted-services`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    serviceCatalogItemId: data.serviceCatalogItemId,
                    priceOverride: data.priceOverride,
                    taxRateOverride: data.taxRateOverride,
                    periodicityOverride: data.periodicityOverride,
                    startDate: data.startDate,
                }),
            });
            if (res.ok) {
                setServiceModalOpen(false);
                resetServiceForm({ serviceCatalogItemId: '', startDate: new Date().toISOString().slice(0, 10) });
                await reloadContractedServices();
            } else {
                setServiceMessage({ type: 'error', text: 'Error al contratar el servicio.' });
            }
        } catch {
            setServiceMessage({ type: 'error', text: 'Error de conexión.' });
        }
    };

    const onCancelService = async (contractId: string) => {
        if (!confirm('¿Cancelar este servicio contratado?')) return;
        const res = await fetch(`/api/proxy/clients/${clientId}/contracted-services/${contractId}`, { method: 'DELETE' });
        if (res.ok) await reloadContractedServices();
    };

    return (
        <PageContainer>
            <div style={{ marginBottom: '28px' }}>
                <h1 style={{ fontSize: '22px', fontWeight: 800, color: 'var(--text-primary)', marginBottom: '4px' }}>
                    {initialClient ? 'Editar Cliente' : 'Nuevo Cliente'}
                </h1>
                <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>
                    {initialClient ? `${initialClient.name} (${initialClient.taxId})` : 'Crear un nuevo cliente'}
                </p>
            </div>

            {message && (
                <div style={{
                    marginBottom: '16px', padding: '12px 16px', borderRadius: '6px',
                    background: message.type === 'success' ? 'rgba(34,197,94,0.1)' : 'rgba(239,68,68,0.1)',
                    border: `1px solid ${message.type === 'success' ? '#22c55e' : '#ef4444'}`,
                    color: message.type === 'success' ? '#15803d' : '#991b1b',
                    fontSize: '13px',
                }}>
                    {message.text}
                </div>
            )}

            <form className="erp-card" style={{ padding: '20px' }} onSubmit={handleSubmit(onSubmit)}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                    <div>
                        <FormLabel htmlFor="client-name" required>Nombre</FormLabel>
                        <input id="client-name" className="erp-input" {...register('name')} placeholder="Ej: Empresa SL" />
                        {errors.name && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.name.message}</p>}
                    </div>
                    <div>
                        <FormLabel htmlFor="client-taxId" required>CIF/NIF</FormLabel>
                        <input id="client-taxId" className="erp-input" {...register('taxId')} placeholder="Ej: A12345678" />
                        {errors.taxId && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.taxId.message}</p>}
                    </div>
                    <div>
                        <FormLabel htmlFor="client-email">Email</FormLabel>
                        <input id="client-email" type="email" className="erp-input" {...register('email')} />
                        {errors.email && <p style={{ color: 'var(--danger)', fontSize: 12 }}>{errors.email.message}</p>}
                    </div>
                    <div>
                        <FormLabel htmlFor="client-phone">Teléfono</FormLabel>
                        <input id="client-phone" type="tel" className="erp-input" {...register('phone')} />
                    </div>
                    <div style={{ gridColumn: '1 / -1' }}>
                        <FormLabel htmlFor="client-address">Dirección</FormLabel>
                        <input id="client-address" className="erp-input" {...register('address')} />
                    </div>
                    <div>
                        <FormLabel htmlFor="client-city">Ciudad</FormLabel>
                        <input id="client-city" className="erp-input" {...register('city')} />
                    </div>
                    <div>
                        <FormLabel htmlFor="client-zipCode">Código postal</FormLabel>
                        <input id="client-zipCode" className="erp-input" {...register('zipCode')} />
                    </div>
                    <div>
                        <FormLabel htmlFor="client-country">País</FormLabel>
                        <input id="client-country" className="erp-input" {...register('country')} />
                    </div>
                </div>
                <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '20px', paddingTop: '20px', borderTop: '1px solid var(--border)' }}>
                    <button type="button" className="btn btn-secondary" onClick={() => router.push('/crm/clients')}>Cancelar</button>
                    <button type="submit" className="btn btn-primary" disabled={saving}>
                        {saving ? 'Guardando...' : 'Guardar Cliente'}
                    </button>
                </div>
            </form>

            {!isNew && (
                <div className="erp-card" style={{ padding: '20px', marginTop: '20px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                        <h2 style={{ fontSize: '16px', fontWeight: 700 }}>Servicios contratados</h2>
                        <button
                            onClick={() => setServiceModalOpen(true)}
                            style={{
                                padding: '8px 14px', borderRadius: '6px',
                                background: 'linear-gradient(135deg, #2563eb, #1e40af)',
                                color: 'white', border: 'none', fontSize: '13px', fontWeight: 600, cursor: 'pointer',
                            }}
                        >
                            Añadir servicio
                        </button>
                    </div>

                    {contractedServices.length === 0 ? (
                        <p style={{ fontSize: '13px', color: 'var(--text-muted)' }}>Este cliente no tiene servicios contratados.</p>
                    ) : (
                        <table className="erp-table">
                            <thead>
                                <tr>
                                    <th>Servicio</th>
                                    <th>Precio</th>
                                    <th>Periodicidad</th>
                                    <th>Próxima factura</th>
                                    <th>Estado</th>
                                    <th>Acciones</th>
                                </tr>
                            </thead>
                            <tbody>
                                {contractedServices.map(cs => (
                                    <tr key={cs.id}>
                                        <td>{cs.serviceName}</td>
                                        <td>{cs.price.toFixed(2)} €</td>
                                        <td>{periodicityLabel[cs.periodicity]}</td>
                                        <td>{new Date(cs.nextBillingDate).toLocaleDateString('es-ES')}</td>
                                        <td>{cs.status === 'Active' ? 'Activo' : 'Cancelado'}</td>
                                        <td>
                                            {cs.status === 'Active' && (
                                                <button
                                                    onClick={() => onCancelService(cs.id)}
                                                    style={{ background: 'none', border: 'none', color: 'var(--danger)', cursor: 'pointer', padding: 0, fontSize: '13px' }}
                                                >
                                                    Cancelar
                                                </button>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}

                    {serviceMessage && (
                        <p style={{ color: serviceMessage.type === 'error' ? 'var(--danger)' : 'var(--success)', marginTop: '8px', fontSize: '13px' }}>
                            {serviceMessage.text}
                        </p>
                    )}

                    <AccessibleModal
                        open={serviceModalOpen}
                        onClose={() => setServiceModalOpen(false)}
                        title="Añadir servicio contratado"
                        footer={
                            <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '20px' }}>
                                <button type="button" onClick={() => setServiceModalOpen(false)} style={{ background: 'none', border: 'none', color: 'var(--text-muted)', cursor: 'pointer', fontSize: '13px' }}>
                                    Cancelar
                                </button>
                                <button
                                    type="submit"
                                    form="contracted-service-form"
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
                        <form id="contracted-service-form" onSubmit={handleServiceSubmit(onAddService)}>
                            <FormLabel htmlFor="cs-catalog" required>Servicio del catálogo</FormLabel>
                            <select id="cs-catalog" className="erp-input" {...registerService('serviceCatalogItemId')} style={{ marginBottom: '4px' }}>
                                <option value="">Selecciona un servicio…</option>
                                {serviceCatalog.filter(s => s.isActive).map(s => (
                                    <option key={s.id} value={s.id}>{s.name} ({s.defaultPrice.toFixed(2)} €)</option>
                                ))}
                            </select>
                            {serviceErrors.serviceCatalogItemId && <p style={{ color: 'var(--danger)', fontSize: 12, marginBottom: '8px' }}>{serviceErrors.serviceCatalogItemId.message}</p>}

                            <FormLabel htmlFor="cs-start-date" required>Fecha de inicio</FormLabel>
                            <input id="cs-start-date" type="date" className="erp-input" {...registerService('startDate')} style={{ marginBottom: '4px' }} />
                            {serviceErrors.startDate && <p style={{ color: 'var(--danger)', fontSize: 12, marginBottom: '8px' }}>{serviceErrors.startDate.message}</p>}

                            <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginTop: '8px' }}>
                                El precio, IVA y periodicidad se toman del catálogo salvo que se negocie un precio distinto con el cliente.
                            </p>
                        </form>
                    </AccessibleModal>
                </div>
            )}
        </PageContainer>
    );
}

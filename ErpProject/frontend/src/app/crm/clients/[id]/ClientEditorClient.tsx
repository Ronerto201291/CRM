'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import PageContainer from '@/components/PageContainer';
import FormLabel from '@/components/FormLabel';
import { clientFormSchema, type ClientFormValues } from '@/lib/schemas/clientSchema';

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

export default function ClientEditorClient({
    clientId,
    initialClient,
}: {
    clientId: string;
    initialClient: Client | null;
}) {
    const router = useRouter();
    const isNew = clientId === 'new';
    const [saving, setSaving] = useState(false);
    const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

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
        </PageContainer>
    );
}

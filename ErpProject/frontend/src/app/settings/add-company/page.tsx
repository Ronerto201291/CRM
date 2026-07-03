'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import PageContainer from '@/components/PageContainer';
import FormLabel from '@/components/FormLabel';
import { addCompanySchema, type AddCompanyFormValues } from '@/lib/schemas/addCompanySchema';
import { useTenant } from '@/context/TenantContext';

export default function AddCompanyPage() {
    const router = useRouter();
    const { setTenant } = useTenant();
    const [error, setError] = useState<string | null>(null);
    const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<AddCompanyFormValues>({
        resolver: zodResolver(addCompanySchema),
        defaultValues: { companyName: '', companyTaxId: '', companyAddress: '' },
    });

    const onSubmit = async (data: AddCompanyFormValues) => {
        setError(null);
        const res = await fetch('/api/proxy/auth/add-company', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        const body = await res.json().catch(() => ({}));
        if (!res.ok) {
            setError(body.error || 'No se pudo crear la empresa');
            return;
        }
        document.cookie = `erp_token=${body.token}; path=/; max-age=${60 * 60 * 8}; SameSite=Lax`;
        document.cookie = `tenantId=${body.companyId}; path=/; max-age=${60 * 60 * 8}; SameSite=Lax`;
        document.cookie = `tenantName=${encodeURIComponent(body.companyName)}; path=/; max-age=${60 * 60 * 8}; SameSite=Lax`;
        setTenant(body.companyId, body.companyName);
        router.push('/dashboard');
        router.refresh();
    };

    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Añadir empresa</h1>
                    <p className="page-subtitle">Vincula otra empresa a tu cuenta (multi-empresa #42a)</p>
                </div>
            </div>

            <div className="erp-card" style={{ maxWidth: 480, padding: 24 }}>
                <form onSubmit={handleSubmit(onSubmit)}>
                    <div className="form-group" style={{ marginBottom: 16 }}>
                        <FormLabel htmlFor="companyName" required>Nombre empresa</FormLabel>
                        <input id="companyName" className="erp-input" {...register('companyName')} />
                        {errors.companyName && <p style={{ color: 'var(--danger)', fontSize: 12, marginTop: 4 }}>{errors.companyName.message}</p>}
                    </div>
                    <div className="form-group" style={{ marginBottom: 16 }}>
                        <FormLabel htmlFor="companyTaxId" required>CIF/NIF</FormLabel>
                        <input id="companyTaxId" className="erp-input" {...register('companyTaxId')} />
                        {errors.companyTaxId && <p style={{ color: 'var(--danger)', fontSize: 12, marginTop: 4 }}>{errors.companyTaxId.message}</p>}
                    </div>
                    <div className="form-group" style={{ marginBottom: 20 }}>
                        <FormLabel htmlFor="companyAddress">Dirección</FormLabel>
                        <input id="companyAddress" className="erp-input" {...register('companyAddress')} />
                    </div>
                    {error && <p role="alert" style={{ color: 'var(--danger)', fontSize: 13, marginBottom: 12 }}>{error}</p>}
                    <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
                        {isSubmitting ? 'Creando...' : 'Crear y cambiar a esta empresa'}
                    </button>
                </form>
            </div>
        </PageContainer>
    );
}

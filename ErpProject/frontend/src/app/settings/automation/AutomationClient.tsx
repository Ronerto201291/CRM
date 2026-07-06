'use client';
import { useState } from 'react';
import PageContainer from '@/components/PageContainer';
import FormLabel from '@/components/FormLabel';
import { automationRuleSchema } from '@/lib/schemas/automationRuleSchema';

interface Rule {
    id: string;
    name: string;
    description: string;
    triggerEvent: string;
    isActive: boolean;
    conditionsSummary: string;
    actionsSummary: string;
    createdAt: string;
}

const SYSTEM_RULES = [
    { name: 'Facturas vencidas', trigger: 'Diario 9:00', conditions: 'DueDate pasada, no pagada', actions: 'Email recordatorio de cobro', isActive: true },
    { name: 'Stock bajo reorden', trigger: 'Diario 9:00', conditions: 'Stock ≤ punto de reorden', actions: 'Email alerta de reposición', isActive: true },
];

const TRIGGER_OPTIONS = ['OnInvoiceCreated', 'OnLeadStatusChanged', 'OnExpenseApproved', 'OnStockBelowReorder'];
const ACTION_OPTIONS = ['SendEmail', 'CreateTask', 'NotifyAdmin'];

interface AutomationClientProps {
    initialRules: Rule[];
}

export default function AutomationClient({ initialRules }: AutomationClientProps) {
    const [rules, setRules] = useState<Rule[]>(initialRules);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [showForm, setShowForm] = useState(false);
    const [saving, setSaving] = useState(false);
    const [form, setForm] = useState({
        name: '',
        description: '',
        triggerEvent: TRIGGER_OPTIONS[0],
        conditionField: 'Total',
        conditionOperator: '>',
        conditionValue: '1000',
        actionType: ACTION_OPTIONS[0],
    });
    const [formError, setFormError] = useState('');

    const load = async () => {
        setLoading(true);
        setError('');
        try {
            const res = await fetch('/api/proxy/automation/rules');
            if (!res.ok) {
                const data = await res.json().catch(() => ({}));
                throw new Error(data.detail || data.error || 'Error al cargar reglas');
            }
            setRules(await res.json());
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Error al cargar reglas');
        } finally {
            setLoading(false);
        }
    };

    const createRule = async () => {
        setFormError('');
        const parsed = automationRuleSchema.safeParse(form);
        if (!parsed.success) {
            setFormError(parsed.error.issues[0]?.message ?? 'Revisa el formulario');
            return;
        }
        setSaving(true);
        try {
            const res = await fetch('/api/proxy/automation/rules', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    name: form.name,
                    description: form.description,
                    triggerEvent: form.triggerEvent,
                    isActive: true,
                    conditionField: form.conditionField,
                    conditionOperator: form.conditionOperator,
                    conditionValue: form.conditionValue,
                    actionType: form.actionType,
                }),
            });
            if (!res.ok) {
                const data = await res.json().catch(() => ({}));
                throw new Error(data.detail || data.error || 'Error al crear regla');
            }
            setShowForm(false);
            setForm({ name: '', description: '', triggerEvent: TRIGGER_OPTIONS[0], conditionField: 'Total', conditionOperator: '>', conditionValue: '1000', actionType: ACTION_OPTIONS[0] });
            await load();
        } catch (e) {
            setFormError(e instanceof Error ? e.message : 'Error al crear regla');
        } finally {
            setSaving(false);
        }
    };

    const toggleRule = async (rule: Rule) => {
        const res = await fetch(`/api/proxy/automation/rules/${rule.id}/toggle`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isActive: !rule.isActive }),
        });
        if (res.ok) load();
    };

    return (
        <PageContainer title="Motor de Automatización">
            <div className="space-y-8">
                <div className="flex justify-between items-center">
                    <p className="text-gray-500">Reglas del sistema (ejecutadas por Hangfire) y reglas personalizadas por empresa.</p>
                    <button
                        type="button"
                        onClick={() => setShowForm(!showForm)}
                        className="btn-primary"
                    >
                        {showForm ? 'Cancelar' : 'Crear Regla'}
                    </button>
                </div>

                {error && <div className="erp-card p-4 text-red-600 border border-red-200">{error}</div>}

                {showForm && (
                    <div className="erp-card p-6 space-y-4">
                        <h2 className="text-lg font-semibold">Nueva regla</h2>
                        {formError && (
                            <div className="erp-card p-3 text-red-600 border border-red-200" style={{ fontSize: 13 }}>
                                {formError}
                            </div>
                        )}
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <label className="block">
                                <FormLabel htmlFor="rule-name" required>Nombre</FormLabel>
                                <input id="rule-name" className="erp-input w-full mt-1" value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} />
                            </label>
                            <label className="block">
                                <span className="text-sm text-gray-600">Evento disparador</span>
                                <select className="erp-input w-full mt-1" value={form.triggerEvent} onChange={e => setForm({ ...form, triggerEvent: e.target.value })}>
                                    {TRIGGER_OPTIONS.map(t => <option key={t} value={t}>{t}</option>)}
                                </select>
                            </label>
                            <label className="block md:col-span-2">
                                <span className="text-sm text-gray-600">Descripción</span>
                                <input className="erp-input w-full mt-1" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} />
                            </label>
                            <label className="block">
                                <span className="text-sm text-gray-600">Condición (campo)</span>
                                <input className="erp-input w-full mt-1" value={form.conditionField} onChange={e => setForm({ ...form, conditionField: e.target.value })} />
                            </label>
                            <label className="block">
                                <span className="text-sm text-gray-600">Operador / valor</span>
                                <div className="flex gap-2 mt-1">
                                    <input className="erp-input w-20" value={form.conditionOperator} onChange={e => setForm({ ...form, conditionOperator: e.target.value })} />
                                    <input className="erp-input flex-1" value={form.conditionValue} onChange={e => setForm({ ...form, conditionValue: e.target.value })} />
                                </div>
                            </label>
                            <label className="block">
                                <span className="text-sm text-gray-600">Acción</span>
                                <select className="erp-input w-full mt-1" value={form.actionType} onChange={e => setForm({ ...form, actionType: e.target.value })}>
                                    {ACTION_OPTIONS.map(a => <option key={a} value={a}>{a}</option>)}
                                </select>
                            </label>
                        </div>
                        <button type="button" onClick={createRule} disabled={saving} className="btn-primary">
                            {saving ? 'Guardando…' : 'Guardar regla'}
                        </button>
                    </div>
                )}

                <div className="erp-card overflow-hidden">
                    <h2 className="px-6 py-4 font-semibold border-b bg-gray-50">Reglas del sistema (fijas)</h2>
                    <table className="min-w-full divide-y divide-gray-200">
                        <thead className="bg-gray-50">
                            <tr>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Nombre</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Programación</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Condiciones</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Acciones</th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Estado</th>
                            </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-200">
                            {SYSTEM_RULES.map(r => (
                                <tr key={r.name}>
                                    <td className="px-6 py-4 text-sm font-medium">{r.name}</td>
                                    <td className="px-6 py-4 text-sm text-gray-500">{r.trigger}</td>
                                    <td className="px-6 py-4 text-sm text-gray-500">{r.conditions}</td>
                                    <td className="px-6 py-4 text-sm text-gray-500">{r.actions}</td>
                                    <td className="px-6 py-4">
                                        <span className="px-2 py-1 text-xs font-semibold rounded-full bg-green-100 text-green-800">Activo</span>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                <div className="erp-card overflow-hidden">
                    <h2 className="px-6 py-4 font-semibold border-b bg-gray-50">Reglas personalizadas</h2>
                    {loading ? (
                        <p className="p-6 text-gray-500">Cargando…</p>
                    ) : rules.length === 0 ? (
                        <p className="p-6 text-gray-500">No hay reglas personalizadas. Crea una con el botón superior.</p>
                    ) : (
                        <table className="min-w-full divide-y divide-gray-200">
                            <thead className="bg-gray-50">
                                <tr>
                                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Nombre</th>
                                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Trigger</th>
                                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Condiciones</th>
                                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Acciones</th>
                                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Estado</th>
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-200">
                                {rules.map(rule => (
                                    <tr key={rule.id}>
                                        <td className="px-6 py-4 text-sm font-medium">{rule.name}</td>
                                        <td className="px-6 py-4 text-sm text-gray-500">{rule.triggerEvent}</td>
                                        <td className="px-6 py-4 text-sm text-gray-500">{rule.conditionsSummary}</td>
                                        <td className="px-6 py-4 text-sm text-gray-500">{rule.actionsSummary}</td>
                                        <td className="px-6 py-4">
                                            <button
                                                type="button"
                                                onClick={() => toggleRule(rule)}
                                                className={`px-2 py-1 text-xs font-semibold rounded-full ${rule.isActive ? 'bg-green-100 text-green-800' : 'bg-gray-100 text-gray-600'}`}
                                            >
                                                {rule.isActive ? 'Activo' : 'Inactivo'}
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    )}
                </div>
            </div>
        </PageContainer>
    );
}

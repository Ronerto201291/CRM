'use client';
import { useEffect, useState } from 'react';

interface User {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    roleId: string | null;
    roleName: string | null;
    isActive: boolean;
    twoFactorEnabled: boolean;
    createdAt: string;
}

interface Role {
    id: string;
    name: string;
}

const ROLE_COLORS: Record<string, string> = {
    Admin: '#ef4444',
    Manager: '#f59e0b',
    Contable: '#3b82f6',
    Sales: '#10b981',
    Viewer: '#6b7280',
};

const roleColor = (name: string | null) => ROLE_COLORS[name ?? ''] ?? '#6b7280';

export default function UsersPage() {
    const [users, setUsers]       = useState<User[]>([]);
    const [roles, setRoles]       = useState<Role[]>([]);
    const [loading, setLoading]   = useState(true);
    const [showModal, setShowModal] = useState(false);
    const [form, setForm]         = useState({ firstName: '', lastName: '', email: '', roleId: '' });
    const [saving, setSaving]     = useState(false);
    const [tempPassword, setTempPassword] = useState<string | null>(null);
    const [message, setMessage]   = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const loadUsers = async () => {
        const res = await fetch('/api/proxy/users');
        if (res.ok) setUsers(await res.json());
        setLoading(false);
    };

    const loadRoles = async () => {
        const res = await fetch('/api/proxy/users/roles');
        if (res.ok) {
            const data: Role[] = await res.json();
            setRoles(data);
            if (data.length > 0) setForm(f => ({ ...f, roleId: data[data.length - 1].id })); // default Viewer/last
        }
    };

    useEffect(() => { loadUsers(); loadRoles(); }, []);

    const showMsg = (type: 'success' | 'error', text: string) => {
        setMessage({ type, text });
        setTimeout(() => setMessage(null), 4000);
    };

    const handleCreate = async () => {
        if (!form.firstName || !form.email) {
            showMsg('error', 'Nombre y email son obligatorios');
            return;
        }
        setSaving(true);
        const res = await fetch('/api/proxy/users', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                firstName: form.firstName,
                lastName: form.lastName || null,
                email: form.email,
                roleId: form.roleId || null,
            }),
        });
        setSaving(false);
        if (res.ok) {
            const data = await res.json();
            setTempPassword(data.tempPassword ?? null);
            setShowModal(false);
            setForm({ firstName: '', lastName: '', email: '', roleId: roles[roles.length - 1]?.id ?? '' });
            loadUsers();
            showMsg('success', 'Usuario creado correctamente');
        } else {
            const err = await res.json().catch(() => ({}));
            showMsg('error', err.error || err.message || 'Error al crear el usuario');
        }
    };

    const handleChangeRole = async (userId: string, roleId: string) => {
        const res = await fetch(`/api/proxy/users/${userId}/role`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ roleId }),
        });
        if (res.ok) { loadUsers(); showMsg('success', 'Rol actualizado'); }
        else showMsg('error', 'Error actualizando rol');
    };

    const handleToggleActive = async (userId: string, newStatus: boolean) => {
        const res = await fetch(`/api/proxy/users/${userId}/status`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ isActive: newStatus }),
        });
        if (res.ok) { loadUsers(); showMsg('success', newStatus ? 'Usuario activado' : 'Usuario desactivado'); }
    };

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
            <div className="page-header">
                <div>
                    <h1 className="page-title">Usuarios y Roles</h1>
                    <p className="page-subtitle">Gestión de acceso por rol para tu empresa</p>
                </div>
                <button className="btn btn-primary" onClick={() => { setTempPassword(null); setShowModal(true); }}>
                    <svg width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
                    </svg>
                    Nuevo Usuario
                </button>
            </div>

            {message && (
                <div style={{
                    marginBottom: '16px', padding: '10px 14px', borderRadius: '8px', fontSize: '13px',
                    background: message.type === 'success' ? 'var(--success-bg)' : 'var(--danger-bg)',
                    color: message.type === 'success' ? 'var(--success)' : 'var(--danger)',
                    border: `1px solid ${message.type === 'success' ? 'rgba(16,185,129,0.2)' : 'rgba(239,68,68,0.2)'}`,
                }}>
                    {message.type === 'success' ? '✓' : '⚠'} {message.text}
                </div>
            )}

            {/* Contraseña temporal tras crear usuario */}
            {tempPassword && (
                <div style={{
                    marginBottom: '16px', padding: '14px 18px', borderRadius: '8px', fontSize: '13px',
                    background: '#fffbeb', border: '1px solid #fde68a', color: '#92400e',
                }}>
                    <strong>Contraseña temporal generada:</strong>{' '}
                    <code style={{ background: '#fef3c7', padding: '2px 8px', borderRadius: '4px', fontWeight: 700, letterSpacing: '0.05em' }}>
                        {tempPassword}
                    </code>
                    <span style={{ marginLeft: '12px', fontSize: '12px' }}>
                        Compártela con el usuario — deberá cambiarla en su primer acceso.
                    </span>
                    <button onClick={() => setTempPassword(null)} style={{ float: 'right', background: 'none', border: 'none', cursor: 'pointer', color: '#92400e', fontWeight: 700 }}>✕</button>
                </div>
            )}

            {loading ? (
                <div className="erp-card" style={{ padding: '48px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '13px' }}>
                    Cargando usuarios...
                </div>
            ) : users.length === 0 ? (
                <div className="erp-card" style={{ padding: '48px', textAlign: 'center' }}>
                    <p style={{ color: 'var(--text-muted)', fontSize: '14px' }}>No hay usuarios registrados</p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'hidden' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Nombre</th>
                                <th>Email</th>
                                <th>Rol</th>
                                <th>Creado</th>
                                <th>2FA</th>
                                <th>Estado</th>
                            </tr>
                        </thead>
                        <tbody>
                            {users.map(u => (
                                <tr key={u.id}>
                                    <td style={{ fontWeight: 600 }}>
                                        {u.firstName} {u.lastName}
                                    </td>
                                    <td style={{ color: 'var(--text-muted)' }}>{u.email}</td>
                                    <td>
                                        <select
                                            value={u.roleId ?? ''}
                                            onChange={e => handleChangeRole(u.id, e.target.value)}
                                            style={{
                                                padding: '4px 10px', borderRadius: '99px', border: 'none',
                                                background: roleColor(u.roleName) + '20',
                                                color: roleColor(u.roleName), fontSize: '11px', fontWeight: 700,
                                                cursor: 'pointer', outline: 'none',
                                            }}
                                        >
                                            <option value="">Sin rol</option>
                                            {roles.map(r => (
                                                <option key={r.id} value={r.id}>{r.name}</option>
                                            ))}
                                        </select>
                                    </td>
                                    <td style={{ fontSize: '12px', color: 'var(--text-muted)' }}>
                                        {new Date(u.createdAt).toLocaleDateString('es-ES')}
                                    </td>
                                    <td>
                                        <span style={{
                                            fontSize: '11px', fontWeight: 600, padding: '2px 8px', borderRadius: '99px',
                                            background: u.twoFactorEnabled ? 'var(--success-bg)' : 'var(--bg-secondary)',
                                            color: u.twoFactorEnabled ? 'var(--success)' : 'var(--text-muted)',
                                        }}>
                                            {u.twoFactorEnabled ? '2FA Activo' : '2FA Off'}
                                        </span>
                                    </td>
                                    <td>
                                        <button
                                            onClick={() => handleToggleActive(u.id, !u.isActive)}
                                            style={{
                                                padding: '4px 12px', borderRadius: '99px', border: 'none',
                                                background: u.isActive ? 'var(--success-bg)' : 'var(--danger-bg)',
                                                color: u.isActive ? 'var(--success)' : 'var(--danger)',
                                                fontSize: '11px', fontWeight: 600, cursor: 'pointer',
                                            }}
                                        >
                                            {u.isActive ? 'Activo' : 'Inactivo'}
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Referencia de roles */}
            {roles.length > 0 && (
                <div style={{ marginTop: '28px' }}>
                    <h3 style={{ fontSize: '13px', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '12px' }}>
                        Roles disponibles
                    </h3>
                    <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
                        {roles.map(r => (
                            <div key={r.id} style={{
                                padding: '6px 14px', borderRadius: '99px', fontSize: '12px', fontWeight: 600,
                                background: roleColor(r.name) + '15', color: roleColor(r.name),
                                border: `1px solid ${roleColor(r.name)}30`,
                            }}>
                                {r.name}
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/* Modal nuevo usuario */}
            {showModal && (
                <div className="modal-overlay" onClick={e => { if (e.target === e.currentTarget) setShowModal(false); }}>
                    <div className="modal-box" style={{ maxWidth: '460px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                            <h2 style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text-primary)' }}>Nuevo Usuario</h2>
                            <button onClick={() => setShowModal(false)} style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}>✕</button>
                        </div>

                        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '20px' }}>
                            <div className="form-group">
                                <label className="erp-label">NOMBRE *</label>
                                <input className="erp-input" placeholder="Juan" value={form.firstName}
                                    onChange={e => setForm({ ...form, firstName: e.target.value })} />
                            </div>
                            <div className="form-group">
                                <label className="erp-label">APELLIDOS</label>
                                <input className="erp-input" placeholder="García" value={form.lastName}
                                    onChange={e => setForm({ ...form, lastName: e.target.value })} />
                            </div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">EMAIL *</label>
                                <input className="erp-input" type="email" placeholder="usuario@empresa.com" value={form.email}
                                    onChange={e => setForm({ ...form, email: e.target.value })} />
                            </div>
                            <div className="form-group" style={{ gridColumn: 'span 2' }}>
                                <label className="erp-label">ROL</label>
                                <select className="erp-input" value={form.roleId}
                                    onChange={e => setForm({ ...form, roleId: e.target.value })}>
                                    <option value="">Sin rol asignado</option>
                                    {roles.map(r => (
                                        <option key={r.id} value={r.id}>{r.name}</option>
                                    ))}
                                </select>
                            </div>
                        </div>

                        <p style={{ fontSize: '12px', color: 'var(--text-muted)', marginBottom: '16px' }}>
                            Se generará una contraseña temporal que deberás compartir con el usuario.
                        </p>

                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                            <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
                            <button className="btn btn-primary" onClick={handleCreate} disabled={saving}>
                                {saving ? 'Creando...' : '✓ Crear Usuario'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

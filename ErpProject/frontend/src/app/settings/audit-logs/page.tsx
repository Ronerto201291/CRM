'use client';
import { useEffect, useState } from 'react';

interface AuditLog {
    id: string;
    entityType: string;
    entityId: string;
    action: string; // Created, Updated, Deleted, Approved, Rejected
    userId: string;
    userName: string;
    changes: string;
    timestamp: string;
    ipAddress?: string;
}

export default function AuditLogsPage() {
    const [logs, setLogs] = useState<AuditLog[]>([]);
    const [loading, setLoading] = useState(true);
    const [filter, setFilter] = useState({
        action: 'all',
        entityType: 'all',
        dateFrom: new Date(new Date().setDate(new Date().getDate() - 30)).toISOString().split('T')[0],
        dateTo: new Date().toISOString().split('T')[0],
    });

    useEffect(() => {
        const loadLogs = async () => {
            try {
                const params = new URLSearchParams();
                if (filter.action !== 'all') params.append('action', filter.action);
                if (filter.entityType !== 'all') params.append('entityType', filter.entityType);
                params.append('dateFrom', filter.dateFrom);
                params.append('dateTo', filter.dateTo);

                const res = await fetch(`/api/proxy/audit-logs?${params.toString()}`);
                if (res.ok) setLogs(await res.json());
            } catch (err) {
                console.error('Error cargando logs:', err);
            } finally {
                setLoading(false);
            }
        };
        loadLogs();
    }, [filter]);

    const entityIcons: Record<string, string> = {
        Invoice: '??',
        Expense: '??',
        Client: '??',
        Supplier: '??',
        Product: '??',
        Account: '??',
        User: '?????',
    };

    return (
        <div style={{ padding: '28px 32px', fontFamily: 'Inter, sans-serif' }}>
            {/* Header */}
            <div className="page-header">
                <div>
                    <h1 className="page-title">Registro de Auditoría</h1>
                    <p className="page-subtitle">Historial completo de cambios en el sistema (Compliance)</p>
                </div>
            </div>

            {/* Filtros */}
            <div className="erp-card" style={{ padding: '16px 20px', marginBottom: '20px' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: '16px' }}>
                    <div>
                        <label className="erp-label">ACCIÓN</label>
                        <select className="erp-input" value={filter.action} onChange={(e) => setFilter({ ...filter, action: e.target.value })}>
                            <option value="all">Todas</option>
                            <option value="Created">Creado</option>
                            <option value="Updated">Actualizado</option>
                            <option value="Deleted">Eliminado</option>
                            <option value="Approved">Aprobado</option>
                            <option value="Rejected">Rechazado</option>
                        </select>
                    </div>
                    <div>
                        <label className="erp-label">TIPO DE ENTIDAD</label>
                        <select className="erp-input" value={filter.entityType} onChange={(e) => setFilter({ ...filter, entityType: e.target.value })}>
                            <option value="all">Todas</option>
                            <option value="Invoice">Factura</option>
                            <option value="Expense">Gasto</option>
                            <option value="Client">Cliente</option>
                            <option value="Supplier">Proveedor</option>
                            <option value="Product">Producto</option>
                            <option value="Account">Cuenta</option>
                            <option value="User">Usuario</option>
                        </select>
                    </div>
                    <div>
                        <label className="erp-label">DESDE</label>
                        <input className="erp-input" type="date" value={filter.dateFrom} onChange={(e) => setFilter({ ...filter, dateFrom: e.target.value })} />
                    </div>
                    <div>
                        <label className="erp-label">HASTA</label>
                        <input className="erp-input" type="date" value={filter.dateTo} onChange={(e) => setFilter({ ...filter, dateTo: e.target.value })} />
                    </div>
                </div>
            </div>

            {/* Tabla */}
            {loading ? (
                <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-muted)' }}>
                    <p>? Cargando auditoría...</p>
                </div>
            ) : logs.length === 0 ? (
                <div className="erp-card" style={{ padding: '40px', textAlign: 'center' }}>
                    <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
                        ?? No hay registros en el período seleccionado
                    </p>
                </div>
            ) : (
                <div className="erp-card" style={{ overflow: 'auto' }}>
                    <table className="erp-table">
                        <thead>
                            <tr>
                                <th>Fecha/Hora</th>
                                <th>Entidad</th>
                                <th>Acción</th>
                                <th>Usuario</th>
                                <th>Cambios</th>
                                <th>IP</th>
                            </tr>
                        </thead>
                        <tbody>
                            {logs.map((log, i) => (
                                <tr key={i}>
                                    <td style={{ fontSize: '11px', color: 'var(--text-muted)' }}>
                                        {new Date(log.timestamp).toLocaleString('es-ES')}
                                    </td>
                                    <td>{entityIcons[log.entityType] || '📋'} {log.entityType}</td>
                                    <td>
                                        <span className={`badge ${
                                            log.action === 'Created' ? 'badge-success' :
                                            log.action === 'Updated' ? 'badge-info' :
                                            log.action === 'Deleted' ? 'badge-danger' :
                                            log.action === 'Approved' ? 'badge-success' :
                                            log.action === 'Rejected' ? 'badge-danger' : 'badge-gray'
                                        }`}>
                                            {log.action}
                                        </span>
                                    </td>
                                    <td style={{ fontWeight: 600 }}>{log.userName}</td>
                                    <td style={{ color: 'var(--text-muted)', fontSize: '11px' }}>
                                        {log.changes ? log.changes.substring(0, 50) + '...' : '-'}
                                    </td>
                                    <td style={{ fontFamily: 'monospace', fontSize: '10px', color: 'var(--text-muted)' }}>
                                        {log.ipAddress || '-'}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}

            {/* Footer con stats */}
            {logs.length > 0 && (
                <div style={{ marginTop: '20px', display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px' }}>
                    <div className="erp-card" style={{ padding: '16px 20px', textAlign: 'center' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', fontWeight: 600 }}>TOTAL</div>
                        <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text-primary)' }}>{logs.length}</div>
                    </div>
                    <div className="erp-card" style={{ padding: '16px 20px', textAlign: 'center' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', fontWeight: 600 }}>CREADOS</div>
                        <div style={{ fontSize: '18px', fontWeight: 700, color: '#065f46' }}>{logs.filter(l => l.action === 'Created').length}</div>
                    </div>
                    <div className="erp-card" style={{ padding: '16px 20px', textAlign: 'center' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', fontWeight: 600 }}>ACTUALIZADOS</div>
                        <div style={{ fontSize: '18px', fontWeight: 700, color: '#1e40af' }}>{logs.filter(l => l.action === 'Updated').length}</div>
                    </div>
                    <div className="erp-card" style={{ padding: '16px 20px', textAlign: 'center' }}>
                        <div style={{ fontSize: '11px', color: 'var(--text-muted)', marginBottom: '4px', fontWeight: 600 }}>ELIMINADOS</div>
                        <div style={{ fontSize: '18px', fontWeight: 700, color: '#991b1b' }}>{logs.filter(l => l.action === 'Deleted').length}</div>
                    </div>
                </div>
            )}
        </div>
    );
}

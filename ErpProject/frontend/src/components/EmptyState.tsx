import { type ReactNode } from 'react';

interface EmptyStateProps {
    icon?: string;
    title: string;
    description?: string;
    children?: ReactNode;
}

/** Estado vacío reutilizable (ADR-0018 #37). */
export default function EmptyState({ icon = '📭', title, description, children }: EmptyStateProps) {
    return (
        <div className="empty-state" style={{ padding: '48px 24px', textAlign: 'center' }}>
            <div className="empty-state-icon" style={{ fontSize: '36px', marginBottom: '12px' }}>{icon}</div>
            <div className="empty-state-title" style={{ fontWeight: 700, fontSize: '15px', marginBottom: description ? '8px' : 0 }}>{title}</div>
            {description ? <p style={{ color: 'var(--text-muted)', fontSize: '13px', margin: '0 0 16px' }}>{description}</p> : null}
            {children}
        </div>
    );
}

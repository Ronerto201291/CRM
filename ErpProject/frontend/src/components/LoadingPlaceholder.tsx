interface LoadingPlaceholderProps {
    message?: string;
}

/** Indicador de carga estándar (ADR-0018 #37). */
export default function LoadingPlaceholder({ message = 'Cargando...' }: LoadingPlaceholderProps) {
    return (
        <div className="erp-card" style={{ padding: '48px', textAlign: 'center', color: 'var(--text-muted)', fontSize: '14px' }}>
            {message}
        </div>
    );
}

interface FormErrorBannerProps {
    message: string | null | undefined;
}

/** Banner de error de formulario reutilizable (ADR-0018 #37). */
export default function FormErrorBanner({ message }: FormErrorBannerProps) {
    if (!message) return null;
    return (
        <div
            role="alert"
            style={{
                background: 'var(--danger-bg)',
                border: '1px solid rgba(239,68,68,0.25)',
                borderRadius: '8px',
                padding: '10px 14px',
                marginBottom: '16px',
                fontSize: '13px',
                color: 'var(--danger)',
            }}
        >
            {message}
        </div>
    );
}

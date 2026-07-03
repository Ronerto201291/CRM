'use client';

import { useEffect, useId, useRef, type ReactNode } from 'react';

interface AccessibleModalProps {
    open: boolean;
    onClose: () => void;
    title: string;
    children: ReactNode;
    maxWidth?: string;
    footer?: ReactNode;
}

export default function AccessibleModal({
    open,
    onClose,
    title,
    children,
    maxWidth = '520px',
    footer,
}: AccessibleModalProps) {
    const titleId = useId();
    const dialogRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!open) return;
        const onKey = (e: KeyboardEvent) => {
            if (e.key === 'Escape') onClose();
        };
        document.addEventListener('keydown', onKey);
        dialogRef.current?.focus();
        return () => document.removeEventListener('keydown', onKey);
    }, [open, onClose]);

    if (!open) return null;

    return (
        <div
            className="modal-overlay"
            role="presentation"
            onClick={e => { if (e.target === e.currentTarget) onClose(); }}
        >
            <div
                ref={dialogRef}
                role="dialog"
                aria-modal="true"
                aria-labelledby={titleId}
                tabIndex={-1}
                className="modal-box"
                style={{ maxWidth }}
            >
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                    <h2 id={titleId} style={{ fontSize: '18px', fontWeight: 700 }}>{title}</h2>
                    <button
                        type="button"
                        onClick={onClose}
                        aria-label="Cerrar"
                        style={{ border: 'none', background: 'none', cursor: 'pointer', fontSize: '20px', color: 'var(--text-muted)' }}
                    >
                        ✕
                    </button>
                </div>
                {children}
                {footer}
            </div>
        </div>
    );
}

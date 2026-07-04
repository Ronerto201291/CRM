'use client';
import { useRef, useState } from 'react';
import { useParams } from 'next/navigation';

const PRIMARY = '#1B3A6B';
const PRIMARY_BG = '#EAF0FB';
const SUCCESS = '#15803d';
const DANGER = '#b91c1c';

export default function SupplierUploadPage() {
    const { token } = useParams<{ token: string }>();
    const fileInputRef = useRef<HTMLInputElement>(null);
    const [comment, setComment] = useState('');
    const [uploading, setUploading] = useState(false);
    const [result, setResult] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

    const handleUpload = async () => {
        const file = fileInputRef.current?.files?.[0];
        if (!file) {
            setResult({ type: 'error', text: 'Selecciona un archivo (PDF, JPG, PNG o WebP).' });
            return;
        }

        setUploading(true);
        setResult(null);
        try {
            const form = new FormData();
            form.append('file', file);
            if (comment) form.append('comment', comment);

            const res = await fetch(`/api/proxy/v1/public/supplier-uploads/${token}`, { method: 'POST', body: form });
            const data = await res.json().catch(() => ({}));
            if (res.ok) {
                setResult({ type: 'success', text: data.message || 'Factura recibida correctamente.' });
                setComment('');
                if (fileInputRef.current) fileInputRef.current.value = '';
            } else {
                setResult({ type: 'error', text: data.error || 'No se pudo subir la factura. Comprueba el enlace.' });
            }
        } catch {
            setResult({ type: 'error', text: 'Error de conexión. Inténtalo de nuevo.' });
        } finally {
            setUploading(false);
        }
    };

    return (
        <div style={{ minHeight: '100vh', background: '#f1f5f9', fontFamily: 'Inter, sans-serif', padding: '24px 16px', display: 'flex', alignItems: 'flex-start', justifyContent: 'center' }}>
            <div style={{ maxWidth: '520px', width: '100%', marginTop: '40px' }}>
                <div style={{ background: PRIMARY, borderRadius: '12px 12px 0 0', padding: '24px 28px' }}>
                    <div style={{ color: '#B0C4DE', fontSize: '11px', fontWeight: 700, letterSpacing: '0.1em', textTransform: 'uppercase', marginBottom: '6px' }}>PORTAL DE PROVEEDORES</div>
                    <div style={{ color: 'white', fontSize: '20px', fontWeight: 800 }}>Subir factura</div>
                </div>

                <div style={{ background: PRIMARY_BG, padding: '16px 28px', borderBottom: '1px solid #C8D4E0', fontSize: '13px', color: '#374151', lineHeight: 1.6 }}>
                    Sube aquí tu factura en PDF, JPG, PNG o WebP. Nuestro equipo de compras la revisará y la
                    gestionará manualmente — no necesitas hacer nada más.
                </div>

                <div style={{ background: 'white', padding: '24px 28px', borderRadius: '0 0 12px 12px' }}>
                    <label htmlFor="supplier-invoice-file" style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#1c2b3a', marginBottom: '6px' }}>
                        Archivo de factura
                    </label>
                    <input
                        id="supplier-invoice-file"
                        ref={fileInputRef}
                        type="file"
                        accept=".pdf,.jpg,.jpeg,.png,.webp"
                        style={{ display: 'block', width: '100%', marginBottom: '16px', fontSize: '13px' }}
                    />

                    <label htmlFor="supplier-invoice-comment" style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#1c2b3a', marginBottom: '6px' }}>
                        Comentario (opcional)
                    </label>
                    <textarea
                        id="supplier-invoice-comment"
                        value={comment}
                        onChange={e => setComment(e.target.value)}
                        placeholder="p. ej. Factura de marzo, pedido #1234"
                        rows={3}
                        style={{ width: '100%', padding: '10px 12px', borderRadius: '7px', border: '1px solid #e2e8f0', fontSize: '13px', fontFamily: 'Inter, sans-serif', resize: 'vertical', boxSizing: 'border-box', outline: 'none', marginBottom: '16px' }}
                    />

                    <button
                        onClick={handleUpload}
                        disabled={uploading}
                        style={{
                            width: '100%', padding: '12px 16px', borderRadius: '8px', border: 'none',
                            background: SUCCESS, color: 'white', fontWeight: 700, fontSize: '14px',
                            cursor: uploading ? 'default' : 'pointer', opacity: uploading ? 0.7 : 1,
                        }}
                    >
                        {uploading ? 'Subiendo…' : '📤 Subir factura'}
                    </button>

                    {result && (
                        <div style={{
                            marginTop: '16px', padding: '12px 14px', borderRadius: '8px', fontSize: '13px',
                            background: result.type === 'success' ? '#ecfdf5' : '#fef2f2',
                            color: result.type === 'success' ? '#065f46' : DANGER,
                            borderLeft: `4px solid ${result.type === 'success' ? SUCCESS : DANGER}`,
                        }}>
                            {result.type === 'success' ? '✓ ' : ''}{result.text}
                        </div>
                    )}
                </div>

                <div style={{ textAlign: 'center', padding: '16px', fontSize: '11px', color: '#94a3b8' }}>
                    Enlace de subida exclusivo para proveedores — no compartas este enlace.
                </div>
            </div>
        </div>
    );
}

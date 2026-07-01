'use client';
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Logo from '@/components/Logo';

export default function LoginPage() {
  const router = useRouter();
  const [form, setForm] = useState({ email: '', password: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const res = await fetch('/api/proxy/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(form),
      });
      if (res.ok) {
        router.push('/dashboard');
      } else {
        const data = await res.json().catch(() => ({}));
        setError(data.error || 'Credenciales incorrectas');
      }
    } catch {
      setError('Error de conexión con el servidor');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center',
      background: 'var(--bg)', fontFamily: 'Inter, sans-serif',
    }}>
      <div style={{ width: '100%', maxWidth: '400px', padding: '0 20px' }}>
        {/* Logo */}
        <div style={{ textAlign: 'center', marginBottom: '36px' }}>
          <div style={{ display: 'flex', justifyContent: 'center', marginBottom: '16px' }}>
             <Logo size={120} />
          </div>
          <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
            Accede a tu cuenta empresarial
          </p>
        </div>

        {/* Card */}
        <div className="erp-card" style={{ padding: '32px' }}>
          <form onSubmit={handleLogin}>
            <div className="form-group" style={{ marginBottom: '16px' }}>
              <label className="erp-label">EMAIL</label>
              <input
                className="erp-input"
                type="email"
                placeholder="admin@empresa.com"
                value={form.email}
                onChange={e => setForm({ ...form, email: e.target.value })}
                required
                autoFocus
              />
            </div>
            <div className="form-group" style={{ marginBottom: '24px' }}>
              <label className="erp-label">CONTRASEÑA</label>
              <input
                className="erp-input"
                type="password"
                placeholder="••••••••"
                value={form.password}
                onChange={e => setForm({ ...form, password: e.target.value })}
                required
              />
            </div>

            {error && (
              <div style={{
                background: 'var(--danger-bg)', border: '1px solid rgba(239,68,68,0.2)',
                borderRadius: '8px', padding: '10px 14px', marginBottom: '16px',
                fontSize: '13px', color: 'var(--danger)', display: 'flex', gap: '8px',
              }}>
                <span>⚠️</span> {error}
              </div>
            )}

            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading}
              style={{ width: '100%', padding: '12px', fontSize: '14px', fontWeight: 700 }}
            >
              {loading ? 'Iniciando sesión...' : 'Iniciar sesión →'}
            </button>
          </form>
        </div>

        <p style={{ textAlign: 'center', fontSize: '13px', color: 'var(--text-muted)', marginTop: '20px' }}>
          ¿No tienes cuenta?{' '}
          <a href="/signup" style={{ color: 'var(--brand-primary)', fontWeight: 600, textDecoration: 'none' }}>
            Crear empresa gratis
          </a>
        </p>
        <p style={{ textAlign: 'center', fontSize: '11px', color: 'var(--text-muted)', marginTop: '8px' }}>
          Orbital ERP · Gestión empresarial
        </p>
      </div>
    </div>
  );
}

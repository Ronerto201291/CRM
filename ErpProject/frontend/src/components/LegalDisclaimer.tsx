'use client';

export type LegalDisclaimerVariant = 'warning' | 'info' | 'danger';

const VARIANT_STYLES: Record<LegalDisclaimerVariant, { bg: string; border: string; color: string }> = {
    warning: { bg: 'var(--warning-bg)', border: 'rgba(245,158,11,0.35)', color: '#92400e' },
    info: { bg: 'var(--info-bg)', border: 'rgba(59,130,246,0.25)', color: 'var(--info)' },
    danger: { bg: 'var(--danger-bg)', border: 'rgba(239,68,68,0.3)', color: 'var(--danger)' },
};

export interface LegalDisclaimerProps {
    title: string;
    children: React.ReactNode;
    variant?: LegalDisclaimerVariant;
    /** data-testid for E2E / Vitest */
    testId?: string;
}

/** Aviso legal visible — no sustituye asesoría fiscal, laboral ni homologación oficial. */
export default function LegalDisclaimer({
    title,
    children,
    variant = 'warning',
    testId = 'legal-disclaimer',
}: LegalDisclaimerProps) {
    const s = VARIANT_STYLES[variant];
    return (
        <div
            role="note"
            aria-label={title}
            data-testid={testId}
            className="erp-card"
            style={{
                padding: '14px 18px',
                marginBottom: '16px',
                background: s.bg,
                border: `1px solid ${s.border}`,
                color: s.color,
                fontSize: '13px',
                lineHeight: 1.55,
            }}
        >
            <strong style={{ display: 'block', marginBottom: '6px', fontSize: '14px' }}>{title}</strong>
            {children}
        </div>
    );
}

export const FISCAL_EXPORT_DISCLAIMER_TEXT =
    'Los exportes generados por este ERP son orientativos y no tienen validez legal ni fiscal. ' +
    'No sustituyen la presentación oficial ante la AEAT, TGSS u otros organismos, ni la validación ' +
    'por un asesor fiscal, laboral o gestoría homologado. Revise siempre el formato y los importes ' +
    'antes de cualquier presentación o remisión.';

export const PAYROLL_MODULE_DISCLAIMER_TEXT =
    'Este módulo de nóminas es una herramienta de apoyo (Fase 2). No calcula convenios colectivos ' +
    'oficiales ni sustituye SILTRA/TGSS homologado, certificado digital de la Seguridad Social, ' +
    'asesoría laboral ni gestoría de nóminas. Los importes introducidos o calculados deben ser ' +
    'revisados por un profesional antes de cotizar, retener IRPF o presentar modelos 111/190/TC1/TC2.';

export const PAYROLL_EXPORT_DISCLAIMER_TEXT =
    'Exportes TC1, TC2, RED/SILTRA y XML orientativos. No sustituyen remisión oficial a la TGSS ' +
    'ni certificado digital. Valide NAF, CCC y bases con su gestoría antes de presentar.';

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import LegalDisclaimer, {
    FISCAL_EXPORT_DISCLAIMER_TEXT,
    PAYROLL_MODULE_DISCLAIMER_TEXT,
} from '@/components/LegalDisclaimer';

describe('LegalDisclaimer', () => {
    it('renderiza título y contenido con testId', () => {
        render(
            <LegalDisclaimer title="Aviso legal" testId="test-disclaimer">
                Texto de prueba
            </LegalDisclaimer>,
        );
        expect(screen.getByTestId('test-disclaimer')).toBeInTheDocument();
        expect(screen.getByText('Aviso legal')).toBeInTheDocument();
        expect(screen.getByText('Texto de prueba')).toBeInTheDocument();
    });

    it('exporta textos legales obligatorios', () => {
        expect(FISCAL_EXPORT_DISCLAIMER_TEXT).toMatch(/orientativos/i);
        expect(PAYROLL_MODULE_DISCLAIMER_TEXT).toMatch(/SILTRA|TGSS/i);
    });
});

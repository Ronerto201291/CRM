import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import FormErrorBanner from '../FormErrorBanner';

describe('FormErrorBanner', () => {
    it('no renderiza nada sin mensaje', () => {
        const { container } = render(<FormErrorBanner message={null} />);
        expect(container.firstChild).toBeNull();
    });

    it('muestra mensaje de error con role alert', () => {
        render(<FormErrorBanner message="Error de validación" />);
        expect(screen.getByRole('alert')).toHaveTextContent('Error de validación');
    });
});

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import LoadingPlaceholder from '../LoadingPlaceholder';

describe('LoadingPlaceholder', () => {
    it('muestra mensaje por defecto', () => {
        render(<LoadingPlaceholder />);
        expect(screen.getByText('Cargando...')).toBeInTheDocument();
    });

    it('acepta mensaje personalizado', () => {
        render(<LoadingPlaceholder message="Procesando..." />);
        expect(screen.getByText('Procesando...')).toBeInTheDocument();
    });
});

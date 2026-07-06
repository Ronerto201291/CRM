import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import EmptyState from '../EmptyState';

describe('EmptyState', () => {
    it('muestra título y descripción', () => {
        render(<EmptyState title="Sin datos" description="Añade el primero" />);
        expect(screen.getByText('Sin datos')).toBeInTheDocument();
        expect(screen.getByText('Añade el primero')).toBeInTheDocument();
    });

    it('usa icono por defecto', () => {
        render(<EmptyState title="Vacío" />);
        expect(screen.getByText('📭')).toBeInTheDocument();
    });
});

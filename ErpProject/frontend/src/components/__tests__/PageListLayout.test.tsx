import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import PageListLayout from '../PageListLayout';

describe('PageListLayout', () => {
    it('renderiza título y subtítulo', () => {
        render(
            <PageListLayout title="Clientes" subtitle="10 registros">
                <p>Contenido</p>
            </PageListLayout>
        );
        expect(screen.getByRole('heading', { name: 'Clientes' })).toBeInTheDocument();
        expect(screen.getByText('10 registros')).toBeInTheDocument();
        expect(screen.getByText('Contenido')).toBeInTheDocument();
    });

    it('renderiza acciones opcionales', () => {
        render(
            <PageListLayout title="Lista" actions={<button type="button">Nuevo</button>}>
                <span>Body</span>
            </PageListLayout>
        );
        expect(screen.getByRole('button', { name: 'Nuevo' })).toBeInTheDocument();
    });
});

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import ConsolidationClient from '@/app/treasury/consolidation/ConsolidationClient';

describe('ConsolidationClient', () => {
    it('renderiza módulo de consolidación', () => {
        render(
            <ConsolidationClient
                initialGroups={[{
                    id: 'g-1',
                    name: 'Grupo Holding',
                    currency: 'EUR',
                    totalAssets: 100000,
                    subsidiaries: [],
                }]}
            />,
        );
        expect(screen.getByRole('heading', { name: /consolidación de grupos/i })).toBeInTheDocument();
        expect(screen.getByText('Grupo Holding')).toBeInTheDocument();
    });
});

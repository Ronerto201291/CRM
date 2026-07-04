import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import AeatClient from '@/app/accounting/aeat/AeatClient';

describe('AeatClient', () => {
    it('renderiza sección de exportaciones AEAT', () => {
        render(<AeatClient />);

        expect(screen.getByRole('heading', { name: /modelos aeat/i })).toBeInTheDocument();
        expect(screen.getByText(/modelo 303/i)).toBeInTheDocument();
        expect(screen.getByRole('heading', { name: /validación vies/i })).toBeInTheDocument();
    });
});

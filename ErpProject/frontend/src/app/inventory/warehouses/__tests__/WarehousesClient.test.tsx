import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import WarehousesClient from '@/app/inventory/warehouses/WarehousesClient';

describe('WarehousesClient', () => {
    const initialWarehouses = [
        { id: 'wh-1', name: 'Almacén Central', location: 'Madrid', isActive: true },
    ];

    it('renderiza listado de almacenes', () => {
        render(<WarehousesClient initialWarehouses={initialWarehouses} />);
        expect(screen.getByRole('heading', { name: /almacenes/i })).toBeInTheDocument();
        expect(screen.getByText('Almacén Central')).toBeInTheDocument();
    });

    it('abre modal de nuevo almacén', () => {
        render(<WarehousesClient initialWarehouses={[]} />);
        fireEvent.click(screen.getByRole('button', { name: /nuevo almacén/i }));
        expect(screen.getByRole('dialog')).toBeInTheDocument();
    });
});

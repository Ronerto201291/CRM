import { z } from 'zod';

export const costCenterSchema = z.object({
    code: z.string().min(1, 'El código es obligatorio'),
    name: z.string().min(1, 'El nombre es obligatorio'),
    type: z.enum(['Department', 'Product', 'Project']),
});

export type CostCenterFormValues = z.infer<typeof costCenterSchema>;

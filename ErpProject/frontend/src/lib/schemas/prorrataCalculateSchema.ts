import { z } from 'zod';

export const prorrataCalculateSchema = z.object({
    fiscalYear: z.number().int().min(2000).max(2100),
    inlandRevenue: z.number().min(0),
    exemptRevenue: z.number().min(0),
    type: z.enum(['General', 'Special']),
});

export type ProrrataCalculateFormValues = z.infer<typeof prorrataCalculateSchema>;

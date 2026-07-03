import { z } from 'zod';

export const financingConfirmingSchema = z.object({
    bank: z.string().min(1, 'Banco obligatorio'),
    amount: z.string().min(1, 'Monto obligatorio'),
    fee: z.string().optional(),
    maturityDate: z.string().optional(),
});

export const financingFactoringSchema = z.object({
    clientName: z.string().min(1, 'Cliente obligatorio'),
    amount: z.string().min(1, 'Monto obligatorio'),
    fee: z.string().optional(),
});

export const financingCreditLineSchema = z.object({
    bank: z.string().min(1, 'Banco obligatorio'),
    limit: z.string().min(1, 'Límite obligatorio'),
    interestRate: z.string().optional(),
});

export const consolidationGroupSchema = z.object({
    name: z.string().min(1, 'Nombre del grupo es obligatorio'),
    currency: z.string().min(1),
});

export const consolidationSubsidiarySchema = z.object({
    name: z.string().min(1, 'Nombre de filial obligatorio'),
    participationPct: z.string().min(1, 'Participación obligatoria'),
    currency: z.string().min(1),
});

import { z } from 'zod';

export const automationRuleSchema = z.object({
    name: z.string().min(1, 'El nombre de la regla es obligatorio'),
    description: z.string().optional(),
    triggerEvent: z.string().min(1),
    conditionField: z.string().min(1),
    conditionOperator: z.string().min(1),
    conditionValue: z.string().min(1),
    actionType: z.string().min(1),
});

export type AutomationRuleForm = z.infer<typeof automationRuleSchema>;

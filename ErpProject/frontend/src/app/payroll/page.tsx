import { serverFetch } from '@/lib/serverFetch';
import PayrollClient from './PayrollClient';

interface Employee {
    id: string;
    taxId: string;
    fullName: string;
    socialSecurityNumber?: string;
    hireDate: string;
    contractType: string;
    weeklyHours: number;
}

interface Settlement {
    id: string;
    year: number;
    month: number;
    status: string;
    lineCount: number;
    totalGross: number;
    totalIrpf: number;
    totalEmployerSs: number;
}

interface Template {
    id: string;
    name: string;
    employeeSsRatePercent: number;
    employerSsRatePercent: number;
    defaultIrpfRatePercent: number;
    isDefault: boolean;
}

export default async function PayrollPage() {
    const year = new Date().getFullYear();
    const [employees, settlements, templates] = await Promise.all([
        serverFetch<Employee[]>('payroll/employees'),
        serverFetch<Settlement[]>(`payroll/settlements?year=${year}`),
        serverFetch<Template[]>('payroll/templates'),
    ]);
    return (
        <PayrollClient
            initialEmployees={Array.isArray(employees) ? employees : []}
            initialSettlements={Array.isArray(settlements) ? settlements : []}
            initialTemplates={Array.isArray(templates) ? templates : []}
            initialYear={year}
        />
    );
}

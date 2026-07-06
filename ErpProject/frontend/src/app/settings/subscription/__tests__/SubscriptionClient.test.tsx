import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import SubscriptionClient from '@/app/settings/subscription/SubscriptionClient';

vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }));

describe('SubscriptionClient', () => {
  const initialPlans = [
    {
      id: 'plan-1',
      name: 'Starter',
      monthlyPrice: 19,
      yearlyPrice: 190,
      maxUsers: 3,
      maxInvoicesPerMonth: 50,
    },
  ];

  const initialSubscription = {
    id: 'sub-1',
    planId: 'plan-1',
    planName: 'Starter',
    status: 'active',
    stripeStatus: 'active',
  };

  const initialModules = [
    { id: 'mod-1', moduleName: 'CRM', isEnabled: true },
    { id: 'mod-2', moduleName: 'Billing', isEnabled: true },
  ];

  it('muestra plan actual y módulos', () => {
    render(
      <SubscriptionClient
        initialPlans={initialPlans}
        initialSubscription={initialSubscription}
        initialModules={initialModules}
        initialStripeInvoices={[]}
      />,
    );

    expect(screen.getByText('Plan Activo')).toBeInTheDocument();
    expect(screen.getAllByText(/starter/i).length).toBeGreaterThan(0);
    expect(screen.getByText('CRM')).toBeInTheDocument();
    expect(screen.getByText('Billing')).toBeInTheDocument();
  });

  it('muestra planes disponibles para cambio', () => {
    render(
      <SubscriptionClient
        initialPlans={initialPlans}
        initialSubscription={null}
        initialModules={initialModules}
        initialStripeInvoices={[]}
      />,
    );

    expect(screen.getByRole('heading', { name: /suscripción y licencias/i })).toBeInTheDocument();
    expect(screen.getAllByText(/19/).length).toBeGreaterThan(0);
  });
});

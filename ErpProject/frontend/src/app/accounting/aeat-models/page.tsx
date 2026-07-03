import { redirect } from 'next/navigation';

/** Ruta legacy — modelos reales en /accounting/aeat (ADR-0018 #24/#25). */
export default function AeatModelsLegacyPage() {
    redirect('/accounting/aeat');
}

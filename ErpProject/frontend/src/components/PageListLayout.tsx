import { type ReactNode } from 'react';
import PageContainer from '@/components/PageContainer';

interface PageListLayoutProps {
    title: string;
    subtitle?: string;
    actions?: ReactNode;
    children: ReactNode;
}

/** Cabecera + contenido estándar para listados (ADR-0018 #37). */
export default function PageListLayout({ title, subtitle, actions, children }: PageListLayoutProps) {
    return (
        <PageContainer>
            <div className="page-header">
                <div>
                    <h1 className="page-title">{title}</h1>
                    {subtitle ? <p className="page-subtitle">{subtitle}</p> : null}
                </div>
                {actions}
            </div>
            {children}
        </PageContainer>
    );
}

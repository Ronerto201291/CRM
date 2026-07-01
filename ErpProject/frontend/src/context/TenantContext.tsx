'use client';
import { createContext, useContext, useState, useEffect } from 'react';

interface ITenantContext {
    tenantId: string | null;
    tenantName: string | null;
    setTenant: (id: string, name: string) => void;
    clearTenant: () => void;
}

const TenantContext = createContext<ITenantContext>({
    tenantId: null,
    tenantName: null,
    setTenant: () => {},
    clearTenant: () => {},
});

export function TenantProvider({ children }: { children: React.ReactNode }) {
    const [tenantId, setTenantId] = useState<string | null>(null);
    const [tenantName, setTenantName] = useState<string | null>(null);
    const [isHydrated, setIsHydrated] = useState(false);

    // Cargar desde localStorage al montar
    useEffect(() => {
        if (typeof window !== 'undefined') {
            const stored = localStorage.getItem('tenantId');
            const storedName = localStorage.getItem('tenantName');
            setTenantId(stored);
            setTenantName(storedName);
            setIsHydrated(true);
        }
    }, []);

    const value: ITenantContext = {
        tenantId,
        tenantName,
        setTenant: (id: string, name: string) => {
            setTenantId(id);
            setTenantName(name);
            if (typeof window !== 'undefined') {
                localStorage.setItem('tenantId', id);
                localStorage.setItem('tenantName', name);
            }
        },
        clearTenant: () => {
            setTenantId(null);
            setTenantName(null);
            if (typeof window !== 'undefined') {
                localStorage.removeItem('tenantId');
                localStorage.removeItem('tenantName');
            }
        }
    };

    if (!isHydrated) {
        return <>{children}</>;
    }

    return (
        <TenantContext.Provider value={value}>
            {children}
        </TenantContext.Provider>
    );
}

export function useTenant() {
    const context = useContext(TenantContext);
    if (!context) {
        throw new Error('useTenant debe usarse dentro de TenantProvider');
    }
    return context;
}

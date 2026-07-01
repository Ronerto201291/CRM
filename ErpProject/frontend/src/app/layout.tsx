import type { Metadata } from "next";
import "./globals.css";
import Sidebar from "@/components/Sidebar";
import AlertPoller from "@/components/AlertPoller";
import { TenantProvider } from "@/context/TenantContext";
import { cookies } from "next/headers";

export const metadata: Metadata = {
  title: "ERP SaaS – Gestión Empresarial para PYMES",
  description: "Plataforma SaaS de gestión empresarial para PYMES españolas. Facturación, CRM, Contabilidad y más.",
};

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  const cookieStore = await cookies();
  const token = cookieStore.get("erp_token")?.value;

  return (
    <html lang="es">
      <body>
        <TenantProvider>
          {token ? (
            <div style={{ display: "flex", height: "100vh", overflow: "hidden" }}>
              <Sidebar />
              <main style={{ flex: 1, overflow: "auto", background: "var(--surface-2)" }}>
                {children}
              </main>
              <AlertPoller />
            </div>
          ) : (
            <div style={{ minHeight: "100vh", background: "var(--surface-2)" }}>
              {children}
            </div>
          )}
        </TenantProvider>
      </body>
    </html>
  );
}

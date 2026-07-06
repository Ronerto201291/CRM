import Link from "next/link";

export default function NotFound() {
  return (
    <div
      style={{
        minHeight: "60vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "2rem",
      }}
    >
      <div style={{ textAlign: "center", maxWidth: 420 }}>
        <h1
          style={{
            fontSize: "1.5rem",
            fontWeight: 700,
            color: "var(--text-primary)",
            marginBottom: "0.5rem",
          }}
        >
          Página no encontrada
        </h1>
        <p
          style={{
            color: "var(--text-secondary)",
            fontSize: "0.9rem",
            marginBottom: "1.5rem",
            lineHeight: 1.5,
          }}
        >
          La ruta que buscas no existe o ha sido movida.
        </p>
        <Link
          href="/dashboard"
          style={{
            display: "inline-block",
            background: "var(--brand-primary)",
            color: "#fff",
            padding: "0.6rem 1.25rem",
            borderRadius: "var(--radius-sm)",
            fontSize: "0.875rem",
            fontWeight: 500,
            textDecoration: "none",
          }}
        >
          Ir al panel
        </Link>
      </div>
    </div>
  );
}

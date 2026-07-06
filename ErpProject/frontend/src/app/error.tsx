"use client";

export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <html lang="es">
      <body
        style={{
          minHeight: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          background: "var(--surface-2)",
          fontFamily: "Inter, system-ui, sans-serif",
          padding: "2rem",
        }}
      >
        <div
          style={{
            maxWidth: 480,
            width: "100%",
            background: "var(--surface)",
            border: "1px solid var(--border)",
            borderRadius: "var(--radius-lg)",
            padding: "2rem",
            boxShadow: "var(--shadow-md)",
          }}
        >
          <h1
            style={{
              fontSize: "1.25rem",
              fontWeight: 600,
              color: "var(--text-primary)",
              marginBottom: "0.5rem",
            }}
          >
            Ha ocurrido un error
          </h1>
          <p
            style={{
              color: "var(--text-secondary)",
              fontSize: "0.9rem",
              marginBottom: "1.5rem",
              lineHeight: 1.5,
            }}
          >
            {error.message || "No se pudo cargar esta página. Inténtalo de nuevo."}
          </p>
          <button
            type="button"
            onClick={() => reset()}
            style={{
              background: "var(--brand-primary)",
              color: "#fff",
              border: "none",
              borderRadius: "var(--radius-sm)",
              padding: "0.6rem 1.25rem",
              fontSize: "0.875rem",
              fontWeight: 500,
              cursor: "pointer",
            }}
          >
            Reintentar
          </button>
        </div>
      </body>
    </html>
  );
}

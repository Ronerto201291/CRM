import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: 'standalone', // necesario para el build multi-stage de frontend/Dockerfile
  // Next.js 16 ya no soporta la clave `eslint` en next.config (el lint se
  // desacopló del build); si hace falta ignorar errores de lint en CI, usar
  // `next lint` con sus propias opciones en vez de esta config.
  typescript: {
    ignoreBuildErrors: true,
  },
};

export default nextConfig;

import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: 'standalone',  // necesario para Dockerfile multi-stage
};

export default nextConfig;

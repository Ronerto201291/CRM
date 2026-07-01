/** @type {import('next').NextConfig} */
const nextConfig = {
  eslint: {
    // ?? This allows the build to succeed even if the project has ESLint errors.
    ignoreDuringBuilds: true,
  },
  typescript: {
    // ?? Dangerously allow build to succeed even if you have type errors.
    // It is recommended to fix these errors.
    ignoreBuildErrors: true,
  },
};

module.exports = nextConfig;


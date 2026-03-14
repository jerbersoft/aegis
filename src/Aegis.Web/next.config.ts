import type { NextConfig } from "next";

const backendBaseUrl = (process.env.NEXT_PUBLIC_BACKEND_URL ?? "http://localhost:5078").replace(/\/$/, "");

const nextConfig: NextConfig = {
  async rewrites() {
    return [
      {
        source: "/hubs/:path*",
        destination: `${backendBaseUrl}/hubs/:path*`,
      },
    ];
  },
};

export default nextConfig;

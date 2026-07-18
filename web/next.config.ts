import type { NextConfig } from "next";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const nextConfig: NextConfig = {
  // Avoid picking /home/itbienvenu/package-lock.json as monorepo root
  outputFileTracingRoot: path.join(__dirname),
  async redirects() {
    return [
      { source: "/activate.html", destination: "/activate", permanent: true },
      { source: "/pricing.html", destination: "/pricing", permanent: true },
      { source: "/download.html", destination: "/download", permanent: true },
      { source: "/docs.html", destination: "/docs", permanent: true },
      { source: "/account/login.html", destination: "/account/login", permanent: true },
      { source: "/account/register.html", destination: "/account/register", permanent: true },
      { source: "/account/dashboard.html", destination: "/account/dashboard", permanent: true },
      { source: "/admin/index.html", destination: "/admin", permanent: true },
      { source: "/admin/", destination: "/admin", permanent: true },
    ];
  },
};

export default nextConfig;

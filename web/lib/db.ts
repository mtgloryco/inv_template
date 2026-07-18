import postgres from "postgres";

let sql: ReturnType<typeof postgres> | null = null;
let schemaReady = false;

function connectionString(): string {
  return (
    process.env.POSTGRES_URL ??
    process.env.DATABASE_URL ??
    process.env.POSTGRES_PRISMA_URL ??
    ""
  );
}

export function getSql() {
  const url = connectionString();
  if (!url) {
    throw new Error("DATABASE_URL or POSTGRES_URL is not configured.");
  }
  if (!sql) {
    sql = postgres(url, {
      ssl: url.includes("localhost") ? false : "require",
      prepare: false,
    });
  }
  return sql;
}

export async function ensureSchema() {
  if (schemaReady) return;
  const db = getSql();

  await db`
    CREATE TABLE IF NOT EXISTS organizations (
      id UUID PRIMARY KEY,
      name TEXT NOT NULL,
      created_at TIMESTAMPTZ NOT NULL
    )
  `;
  await db`
    CREATE TABLE IF NOT EXISTS users (
      id UUID PRIMARY KEY,
      email TEXT NOT NULL UNIQUE,
      password_hash TEXT NOT NULL,
      organization_id UUID NOT NULL REFERENCES organizations(id),
      role TEXT NOT NULL DEFAULT 'customer',
      created_at TIMESTAMPTZ NOT NULL
    )
  `;
  await db`
    ALTER TABLE users ADD COLUMN IF NOT EXISTS role TEXT NOT NULL DEFAULT 'customer'
  `;
  await db`
    CREATE TABLE IF NOT EXISTS license_requests (
      id UUID PRIMARY KEY,
      email TEXT NOT NULL,
      company TEXT NOT NULL,
      tier TEXT NOT NULL,
      hardware_id TEXT NOT NULL,
      created_at TIMESTAMPTZ NOT NULL,
      status TEXT NOT NULL DEFAULT 'pending',
      license_key TEXT,
      license_id UUID,
      expiry TIMESTAMPTZ,
      processed_at TIMESTAMPTZ,
      admin_notes TEXT
    )
  `;
  await db`
    CREATE INDEX IF NOT EXISTS ix_license_requests_created ON license_requests(created_at)
  `;

  schemaReady = true;
}

export type LicenseRequestRow = {
  id: string;
  email: string;
  company: string;
  tier: string;
  hardware_id: string;
  created_at: Date;
  status: string;
  license_key: string | null;
  license_id: string | null;
  expiry: Date | null;
  processed_at: Date | null;
};

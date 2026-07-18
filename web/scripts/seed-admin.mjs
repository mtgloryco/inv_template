#!/usr/bin/env node
/**
 * Seeds the admin user into Postgres from ADMIN_EMAIL + ADMIN_PASSWORD in .env.local
 * Run: node scripts/seed-admin.mjs
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";
import { randomUUID } from "crypto";
import bcrypt from "bcryptjs";
import postgres from "postgres";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.join(__dirname, "..");
const envPath = path.join(webRoot, ".env.local");

function loadEnv() {
  if (!fs.existsSync(envPath)) {
    throw new Error("Missing web/.env.local — run: npm run env:import");
  }
  for (const line of fs.readFileSync(envPath, "utf8").split("\n")) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) continue;
    const eq = trimmed.indexOf("=");
    if (eq === -1) continue;
    const key = trimmed.slice(0, eq).trim();
    let val = trimmed.slice(eq + 1).trim();
    if (!process.env[key]) process.env[key] = val;
  }
}

loadEnv();

const email = (process.env.ADMIN_EMAIL ?? "").trim().toLowerCase();
const password = process.env.ADMIN_PASSWORD ?? "";
const url = process.env.POSTGRES_URL || process.env.DATABASE_URL;

if (!email || !password) {
  console.error("Set ADMIN_EMAIL and ADMIN_PASSWORD in .env.local");
  process.exit(1);
}
if (!url) {
  console.error("Set POSTGRES_URL in .env.local (Neon connection string)");
  process.exit(1);
}

const sql = postgres(url, { ssl: url.includes("localhost") ? false : "require", prepare: false });

async function main() {
  await sql`
    CREATE TABLE IF NOT EXISTS organizations (
      id UUID PRIMARY KEY,
      name TEXT NOT NULL,
      created_at TIMESTAMPTZ NOT NULL
    )
  `;
  await sql`
    CREATE TABLE IF NOT EXISTS users (
      id UUID PRIMARY KEY,
      email TEXT NOT NULL UNIQUE,
      password_hash TEXT NOT NULL,
      organization_id UUID NOT NULL REFERENCES organizations(id),
      role TEXT NOT NULL DEFAULT 'customer',
      created_at TIMESTAMPTZ NOT NULL
    )
  `;
  await sql`ALTER TABLE users ADD COLUMN IF NOT EXISTS role TEXT NOT NULL DEFAULT 'customer'`;

  const passwordHash = await bcrypt.hash(password, 10);
  const now = new Date();
  const existing = await sql`SELECT id FROM users WHERE email = ${email} LIMIT 1`;

  if (existing[0]) {
    await sql`
      UPDATE users SET password_hash = ${passwordHash}, role = 'admin' WHERE email = ${email}
    `;
    console.log("Updated admin user:", email);
  } else {
    const orgId = randomUUID();
    const userId = randomUUID();
    await sql.begin(async (tx) => {
      await tx`
        INSERT INTO organizations (id, name, created_at)
        VALUES (${orgId}, ${"MT GLORY CO Admin"}, ${now})
      `;
      await tx`
        INSERT INTO users (id, email, password_hash, organization_id, role, created_at)
        VALUES (${userId}, ${email}, ${passwordHash}, ${orgId}, ${"admin"}, ${now})
      `;
    });
    console.log("Created admin user:", email);
  }

  await sql.end();
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

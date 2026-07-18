import { randomUUID } from "crypto";
import { ensureSchema, getSql } from "./db";
import { createToken, hashPassword, verifyPassword } from "./auth";

export async function ensureAdminUser() {
  const email = (process.env.ADMIN_EMAIL ?? "").trim().toLowerCase();
  const password = process.env.ADMIN_PASSWORD ?? "";
  if (!email || !password) return null;

  await ensureSchema();
  const db = getSql();
  const passwordHash = await hashPassword(password);
  const now = new Date();

  const existing = await db<{ id: string; organization_id: string }[]>`
    SELECT id, organization_id FROM users WHERE email = ${email} LIMIT 1
  `;

  if (existing[0]) {
    await db`
      UPDATE users
      SET password_hash = ${passwordHash}, role = 'admin'
      WHERE email = ${email}
    `;
    return existing[0].id;
  }

  const orgId = randomUUID();
  const userId = randomUUID();
  const orgName = "MT GLORY CO Admin";

  await db.begin(async (tx) => {
    await tx`
      INSERT INTO organizations (id, name, created_at) VALUES (${orgId}, ${orgName}, ${now})
    `;
    await tx`
      INSERT INTO users (id, email, password_hash, organization_id, role, created_at)
      VALUES (${userId}, ${email}, ${passwordHash}, ${orgId}, 'admin', ${now})
    `;
  });

  return userId;
}

export async function registerUser(input: {
  email: string;
  password: string;
  organizationName?: string;
}) {
  const email = input.email?.trim().toLowerCase() ?? "";
  const password = input.password ?? "";
  if (!email || !password) return null;

  await ensureSchema();
  const db = getSql();
  const orgId = randomUUID();
  const userId = randomUUID();
  const orgName =
    input.organizationName?.trim() ||
    `${email.split("@")[0]} Workspace`;
  const passwordHash = await hashPassword(password);
  const now = new Date();

  try {
    await db.begin(async (tx) => {
      await tx`
        INSERT INTO organizations (id, name, created_at) VALUES (${orgId}, ${orgName}, ${now})
      `;
      await tx`
        INSERT INTO users (id, email, password_hash, organization_id, role, created_at)
        VALUES (${userId}, ${email}, ${passwordHash}, ${orgId}, 'customer', ${now})
      `;
    });
  } catch {
    return null;
  }

  const token = await createToken(userId, orgId, email, "customer");
  return {
    token,
    organizationId: orgId,
    organizationName: orgName,
    userId,
    email,
    role: "customer" as const,
  };
}

export async function loginUser(input: { email: string; password: string }) {
  const email = input.email?.trim().toLowerCase() ?? "";
  const password = input.password ?? "";
  if (!email || !password) return null;

  await ensureSchema();
  const db = getSql();
  const rows = await db<
    {
      id: string;
      email: string;
      password_hash: string;
      organization_id: string;
      name: string;
      role: string;
    }[]
  >`
    SELECT u.id, u.email, u.password_hash, u.organization_id, u.role, o.name
    FROM users u
    JOIN organizations o ON o.id = u.organization_id
    WHERE u.email = ${email}
    LIMIT 1
  `;
  const user = rows[0];
  if (!user) return null;
  if (!(await verifyPassword(password, user.password_hash))) return null;

  const token = await createToken(user.id, user.organization_id, user.email, user.role);
  return {
    token,
    organizationId: user.organization_id,
    organizationName: user.name,
    userId: user.id,
    email: user.email,
    role: user.role,
  };
}

export async function loginAdmin(input: { email: string; password: string }) {
  await ensureAdminUser();
  const result = await loginUser(input);
  if (!result || result.role !== "admin") return null;
  return result;
}

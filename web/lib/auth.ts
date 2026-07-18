import bcrypt from "bcryptjs";
import { SignJWT, jwtVerify } from "jose";

const issuer = "glorydesk-web";
const audience = "glorydesk-clients";

function secret() {
  const s = process.env.JWT_SECRET ?? process.env.Jwt__Key;
  if (!s || s.length < 32) {
    throw new Error("JWT_SECRET must be at least 32 characters.");
  }
  return new TextEncoder().encode(s);
}

export async function hashPassword(password: string) {
  return bcrypt.hash(password, 10);
}

export async function verifyPassword(password: string, hash: string) {
  return bcrypt.compare(password, hash);
}

export async function createToken(
  userId: string,
  organizationId: string,
  email: string,
  role = "customer"
) {
  return new SignJWT({ org_id: organizationId, user_id: userId, role })
    .setProtectedHeader({ alg: "HS256" })
    .setSubject(email)
    .setIssuer(issuer)
    .setAudience(audience)
    .setExpirationTime("30d")
    .setIssuedAt()
    .sign(secret());
}

export async function verifyToken(token: string) {
  const { payload } = await jwtVerify(token, secret(), { issuer, audience });
  return {
    email: payload.sub as string,
    userId: payload.user_id as string,
    organizationId: payload.org_id as string,
    role: (payload.role as string) ?? "customer",
  };
}

export function getBearerToken(header: string | null) {
  if (!header?.startsWith("Bearer ")) return null;
  return header.slice(7);
}

export function isAdminRequest(adminKeyHeader: string | null) {
  const expected = process.env.ADMIN_API_KEY ?? process.env.Admin__ApiKey;
  if (!expected) return false;
  return adminKeyHeader === expected;
}

export async function authorizeAdmin(request: Request) {
  if (isAdminRequest(request.headers.get("x-admin-key"))) return true;

  const token = getBearerToken(request.headers.get("authorization"));
  if (!token) return false;

  try {
    const claims = await verifyToken(token);
    return claims.role === "admin";
  } catch {
    return false;
  }
}

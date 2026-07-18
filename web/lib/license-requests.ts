import { randomUUID } from "crypto";
import { ensureSchema, getSql, type LicenseRequestRow } from "./db";
import { VALID_TIERS, type LicenseTier } from "./config";
import { generateLicenseKey, isLicenseSigningConfigured } from "./license";
import { sendLicenseIssuedEmail } from "./email";

export async function submitLicenseRequest(input: {
  email: string;
  company: string;
  tier: string;
  hardwareId: string;
}) {
  await ensureSchema();
  const email = input.email?.trim() ?? "";
  const company = input.company?.trim() ?? "";
  const tier = input.tier?.trim() ?? "";
  const hardwareId = input.hardwareId?.trim() ?? "";

  if (!email.includes("@")) {
    return { success: false as const, message: "A valid business email is required." };
  }
  if (!company) {
    return { success: false as const, message: "Company or shop name is required." };
  }
  if (!VALID_TIERS.includes(tier as LicenseTier)) {
    return {
      success: false as const,
      message: "Select a valid tier: Basic, Medium, Pro, or Enterprise.",
    };
  }
  if (hardwareId.length < 8) {
    return {
      success: false as const,
      message: "Paste the Hardware ID from Glory Desk → License.",
    };
  }

  const id = randomUUID();
  const now = new Date();
  const db = getSql();

  await db`
    INSERT INTO license_requests (id, email, company, tier, hardware_id, created_at, status)
    VALUES (${id}, ${email.toLowerCase()}, ${company}, ${tier}, ${hardwareId}, ${now}, 'pending')
  `;

  return {
    success: true as const,
    message:
      "Request received. MT GLORY CO will email your signed license key within 1–2 business days.",
  };
}

export async function listLicenseRequests(status?: string | null) {
  await ensureSchema();
  const db = getSql();
  const rows = status
    ? await db<LicenseRequestRow[]>`
        SELECT id, email, company, tier, hardware_id, created_at, status,
               license_key, license_id, expiry, processed_at
        FROM license_requests WHERE status = ${status}
        ORDER BY created_at DESC LIMIT 200
      `
    : await db<LicenseRequestRow[]>`
        SELECT id, email, company, tier, hardware_id, created_at, status,
               license_key, license_id, expiry, processed_at
        FROM license_requests
        ORDER BY created_at DESC LIMIT 200
      `;

  return rows.map(mapRequest);
}

export async function issueLicenseRequest(id: string, validYears = 1, notes?: string) {
  if (!isLicenseSigningConfigured()) {
    return { success: false as const, message: "License signing key is not configured on the server." };
  }

  await ensureSchema();
  const db = getSql();
  const rows = await db<LicenseRequestRow[]>`
    SELECT id, email, company, tier, hardware_id, created_at, status,
           license_key, license_id, expiry, processed_at
    FROM license_requests WHERE id = ${id} LIMIT 1
  `;
  const request = rows[0];
  if (!request) return { success: false as const, message: "Request not found." };
  if (request.status === "issued") {
    return {
      success: false as const,
      message: "License already issued for this request.",
      licenseKey: request.license_key ?? undefined,
    };
  }

  const years = Math.min(10, Math.max(1, validYears));
  const expiry = new Date();
  expiry.setUTCFullYear(expiry.getUTCFullYear() + years);
  const { licenseKey, licenseId } = generateLicenseKey(
    request.hardware_id,
    request.company,
    request.tier,
    expiry
  );
  const now = new Date();

  await db`
    UPDATE license_requests
    SET status = 'issued', license_key = ${licenseKey}, license_id = ${licenseId},
        expiry = ${expiry}, processed_at = ${now}, admin_notes = ${notes ?? null}
    WHERE id = ${id}
  `;

  await sendLicenseIssuedEmail(
    request.email,
    request.company,
    request.tier,
    licenseKey,
    expiry
  ).catch((err) => console.error("Email error:", err));

  return {
    success: true as const,
    message: `License issued. Valid until ${expiry.toISOString().slice(0, 10)}.`,
    licenseKey,
  };
}

export async function rejectLicenseRequest(id: string, reason?: string) {
  await ensureSchema();
  const db = getSql();
  const now = new Date();
  const result = await db`
    UPDATE license_requests
    SET status = 'rejected', processed_at = ${now}, admin_notes = ${reason ?? null}
    WHERE id = ${id}
  `;
  if (result.count === 0) return { success: false as const, message: "Request not found." };
  return { success: true as const, message: "Request rejected." };
}

/** Manual issue — same as old license-manager-web POST /api/admin/licenses */
export async function manualIssueLicense(input: {
  email: string;
  company: string;
  hardwareId: string;
  tier: string;
  validYears?: number;
}) {
  if (!isLicenseSigningConfigured()) {
    return { success: false as const, message: "License signing key is not configured." };
  }

  const email = input.email?.trim().toLowerCase() ?? "";
  const company = input.company?.trim() ?? "";
  const hardwareId = input.hardwareId?.trim() ?? "";
  const tier = input.tier?.trim() ?? "";

  if (!email.includes("@") || !company || hardwareId.length < 8) {
    return { success: false as const, message: "Email, company name, and Hardware ID are required." };
  }
  if (!VALID_TIERS.includes(tier as LicenseTier)) {
    return { success: false as const, message: "Invalid tier." };
  }

  const years = Math.min(10, Math.max(1, input.validYears ?? 1));
  const expiry = new Date();
  expiry.setUTCFullYear(expiry.getUTCFullYear() + years);
  const { licenseKey, licenseId } = generateLicenseKey(hardwareId, company, tier, expiry);
  const id = randomUUID();
  const now = new Date();
  const db = getSql();
  await ensureSchema();

  await db`
    INSERT INTO license_requests (
      id, email, company, tier, hardware_id, created_at, status,
      license_key, license_id, expiry, processed_at, admin_notes
    ) VALUES (
      ${id}, ${email}, ${company}, ${tier}, ${hardwareId}, ${now}, 'issued',
      ${licenseKey}, ${licenseId}, ${expiry}, ${now}, 'Manual issue by admin'
    )
  `;

  await sendLicenseIssuedEmail(email, company, tier, licenseKey, expiry).catch(console.error);

  return {
    success: true as const,
    message: `Manual license issued. Valid until ${expiry.toISOString().slice(0, 10)}.`,
    licenseKey,
    id,
  };
}

export async function getLicensesForEmail(email: string) {
  await ensureSchema();
  const db = getSql();
  const rows = await db<LicenseRequestRow[]>`
    SELECT id, email, company, tier, hardware_id, created_at, status,
           license_key, license_id, expiry, processed_at
    FROM license_requests
    WHERE lower(email) = ${email.trim().toLowerCase()} AND status = 'issued'
    ORDER BY processed_at DESC
  `;
  return rows.map((r) => ({
    id: r.id,
    tier: r.tier,
    company: r.company,
    hardwareId: r.hardware_id,
    issuedAt: r.created_at,
    expiry: r.expiry,
    status: r.status,
    licenseKey: r.license_key,
  }));
}

function mapRequest(r: LicenseRequestRow) {
  return {
    id: r.id,
    email: r.email,
    company: r.company,
    tier: r.tier,
    hardwareId: r.hardware_id,
    createdAt: r.created_at,
    status: r.status,
    licenseKey: r.license_key,
    licenseId: r.license_id,
    expiry: r.expiry,
    processedAt: r.processed_at,
  };
}

import crypto from "crypto";
import { randomUUID } from "crypto";

export type LicensePayload = {
  LicenseId: string;
  HardwareId: string;
  IssuedTo: string;
  IssuedAt: string;
  Expiry: string;
  Tier: string;
};

function getPrivateKey() {
  // Legacy license-manager-web: PEM stored as base64 in LICENSE_PRIVATE_KEY
  const pemB64 = process.env.LICENSE_PRIVATE_KEY;
  if (pemB64) {
    try {
      const pem = Buffer.from(pemB64.replace(/\s/g, ""), "base64").toString("utf8");
      return crypto.createPrivateKey(pem);
    } catch {
      /* try DER below */
    }
  }

  const b64 =
    process.env.LICENSE_RSA_PRIVATE_KEY_B64 ??
    process.env.License__PrivateKeyBase64;
  if (!b64) return null;
  try {
    return crypto.createPrivateKey({
      key: Buffer.from(b64.replace(/\s/g, ""), "base64"),
      format: "der",
      type: "pkcs8",
    });
  } catch {
    return null;
  }
}

export function isLicenseSigningConfigured() {
  return getPrivateKey() !== null;
}

export function generateLicenseKey(
  hardwareId: string,
  issuedTo: string,
  tier: string,
  expiry: Date
): { licenseKey: string; licenseId: string } {
  const key = getPrivateKey();
  if (!key) {
    throw new Error("License signing key is not configured.");
  }

  const licenseId = randomUUID();
  const payload: LicensePayload = {
    LicenseId: licenseId,
    HardwareId: hardwareId.trim(),
    IssuedTo: issuedTo.trim(),
    IssuedAt: new Date().toISOString(),
    Expiry: expiry.toISOString(),
    Tier: tier.trim(),
  };

  const payloadJson = JSON.stringify(payload);
  const data = Buffer.from(payloadJson, "utf8");
  const signature = crypto.sign("sha256", data, {
    key,
    padding: crypto.constants.RSA_PKCS1_PADDING,
  });

  return {
    licenseId,
    licenseKey: `${data.toString("base64")}.${signature.toString("base64")}`,
  };
}

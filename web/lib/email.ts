import nodemailer from "nodemailer";

function smtpConfigured() {
  const pass = process.env.SMTP_PASS ?? process.env.SMTP_PASSWORD;
  return Boolean(process.env.SMTP_HOST && process.env.SMTP_USER && pass);
}

export async function sendLicenseIssuedEmail(
  to: string,
  company: string,
  tier: string,
  licenseKey: string,
  expiry: Date
) {
  if (!smtpConfigured()) {
    console.info("[email skipped] License issued to", to);
    return;
  }

  const transporter = nodemailer.createTransport({
    host: process.env.SMTP_HOST,
    port: Number(process.env.SMTP_PORT ?? 587),
    secure: process.env.SMTP_SECURE === "true",
    auth: {
      user: process.env.SMTP_USER,
      pass: process.env.SMTP_PASS ?? process.env.SMTP_PASSWORD,
    },
  });

  const from = process.env.SMTP_FROM ?? "noreply@mtglory.com";

  await transporter.sendMail({
    from,
    to,
    subject: `Your Glory Desk ${tier} license key`,
    text: `Hello,

Your Glory Desk license for ${company} has been approved.

Tier: ${tier}
Valid until: ${expiry.toISOString().slice(0, 10)} UTC

License key (paste in Glory Desk → License):
${licenseKey}

This key is bound to your Hardware ID and works on one computer only.

— MT GLORY CO · Glory Desk
https://glorydesk.mtglory.com`,
  });
}

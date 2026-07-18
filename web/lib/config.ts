export const VALID_TIERS = ["Basic", "Medium", "Pro", "Enterprise"] as const;
export type LicenseTier = (typeof VALID_TIERS)[number];

export const pricing = {
  currency: "RWF",
  tiers: [
    { name: "Basic", products: 50, locations: 1, price: "150000", period: "year" },
    { name: "Medium", products: 500, locations: 3, price: "350000", period: "year" },
    { name: "Pro", products: "Unlimited", locations: "Unlimited", price: "650000", period: "year" },
    { name: "Enterprise", products: "Unlimited", locations: "Unlimited", price: "Contact sales", period: "year" },
  ],
};

export function getDownloadInfo() {
  const downloadUrl =
    process.env.GITHUB_RELEASE_URL ??
    process.env.NEXT_PUBLIC_DOWNLOAD_URL_WINDOWS ??
    "https://github.com/mtgloryco/glorydesk/releases/latest";
  return {
    platform: "Windows",
    version: "1.0.1",
    fileName: process.env.WINDOWS_INSTALLER_FILE ?? "GloryDesk_Setup_v1.0.1_Windows.exe",
    sizeMb: "102",
    available: false,
    downloadUrl,
    requirements: [
      "Windows 10/11 64-bit",
      "4 GB RAM minimum",
      "500 MB disk space",
    ],
  };
}

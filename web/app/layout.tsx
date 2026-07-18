import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Glory Desk — MT GLORY CO",
  description:
    "Glory Desk by MT GLORY CO. Inventory, POS, accounting, and reports for retail and growing businesses.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}

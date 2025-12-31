import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata = {
  title: "IMS Professional | License Management Hub",
  description: "Advanced Inventory Management System for professional businesses. Manage your RSA-signed licenses, activations, and offline-first stock control solutions by MSS.",
  keywords: "IMS, Inventory Management System, License Manager, ERP, MIS, Management Systems, Offline Inventory, Stock Tracking",
  icons: {
    icon: "/favicon.ico",
  },
  openGraph: {
    title: "IMS Professional | License Management Hub",
    description: "Secure license activation and management for the IMS stock control ecosystem.",
    type: "website",
  }
};

export default function RootLayout({ children }) {
  return (
    <html lang="en">
      <head>
        <title>{metadata.title}</title>
        <meta name="google-adsense-account" content="ca-pub-1595689628350805"></meta>
        <meta name="description" content={metadata.description} />
        <script
          async
          src="https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js?client=ca-pub-1595689628350805"
          crossOrigin="anonymous"
        ></script>
        {/* Ezoic Privacy Scripts */}
        <script data-cfasync="false" src="https://cmp.gatekeeperconsent.com/min.js"></script>
        <script data-cfasync="false" src="https://the.gatekeeperconsent.com/cmp.min.js"></script>
        {/* Ezoic Header Script */}
        <script async src="//www.ezojs.com/ezoic/sa.min.js"></script>
        <script
          dangerouslySetInnerHTML={{
            __html: `
            window.ezstandalone = window.ezstandalone || {};
            ezstandalone.cmd = ezstandalone.cmd || [];
          `,
          }}
        />
      </head>
      <body className={`${geistSans.variable} ${geistMono.variable}`}>
        {children}
      </body>
    </html>
  );
}

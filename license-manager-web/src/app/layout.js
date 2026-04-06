import { Geist, Geist_Mono } from "next/font/google";
import Script from "next/script";
import "./globals.css";
import NativeBanner from "@/components/NativeBanner";

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
        {/* Scripts moved to body for performance and DOM access */}
      </head>
      <body className={`${geistSans.variable} ${geistMono.variable}`}>
        {children}

        {/* Ezoic Privacy & Helper Scripts (Moved to Body) */}
        <Script strategy="afterInteractive" data-cfasync="false" src="https://cmp.gatekeeperconsent.com/min.js" />
        <Script strategy="afterInteractive" data-cfasync="false" src="https://the.gatekeeperconsent.com/cmp.min.js" />

        {/* Ezoic Analytics Queue Init */}
        <Script
          id="ezo-init"
          strategy="afterInteractive"
          dangerouslySetInnerHTML={{
            __html: `
            window._ezaq = window._ezaq || [];
            window.ezstandalone = window.ezstandalone || {};
            ezstandalone.cmd = ezstandalone.cmd || [];
          `,
          }}
        />
        <Script strategy="lazyOnload" src="//www.ezojs.com/ezoic/sa.min.js" />

        {/* EffectiveGateCPM Main Script */}
        <Script strategy="lazyOnload" src="https://pl28373489.effectivegatecpm.com/53/3b/f2/533bf2824e1da8c50fb338693c952f5d.js" />

        {/* Native Banner Component */}
        <NativeBanner />
      </body>
    </html>
  );
}

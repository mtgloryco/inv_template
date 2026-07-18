import Link from "next/link";
import { CompanyLogo } from "./CompanyLogo";

type NavItem = { href: string; label: string };

const nav: NavItem[] = [
  { href: "/", label: "Product" },
  { href: "/pricing", label: "Pricing" },
  { href: "/download", label: "Download" },
  { href: "/activate", label: "Activate" },
  { href: "/docs", label: "Docs" },
  { href: "/account/login", label: "Account" },
];

export function SiteHeader({ active }: { active?: string }) {
  return (
    <>
      <div className="topbar">
        Licensed software by <strong>MT GLORY CO</strong> · Secure offline-first desktop ·{" "}
        <Link href="/download">Download for Windows</Link>
      </div>
      <header className="site-header">
        <div className="header-inner">
          <Link className="logo" href="/">
            <CompanyLogo tone="light" />
          </Link>
          <nav className="main-nav">
            {nav.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                className={active === item.href ? "active" : undefined}
              >
                {item.label}
              </Link>
            ))}
            <Link className="btn btn-sm btn-white nav-cta" href="/download">
              Get Glory Desk
            </Link>
          </nav>
        </div>
      </header>
    </>
  );
}

export function TrustBar() {
  return (
    <div className="trust-bar">
      <div className="trust-inner">
        <span>Offline-first desktop</span>
        <span>RSA-signed licenses</span>
        <span>EN · FR · RW</span>
        <span>Enterprise cloud sync</span>
      </div>
    </div>
  );
}

import Link from "next/link";
import { CompanyLogoMark } from "./CompanyLogo";

export function SiteFooter() {
  return (
    <footer className="site-footer">
      <div className="footer-inner">
        <div className="footer-brand">
          <CompanyLogoMark size={52} />
          <p className="footer-tagline">EXCELLENCE ENGINEERED</p>
          <p>
            Glory Desk — stock, sales &amp; accounts in one place. A product of MT GLORY CO,
            building professional software for growing businesses.
          </p>
        </div>
        <div className="footer-col">
          <h4>Company</h4>
          <Link href="/">About Glory Desk</Link>
          <Link href="/docs">Documentation</Link>
          <Link href="/pricing">Pricing</Link>
          <Link href="/legal/terms">Terms of Service</Link>
        </div>
        <div className="footer-col">
          <h4>Store</h4>
          <Link href="/download">Download</Link>
          <Link href="/activate">Activate license</Link>
          <Link href="/account/login">Customer portal</Link>
          <Link href="/admin">Admin</Link>
        </div>
        <div className="footer-col footer-contact">
          <h4 className="footer-accent-heading">Email us</h4>
          <a className="footer-email" href="mailto:support@mtglory.com">
            support@mtglory.com
          </a>
          <Link className="btn btn-white btn-footer-cta" href="/activate">
            Contact support
            <span aria-hidden>→</span>
          </Link>
        </div>
      </div>
      <div className="footer-bottom">
        <p>© 2026 MT GLORY CO. All rights reserved.</p>
        <p>
          <Link href="/legal/privacy">Privacy Policy</Link>
          {" · "}
          <Link href="/legal/eula">EULA</Link>
        </p>
      </div>
    </footer>
  );
}

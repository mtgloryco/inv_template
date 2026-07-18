import Link from "next/link";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteHeader } from "@/components/SiteHeader";

export default function DocsPage() {
  return (
    <>
      <SiteHeader active="/docs" />
      <section className="hero hero-compact">
        <div className="hero-inner">
          <h1>Documentation</h1>
          <p className="hero-lead">Install, activate, and operate Glory Desk.</p>
        </div>
      </section>
      <main>
        <div className="container container-narrow">
          <div className="card">
            <h2>Install</h2>
            <p>
              Download from <Link href="/download">Download</Link>, run the installer, launch from Start menu.
            </p>
          </div>
          <div className="card">
            <h2>Activate</h2>
            <p>
              Copy Hardware ID from License screen → <Link href="/activate">submit request</Link> → paste key in app.
            </p>
          </div>
          <div className="card">
            <h2>Support</h2>
            <p>
              <a href="mailto:support@mtglory.com">support@mtglory.com</a>
            </p>
          </div>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}

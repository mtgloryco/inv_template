import { Suspense } from "react";
import Link from "next/link";
import { ActivateForm } from "@/components/ActivateForm";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteHeader } from "@/components/SiteHeader";

export default function ActivatePage() {
  return (
    <>
      <SiteHeader active="/activate" />
      <section className="hero hero-compact">
        <div className="hero-inner">
          <h1>License activation</h1>
          <p className="hero-lead">
            Each license is cryptographically signed and bound to one computer. Activate inside
            the Glory Desk desktop app after you receive your key.
          </p>
        </div>
      </section>
      <main>
        <div className="container">
          <div className="card">
            <h2>Request a license key</h2>
            <Suspense fallback={<p>Loading form…</p>}>
              <ActivateForm />
            </Suspense>
            <p className="text-muted" style={{ marginTop: "1rem", fontSize: "0.875rem" }}>
              Or email <a href="mailto:support@mtglory.com">support@mtglory.com</a> with your Hardware ID.
            </p>
          </div>
          <div className="alert alert-warn">
            <strong>One machine per key.</strong> See <Link href="/legal/eula">EULA</Link> for transfer rules.
          </div>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}

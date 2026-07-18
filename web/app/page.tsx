import Image from "next/image";
import Link from "next/link";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteHeader, TrustBar } from "@/components/SiteHeader";

export default function HomePage() {
  return (
    <>
      <SiteHeader active="/" />
      <TrustBar />
      <section className="hero">
        <div className="hero-inner">
          <h1>Business operations, one professional desk</h1>
          <p className="hero-lead">
            Glory Desk is licensed desktop software for inventory, point of sale, purchases,
            general ledger, and financial reporting — built for shops and SMEs that have
            outgrown spreadsheets.
          </p>
          <div className="hero-actions">
            <Link className="btn btn-lg btn-white" href="/download">
              Download for Windows
            </Link>
            <Link className="btn btn-lg btn-outline-light" href="/pricing">
              View pricing
            </Link>
          </div>
        </div>
      </section>
      <main>
        <div className="container">
          <section className="screenshots-section" aria-label="Product screenshots">
            <div className="section-title mb-2">
              <h2>See Glory Desk in action</h2>
              <p>Real screens from the desktop app — dashboard, POS, inventory, and reports.</p>
            </div>
            <figure className="screenshot-feature">
              <Image
                src="/images/screenshots/dashboard.png"
                alt="Glory Desk dashboard overview"
                width={1280}
                height={720}
                priority
                style={{ width: "100%", height: "auto" }}
              />
              <figcaption>Dashboard — daily overview and quick actions</figcaption>
            </figure>
            <div className="screenshot-grid">
              {[
                ["pos.png", "Point of Sale", "Glory Desk point of sale"],
                ["inventory.png", "Inventory", "Glory Desk inventory management"],
                ["reports.png", "Reports", "Glory Desk financial reports"],
              ].map(([file, caption, alt]) => (
                <figure className="screenshot-card" key={file}>
                  <Image
                    src={`/images/screenshots/${file}`}
                    alt={alt}
                    width={1280}
                    height={720}
                    style={{ width: "100%", height: "auto" }}
                  />
                  <figcaption>{caption}</figcaption>
                </figure>
              ))}
            </div>
          </section>

          <div className="grid-2">
            <div className="card text-center">
              <h2>Activate your license</h2>
              <p className="text-muted">Hardware-bound RSA keys. Request online or contact sales.</p>
              <Link className="btn" href="/activate">
                Activate license
              </Link>
            </div>
            <div className="card text-center">
              <h2>Customer account</h2>
              <p className="text-muted">Track license requests and retrieve issued keys.</p>
              <Link className="btn btn-outline" href="/account/login">
                Sign in
              </Link>
            </div>
          </div>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}

import Link from "next/link";
import { pricing } from "@/lib/config";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteHeader } from "@/components/SiteHeader";

const highlights: Record<string, string[]> = {
  Basic: ["Core inventory", "1 location", "50 products"],
  Medium: ["POS & PO", "3 locations", "500 products"],
  Pro: ["Unlimited scale", "Full reports", "Audit trail"],
  Enterprise: ["Cloud sync", "Enterprise hub", "Priority support"],
};

export default function PricingPage() {
  return (
    <>
      <SiteHeader active="/pricing" />
      <section className="hero hero-compact">
        <div className="hero-inner">
          <h1>Transparent licensing</h1>
          <p className="hero-lead">Annual perpetual licenses in RWF. Email support included.</p>
        </div>
      </section>
      <main>
        <div className="container">
          <div className="grid-4 mb-2">
            {pricing.tiers.map((t) => (
              <div className={`price-card ${t.name === "Pro" ? "featured" : ""}`} key={t.name}>
                {t.name === "Pro" && <span className="badge">Popular</span>}
                <h3>{t.name}</h3>
                <div className="price">
                  {t.price === "Contact sales" ? "Custom" : `${t.price} RWF`}
                  <br />
                  <small>per {t.period}</small>
                </div>
                <ul>
                  {(highlights[t.name] ?? []).map((h) => (
                    <li key={h}>{h}</li>
                  ))}
                </ul>
                <Link className={`btn ${t.name === "Enterprise" ? "btn-navy" : ""}`} href={`/activate?tier=${t.name}`}>
                  {t.name === "Enterprise" ? "Contact sales" : "Request license"}
                </Link>
              </div>
            ))}
          </div>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}

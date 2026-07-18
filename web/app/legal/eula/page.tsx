import Link from "next/link";
import { SiteHeader } from "@/components/SiteHeader";

export default function EulaPage() {
  return (
    <>
      <SiteHeader />
      <main>
        <div className="container container-narrow">
          <h1>End User License Agreement</h1>
          <div className="card">
            <p>One license key per computer. No redistribution. Hardware transfer requires MT GLORY CO approval.</p>
            <p><Link href="/legal/terms">Terms of Service</Link></p>
          </div>
        </div>
      </main>
    </>
  );
}

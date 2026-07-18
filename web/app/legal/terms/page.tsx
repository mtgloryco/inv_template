import Link from "next/link";
import { SiteHeader } from "@/components/SiteHeader";

export default function TermsPage() {
  return (
    <>
      <SiteHeader />
      <main>
        <div className="container container-narrow">
          <h1>Terms of Service</h1>
          <div className="card">
            <p>Glory Desk is licensed software published by MT GLORY CO. By using the product you agree to the EULA and these terms.</p>
            <p>See also <Link href="/legal/eula">EULA</Link> and <Link href="/legal/privacy">Privacy Policy</Link>.</p>
          </div>
        </div>
      </main>
    </>
  );
}

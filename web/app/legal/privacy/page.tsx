import { SiteHeader } from "@/components/SiteHeader";

export default function PrivacyPage() {
  return (
    <>
      <SiteHeader />
      <main>
        <div className="container container-narrow">
          <h1>Privacy Policy</h1>
          <div className="card">
            <p>We collect email, company name, and Hardware ID for license requests. Desktop business data stays on your PC unless Enterprise cloud sync is enabled.</p>
            <p>Contact: <a href="mailto:support@mtglory.com">support@mtglory.com</a></p>
          </div>
        </div>
      </main>
    </>
  );
}

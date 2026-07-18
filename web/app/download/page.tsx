import Image from "next/image";
import Link from "next/link";
import { getDownloadInfo } from "@/lib/config";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteHeader } from "@/components/SiteHeader";

export default function DownloadPage() {
  const info = getDownloadInfo();
  return (
    <>
      <SiteHeader active="/download" />
      <section className="hero hero-compact">
        <div className="hero-inner">
          <h1>Download Glory Desk</h1>
          <p className="hero-lead">
            Windows installer · Version {info.version} · ~{info.sizeMb} MB
          </p>
        </div>
      </section>
      <main>
        <div className="container container-narrow">
          <div className="card text-center">
            <h2>Windows 64-bit</h2>
            <p className="text-muted">{info.fileName}</p>
            <p>
              <a className="btn btn-lg btn-navy" href={info.downloadUrl}>
                Download from GitHub Releases
              </a>
            </p>
            <div className="screenshot-strip">
              {["dashboard.png", "pos.png", "inventory.png"].map((file, i) => (
                <figure className="screenshot-card" key={file}>
                  <Image src={`/images/screenshots/${file}`} alt="" width={640} height={360} style={{ width: "100%", height: "auto" }} />
                  <figcaption>{["Dashboard", "POS", "Inventory"][i]}</figcaption>
                </figure>
              ))}
            </div>
          </div>
          <div className="card">
            <h2>After installation</h2>
            <ol className="steps">
              <li>Open <strong>Glory Desk</strong> from the Start menu.</li>
              <li>Go to <strong>License</strong> and copy your <strong>Hardware ID</strong>.</li>
              <li><Link href="/activate">Request or activate</Link> your license tier.</li>
            </ol>
          </div>
        </div>
      </main>
      <SiteFooter />
    </>
  );
}

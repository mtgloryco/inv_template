"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { SiteHeader } from "@/components/SiteHeader";

type License = {
  tier: string;
  company: string;
  hardwareId: string;
  expiry: string | null;
  licenseKey: string | null;
};

export default function DashboardPage() {
  const router = useRouter();
  const [licenses, setLicenses] = useState<License[] | null>(null);

  useEffect(() => {
    const token = localStorage.getItem("gd_token");
    if (!token) {
      router.push("/account/login");
      return;
    }
    fetch("/api/account/licenses", {
      headers: { Authorization: `Bearer ${token}` },
    }).then(async (res) => {
      if (res.status === 401) {
        localStorage.removeItem("gd_token");
        router.push("/account/login");
        return;
      }
      setLicenses(await res.json());
    });
  }, [router]);

  return (
    <>
      <SiteHeader />
      <main>
        <div className="container">
          <h1>Your licenses</h1>
          {!licenses ? (
            <p>Loading…</p>
          ) : licenses.length === 0 ? (
            <div className="alert alert-info">
              No issued licenses yet. <Link href="/activate">Submit a license request</Link>.
            </div>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Tier</th><th>Company</th><th>Hardware ID</th><th>Expires</th><th>Key</th>
                  </tr>
                </thead>
                <tbody>
                  {licenses.map((l, i) => (
                    <tr key={i}>
                      <td><strong>{l.tier}</strong></td>
                      <td>{l.company}</td>
                      <td><code style={{ fontSize: "0.75rem" }}>{l.hardwareId}</code></td>
                      <td>{l.expiry ? String(l.expiry).slice(0, 10) : "—"}</td>
                      <td>
                        {l.licenseKey ? (
                          <code style={{ fontSize: "0.65rem", wordBreak: "break-all" }}>{l.licenseKey}</code>
                        ) : (
                          "—"
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <p style={{ marginTop: "1rem" }}>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => {
                localStorage.removeItem("gd_token");
                router.push("/account/login");
              }}
            >
              Sign out
            </button>
          </p>
        </div>
      </main>
    </>
  );
}

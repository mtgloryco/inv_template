"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { CompanyLogoMark } from "@/components/CompanyLogo";

type RequestRow = {
  id: string;
  company: string;
  email: string;
  tier: string;
  hardwareId: string;
  createdAt: string;
  status: string;
  licenseKey?: string | null;
  expiry?: string | null;
};

type ManualForm = {
  email: string;
  company: string;
  hardwareId: string;
  tier: string;
  validYears: number;
};

const emptyManual: ManualForm = {
  email: "",
  company: "",
  hardwareId: "",
  tier: "Pro",
  validYears: 1,
};

export function AdminDashboard() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [token, setToken] = useState("");
  const [authed, setAuthed] = useState(false);
  const [loginError, setLoginError] = useState("");
  const [filter, setFilter] = useState("pending");
  const [rows, setRows] = useState<RequestRow[]>([]);
  const [msg, setMsg] = useState("");
  const [showManual, setShowManual] = useState(false);
  const [manual, setManual] = useState<ManualForm>(emptyManual);
  const [issueYears, setIssueYears] = useState(1);

  function authHeaders(): HeadersInit {
    return token ? { Authorization: `Bearer ${token}` } : {};
  }

  useEffect(() => {
    const saved = sessionStorage.getItem("gd_admin_token");
    if (saved) {
      setToken(saved);
      setAuthed(true);
    }
  }, []);

  useEffect(() => {
    if (authed && token) loadRows(filter, token);
  }, [authed, filter, token]);

  async function adminLogin(e: React.FormEvent) {
    e.preventDefault();
    setLoginError("");
    const res = await fetch("/api/auth/admin/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
    const data = await res.json();
    if (!res.ok || !data.token) {
      setLoginError(data.error ?? "Invalid email or password.");
      return;
    }
    sessionStorage.setItem("gd_admin_token", data.token);
    setToken(data.token);
    setAuthed(true);
  }

  async function loadRows(status: string, authToken: string) {
    const url = status
      ? `/api/admin/license-requests?status=${status}`
      : "/api/admin/license-requests";
    const res = await fetch(url, { headers: { Authorization: `Bearer ${authToken}` } });
    if (res.status === 401) {
      sessionStorage.removeItem("gd_admin_token");
      setAuthed(false);
      setToken("");
      return;
    }
    setRows(await res.json());
  }

  async function issue(id: string) {
    if (!confirm(`Approve and generate RSA license key (${issueYears} year(s))?`)) return;
    const res = await fetch(`/api/admin/license-requests/${id}/issue`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: JSON.stringify({ validYears: issueYears }),
    });
    const data = await res.json();
    setMsg(data.message ?? "");
    if (data.licenseKey) {
      window.prompt("License key generated — copy for customer:", data.licenseKey);
    }
    loadRows(filter, token);
  }

  async function reject(id: string) {
    if (!confirm("Reject this request?")) return;
    await fetch(`/api/admin/license-requests/${id}/reject`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: JSON.stringify({ notes: "Rejected by admin" }),
    });
    loadRows(filter, token);
  }

  async function submitManual(e: React.FormEvent) {
    e.preventDefault();
    const res = await fetch("/api/admin/license-requests/manual", {
      method: "POST",
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: JSON.stringify(manual),
    });
    const data = await res.json();
    if (data.success && data.licenseKey) {
      window.prompt("Manual license generated — copy key:", data.licenseKey);
      setShowManual(false);
      setManual(emptyManual);
      setFilter("");
      loadRows("", token);
    } else {
      alert(data.message ?? "Failed");
    }
  }

  function logout() {
    sessionStorage.removeItem("gd_admin_token");
    setAuthed(false);
    setToken("");
    setPassword("");
  }

  if (!authed) {
    return (
      <div className="container container-narrow">
        <div className="card" style={{ marginTop: "3rem" }}>
          <h2>MT GLORY CO — License admin</h2>
          <p className="text-muted">Sign in with your admin email and password.</p>
          <form onSubmit={adminLogin}>
            <label htmlFor="email">Admin email</label>
            <input
              id="email"
              type="email"
              autoComplete="username"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
            {loginError && <p className="alert alert-error">{loginError}</p>}
            <button className="btn btn-navy" type="submit">
              Sign in
            </button>
          </form>
        </div>
      </div>
    );
  }

  return (
    <div className="admin-layout" style={{ display: "grid" }}>
      <aside className="admin-sidebar">
        <CompanyLogoMark size={48} />
        <h2 style={{ marginTop: "1rem" }}>Glory Desk Admin</h2>
        <nav>
          <a href="#" onClick={(e) => { e.preventDefault(); setFilter("pending"); }}>Pending requests</a>
          <a href="#" onClick={(e) => { e.preventDefault(); setFilter("issued"); }}>Issued</a>
          <a href="#" onClick={(e) => { e.preventDefault(); setFilter(""); }}>All</a>
          <a href="#" onClick={(e) => { e.preventDefault(); setShowManual(true); }}>Manual issue</a>
          <Link href="/" style={{ marginTop: "2rem", display: "block" }}>← Public site</Link>
          <button
            type="button"
            className="btn btn-outline btn-sm"
            style={{ marginTop: "1rem" }}
            onClick={logout}
          >
            Log out
          </button>
        </nav>
      </aside>
      <div className="admin-main">
        <h1>License activations</h1>
        <p className="text-muted">
          Approve customer requests → generate signed key → email customer (if SMTP configured).
        </p>

        <div style={{ marginBottom: "1rem", display: "flex", gap: "1rem", alignItems: "center" }}>
          <label>
            Default validity (years):{" "}
            <input
              type="number"
              min={1}
              max={10}
              value={issueYears}
              onChange={(e) => setIssueYears(Number(e.target.value))}
              style={{ width: 60, marginBottom: 0 }}
            />
          </label>
          <button className="btn btn-sm btn-navy" type="button" onClick={() => setShowManual(true)}>
            + Manual issue
          </button>
        </div>

        {msg && <p className="alert alert-success">{msg}</p>}

        {showManual && (
          <div className="card" style={{ marginBottom: "1.5rem" }}>
            <h2>Manual license (assign directly)</h2>
            <p className="text-muted">Same as old admin — issue a key without a prior request.</p>
            <form onSubmit={submitManual}>
              <div className="form-row">
                <div>
                  <label>Customer email</label>
                  <input
                    required
                    type="email"
                    value={manual.email}
                    onChange={(e) => setManual({ ...manual, email: e.target.value })}
                  />
                </div>
                <div>
                  <label>Company / name</label>
                  <input
                    required
                    value={manual.company}
                    onChange={(e) => setManual({ ...manual, company: e.target.value })}
                  />
                </div>
              </div>
              <label>Hardware ID (from desktop app)</label>
              <textarea
                required
                rows={2}
                value={manual.hardwareId}
                onChange={(e) => setManual({ ...manual, hardwareId: e.target.value })}
              />
              <div className="form-row">
                <div>
                  <label>Tier</label>
                  <select
                    value={manual.tier}
                    onChange={(e) => setManual({ ...manual, tier: e.target.value })}
                  >
                    <option>Basic</option>
                    <option>Medium</option>
                    <option>Pro</option>
                    <option>Enterprise</option>
                  </select>
                </div>
                <div>
                  <label>Valid years</label>
                  <input
                    type="number"
                    min={1}
                    max={10}
                    value={manual.validYears}
                    onChange={(e) => setManual({ ...manual, validYears: Number(e.target.value) })}
                  />
                </div>
              </div>
              <button className="btn btn-navy" type="submit">Generate &amp; assign key</button>{" "}
              <button className="btn btn-outline btn-sm" type="button" onClick={() => setShowManual(false)}>
                Cancel
              </button>
            </form>
          </div>
        )}

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Date</th>
                <th>Company</th>
                <th>Email</th>
                <th>Tier</th>
                <th>Hardware ID</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 ? (
                <tr><td colSpan={7}>No records.</td></tr>
              ) : (
                rows.map((r) => (
                  <tr key={r.id}>
                    <td>{r.createdAt?.slice(0, 10)}</td>
                    <td>{r.company}</td>
                    <td>{r.email}</td>
                    <td><strong>{r.tier}</strong></td>
                    <td>
                      <code style={{ fontSize: "0.65rem" }}>{r.hardwareId?.slice(0, 16)}…</code>
                    </td>
                    <td className={`status-${r.status}`}>{r.status}</td>
                    <td>
                      {r.status === "pending" && (
                        <>
                          <button className="btn btn-sm" type="button" onClick={() => issue(r.id)}>
                            Approve
                          </button>{" "}
                          <button className="btn btn-sm btn-outline" type="button" onClick={() => reject(r.id)}>
                            Reject
                          </button>
                        </>
                      )}
                      {r.status === "issued" && r.licenseKey && (
                        <button
                          className="btn btn-sm btn-outline"
                          type="button"
                          onClick={() => window.prompt("License key:", r.licenseKey!)}
                        >
                          Copy key
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

"use client";

import { FormEvent, useState } from "react";
import { useSearchParams } from "next/navigation";

export function ActivateForm() {
  const params = useSearchParams();
  const [tier, setTier] = useState(params.get("tier") ?? "Pro");
  const [message, setMessage] = useState("");
  const [error, setError] = useState(false);

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setMessage("Submitting…");
    setError(false);
    const form = e.currentTarget;
    const data = Object.fromEntries(new FormData(form)) as Record<string, string>;
    const res = await fetch("/api/license/request", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: data.email,
        company: data.company,
        tier: data.tier,
        hardwareId: data.hardwareId,
      }),
    });
    const body = await res.json();
    setError(!res.ok);
    setMessage(body.message ?? (res.ok ? "Request received." : "Request failed."));
    if (res.ok) form.reset();
  }

  return (
    <>
      <form onSubmit={onSubmit}>
        <label htmlFor="email">Business email</label>
        <input id="email" name="email" type="email" required />
        <label htmlFor="company">Company / shop name</label>
        <input id="company" name="company" required />
        <label htmlFor="tier">License tier</label>
        <select id="tier" name="tier" value={tier} onChange={(e) => setTier(e.target.value)} required>
          <option value="Basic">Basic — 150,000 RWF/yr</option>
          <option value="Medium">Medium — 350,000 RWF/yr</option>
          <option value="Pro">Pro — 650,000 RWF/yr</option>
          <option value="Enterprise">Enterprise — Contact sales</option>
        </select>
        <label htmlFor="hardwareId">Hardware ID (from desktop app)</label>
        <textarea id="hardwareId" name="hardwareId" rows={3} required />
        <button className="btn btn-navy" type="submit">
          Submit license request
        </button>
      </form>
      {message && (
        <p className={error ? "alert alert-error" : "alert alert-success"} style={{ marginTop: "1rem" }}>
          {message}
        </p>
      )}
    </>
  );
}

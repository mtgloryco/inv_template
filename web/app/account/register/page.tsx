"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { SiteHeader } from "@/components/SiteHeader";

export default function RegisterPage() {
  const router = useRouter();
  const [error, setError] = useState("");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = Object.fromEntries(new FormData(e.currentTarget));
    const res = await fetch("/api/auth/register", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        email: data.email,
        password: data.password,
        organizationName: data.org,
      }),
    });
    const body = await res.json();
    if (res.ok && body.token) {
      localStorage.setItem("gd_token", body.token);
      router.push("/account/dashboard");
    } else {
      setError(body.error ?? "Registration failed.");
    }
  }

  return (
    <>
      <SiteHeader />
      <main>
        <div className="container container-narrow">
          <div className="card" style={{ maxWidth: 420, margin: "2rem auto" }}>
            <h2>Create account</h2>
            <form onSubmit={onSubmit}>
              <label htmlFor="org">Organization name</label>
              <input id="org" name="org" required />
              <label htmlFor="email">Email</label>
              <input id="email" name="email" type="email" required />
              <label htmlFor="password">Password</label>
              <input id="password" name="password" type="password" required minLength={8} />
              <button className="btn btn-navy" type="submit" style={{ width: "100%" }}>
                Create account
              </button>
            </form>
            {error && <p className="alert alert-error">{error}</p>}
            <p className="text-muted" style={{ fontSize: "0.875rem" }}>
              Already registered? <Link href="/account/login">Sign in</Link>
            </p>
          </div>
        </div>
      </main>
    </>
  );
}

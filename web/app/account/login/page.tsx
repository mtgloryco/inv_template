"use client";

import { FormEvent, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { SiteHeader } from "@/components/SiteHeader";

export default function LoginPage() {
  const router = useRouter();
  const [error, setError] = useState("");

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = Object.fromEntries(new FormData(e.currentTarget));
    const res = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email: data.email, password: data.password }),
    });
    const body = await res.json();
    if (res.ok && body.token) {
      localStorage.setItem("gd_token", body.token);
      router.push("/account/dashboard");
    } else {
      setError("Invalid email or password.");
    }
  }

  return (
    <>
      <SiteHeader />
      <main>
        <div className="container container-narrow">
          <div className="card" style={{ maxWidth: 420, margin: "2rem auto" }}>
            <h2>Sign in</h2>
            <form onSubmit={onSubmit}>
              <label htmlFor="email">Email</label>
              <input id="email" name="email" type="email" required />
              <label htmlFor="password">Password</label>
              <input id="password" name="password" type="password" required />
              <button className="btn btn-navy" type="submit" style={{ width: "100%" }}>
                Sign in
              </button>
            </form>
            {error && <p className="alert alert-error">{error}</p>}
            <p className="text-muted" style={{ fontSize: "0.875rem" }}>
              No account? <Link href="/account/register">Register</Link>
            </p>
          </div>
        </div>
      </main>
    </>
  );
}

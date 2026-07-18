import { NextResponse } from "next/server";
import { loginUser } from "@/lib/users";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const result = await loginUser({ email: body.email, password: body.password });
    if (!result) {
      return NextResponse.json({ error: "Invalid credentials" }, { status: 401 });
    }
    return NextResponse.json(result);
  } catch (e) {
    const message = e instanceof Error ? e.message : "Login failed.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

export const dynamic = "force-dynamic";

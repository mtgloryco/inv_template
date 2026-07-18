import { NextResponse } from "next/server";
import { loginAdmin } from "@/lib/users";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const result = await loginAdmin({
      email: body.email,
      password: body.password,
    });
    if (!result) {
      return NextResponse.json({ error: "Invalid admin credentials" }, { status: 401 });
    }
    return NextResponse.json({
      token: result.token,
      user: { email: result.email, role: result.role },
    });
  } catch (e) {
    const message = e instanceof Error ? e.message : "Admin login failed.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

export const dynamic = "force-dynamic";

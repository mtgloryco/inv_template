import { NextResponse } from "next/server";
import { registerUser } from "@/lib/users";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const result = await registerUser({
      email: body.email,
      password: body.password,
      organizationName: body.organizationName,
    });
    if (!result) {
      return NextResponse.json(
        { error: "Registration failed. Email may already exist." },
        { status: 400 }
      );
    }
    return NextResponse.json(result);
  } catch (e) {
    const message = e instanceof Error ? e.message : "Registration failed.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

export const dynamic = "force-dynamic";

import { NextResponse } from "next/server";
import { getBearerToken, verifyToken } from "@/lib/auth";
import { getLicensesForEmail } from "@/lib/license-requests";

export async function GET(request: Request) {
  const token = getBearerToken(request.headers.get("authorization"));
  if (!token) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const user = await verifyToken(token);
    const licenses = await getLicensesForEmail(user.email);
    return NextResponse.json(licenses);
  } catch {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }
}

export const dynamic = "force-dynamic";

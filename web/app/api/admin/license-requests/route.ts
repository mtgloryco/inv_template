import { NextResponse } from "next/server";
import { authorizeAdmin } from "@/lib/auth";
import { listLicenseRequests } from "@/lib/license-requests";

export async function GET(request: Request) {
  if (!(await authorizeAdmin(request))) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const { searchParams } = new URL(request.url);
    const status = searchParams.get("status");
    const items = await listLicenseRequests(status);
    return NextResponse.json(items);
  } catch (e) {
    const message = e instanceof Error ? e.message : "Failed to list requests.";
    return NextResponse.json({ error: message }, { status: 500 });
  }
}

export const dynamic = "force-dynamic";

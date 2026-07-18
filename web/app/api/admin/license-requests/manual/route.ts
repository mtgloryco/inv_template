import { NextResponse } from "next/server";
import { authorizeAdmin } from "@/lib/auth";
import { manualIssueLicense } from "@/lib/license-requests";

export async function POST(request: Request) {
  if (!(await authorizeAdmin(request))) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const body = await request.json();
    const result = await manualIssueLicense({
      email: body.email,
      company: body.company ?? body.name,
      hardwareId: body.hardwareId,
      tier: body.tier,
      validYears: body.validYears ?? body.durationDays ? Math.ceil(body.durationDays / 365) : 1,
    });
    return NextResponse.json(result, { status: result.success ? 201 : 400 });
  } catch (e) {
    const message = e instanceof Error ? e.message : "Manual issue failed.";
    return NextResponse.json({ success: false, message }, { status: 500 });
  }
}

export const dynamic = "force-dynamic";

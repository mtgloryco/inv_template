import { NextResponse } from "next/server";
import { submitLicenseRequest } from "@/lib/license-requests";

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const result = await submitLicenseRequest({
      email: body.email,
      company: body.company,
      tier: body.tier,
      hardwareId: body.hardwareId,
    });
    return NextResponse.json(
      { success: result.success, message: result.message },
      { status: result.success ? 200 : 400 }
    );
  } catch (e) {
    const message = e instanceof Error ? e.message : "Request failed.";
    return NextResponse.json({ success: false, message }, { status: 500 });
  }
}

export const dynamic = "force-dynamic";

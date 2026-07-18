import { NextResponse } from "next/server";
import { authorizeAdmin } from "@/lib/auth";
import { rejectLicenseRequest } from "@/lib/license-requests";

export async function POST(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  if (!(await authorizeAdmin(request))) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }
  try {
    const { id } = await params;
    const body = await request.json().catch(() => ({}));
    const result = await rejectLicenseRequest(id, body.notes);
    return NextResponse.json(result, { status: result.success ? 200 : 400 });
  } catch (e) {
    const message = e instanceof Error ? e.message : "Reject failed.";
    return NextResponse.json({ success: false, message }, { status: 500 });
  }
}

export const dynamic = "force-dynamic";

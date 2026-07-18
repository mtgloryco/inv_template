import { NextResponse } from "next/server";

export async function GET() {
  return NextResponse.json({
    status: "healthy",
    service: "Glory Desk Web",
    utc: new Date().toISOString(),
  });
}

export const dynamic = "force-dynamic";

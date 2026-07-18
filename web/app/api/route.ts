import { NextResponse } from "next/server";
import { getDownloadInfo, pricing } from "@/lib/config";

export async function GET() {
  return NextResponse.json({
    service: "Glory Desk Web",
    product: "Glory Desk",
    publisher: "MT GLORY CO",
    activate: "/activate",
    download: "/download",
    account: "/account/login",
    admin: "/admin",
    health: "/api/health",
  });
}

export const dynamic = "force-dynamic";

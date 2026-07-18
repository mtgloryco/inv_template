import { NextResponse } from "next/server";
import { getDownloadInfo } from "@/lib/config";

export async function GET() {
  return NextResponse.json(getDownloadInfo());
}

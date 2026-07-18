import { NextResponse } from "next/server";
import { pricing } from "@/lib/config";

export async function GET() {
  return NextResponse.json({
    ...pricing,
    note: "Prices exclude VAT. Enterprise includes cloud sync and priority support.",
  });
}

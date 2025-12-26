import { NextResponse } from "next/server";

export async function GET() {
    // This is the "Remote Configuration" that the Desktop App will fetch.
    // In the future, you can change 'updateUrl' here to point to S3, Azure, or anywhere else.
    // The Desktop App will always check here first.

    const config = {
        // Current location of update files (Releases folder)
        // For now, we point to the same Vercel deployment's /updates folder
        updateUrl: "https://ims-lilac-beta.vercel.app/updates",

        // You can add other feature flags here
        maintenanceMode: false,
        minVersionArgs: "1.0.0"
    };

    return NextResponse.json(config);
}

import { NextResponse } from 'next/server';

export async function GET() {
    // In a real scenario, this logic would check a database or file system 
    // to find the latest version. For now, we return a hardcoded response 
    // that can be manually updated when a new release is deployed.

    const latestRelease = {
        version: "1.0.0", // Update this when releasing new versions
        releaseNotesUrl: "http://localhost:3000/releases/latest", // Link to release notes page
        // Velopack might look for specific file structures, but this endpoint 
        // helps our custom UI check before handing off to Velopack.
        downloadUrl: "http://localhost:3000/updates/"
    };

    return NextResponse.json(latestRelease);
}

import { NextResponse } from 'next/server';
import clientPromise from '@/lib/mongodb';

const DB_NAME = 'license-manager';
const COLLECTION = 'downloads';

export async function GET() {
    try {
        const client = await clientPromise;
        const db = client.db(DB_NAME);

        // Fetch the most recent featured download
        const latestVersion = await db.collection(COLLECTION)
            .findOne({ isFeatured: true }, { sort: { releaseDate: -1 } });

        if (!latestVersion) {
            return NextResponse.json({ error: 'No featured version found' }, { status: 404 });
        }

        // Return only the essential info for other apps to consume
        return NextResponse.json({
            version: latestVersion.version,
            downloadLink: latestVersion.link,
            os: latestVersion.os,
            releaseDate: latestVersion.releaseDate,
            description: latestVersion.description
        });
    } catch (e) {
        console.error('Public version fetch error:', e);
        return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
    }
}

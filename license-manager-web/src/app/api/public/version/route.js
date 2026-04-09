import { NextResponse } from 'next/server';
import clientPromise from '@/lib/mongodb';

const DB_NAME = 'license-manager';
const COLLECTION = 'downloads';

export async function GET() {
    try {
        const client = await clientPromise;
        const db = client.db(DB_NAME);

        // Fetch all featured downloads
        const versions = await db.collection(COLLECTION)
            .find({ isFeatured: true })
            .sort({ releaseDate: -1 })
            .toArray();

        if (versions.length === 0) {
            return NextResponse.json({ error: 'No featured versions found' }, { status: 404 });
        }

        // Return the essential info for all featured versions
        const result = versions.map(v => ({
            version: v.version,
            downloadLink: v.link,
            os: v.os,
            type: v.type,
            releaseDate: v.releaseDate,
            description: v.description
        }));

        return NextResponse.json(result);
    } catch (e) {
        console.error('Public version fetch error:', e);
        return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
    }
}

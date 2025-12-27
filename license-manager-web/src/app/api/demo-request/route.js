import { NextResponse } from 'next/server';
import clientPromise from '@/lib/mongodb';
import { signLicense } from '@/lib/license-crypto';
import { v4 as uuidv4 } from 'uuid';

export async function POST(request) {
    try {
        const { name, email, hardwareId, duration, proofImage } = await request.json();

        // 1. Validation
        if (!name || !email || !hardwareId || !duration) {
            return NextResponse.json({ error: 'Missing required fields.' }, { status: 400 });
        }

        const validDurations = ['6h', '1d', '2d'];
        if (!validDurations.includes(duration)) {
            return NextResponse.json({ error: 'Invalid duration.' }, { status: 400 });
        }

        // 2. Automated Path: 6 Hours (Free)
        if (duration === '6h') {
            const client = await clientPromise;
            const db = client.db('license_manager');

            // Check if this HWID has already used a 6h trial
            const existingTrial = await db.collection('demo_requests').findOne({
                hardwareId: hardwareId,
                duration: '6h'
            });

            if (existingTrial) {
                return NextResponse.json({
                    error: 'Free trial already used on this device. Please purchase a daily pass.'
                }, { status: 403 });
            }

            // Generate License Immediately
            const expiry = new Date();
            expiry.setHours(expiry.getHours() + 6);

            const payload = {
                licenseId: uuidv4(),
                hardwareId,
                issuedTo: name, // Desktop app expects issuedTo
                issuedAt: new Date().toISOString(),
                expiry: expiry.toISOString(),
                tier: 'Enterprise' // Full access for demo
            };

            const licenseKey = signLicense(payload);

            // Log the "Active" demo in DB for record keeping
            await db.collection('demo_requests').insertOne({
                name,
                email,
                hardwareId,
                duration,
                status: 'Sent', // Auto-sent
                issuedKey: licenseKey,
                paymentProof: null,
                createdAt: new Date()
            });

            return NextResponse.json({
                success: true,
                licenseKey,
                message: '6-Hour Demo Activated. Copy your key below.'
            });
        }

        // 3. Manual Path: 1 Day / 2 Days (Paid)
        if (duration === '1d' || duration === '2d') {
            if (!proofImage) {
                return NextResponse.json({ error: 'Payment proof is required for paid demos.' }, { status: 400 });
            }

            const client = await clientPromise;
            const db = client.db('license_manager');

            // Store request as Pending
            await db.collection('demo_requests').insertOne({
                name,
                email,
                hardwareId,
                duration,
                status: 'Pending',
                paymentProof: proofImage, // Base64 string
                createdAt: new Date()
            });

            return NextResponse.json({
                success: true,
                message: 'Request received. We will verify your payment and email the license key shortly.'
            });
        }

        return NextResponse.json({ error: 'Unknown flow.' }, { status: 400 });

    } catch (error) {
        console.error('Demo Request Error:', error);
        return NextResponse.json({ error: error.message }, { status: 500 });
    }
}

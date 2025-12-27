import { NextResponse } from 'next/server';
import clientPromise from '@/lib/mongodb';
import { getAuthUser } from '@/lib/auth';
import { signLicense } from '@/lib/license-crypto';
import { ObjectId } from 'mongodb';
import { LICENSE_CONFIG } from '@/lib/config';
import { sendLicenseActivationEmail } from '@/lib/email';

export async function GET(request) {
    const user = await getAuthUser(request);
    if (!user || user.role !== 'admin') {
        return NextResponse.json({ error: 'Forbidden' }, { status: 403 });
    }

    const client = await clientPromise;
    const db = client.db('license_manager');

    const licenses = await db.collection('licenses')
        .aggregate([
            {
                $addFields: {
                    user_oid: { $toObjectId: '$userId' }
                }
            },
            {
                $lookup: {
                    from: 'users',
                    localField: 'user_oid',
                    foreignField: '_id',
                    as: 'userDetails'
                }
            },
            {
                $unwind: {
                    path: '$userDetails',
                    preserveNullAndEmptyArrays: true // Keep license even if user not found
                }
            },
            {
                $project: {
                    licenseId: 1,
                    licenseKey: 1,
                    planType: 1,
                    status: 1,
                    expirationDate: 1,
                    hardwareId: 1,
                    tier: 1,
                    paymentProof: 1,
                    createdAt: 1,
                    'userDetails.email': 1,
                    'userDetails.username': 1,
                    manualIssuedTo: 1
                }
            }
        ])
        .sort({ createdAt: -1 })
        .toArray();

    return NextResponse.json(licenses);
}

// Manual License Generation by Admin
export async function POST(request) {
    const user = await getAuthUser(request);
    if (!user || user.role !== 'admin') {
        return NextResponse.json({ error: 'Forbidden' }, { status: 403 });
    }

    try {
        const { name, hardwareId, tier, durationDays } = await request.json();

        if (!name || !hardwareId || !tier) {
            return NextResponse.json({ error: 'Name, Hardware ID, and Tier are required.' }, { status: 400 });
        }

        const issuedAt = new Date();
        const expirationDate = new Date();
        expirationDate.setDate(expirationDate.getDate() + (parseInt(durationDays) || 365));

        const licenseId = require('uuid').v4(); // Ensure uuid is imported or use crypto.randomUUID

        const payload = {
            licenseId,
            hardwareId,
            issuedTo: name,
            issuedAt: issuedAt.toISOString(),
            expiry: expirationDate.toISOString(),
            tier
        };

        const licenseKey = signLicense(payload);

        const newLicense = {
            licenseId,
            userId: null, // No system user linked
            manualIssuedTo: name,
            hardwareId,
            tier,
            planType: tier.toLowerCase(), // approximate mapping
            status: 'Active',
            paymentProof: null,
            issuedAt,
            expirationDate,
            licenseKey,
            createdAt: new Date(),
            updatedAt: new Date()
        };

        const client = await clientPromise;
        const db = client.db('license_manager');

        await db.collection('licenses').insertOne(newLicense);

        return NextResponse.json(newLicense, { status: 201 });

    } catch (error) {
        console.error('Manual License Error:', error);
        return NextResponse.json({ error: error.message }, { status: 500 });
    }
}


export async function PATCH(request) {
    const user = await getAuthUser(request);
    if (!user || user.role !== 'admin') {
        return NextResponse.json({ error: 'Forbidden' }, { status: 403 });
    }

    try {
        const { id, action, status, extensionDays } = await request.json();
        const client = await clientPromise;
        const db = client.db('license_manager');

        const existingLicense = await db.collection('licenses').findOne({ _id: new ObjectId(id) });
        if (!existingLicense) return NextResponse.json({ error: 'License not found' }, { status: 404 });

        let updateData = { updatedAt: new Date() };

        if (action === 'approve') {
            const plan = LICENSE_CONFIG.plans[existingLicense.planType];
            const tierName = plan ? plan.name : 'Freemium Starter';
            const durationDays = plan ? plan.durationDays : 30;
            const issuedAt = new Date().toISOString();
            const expirationDate = new Date();
            expirationDate.setDate(expirationDate.getDate() + durationDays);
            const expiry = expirationDate.toISOString();

            const licenseUser = await db.collection('users').findOne({ _id: new ObjectId(existingLicense.userId) });
            if (!licenseUser) return NextResponse.json({ error: 'Associated user not found' }, { status: 404 });

            const payload = {
                licenseId: existingLicense.licenseId,
                hardwareId: existingLicense.hardwareId,
                issuedTo: licenseUser.email,
                issuedAt,
                expiry,
                tier: tierName
            };

            const signedLicenseKey = signLicense(payload);

            updateData = {
                ...updateData,
                status: 'Active',
                licenseKey: signedLicenseKey,
                issuedAt: new Date(issuedAt),
                expirationDate: new Date(expiry),
                tier: tierName,
                paymentProof: null // Clear storage space after approval
            };
        } else if (action === 'update_status') {
            updateData.status = status;
        } else if (action === 'extend') {
            const currentExpiry = new Date(existingLicense.expirationDate);
            const newExpiry = new Date(currentExpiry.getTime() + (extensionDays || 30) * 24 * 60 * 60 * 1000);
            const licenseUser = await db.collection('users').findOne({ _id: new ObjectId(existingLicense.userId) });
            if (!licenseUser) return NextResponse.json({ error: 'Associated user not found' }, { status: 404 });
            const plan = LICENSE_CONFIG.plans[existingLicense.planType];
            const tierName = plan ? plan.name : existingLicense.tier;

            const payload = {
                licenseId: existingLicense.licenseId,
                hardwareId: existingLicense.hardwareId,
                issuedTo: licenseUser.email,
                issuedAt: existingLicense.issuedAt.toISOString ? existingLicense.issuedAt.toISOString() : new Date(existingLicense.issuedAt).toISOString(),
                expiry: newExpiry.toISOString(),
                tier: tierName
            };

            const newSignedKey = signLicense(payload);
            updateData.licenseKey = newSignedKey;
            updateData.expirationDate = newExpiry;
            updateData.status = 'Active';
            updateData.tier = tierName;
        }

        await db.collection('licenses').updateOne(
            { _id: new ObjectId(id) },
            { $set: updateData }
        );

        // Notify User (Non-blocking)
        if (action === 'approve') {
            const updatedLic = await db.collection('licenses').findOne({ _id: new ObjectId(id) });
            const licenseUser = await db.collection('users').findOne({ _id: new ObjectId(existingLicense.userId) });
            if (licenseUser && updatedLic && updatedLic.licenseKey) {
                sendLicenseActivationEmail(
                    licenseUser.email,
                    licenseUser.username,
                    updatedLic.licenseKey,
                    updatedLic.tier
                ).catch(err => console.error('Email trigger error:', err));
            }
        }

        return NextResponse.json({ message: 'License updated successfully' });
    } catch (error) {
        console.error('Admin Update Error:', error);
        return NextResponse.json({ error: error.message }, { status: 500 });
    }
}

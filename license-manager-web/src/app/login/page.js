'use client';
import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

export default function LoginPage() {
    const router = useRouter();
    useEffect(() => {
        router.push('/?auth=login');
    }, [router]);
    return <div className="container" style={{ textAlign: 'center', padding: '5rem' }}>Redirecting to login...</div>;
}

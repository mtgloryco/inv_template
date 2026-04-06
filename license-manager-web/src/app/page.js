'use client';

import { useState, useEffect } from 'react';
import Link from 'next/link';
import {
    Shield, Smartphone, Zap, CloudOff, Package, Check,
    ArrowRight, Menu, X, Laptop, Database, RefreshCw,
    Mail, Send, Box, Calendar, Clock, Download
} from 'lucide-react';
import { LICENSE_CONFIG } from '@/lib/config';
import LoginModal from '@/components/LoginModal';
import RegisterModal from '@/components/RegisterModal';
import DemoRequestModal from '@/components/DemoRequestModal';

export default function LandingPage() {
    const [isMenuOpen, setIsMenuOpen] = useState(false);
    const [showLogin, setShowLogin] = useState(false);

    const [showRegister, setShowRegister] = useState(false);
    const [showDemo, setShowDemo] = useState(false);
    const [scrolled, setScrolled] = useState(false);

    const [plans, setPlans] = useState([]);

    const [contactName, setContactName] = useState('');
    const [contactEmail, setContactEmail] = useState('');
    const [contactMsg, setContactMsg] = useState('');
    const [sending, setSending] = useState(false);
    const [sent, setSent] = useState(false);

    // Downloads State
    const [downloads, setDownloads] = useState([]);
    const [showHistory, setShowHistory] = useState(false);

    useEffect(() => {
        const handleScroll = () => setScrolled(window.scrollY > 50);
        window.addEventListener('scroll', handleScroll);

        const params = new URLSearchParams(window.location.search);
        if (params.get('auth') === 'login') setShowLogin(true);
        if (params.get('auth') === 'register') setShowRegister(true);

        // Fetch dynamic plans
        fetch('/api/plans')
            .then(res => res.json())
            .then(data => {
                if (Array.isArray(data)) setPlans(data);
            })
            .catch(err => console.error('Failed to load plans:', err));

        // Fetch downloads
        fetch('/api/downloads')
            .then(res => res.json())
            .then(data => {
                if (Array.isArray(data)) setDownloads(data);
            })
            .catch(err => console.error('Failed to load downloads:', err));

        return () => window.removeEventListener('scroll', handleScroll);
    }, []);

    const toggleMenu = () => setIsMenuOpen(!isMenuOpen);
    const openLogin = () => { setShowLogin(true); setShowRegister(false); setIsMenuOpen(false); };
    const openRegister = () => { setShowRegister(true); setShowLogin(false); setIsMenuOpen(false); };

    // Fallback static plans if API fails or while loading (optional, but good for UX)
    const staticPlans = Object.entries(LICENSE_CONFIG.plans).map(([key, plan]) => ({
        id: key,
        ...plan,
        features: plan.tier === 'Enterprise'
            ? ['Real Cloud Sync', 'Global Audit Trail', 'Auto-Reorder Workflows', 'Custom API Integrations', 'Priority Support']
            : plan.tier === 'Pro'
                ? ['Intelligent Forecasting', 'Advanced BI Analytics', 'Full Reporting Engine', 'Kitting & Bundles', 'Unlimited Locations']
                : plan.tier === 'Medium'
                    ? ['Smart POS & Receipts', 'Supplier Procurement', 'Expiry Tracking', 'Multi-location (Up to 3)', 'Returns Management']
                    : ['Inventory Notebook', 'Stock Tracking', 'Local Backups', 'Max 50 Products', 'Single Location Only']
    }));

    const displayPlans = plans.length > 0 ? plans : staticPlans;
    const featuredDownloads = downloads.filter(d => d.isFeatured);
    const hasDownloads = downloads.length > 0;

    const handleContact = async (e) => {
        e.preventDefault();
        setSending(true);
        try {
            const res = await fetch('/api/contacts', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name: contactName, email: contactEmail, message: contactMsg })
            });
            if (res.ok) {
                setSent(true);
                setContactName(''); setContactEmail(''); setContactMsg('');
                setTimeout(() => setSent(false), 5000);
            }
        } catch (e) { alert('Failed to send message'); }
        finally { setSending(false); }
    };

    const handleDownload = async (download) => {
        // 1. Track download
        try {
            await fetch('/api/downloads/track', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: download._id })
            });
        } catch (e) {
            console.error('Tracking failed', e);
        }

        // 2. Open link
        window.open(download.link, '_blank');

        // 3. Optimistically update UI (optional, but nice)
        setDownloads(prev => prev.map(d =>
            d._id === download._id
                ? { ...d, downloadCount: (d.downloadCount || 0) + 1 }
                : d
        ));
    };

    return (
        <div className="landing-wrap">
            {/* Header */}
            <nav className={`nav ${scrolled ? 'nav-scrolled' : ''}`}>
                <div className="container nav-container">
                    <Link href="/" className="logo">
                        <Box size={32} color="var(--primary)" />
                        <span>IMS Manager</span>
                    </Link>

                    <ul className="nav-links desktop-menu">
                        <li><a href="#features" className="nav-item">Features</a></li>
                        <li><a href="#downloads" className="nav-item">Downloads</a></li>
                        <li><a href="#pricing" className="nav-item">Pricing</a></li>
                        <li><Link href="/about" className="nav-item">About Us</Link></li>
                        <li><button onClick={openLogin} className="nav-btn">Login</button></li>
                        <li><button onClick={openRegister} className="btn btn-primary nav-cta">Get Started</button></li>
                    </ul>

                    <button className="mobile-toggle" onClick={toggleMenu}>
                        {isMenuOpen ? <X size={28} /> : <Menu size={28} />}
                    </button>
                </div>

                {isMenuOpen && (
                    <div className="mobile-menu">
                        <a href="#features" onClick={toggleMenu}>Features</a>
                        <a href="#downloads" onClick={toggleMenu}>Downloads</a>
                        <a href="#pricing" onClick={toggleMenu}>Pricing</a>
                        <Link href="/about" onClick={toggleMenu}>About Us</Link>
                        <button onClick={openLogin} className="btn btn-secondary">Login</button>
                        <button onClick={openRegister} className="btn btn-primary">Get Started</button>
                    </div>
                )}
            </nav>

            {/* Hero Section - Focused on IMS */}
            <header className="hero">
                <div className="container hero-content">
                    <div className="badge">PRODUCT VERSION 2.0.0-ENTERPRISE</div>
                    <h1 className="hero-title">
                        Advanced Inventory <br />
                        <span className="text-gradient">Management System.</span>
                    </h1>
                    <p className="hero-desc">
                        The ultimate offline-first software for stock control.
                        Request, activate, and manage your IMS Professional licenses through our secure hub.
                    </p>
                    <div className="hero-actions">
                        <button onClick={openRegister} className="btn btn-primary btn-lg">
                            Request My License <ArrowRight size={20} />
                        </button>
                        <button onClick={() => setShowDemo(true)} className="btn btn-outline btn-lg" style={{ borderColor: '#aaa', background: 'rgba(255,255,255,0.5)' }}>
                            <Clock size={20} /> Try Demo
                        </button>
                        <button onClick={openLogin} className="btn btn-secondary btn-lg">
                            Software Login
                        </button>
                    </div>
                </div>
            </header>

            {/* Features Section - Focused on Product */}
            <section id="features" className="section bg-white">
                <div className="container">
                    <div className="section-header">
                        <h2 className="section-title">The IMS Professional Advantage</h2>
                        <p className="section-subtitle">Powerful tools designed for serious inventory management.</p>
                    </div>

                    <div className="feature-grid">
                        <FeatureBox
                            icon={<Zap size={32} color="var(--primary)" />}
                            title="RSA Activation"
                            description="Cryptographically signed licenses bound to your machine for maximum security."
                        />
                        <FeatureBox
                            icon={<Package size={32} color="#f59e0b" />}
                            title="Stock Lifecycle"
                            description="Complete control over Stock IN, Stock OUT, and real-time movement audits."
                        />
                        <FeatureBox
                            icon={<Laptop size={32} color="#10b981" />}
                            title="Native Desktop"
                            description="Native performance on Windows, Linux, and macOS without needing an internet connection."
                        />
                        <FeatureBox
                            icon={<CloudOff size={32} color="var(--secondary)" />}
                            title="Offline Reliable"
                            description="Work from anywhere. Your database lives on your machine, always accessible."
                        />
                    </div>


                </div>
            </section>

            {/* Downloads Section */}
            <section id="downloads" className="section bg-with-pattern">
                <div className="container">
                    <div className="section-header">
                        <h2 className="section-title">Download IMS Ready</h2>
                        <p className="section-subtitle">Get the native desktop application for your operating system.</p>
                    </div>

                    <div className="download-grid">
                        {featuredDownloads.length > 0 ? (
                            featuredDownloads.map(d => (
                                <div key={d._id} className="download-card">
                                    <div className={`os-icon ${d.os === 'Windows' ? 'win' : 'lin'}`}>
                                        <Box size={40} />
                                    </div>
                                    <h3>{d.type} for {d.os}</h3>
                                    <p>{d.description || `Latest version for ${d.os}`}</p>
                                    <button
                                        onClick={() => handleDownload(d)}
                                        className={`btn ${d.os === 'Windows' ? 'btn-primary' : 'btn-secondary'} btn-block`}
                                        style={{ cursor: 'pointer', border: 'none' }}
                                    >
                                        <Download size={18} /> Download v{d.version}
                                    </button>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '1rem', fontSize: '0.8rem', color: '#999' }}>
                                        <span className="file-info">{new Date(d.releaseDate).toLocaleDateString()}</span>
                                        <span title="Total Downloads" style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                            <Download size={12} /> {d.downloadCount ? d.downloadCount.toLocaleString() : 0}
                                        </span>
                                    </div>
                                </div>
                            ))
                        ) : (
                            <div style={{ gridColumn: '1/-1', textAlign: 'center', padding: '2rem', color: '#666', background: '#fff', borderRadius: '12px' }}>
                                <p>No downloads currently available. Please check back later.</p>
                            </div>
                        )}
                    </div>



                    {downloads.length > 0 && (
                        <div style={{ textAlign: 'center', marginTop: '2rem' }}>
                            <button onClick={() => setShowHistory(true)} className="btn btn-outline" style={{ fontSize: '0.9rem' }}>
                                <Clock size={16} /> See Previous Versions
                            </button>
                        </div>
                    )}

                    {/* V2 Section - Moved Here */}
                    <div className="v2-preview" style={{ marginTop: '4rem', background: '#0f172a' }}>
                        <div className="v2-content">
                            <span className="v2-tag">UPCOMING IN V2.0</span>
                            <h3>IMS Cloud Integration</h3>
                            <div className="v2-grid">
                                <div className="v2-item"><RefreshCw size={24} className="v2-icon" /><span>Universal Sync</span></div>
                                <div className="v2-item"><Database size={24} className="v2-icon" /><span>Encrypted Backup</span></div>
                                <div className="v2-item"><Smartphone size={24} className="v2-icon" /><span>Mobile Reports</span></div>
                            </div>
                        </div>
                    </div>

                    <div className="install-guide">
                        <h3 className="guide-title">Installation & Activation Guide</h3>
                        <div className="steps-grid">
                            <div className="step-card">
                                <span className="step-num">01</span>
                                <h4>Download & Install</h4>
                                <p>Download the version compatible with your OS. Run the installer (Windows) or extract the archive (Linux).</p>
                            </div>
                            <div className="step-card">
                                <span className="step-num">02</span>
                                <h4>Create Account</h4>
                                <p>Click &quot;Get Started&quot; on this website to create your account. You need this to manage your licenses.</p>
                            </div>
                            <div className="step-card">
                                <span className="step-num">03</span>
                                <h4>Request License</h4>
                                <p>Log in to your dashboard and request a Free or Premium license. You will receive an activation key.</p>
                            </div>
                            <div className="step-card">
                                <span className="step-num">04</span>
                                <h4>Activate App</h4>
                                <p>Open the desktop app, go to Settings &gt; License, and enter your activation key to unlock features.</p>
                            </div>
                        </div>
                    </div>
                </div>
            </section>


            {/* Pricing Section - Focused on Software License */}
            <section id="pricing" className="section">
                <div className="container">
                    <div className="section-header">
                        <h2 className="section-title">License Plans</h2>
                        <p className="section-subtitle">Choose the tier that fits your business scale.</p>
                    </div>

                    <div className="pricing-grid">

                        {displayPlans.map((plan) => (
                            <div key={plan.id} className={`pricing-card ${plan.id === 'pro' ? 'pricing-highlight' : ''}`}>
                                {plan.id === 'pro' && <div className="save-badge">RECOMMENDED</div>}
                                <h3 className="plan-name">{plan.name}</h3>
                                <div className="plan-price">
                                    {plan.price === 0 ? 'Custom' : `$${plan.price}`}
                                    <span className="period">{plan.price === 0 ? '' : '/mo'}</span>
                                </div>
                                <p className="plan-desc">{plan.description}</p>
                                <ul className="plan-features">
                                    {plan.features.map((feat, i) => (
                                        <li key={i}><Check size={16} color="#10b981" /> {feat}</li>
                                    ))}
                                </ul>

                                {plan.id === 'enterprise' ? (
                                    <a href="#contact" className="btn btn-outline btn-block">
                                        Contact Sales
                                    </a>
                                ) : (
                                    <button onClick={openRegister} className={`btn btn-block ${plan.id === 'pro' ? 'btn-primary' : 'btn-outline'}`}>
                                        Activate IMS
                                    </button>
                                )}
                            </div>
                        ))}

                    </div>
                </div>
            </section >

            {/* Contact Form */}
            < section id="contact" className="section bg-white" >
                <div className="container" style={{ maxWidth: '600px' }}>
                    <div className="section-header">
                        <h2 className="section-title">Support Desk</h2>
                        <p className="section-subtitle">Need help with your IMS activation? Send us a message.</p>
                    </div>

                    <form className="glass-card" style={{ padding: '2.5rem' }} onSubmit={handleContact}>
                        {sent && <div style={{ background: '#e6fffa', color: '#2c7a7b', padding: '1rem', borderRadius: '8px', marginBottom: '1.5rem', textAlign: 'center', fontWeight: 600 }}>Message Sent! We will contact you shortly.</div>}
                        <div style={{ marginBottom: '1.2rem' }}>
                            <label className="input-label">FullName</label>
                            <input type="text" className="input" value={contactName} onChange={e => setContactName(e.target.value)} placeholder="Full Name" required />
                        </div>
                        <div style={{ marginBottom: '1.2rem' }}>
                            <label className="input-label">Email Address</label>
                            <input type="email" className="input" value={contactEmail} onChange={e => setContactEmail(e.target.value)} placeholder="your@email.com" required />
                        </div>
                        <div style={{ marginBottom: '1.5rem' }}>
                            <label className="input-label">How can we help?</label>
                            <textarea className="input" value={contactMsg} onChange={e => setContactMsg(e.target.value)} placeholder="Describe your issue..." rows="4" style={{ resize: 'none' }} required></textarea>
                        </div>
                        <button type="submit" className="btn btn-primary btn-block" disabled={sending}>
                            {sending ? 'Sending...' : <><Send size={18} /> Send Message</>}
                        </button>
                    </form>
                </div>
            </section >

            {/* Footer */}
            < footer className="footer" >
                <div className="container footer-grid">
                    <div className="footer-info">
                        <div className="logo footer-logo">
                            <Box size={24} color="var(--primary)" />
                            <span>IMS Support</span>
                        </div>
                        <p style={{ marginBottom: '1rem' }}>Leading Management Information System (MIS) for professional inventory control.</p>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#666', fontSize: '0.9rem' }}>
                            <Mail size={16} /> mwimulebienvenu05@gmail.com
                        </div>
                    </div>
                    <div className="footer-links-wrap">
                        <div className="footer-links">
                            <h4>Resources</h4>
                            <Link href="/about">About Us</Link>
                            <Link href="/privacy">Privacy Policy</Link>
                            <Link href="/terms">License Terms</Link>
                        </div>
                    </div>
                </div>
                <div className="copyright">
                    &copy; {new Date().getFullYear()} IMS. All rights reserved.
                </div>
            </footer >

            <LoginModal isOpen={showLogin} onClose={() => setShowLogin(false)} onSwitchToRegister={openRegister} />
            <RegisterModal isOpen={showRegister} onClose={() => setShowRegister(false)} onSwitchToLogin={openLogin} />
            <DemoRequestModal isOpen={showDemo} onClose={() => setShowDemo(false)} />

            {/* History Modal */}
            {showHistory && (
                <div className="modal-overlay">
                    <div className="modal-content" style={{ maxWidth: '700px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                            <h3 style={{ fontSize: '1.5rem', fontWeight: 700 }}>Version History</h3>
                            <button onClick={() => setShowHistory(false)} style={{ background: 'none', border: 'none', cursor: 'pointer' }}><X size={24} /></button>
                        </div>
                        <div style={{ maxHeight: '60vh', overflowY: 'auto' }}>
                            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                                <thead style={{ background: '#f8fafc' }}>
                                    <tr>
                                        <th style={{ padding: '10px', textAlign: 'left', fontSize: '0.9rem' }}>Version</th>
                                        <th style={{ padding: '10px', textAlign: 'left', fontSize: '0.9rem' }}>OS</th>
                                        <th style={{ padding: '10px', textAlign: 'left', fontSize: '0.9rem' }}>Downloads</th>
                                        <th style={{ padding: '10px', textAlign: 'left', fontSize: '0.9rem' }}>Date</th>
                                        <th style={{ padding: '10px', textAlign: 'right', fontSize: '0.9rem' }}>Action</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {downloads.map(d => (
                                        <tr key={d._id} style={{ borderBottom: '1px solid #eee' }}>
                                            <td style={{ padding: '12px 10px', fontWeight: 600 }}>{d.version} {d.isFeatured && <span style={{ fontSize: '0.7rem', background: '#dcfce7', color: '#166534', padding: '2px 6px', borderRadius: '4px', marginLeft: '6px' }}>Latest</span>}</td>
                                            <td style={{ padding: '12px 10px' }}>{d.os}</td>
                                            <td style={{ padding: '12px 10px', color: '#64748b' }}>{d.downloadCount || 0}</td>
                                            <td style={{ padding: '12px 10px', color: '#666', fontSize: '0.9rem' }}>{new Date(d.releaseDate).toLocaleDateString()}</td>
                                            <td style={{ padding: '12px 10px', textAlign: 'right' }}>
                                                <button onClick={() => handleDownload(d)} style={{ background: 'none', border: 'none', color: 'var(--primary)', fontWeight: 600, cursor: 'pointer', fontSize: '0.9rem' }}>
                                                    Download
                                                </button>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </div>
                </div>
            )}

            <style jsx global>{`
                .landing-wrap {
                    font-family: 'Inter', -apple-system, system-ui, sans-serif;
                    overflow-x: hidden;
                }
                .container {
                    max-width: 1400px;
                    margin: 0 auto;
                    padding: 0 1.5rem;
                }
                .nav {
                    position: fixed;
                    top: 0;
                    width: 100%;
                    z-index: 1000;
                    transition: all 0.3s;
                    background: transparent;
                }
                .nav-scrolled {
                    background: rgba(255, 255, 255, 0.95);
                    backdrop-filter: blur(10px);
                    box-shadow: 0 2px 20px rgba(0,0,0,0.08);
                    padding: 0.2rem 0;
                }
                .nav-container {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    padding: 1.2rem;
                }
                .logo {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    font-weight: 800;
                    font-size: 1.5rem;
                    color: #000;
                    text-decoration: none;
                }
                .nav-links {
                    display: flex;
                    list-style: none;
                    gap: 2.5rem; /* Increased gap to prevent collision */
                    align-items: center;
                }
                .nav-item {
                    white-space: nowrap; /* Prevent text wrapping */
                }
                .nav-links a, .nav-btn {
                    text-decoration: none;
                    color: #333;
                    font-weight: 600;
                    font-size: 1rem;
                    background: none;
                    border: none;
                    cursor: pointer;
                    transition: color 0.2s;
                }
                .nav-links a:hover, .nav-btn:hover {
                    color: var(--primary);
                }
                .nav-cta {
                    padding: 0.8rem 1.5rem !important;
                    font-size: 0.95rem !important;
                    white-space: nowrap;
                }
                .mobile-toggle {
                    display: none;
                    background: none;
                    border: none;
                    cursor: pointer;
                    color: #333;
                }

                .hero {
                    padding: 12rem 0 8rem;
                    background: radial-gradient(circle at top right, rgba(0, 112, 243, 0.08), transparent),
                                radial-gradient(circle at bottom left, rgba(121, 40, 202, 0.08), transparent),
                                var(--bg-pattern);
                    background-size: 100% 100%, 100% 100%, 24px 24px;
                    background-attachment: scroll, scroll, fixed;
                    text-align: center;
                }
                .hero-title {
                    font-size: 4.5rem;
                    line-height: 1.1;
                    font-weight: 900;
                    margin-bottom: 2rem;
                    letter-spacing: -0.05em;
                }
                .hero-desc {
                    font-size: 1.25rem;
                    color: #555;
                    max-width: 750px;
                    margin: 0 auto 3.5rem;
                    line-height: 1.6;
                }
                .hero-actions {
                    display: flex;
                    gap: 1.5rem;
                    justify-content: center;
                }
                .text-gradient {
                    background: linear-gradient(to right, var(--primary), var(--secondary));
                    -webkit-background-clip: text;
                    -webkit-text-fill-color: transparent;
                }

                .section { padding: 8rem 0; }
                .section-header { text-align: center; margin-bottom: 5rem; }
                .section-title { font-size: 3rem; font-weight: 800; margin-bottom: 1.2rem; letter-spacing: -0.02em; }
                .section-subtitle { color: #666; font-size: 1.2rem; max-width: 600px; margin: 0 auto; }

                .bg-white { 
                    background-color: #fff;
                    background-image: var(--bg-pattern);
                    background-size: 24px 24px;
                    background-attachment: fixed;
                }

                .feature-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
                    gap: 2.5rem;
                }

                .v2-preview {
                    margin-top: 6rem;
                    background: #000;
                    color: #fff;
                    border-radius: 32px;
                    padding: 5rem;
                    text-align: center;
                }
                .v2-tag {
                    display: inline-block;
                    padding: 0.5rem 1.2rem;
                    background: rgba(255,255,255,0.1);
                    border-radius: 50px;
                    font-size: 0.8rem;
                    font-weight: 800;
                    margin-bottom: 2rem;
                }
                .v2-preview h3 { font-size: 2.5rem; font-weight: 700; margin-bottom: 3rem; }
                .v2-grid {
                    display: flex;
                    justify-content: center;
                    gap: 4rem;
                    flex-wrap: wrap;
                }
                .v2-item {
                    display: flex;
                    align-items: center;
                    gap: 12px;
                    font-weight: 600;
                    color: #ccc;
                    font-size: 1.1rem;
                }
                .v2-icon { color: var(--primary); }
                
                /* Download Section Styles */
                .download-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
                    gap: 2rem;
                    max-width: 800px;
                    margin: 0 auto;
                }
                .download-card {
                    background: #fff;
                    padding: 3rem 2rem;
                    border-radius: 24px;
                    border: 1px solid #eee;
                    text-align: center;
                    transition: transform 0.3s ease, box-shadow 0.3s ease;
                }
                .download-card:hover {
                    transform: translateY(-5px);
                    box-shadow: 0 20px 40px rgba(0,0,0,0.08);
                    border-color: var(--primary);
                }
                .os-icon {
                    width: 80px;
                    height: 80px;
                    border-radius: 20px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    margin: 0 auto 1.5rem;
                    color: #fff;
                }
                .os-icon.win { background: linear-gradient(135deg, #0078D7, #00C7FD); }
                .os-icon.lin { background: linear-gradient(135deg, #E95420, #FAA61A); }
                
                .download-card h3 { font-size: 1.5rem; margin-bottom: 0.5rem; font-weight: 700; }
                .download-card p { color: #666; margin-bottom: 2rem; font-size: 0.95rem; }
                .download-card .btn { display: flex; align-items: center; justify-content: center; gap: 8px; margin-bottom: 1rem; }
                .file-info { font-size: 0.8rem; color: #999; font-weight: 500; }

                /* Installation Guide Styles */
                .install-guide { margin-top: 6rem; text-align: center; }
                .guide-title { font-size: 2rem; font-weight: 800; margin-bottom: 3rem; }
                .steps-grid { 
                    display: grid; 
                    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); 
                    gap: 2rem; 
                    text-align: left;
                }
                .step-card {
                    background: #fff;
                    padding: 2rem;
                    border-radius: 20px;
                    border: 1px solid #f0f0f0;
                    position: relative;
                }
                .step-num {
                    font-size: 3rem;
                    font-weight: 900;
                    color: rgba(0, 112, 243, 0.1);
                    position: absolute;
                    top: 10px;
                    right: 20px;
                    line-height: 1;
                }
                .step-card h4 { font-size: 1.1rem; font-weight: 700; margin-bottom: 0.8rem; position: relative; z-index: 1; }
                .step-card p { font-size: 0.95rem; color: #666; line-height: 1.6; position: relative; z-index: 1; }

                .pricing-grid {
                    display: grid;
                    grid-template-columns: repeat(4, 1fr);
                    gap: 1.5rem;
                    padding: 1rem 0;
                }
                .pricing-card {
                    background: #fff;
                    padding: 4rem 2rem;
                    border-radius: 24px;
                    border: 1px solid #eee;
                    position: relative;
                    text-align: center;
                    transition: all 0.4s ease;
                    display: flex;
                    flex-direction: column;
                }
                .pricing-highlight {
                    border: 2px solid var(--primary);
                    box-shadow: 0 30px 60px rgba(0, 112, 243, 0.12);
                    transform: translateY(-10px);
                    z-index: 1;
                }
                .save-badge {
                    position: absolute;
                    top: -15px;
                    left: 50%;
                    transform: translateX(-50%);
                    background: #10b981;
                    color: #fff;
                    padding: 0.4rem 1rem;
                    border-radius: 50px;
                    font-size: 0.75rem;
                    font-weight: 800;
                    box-shadow: 0 4px 10px rgba(16, 185, 129, 0.3);
                    white-space: nowrap;
                }
                .plan-name { font-size: 1.2rem; color: #666; font-weight: 600; margin-bottom: 0.8rem; }
                .plan-price { font-size: 3rem; font-weight: 900; margin-bottom: 1.5rem; line-height: 1; }
                .plan-price .period { font-size: 1rem; color: #999; font-weight: 500; margin-left: 2px; }
                .plan-desc { color: #777; font-size: 0.9rem; margin-bottom: 2rem; height: 3em; }
                .plan-features { list-style: none; text-align: left; margin-bottom: auto; padding-bottom: 2rem; }
                .plan-features li { margin-bottom: 0.8rem; font-size: 0.9rem; display: flex; align-items: start; gap: 10px; color: #444; }

                /* Custom background for Downloads */
                .bg-with-pattern {
                    background: linear-gradient(to bottom, #ffffff, #f9fafb),
                                var(--bg-pattern);
                    background-size: 100% 100%, 24px 24px;
                    background-attachment: scroll, fixed;
                    background-blend-mode: overlay;
                }

                @media (max-width: 1024px) {
                    .pricing-grid {
                        grid-template-columns: repeat(2, 1fr);
                        gap: 2rem;
                    }
                }
                
                @media (max-width: 640px) {
                    .pricing-grid {
                        grid-template-columns: 1fr;
                    }
                }

                .footer { padding: 6rem 0 3rem; background: #fff; border-top: 1px solid #eee; }
                .footer-grid { display: grid; grid-template-columns: 1.5fr 1fr; gap: 5rem; }
                .footer-info p { color: #666; margin-top: 1.5rem; line-height: 1.8; }
                .footer-links h4 { font-size: 1.1rem; margin-bottom: 2rem; font-weight: 800; }
                .footer-links { display: flex; flex-direction: column; gap: 1.2rem; }
                .footer-links a { color: #666; text-decoration: none; font-size: 1rem; transition: color 0.2s; }
                .footer-links a:hover { color: var(--primary); }
                .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.6); backdrop-filter: blur(5px); z-index: 2000; display: flex; align-items: center; justify-content: center; padding: 1rem; }
                .modal-content { background: #fff; width: 100%; max-width: 420px; animation: slideUp 0.3s ease-out; padding: 2rem; border-radius: 20px; }
                @keyframes slideUp { from { transform: translateY(20px); opacity: 0; } to { transform: translateY(0); opacity: 1; } }

                .copyright { margin-top: 5rem; padding-top: 3rem; border-top: 1px solid #f5f5f5; text-align: center; font-size: 0.9rem; color: #aaa; }

                .input-label { font-size: 0.9rem; font-weight: 700; color: #333; margin-bottom: 0.6rem; display: block; }
                .input { 
                    width: 100%; 
                    padding: 1rem; 
                    border: 1px solid #eee; 
                    border-radius: 12px; 
                    background: #fdfdfd; 
                    font-size: 1rem;
                    transition: all 0.2s;
                }
                .input:focus { outline: none; border-color: var(--primary); background: #fff; box-shadow: 0 0 0 4px rgba(0, 112, 243, 0.05); }

                .btn-lg { padding: 1.2rem 2.5rem; font-size: 1.15rem; border-radius: 14px; }
                .btn-secondary { background: #fff; border: 1px solid #ddd; color: #444; }
                .btn-secondary:hover { border-color: #999; background: #fafafa; }
                .btn-outline { background: transparent; border: 1px solid #eee; color: #444; }
                .btn-outline:hover { background: #f9f9f9; border-color: #ddd; }
                .btn-block { width: 100%; padding: 1.2rem; border-radius: 12px; }

                @media (max-width: 1024px) {
                    .hero-title { font-size: 3.8rem; }
                    .footer-grid { grid-template-columns: 1fr; gap: 3rem; text-align: center; }
                    .footer-logo { justify-content: center; }
                    .footer-links { align-items: center; }
                    .nav-links { gap: 1rem; } /* Reduce gap but keep it safe */
                }

                @media (max-width: 768px) {
                    .desktop-menu { display: none; }
                    .mobile-toggle { display: block; }
                    .mobile-menu {
                        position: fixed;
                        top: 75px;
                        left: 0;
                        right: 0;
                        background: #fff;
                        padding: 2.5rem;
                        display: flex;
                        flex-direction: column;
                        gap: 1.8rem;
                        box-shadow: 0 15px 30px rgba(0,0,0,0.1);
                        z-index: 999;
                        border-top: 1px solid #eee;
                    }
                    .mobile-menu a { 
                        font-size: 1.2rem; 
                        font-weight: 700; 
                        text-align: center;
                        text-decoration: none;
                        color: #333;
                        padding: 0.5rem;
                        transition: color 0.2s;
                    }
                    .mobile-menu a:hover { color: var(--primary); }
                    
                    .mobile-menu .btn {
                        width: 100%;
                        padding: 1rem;
                        font-size: 1.1rem;
                        justify-content: center;
                    }
                    .hero { padding: 10rem 0 6rem; }
                    .hero-title { font-size: 3.2rem; }
                    .hero-desc { font-size: 1.1rem; }
                    .hero-actions { flex-direction: column; gap: 1rem; }
                    .hero-actions .btn { width: 100%; }
                    .section-title { font-size: 2.3rem; }
                    .v2-preview { padding: 3rem 2rem; border-radius: 20px; }
                    .v2-grid { gap: 2rem; flex-direction: column; align-items: center; }
                    .plan-price { font-size: 3.2rem; }
                    .section { padding: 5rem 0; }
                }

                /* Make steps grid single column on mobile */
                @media (max-width: 600px) {
                    .steps-grid { grid-template-columns: 1fr; }
                    .download-grid { grid-template-columns: 1fr; }
                }

                .badge {
                    display: inline-block;
                    padding: 0.5rem 1.2rem;
                    background: #ebf8ff;
                    color: var(--primary);
                    border-radius: 50px;
                    font-size: 0.8rem;
                    font-weight: 800;
                    margin-bottom: 2rem;
                    letter-spacing: 0.02em;
                }
            `}</style>
        </div >
    );
}

function FeatureBox({ icon, title, description }) {
    return (
        <div className="glass-card" style={{ padding: '3rem 2.5rem', textAlign: 'left', borderRadius: '24px' }}>
            <div style={{ marginBottom: '1.8rem' }}>{icon}</div>
            <h3 style={{ fontSize: '1.4rem', marginBottom: '1.2rem', fontWeight: 700 }}>{title}</h3>
            <p style={{ color: '#666', fontSize: '1rem', lineHeight: 1.7 }}>{description}</p>
        </div>
    );
}

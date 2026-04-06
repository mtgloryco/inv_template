'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  Shield, Plus, LogOut, Clock, Layers, Monitor,
  Copy, CheckCircle2, Upload, HelpCircle, Info, Image as LucideImage
} from 'lucide-react';

export default function UserDashboard() {
  const [licenses, setLicenses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [requesting, setRequesting] = useState(false);
  const [hardwareId, setHardwareId] = useState('');
  const [selectedPlan, setSelectedPlan] = useState('freemium');
  const [copiedKey, setCopiedKey] = useState(null);

  // Plans Logic
  const [plans, setPlans] = useState([]);
  const [loadingPlans, setLoadingPlans] = useState(true);

  const router = useRouter();

  const fetchPlans = useCallback(async () => {
    try {
      const res = await fetch('/api/plans');
      const data = await res.json();
      setPlans(data);
      if (data.length > 0) setSelectedPlan(data[0].id); // Default to first plan
    } catch (e) {
      console.error('Failed to load plans');
    } finally {
      setLoadingPlans(false);
    }
  }, []);

  const fetchLicenses = useCallback(async () => {
    const token = localStorage.getItem('token');
    if (!token) { router.push('/login'); return; }

    try {
      const res = await fetch('/api/licenses', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (res.status === 401) { router.push('/login'); return; }
      const data = await res.json();
      setLicenses(data);
    } catch (err) { console.error(err); }
    finally { setLoading(false); }
  }, [router]);

  useEffect(() => {
    fetchLicenses();
    fetchPlans();
  }, [fetchLicenses, fetchPlans]);

  const handleSelectPlan = (plan) => {
    if (plan.price === 0) return; // Prevent selecting custom/enterprise
    setSelectedPlan(plan.id);
  };


  const handleRequestLicense = async () => {
    if (!hardwareId) { alert('Please enter your Hardware ID.'); return; }

    const token = localStorage.getItem('token');
    setRequesting(true);
    try {
      const res = await fetch('/api/licenses', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ planType: selectedPlan, hardwareId })
      });
      const data = await res.json();
      if (!res.ok) alert(data.error);
      else {
        setHardwareId('');
        fetchLicenses();
      }
    } catch (err) { alert('Request failed'); }
    finally { setRequesting(false); }
  };

  const handleUploadProof = async (licenseId, file) => {
    if (!file) return;

    // Convert image to base64
    const reader = new FileReader();
    reader.readAsDataURL(file);
    reader.onload = async () => {
      const base64 = reader.result;
      const token = localStorage.getItem('token');
      try {
        const res = await fetch('/api/licenses', {
          method: 'PATCH',
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({ id: licenseId, proofImage: base64 })
        });
        if (res.ok) fetchLicenses();
        else alert('Upload failed');
      } catch (e) { alert('Error uploading proof'); }
    };
  };

  const copyToClipboard = (key) => {
    if (!key) return;
    navigator.clipboard.writeText(key);
    setCopiedKey(key);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  const handleLogout = () => {
    localStorage.clear();
    router.push('/login');
  };

  return (
    <div style={{ background: '#f7fafc', minHeight: '100vh' }}>
      <nav style={{ background: '#fff', borderBottom: '1px solid #eee', position: 'sticky', top: 0, zIndex: 10 }}>
        <div className="container" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1rem 1.5rem' }}>
          <Link href="/" style={{ textDecoration: 'none', color: 'inherit' }}>
            <div style={{ fontWeight: 'bold', fontSize: '1.2rem', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Shield color="var(--primary)" /> <span>IMS Hub</span>
            </div>
          </Link>
          <div style={{ display: 'flex', gap: '1.5rem', alignItems: 'center' }}>
            <button onClick={handleLogout} className="btn" style={{ fontSize: '0.9rem', color: '#666' }}><LogOut size={18} /> Logout</button>
          </div>
        </div>
      </nav>

      <div className="container" style={{ padding: '3rem 1.5rem' }}>
        <header style={{ marginBottom: '3rem' }}>
          <h1 className="title">License Manager</h1>
          <p style={{ color: '#666' }}>Request, activate, and download your IMS Professional signatures.</p>
        </header>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '2rem', marginBottom: '3rem' }}>
          {/* Activation Tool */}
          <div className="glass-card" style={{ background: '#fff' }}>
            <h3 style={{ marginBottom: '1.5rem', display: 'flex', alignItems: 'center', gap: '10px' }}><Plus size={20} color="var(--primary)" /> New Request</h3>

            <div style={{ marginBottom: '1.5rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 700, color: '#444', marginBottom: '1rem' }}>SELECT PLAN</label>

              {loadingPlans ? (
                <div style={{ textAlign: 'center', padding: '2rem', color: '#666' }}>Loading plans...</div>
              ) : (
                <div className="pricing-grid">
                  {plans.map((plan) => (
                    <div
                      key={plan.id}
                      onClick={() => handleSelectPlan(plan)}
                      className={`pricing-card ${selectedPlan === plan.id ? 'pricing-highlight selected' : ''}`}
                      style={{ cursor: plan.price === 0 ? 'default' : 'pointer' }}
                    >
                      {selectedPlan === plan.id && <div className="save-badge">SELECTED</div>}
                      <h3 className="plan-name">{plan.name}</h3>
                      <div className="plan-price">
                        {plan.price === 0 ? 'Custom' : `$${plan.price}`}
                        <span className="period">{plan.price === 0 ? '' : '/mo'}</span>
                      </div>
                      <p className="plan-desc">{plan.description}</p>

                      {plan.price === 0 ? (
                        <a href="mailto:sales@ims.com" className="btn btn-outline btn-block" onClick={(e) => e.stopPropagation()}>
                          Contact Sales
                        </a>
                      ) : (
                        <div className={`btn btn-block ${selectedPlan === plan.id ? 'btn-primary' : 'btn-outline'}`}>
                          {selectedPlan === plan.id ? <CheckCircle2 size={18} /> : 'Select'}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div style={{ marginBottom: '1.5rem' }}>
              <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 700, color: '#444', marginBottom: '0.5rem' }}>HARDWARE ID</label>
              <div style={{ position: 'relative' }}>
                <Monitor size={16} style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)', color: '#888' }} />
                <input
                  type="text"
                  value={hardwareId}
                  onChange={(e) => setHardwareId(e.target.value)}
                  placeholder="Paste from Desktop App"
                  style={{ width: '100%', padding: '12px 12px 12px 40px', borderRadius: '10px', border: '1px solid #eee', fontSize: '0.9rem' }}
                />
              </div>
            </div>

            <button onClick={handleRequestLicense} className="btn btn-primary" style={{ width: '100%', padding: '1rem' }} disabled={requesting || !hardwareId}>
              {requesting ? 'Processing...' : 'Submit Request'}
            </button>
          </div>

          {/* Guidelines */}
          <div className="glass-card" style={{ background: 'linear-gradient(135deg, #1a202c, #2d3748)', color: '#fff' }}>
            <h3 style={{ marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '10px' }}><Info size={20} color="var(--primary)" /> Workflow</h3>
            <ol style={{ paddingLeft: '1.2rem', fontSize: '0.9rem', display: 'grid', gap: '1rem', color: '#cbd5e0' }}>
              <li>Generate <strong>Hardware ID</strong> in the desktop app.</li>
              <li>Submit a new request here for your device.</li>
              <li>Upload your <strong>Payment Proof</strong> (screenshot).</li>
              <li>Wait for Admin approval (approx. 1-12 hours).</li>
              <li>Copy the <strong>Signed Key</strong> into your desktop app.</li>
            </ol>
          </div>
        </div>

        {/* Repos */}
        <section className="glass-card" style={{ background: '#fff' }}>
          <h3 style={{ marginBottom: '2rem', display: 'flex', alignItems: 'center', gap: '10px' }}><Layers size={20} color="var(--primary)" /> Activation Registry</h3>
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Request ID</th>
                  <th>Tier</th>
                  <th>Status</th>
                  <th>Action / Key</th>
                  <th>Expiration</th>
                </tr>
              </thead>
              <tbody>
                {licenses.map(lic => (
                  <tr key={lic._id}>
                    <td><code style={{ fontSize: '0.75rem', color: '#777' }}>{lic.licenseId.substring(0, 8)}...</code></td>
                    <td><span className="status-badge" style={{ background: '#ebf8ff', color: '#2b6cb0' }}>{lic.tier}</span></td>
                    <td>
                      <span className={`status-badge status-${lic.status?.toLowerCase()}`} style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                        {lic.status === 'Pending' && <Clock size={12} />}
                        {lic.status}
                      </span>
                    </td>
                    <td>
                      {lic.status === 'Active' ? (
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                          <code style={{ background: '#f1f5f9', padding: '6px 10px', borderRadius: '6px', fontSize: '0.7rem', maxWidth: '120px', overflow: 'hidden', textOverflow: 'ellipsis' }}>{lic.licenseKey}</code>
                          <button onClick={() => copyToClipboard(lic.licenseKey)} className="btn" style={{ padding: '4px', color: copiedKey === lic.licenseKey ? '#48bb78' : 'var(--primary)' }}>
                            {copiedKey === lic.licenseKey ? <CheckCircle2 size={16} /> : <Copy size={16} />}
                          </button>
                        </div>
                      ) : (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                          {lic.paymentProof ? (
                            <div style={{ fontSize: '0.7rem', color: '#48bb78', display: 'flex', alignItems: 'center', gap: '4px' }}>
                              <LucideImage size={12} /> Proof Uploaded
                            </div>
                          ) : (
                            <label className="btn" style={{ background: '#edf2f7', fontSize: '0.7rem', padding: '6px 10px', cursor: 'pointer' }}>
                              <Upload size={14} /> Upload Proof
                              <input type="file" hidden accept="image/*,.pdf" onChange={(e) => handleUploadProof(lic._id, e.target.files[0])} />
                            </label>
                          )}
                        </div>
                      )}
                    </td>
                    <td style={{ fontSize: '0.85rem' }}>
                      {lic.expirationDate ? new Date(lic.expirationDate).toLocaleDateString() : '--'}
                    </td>
                  </tr>
                ))}
                {licenses.length === 0 && !loading && (
                  <tr><td colSpan="5" style={{ textAlign: 'center', padding: '4rem', color: '#999' }}>Generate a request to get started.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <style jsx global>{`
        .status-pending { background: #fffaf0; color: #dd6b20; border: 1px solid #feebc8; }
        .nav-scrolled { box-shadow: 0 4px 12px rgba(0,0,0,0.05); }

        /* Pricing Grid Styles */
        .pricing-grid {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 1.5rem;
            padding: 1rem 0;
        }
        .pricing-card {
            background: #fff;
            padding: 2rem 1.5rem;
            border-radius: 16px;
            border: 1px solid #eee;
            position: relative;
            text-align: center;
            transition: all 0.2s ease;
            display: flex;
            flex-direction: column;
        }
        .pricing-card:hover {
            border-color: var(--primary);
            transform: translateY(-2px);
        }
        .pricing-highlight {
            border: 2px solid var(--primary);
            box-shadow: 0 10px 30px rgba(0, 112, 243, 0.1);
            z-index: 1;
        }
        .pricing-card.selected {
            background: #f0f7ff;
            border-color: var(--primary);
        }
        .save-badge {
            position: absolute;
            top: -12px;
            left: 50%;
            transform: translateX(-50%);
            background: #10b981;
            color: #fff;
            padding: 0.3rem 0.8rem;
            border-radius: 50px;
            font-size: 0.7rem;
            font-weight: 800;
            white-space: nowrap;
        }
        .plan-name { font-size: 1rem; color: #555; font-weight: 700; margin-bottom: 0.5rem; }
        .plan-price { font-size: 2rem; font-weight: 900; margin-bottom: 1rem; line-height: 1; color: #333; }
        .plan-price .period { font-size: 0.8rem; color: #999; font-weight: 500; margin-left: 2px; }
        .plan-desc { color: #666; font-size: 0.8rem; margin-bottom: 1.5rem; flex-grow: 1; }
        
        @media (max-width: 1024px) {
            .pricing-grid { grid-template-columns: repeat(2, 1fr); }
        }
        @media (max-width: 600px) {
            .pricing-grid { grid-template-columns: 1fr; }
        }
      `}</style>
    </div>
  );
}

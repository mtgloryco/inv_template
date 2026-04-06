'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import NextImage from 'next/image';
import {
    ShieldAlert, Activity, CreditCard, RefreshCw,
    Calendar, Trash2, CheckCircle, Mail, MessageSquare, Clock,
    Eye, XCircle, Search, Edit, Plus, Save, RotateCcw, CheckCircle2,
    Download, Monitor, Server
} from 'lucide-react';

export default function AdminDashboard() {
    const [licenses, setLicenses] = useState([]);
    const [contacts, setContacts] = useState([]);
    const [plans, setPlans] = useState([]);
    const [downloads, setDownloads] = useState([]);
    const [activeTab, setActiveTab] = useState('licenses');
    const [loading, setLoading] = useState(true);
    const [selectedProof, setSelectedProof] = useState(null);
    const [searchQuery, setSearchQuery] = useState('');

    // UI States
    const [replyModal, setReplyModal] = useState(null); // { id, name, replyMessage: '' }
    const [manualModal, setManualModal] = useState(null); // { name: '', hardwareId: '', tier: 'Enterprise', durationDays: 365 }
    const [planModal, setPlanModal] = useState(null);   // { mode: 'edit'|'add', plan: {} }
    const [downloadModal, setDownloadModal] = useState(null); // { mode: 'add'|'edit', item: {} }

    const router = useRouter();

    const fetchAdminData = useCallback(async () => {
        const token = localStorage.getItem('token');
        try {
            const res = await fetch('/api/admin/licenses', { headers: { 'Authorization': `Bearer ${token}` } });
            setLicenses(await res.json());
        } catch (err) { console.error(err); }
        finally { setLoading(false); }
    }, []);

    const fetchContacts = useCallback(async () => {
        try {
            const res = await fetch('/api/contacts');
            setContacts(await res.json());
        } catch (err) { console.error(err); }
    }, []);

    const fetchPlans = useCallback(async () => {
        try {
            const res = await fetch('/api/plans');
            setPlans(await res.json());
        } catch (e) { console.error(e); }
    }, []);

    const fetchDownloads = useCallback(async () => {
        try {
            const res = await fetch('/api/downloads');
            setDownloads(await res.json());
        } catch (e) { console.error(e); }
    }, []);

    useEffect(() => {
        const token = localStorage.getItem('token');
        const user = JSON.parse(localStorage.getItem('user') || '{}');
        if (!token || user.role !== 'admin') {
            router.push('/login');
            return;
        }
        fetchAdminData();
        fetchContacts();
        fetchPlans();
        fetchDownloads();
    }, [router, fetchAdminData, fetchContacts, fetchPlans, fetchDownloads]);

    // --- ACTIONS ---

    const handleApprove = async (id) => {
        if (!confirm('Approve this payment and generate RSA license key?')) return;
        const token = localStorage.getItem('token');
        try {
            const res = await fetch('/api/admin/licenses', {
                method: 'PATCH',
                headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ id, action: 'approve' })
            });
            if (res.ok) fetchAdminData();
            else alert('Approval failed');
        } catch (err) { alert('Error'); }
    };

    const handleManualGenerate = async () => {
        if (!manualModal.name || !manualModal.hardwareId) return alert('Name and Hardware ID are required');
        const token = localStorage.getItem('token');
        try {
            const res = await fetch('/api/admin/licenses', {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
                body: JSON.stringify(manualModal)
            });

            if (res.ok) {
                const data = await res.json();
                setManualModal(null);
                fetchAdminData();
                prompt('License Generated! Copy key below:', data.licenseKey);
            } else {
                const err = await res.json();
                alert('Failed: ' + err.error);
            }
        } catch (err) { alert('Error generating license'); }
    };

    const handleUpdateStatus = async (id, status) => {
        const token = localStorage.getItem('token');
        try {
            await fetch('/api/admin/licenses', {
                method: 'PATCH',
                headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ id, action: 'update_status', status })
            });
            fetchAdminData();
        } catch (err) { alert('Failed'); }
    };

    const sendReply = async () => {
        if (!replyModal.replyMessage) return alert('Message is empty');
        try {
            await fetch('/api/contacts', {
                method: 'PATCH',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: replyModal.id, replyMessage: replyModal.replyMessage })
            });
            setReplyModal(null);
            fetchContacts();
            alert('Reply sent!');
        } catch (e) { alert('Failed to send reply'); }
    };

    const savePlan = async () => {
        const url = '/api/plans';
        const method = planModal.mode === 'add' ? 'POST' : 'PUT';
        try {
            const res = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(planModal.plan)
            });
            if (!res.ok) throw new Error(await res.text());
            setPlanModal(null);
            fetchPlans();
        } catch (e) { alert('Failed to save plan: ' + e.message); }
    };

    const deletePlan = async (id) => {
        if (!confirm('Delete this plan?')) return;
        try {
            await fetch(`/api/plans?id=${id}`, { method: 'DELETE' });
            fetchPlans();
        } catch (e) { alert('Failed to delete'); }
    };

    const saveDownload = async () => {
        const url = '/api/downloads';
        const method = downloadModal.mode === 'add' ? 'POST' : 'PUT';
        try {
            const res = await fetch(url, {
                method,
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(downloadModal.item)
            });
            if (!res.ok) {
                const data = await res.json();
                throw new Error(data.error || 'Failed');
            }
            setDownloadModal(null);
            fetchDownloads();
        } catch (e) { alert('Error: ' + e.message); }
    };

    const deleteDownload = async (id) => {
        if (!confirm('Delete this version?')) return;
        try {
            await fetch(`/api/downloads?id=${id}`, { method: 'DELETE' });
            fetchDownloads();
        } catch (e) { alert('Failed to delete'); }
    };

    // --- FILTERING ---
    const filteredLicenses = licenses.filter(l =>
    (l.userDetails?.email?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        l.userDetails?.username?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        l.manualIssuedTo?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        l.hardwareId?.toLowerCase().includes(searchQuery.toLowerCase()))
    );
    const filteredContacts = contacts.filter(c =>
        c.email.toLowerCase().includes(searchQuery.toLowerCase())
    );

    return (
        <div style={{ background: '#f8fafc', minHeight: '100vh' }}>
            <nav style={{ background: '#111827', padding: '1rem 2rem', position: 'sticky', top: 0, zIndex: 100 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ fontWeight: 800, fontSize: '1.2rem', display: 'flex', alignItems: 'center', gap: '10px', color: '#fff' }}>
                        <ShieldAlert color="#ef4444" /> <span>MSS ADMIN</span>
                    </div>

                    {/* Search Bar */}
                    <div style={{ position: 'relative', width: '300px' }}>
                        <Search size={16} style={{ position: 'absolute', left: '10px', top: '10px', color: '#666' }} />
                        <input
                            type="text"
                            placeholder="Search by email, name, or HWID..."
                            value={searchQuery}
                            onChange={(e) => setSearchQuery(e.target.value)}
                            style={{ width: '100%', padding: '8px 10px 8px 35px', borderRadius: '6px', border: 'none', background: '#374151', color: '#fff', fontSize: '0.9rem' }}
                        />
                    </div>

                    <div style={{ display: 'flex', gap: '1rem' }}>
                        <TabBtn active={activeTab === 'licenses'} onClick={() => setActiveTab('licenses')}>Activations</TabBtn>
                        <TabBtn active={activeTab === 'downloads'} onClick={() => setActiveTab('downloads')}>Downloads</TabBtn>
                        <TabBtn active={activeTab === 'plans'} onClick={() => setActiveTab('plans')}>Plans</TabBtn>
                        <TabBtn active={activeTab === 'contacts'} onClick={() => setActiveTab('contacts')}>Messages</TabBtn>
                        <button onClick={() => { localStorage.clear(); router.push('/login'); }} className="btn" style={{ background: '#ef4444', color: '#fff', fontSize: '0.8rem' }}>Logout</button>
                    </div>
                </div>
            </nav>

            <div className="container" style={{ padding: '3rem 1.5rem' }}>
                <header style={{ marginBottom: '2.5rem' }}>
                    <h1 style={{ fontSize: '2rem', fontWeight: 900, marginBottom: '0.5rem' }}>Authority Control</h1>
                    <p style={{ color: '#64748b' }}>Validate payments, manage tiers, and support users.</p>
                </header>

                {activeTab === 'licenses' && (
                    <>
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '1.5rem', marginBottom: '3rem' }}>
                            <StatCard title="Total" value={licenses.length} color="#3b82f6" icon={<Activity size={20} />} />
                            <StatCard title="Pending" value={licenses.filter(l => l.status === 'Pending').length} color="#f59e0b" icon={<Clock size={20} />} />
                            <StatCard title="Active" value={licenses.filter(l => l.status === 'Active').length} color="#10b981" icon={<CheckCircle2 size={20} />} />
                        </div>

                        <section className="glass-card" style={{ background: '#fff' }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
                                <h3 style={{ marginBottom: '0' }}>License Request Log</h3>
                                <button className="btn btn-primary" onClick={() => setManualModal({ name: '', hardwareId: '', tier: 'Enterprise', durationDays: 365 })}>
                                    <Plus size={16} /> Manual Issue
                                </button>
                            </div>
                            <div className="table-container">
                                <table>
                                    <thead>
                                        <tr>
                                            <th>User / Tier</th>
                                            <th>Status</th>
                                            <th>Hardware ID</th>
                                            <th>Payment Proof</th>
                                            <th>Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {filteredLicenses.map(lic => (
                                            <tr key={lic._id}>
                                                <td>
                                                    <div style={{ fontWeight: 700 }}>
                                                        {lic.userDetails?.username || lic.manualIssuedTo || 'Unknown'}
                                                        {lic.manualIssuedTo && <span style={{ fontSize: '0.6rem', color: '#f59e0b', marginLeft: '4px' }}>(Manual)</span>}
                                                    </div>
                                                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>{lic.userDetails?.email || '-'}</div>
                                                    <span style={{ fontSize: '0.65rem', background: '#f1f5f9', padding: '2px 6px', borderRadius: '4px', marginTop: '4px', display: 'inline-block' }}>{lic.tier}</span>
                                                </td>
                                                <td><span className={`status-badge status-${lic.status?.toLowerCase()}`}>{lic.status}</span></td>
                                                <td><code style={{ fontSize: '0.7rem' }}>{lic.hardwareId}</code></td>
                                                <td>
                                                    {lic.paymentProof ? (
                                                        <button onClick={() => setSelectedProof(lic.paymentProof)} className="btn" style={{ background: '#ecfdf5', color: '#047857', padding: '6px 12px', fontSize: '0.7rem' }}>
                                                            <Eye size={14} /> View
                                                        </button>
                                                    ) : <span style={{ fontSize: '0.7rem', color: '#94a3b8' }}>None</span>}
                                                </td>
                                                <td>
                                                    <div style={{ display: 'flex', gap: '6px' }}>
                                                        {lic.status === 'Pending' && (
                                                            <button onClick={() => handleApprove(lic._id)} disabled={!lic.paymentProof} className="btn btn-primary" style={{ padding: '6px 12px', fontSize: '0.7rem' }}>Approve</button>
                                                        )}
                                                        <button onClick={() => handleUpdateStatus(lic._id, lic.status === 'Active' ? 'Revoked' : 'Active')} className="btn" style={{ background: '#f1f5f9', padding: '6px' }}>
                                                            {lic.status === 'Active' ? <Trash2 size={14} color="#ef4444" /> : <RotateCcw size={14} color="#10b981" />}
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </section>
                    </>
                )}

                {activeTab === 'plans' && (
                    <section className="glass-card" style={{ background: '#fff' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
                            <h3>Pricing Plans</h3>
                            <button className="btn btn-primary" onClick={() => setPlanModal({ mode: 'add', plan: { tier: 'Basic', durationDays: 30 } })}>
                                <Plus size={16} /> Add Plan
                            </button>
                        </div>
                        <div className="table-container">
                            <table>
                                <thead>
                                    <tr>
                                        <th>ID</th>
                                        <th>Name</th>
                                        <th>Price</th>
                                        <th>Duration</th>
                                        <th>Features (Count)</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {plans.map(p => (
                                        <tr key={p._id}>
                                            <td><code>{p.id}</code></td>
                                            <td><b>{p.name}</b><br /><span style={{ fontSize: '0.8rem', color: '#666' }}>{p.tier}</span></td>
                                            <td>${p.price}</td>
                                            <td>{p.durationDays} days</td>
                                            <td>{p.features?.length || 0}</td>
                                            <td>
                                                <div style={{ display: 'flex', gap: '8px' }}>
                                                    <button onClick={() => setPlanModal({ mode: 'edit', plan: { ...p } })} className="btn" style={{ padding: '6px' }}><Edit size={16} /></button>
                                                    <button onClick={() => deletePlan(p.id)} className="btn" style={{ padding: '6px', color: '#ef4444' }}><Trash2 size={16} /></button>
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </section>
                )}

                {activeTab === 'contacts' && (
                    <section className="glass-card" style={{ background: '#fff' }}>
                        <h3 style={{ marginBottom: '2rem' }}>Support Messages</h3>
                        {filteredContacts.map(msg => (
                            <div key={msg._id} style={{ borderBottom: '1px solid #f1f5f9', padding: '1.5rem 0' }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.5rem' }}>
                                    <div>
                                        <span style={{ fontWeight: 700, marginRight: '10px' }}>{msg.name} ({msg.email})</span>
                                        {msg.status === 'Replied' && <span style={{ background: '#dcfce7', color: '#166534', padding: '2px 8px', borderRadius: '4px', fontSize: '0.7rem' }}>Replied</span>}
                                    </div>
                                    <div style={{ fontSize: '0.7rem', color: '#94a3b8' }}>{new Date(msg.createdAt).toLocaleString()}</div>
                                </div>
                                <p style={{ fontSize: '0.9rem', color: '#334155', marginBottom: '1rem' }}>{msg.message}</p>

                                {msg.status !== 'Replied' && (
                                    <button onClick={() => setReplyModal({ id: msg._id, name: msg.name, message: msg.message, replyMessage: '' })} className="btn btn-outline" style={{ fontSize: '0.8rem', padding: '6px 12px' }}>
                                        <Mail size={14} style={{ marginRight: '5px' }} /> Reply
                                    </button>
                                )}

                                {msg.replies && msg.replies.map((r, i) => (
                                    <div key={i} style={{ background: '#f8fafc', padding: '1rem', borderRadius: '8px', marginTop: '1rem', borderLeft: '3px solid #3b82f6' }}>
                                        <p style={{ fontSize: '0.8rem', fontWeight: 700, color: '#3b82f6' }}>Reply from Support:</p>
                                        <p style={{ fontSize: '0.85rem', color: '#475569' }}>{r.message}</p>
                                    </div>
                                ))}
                            </div>
                        ))}
                    </section>
                )}

                {activeTab === 'downloads' && (
                    <section className="glass-card" style={{ background: '#fff' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
                            <h3>Software Versions</h3>
                            <button className="btn btn-primary" onClick={() => setDownloadModal({ mode: 'add', item: { os: 'Windows', type: 'Installer', isFeatured: false } })}>
                                <Plus size={16} /> Add Version
                            </button>
                        </div>
                        <div className="table-container">
                            <table>
                                <thead>
                                    <tr>
                                        <th>Version</th>
                                        <th>Platform</th>
                                        <th>Type</th>
                                        <th>Downloads</th>
                                        <th>Release Date</th>
                                        <th>Featured</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {downloads.map(d => (
                                        <tr key={d._id}>
                                            <td><span style={{ fontWeight: 700 }}>{d.version}</span></td>
                                            <td>
                                                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                                                    {d.os === 'Windows' ? <Monitor size={16} color="#0078d4" /> : <Server size={16} color="#e95420" />}
                                                    {d.os}
                                                </div>
                                            </td>
                                            <td><span style={{ fontSize: '0.8rem', background: '#f1f5f9', padding: '2px 8px', borderRadius: '4px' }}>{d.type}</span></td>
                                            <td style={{ textAlign: 'center' }}><span style={{ fontWeight: 700, color: '#334155' }}>{d.downloadCount || 0}</span></td>
                                            <td>{new Date(d.releaseDate).toLocaleDateString()}</td>
                                            <td>
                                                {d.isFeatured ?
                                                    <span style={{ background: '#dcfce7', color: '#166534', padding: '2px 8px', borderRadius: '12px', fontSize: '0.75rem', fontWeight: 600 }}>Featured</span>
                                                    : <span style={{ color: '#94a3b8', fontSize: '0.75rem' }}>-</span>
                                                }
                                            </td>
                                            <td>
                                                <div style={{ display: 'flex', gap: '8px' }}>
                                                    <button onClick={() => setDownloadModal({ mode: 'edit', item: { ...d, id: d._id } })} className="btn" style={{ padding: '6px' }}><Edit size={16} /></button>
                                                    <button onClick={() => deleteDownload(d._id)} className="btn" style={{ padding: '6px', color: '#ef4444' }}><Trash2 size={16} /></button>
                                                    {d.link && (
                                                        <a href={d.link} target="_blank" rel="noopener noreferrer" className="btn" style={{ padding: '6px', color: '#3b82f6' }} title="Test Link">
                                                            <Download size={16} />
                                                        </a>
                                                    )}
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    </section>
                )}
            </div>

            {/* MODALS */}

            {/* Manual Generate Modal */}
            {manualModal && (
                <div className="modal-overlay">
                    <div className="modal-content">
                        <h3>Manual License Activation</h3>
                        <p style={{ fontSize: '0.85rem', color: '#666', marginBottom: '1rem' }}>
                            Generate a full license key for a client directly. No account required.
                        </p>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                            <input
                                type="text"
                                placeholder="Client Name / Issued To"
                                value={manualModal.name}
                                onChange={e => setManualModal({ ...manualModal, name: e.target.value })}
                                className="input-field"
                            />
                            <input
                                type="text"
                                placeholder="Hardware ID"
                                value={manualModal.hardwareId}
                                onChange={e => setManualModal({ ...manualModal, hardwareId: e.target.value })}
                                className="input-field"
                                style={{ fontFamily: 'monospace' }}
                            />
                            <div style={{ display: 'flex', gap: '1rem' }}>
                                <select
                                    value={manualModal.tier}
                                    onChange={e => setManualModal({ ...manualModal, tier: e.target.value })}
                                    className="input-field"
                                >
                                    <option value="Basic">Basic</option>
                                    <option value="Medium">Medium</option>
                                    <option value="Pro">Pro</option>
                                    <option value="Enterprise">Enterprise</option>
                                </select>
                                <input
                                    type="number"
                                    placeholder="Duration (Days)"
                                    value={manualModal.durationDays}
                                    onChange={e => setManualModal({ ...manualModal, durationDays: e.target.value })}
                                    className="input-field"
                                />
                            </div>
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                            <button onClick={() => setManualModal(null)} className="btn">Cancel</button>
                            <button onClick={handleManualGenerate} className="btn btn-primary">Generate & Activate</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Proof Modal */}
            {selectedProof && (
                <div className="modal-overlay">
                    <div className="modal-content" style={{ width: '90%', maxWidth: '1000px', height: '90%' }}>
                        <button onClick={() => setSelectedProof(null)} className="close-btn"><XCircle size={32} /></button>
                        {selectedProof.startsWith('data:application/pdf') ? (
                            <iframe src={selectedProof} style={{ width: '100%', height: '100%', border: 'none' }} />
                        ) : (
                            <NextImage src={selectedProof} alt="Proof" width={800} height={1200} unoptimized style={{ maxWidth: '100%', height: 'auto', margin: '0 auto' }} />
                        )}
                    </div>
                </div>
            )}

            {/* Reply Modal */}
            {replyModal && (
                <div className="modal-overlay">
                    <div className="modal-content">
                        <h3>Reply to {replyModal.name}</h3>

                        <div style={{ background: '#f8fafc', padding: '1rem', borderRadius: '8px', borderLeft: '4px solid #3b82f6', margin: '1rem 0', fontStyle: 'italic', color: '#555' }}>
                            <p style={{ fontSize: '0.85rem', fontWeight: 600, marginBottom: '0.3rem', color: '#3b82f6' }}>Original Message:</p>
                            <p style={{ fontSize: '0.9rem' }}>&quot;{replyModal.message}&quot;</p>
                        </div>

                        <textarea
                            value={replyModal.replyMessage}
                            onChange={(e) => setReplyModal({ ...replyModal, replyMessage: e.target.value })}
                            placeholder="Type your reply..."
                            style={{ width: '100%', height: '150px', padding: '10px', marginBottom: '1rem', border: '1px solid #ddd', borderRadius: '8px' }}
                        />
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
                            <button onClick={() => setReplyModal(null)} className="btn">Cancel</button>
                            <button onClick={sendReply} className="btn btn-primary">Send Email</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Plan Modal */}
            {planModal && (
                <div className="modal-overlay">
                    <div className="modal-content">
                        <h3>{planModal.mode === 'add' ? 'Add Plan' : 'Edit Plan'}</h3>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', marginTop: '1rem' }}>
                            <input type="text" placeholder="ID (unique slug)" value={planModal.plan.id || ''} onChange={e => setPlanModal({ ...planModal, plan: { ...planModal.plan, id: e.target.value } })} disabled={planModal.mode === 'edit'} className="input-field" />
                            <input type="text" placeholder="Name" value={planModal.plan.name || ''} onChange={e => setPlanModal({ ...planModal, plan: { ...planModal.plan, name: e.target.value } })} className="input-field" />
                            <div style={{ display: 'flex', gap: '1rem' }}>
                                <input type="number" placeholder="Price" value={planModal.plan.price || 0} onChange={e => setPlanModal({ ...planModal, plan: { ...planModal.plan, price: e.target.value } })} className="input-field" />
                                <input type="number" placeholder="Duration (Days)" value={planModal.plan.durationDays || 30} onChange={e => setPlanModal({ ...planModal, plan: { ...planModal.plan, durationDays: e.target.value } })} className="input-field" />
                            </div>
                            <select value={planModal.plan.tier || 'Basic'} onChange={e => setPlanModal({ ...planModal, plan: { ...planModal.plan, tier: e.target.value } })} className="input-field">
                                <option value="Basic">Basic</option>
                                <option value="Medium">Medium</option>
                                <option value="Pro">Pro</option>
                                <option value="Enterprise">Enterprise</option>
                            </select>
                            <textarea placeholder="Description" value={planModal.plan.description || ''} onChange={e => setPlanModal({ ...planModal, plan: { ...planModal.plan, description: e.target.value } })} className="input-field" style={{ height: '80px' }} />
                            <textarea placeholder="Features (comma separated)" value={Array.isArray(planModal.plan.features) ? planModal.plan.features.join(', ') : planModal.plan.features || ''} onChange={e => setPlanModal({ ...planModal, plan: { ...planModal.plan, features: e.target.value.split(',').map(s => s.trim()) } })} className="input-field" style={{ height: '80px' }} />
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                            <button onClick={() => setPlanModal(null)} className="btn">Cancel</button>
                            <button onClick={savePlan} className="btn btn-primary">Save Plan</button>
                        </div>
                    </div>
                </div>
            )}

            {/* Download Modal */}
            {downloadModal && (
                <div className="modal-overlay">
                    <div className="modal-content">
                        <h3>{downloadModal.mode === 'add' ? 'Add Version' : 'Edit Version'}</h3>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', marginTop: '1rem' }}>
                            <div style={{ display: 'flex', gap: '1rem' }}>
                                <input type="text" placeholder="Version (e.g. 1.2.1)" value={downloadModal.item.version || ''} onChange={e => setDownloadModal({ ...downloadModal, item: { ...downloadModal.item, version: e.target.value } })} className="input-field" />
                                <select value={downloadModal.item.os || 'Windows'} onChange={e => setDownloadModal({ ...downloadModal, item: { ...downloadModal.item, os: e.target.value } })} className="input-field">
                                    <option value="Windows">Windows</option>
                                    <option value="Linux">Linux</option>
                                </select>
                            </div>
                            <div style={{ display: 'flex', gap: '1rem' }}>
                                <select value={downloadModal.item.type || 'Installer'} onChange={e => setDownloadModal({ ...downloadModal, item: { ...downloadModal.item, type: e.target.value } })} className="input-field">
                                    <option value="Installer">Installer (.exe/.msi)</option>
                                    <option value="Archive">Archive (.zip/.tar.gz)</option>
                                </select>
                                <div style={{ display: 'flex', items: 'center', gap: '10px', padding: '10px', border: '1px solid #ddd', borderRadius: '8px', flex: 1 }}>
                                    <input
                                        type="checkbox"
                                        id="isFeatured"
                                        checked={downloadModal.item.isFeatured || false}
                                        onChange={e => setDownloadModal({ ...downloadModal, item: { ...downloadModal.item, isFeatured: e.target.checked } })}
                                    />
                                    <label htmlFor="isFeatured" style={{ cursor: 'pointer', fontSize: '0.9rem' }}>Featured Download</label>
                                </div>
                            </div>
                            <input type="text" placeholder="Download Link (URL)" value={downloadModal.item.link || ''} onChange={e => setDownloadModal({ ...downloadModal, item: { ...downloadModal.item, link: e.target.value } })} className="input-field" />
                            <textarea placeholder="Release Notes / Description" value={downloadModal.item.description || ''} onChange={e => setDownloadModal({ ...downloadModal, item: { ...downloadModal.item, description: e.target.value } })} className="input-field" style={{ height: '100px' }} />
                        </div>
                        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '1.5rem' }}>
                            <button onClick={() => setDownloadModal(null)} className="btn">Cancel</button>
                            <button onClick={saveDownload} className="btn btn-primary">Save Version</button>
                        </div>
                    </div>
                </div>
            )}

            <style jsx global>{`
                .status-pending { background: #fef3c7; color: #92400e; }
                .modal-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.7); z-index: 1000; display: flex; alignItems: center; justifyContent: center; padding: 1rem; }
                .modal-content { background: #fff; padding: 2rem; borderRadius: 16px; width: 100%; maxWidth: 600px; position: relative; max-height: 90vh; overflow-y: auto; }
                .close-btn { position: absolute; top: 10px; right: 10px; background: none; border: none; cursor: pointer; }
                .input-field { width: 100%; padding: 10px; border: 1px solid #ddd; border-radius: 8px; font-size: 0.95rem; }
            `}</style>
        </div>
    );
}

function TabBtn({ active, onClick, children }) {
    return (
        <button
            onClick={onClick}
            style={{
                background: active ? '#3b82f6' : 'transparent',
                color: '#fff',
                border: 'none',
                padding: '0.4rem 1rem',
                borderRadius: '6px',
                cursor: 'pointer',
                fontSize: '0.85rem',
                fontWeight: active ? 700 : 500
            }}
        >
            {children}
        </button>
    );
}

function StatCard({ title, value, color, icon }) {
    return (
        <div className="glass-card" style={{ background: '#fff', borderLeft: `4px solid ${color}` }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                    <p style={{ color: '#64748b', fontSize: '0.7rem', fontWeight: 800, textTransform: 'uppercase' }}>{title}</p>
                    <h2 style={{ fontSize: '1.8rem', fontWeight: 900, margin: '5px 0' }}>{value}</h2>
                </div>
                <div style={{ color, opacity: 0.6 }}>{icon}</div>
            </div>
        </div>
    );
}

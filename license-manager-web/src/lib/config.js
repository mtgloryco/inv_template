export const LICENSE_CONFIG = {
    plans: {
        'basic': {
            name: 'IMS Basic',
            durationDays: 30, // Monthly
            price: 5.00,
            tier: 'Basic',
            description: 'Digital Inventory Notebook. Perfect for small retailers needing essential stock control.'
        },
        'medium': {
            name: 'IMS Business',
            durationDays: 30, // Monthly
            price: 15.00,
            tier: 'Medium',
            description: 'The Smart Cashier. Full POS, Supplier Management, and multi-location tracking (up to 3).'
        },
        'pro': {
            name: 'IMS Professional',
            durationDays: 30, // Monthly
            price: 35.00,
            tier: 'Pro',
            description: 'Total ERP Suite. Advanced Analytics, Intelligent Forecasting, and unlimited locations.'
        },
        'enterprise': {
            name: 'IMS Enterprise',
            durationDays: 365,
            price: 0, // Contact Us
            tier: 'Enterprise',
            description: 'Global ERP Power. Cloud Sync, Audit Trails, and custom workflow automation.'
        }
    },
    limits: {
        maxActivePerUser: 10
    }
};

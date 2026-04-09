# API Integration Guide

This guide explains how to fetch the latest product version and download links from your Next.js app in other applications.

## 1. CORS Configuration
CORS has been enabled globally for all `/api` routes via `src/middleware.ts`. This allows any external domain to call your API.

## 2. The Version Endpoint
We created a clean endpoint specifically for external consumption:

**Endpoint:** `GET /api/public/version`

**Response Example:**
```json
{
    "version": "1.2.1",
    "downloadLink": "https://example.com/download/v1.2.1.exe",
    "os": "Windows",
    "releaseDate": "2026-04-09T18:00:00Z",
    "description": "Latest stable release with security patches."
}
```

## 3. How to use in another App (Javascript/React)

You can use the following code in any other application to fetch the latest version:

```javascript
async function getLatestVersion() {
    try {
        const response = await fetch('https://your-domain.com/api/public/version');
        
        if (!response.ok) {
            throw new Error('Failed to fetch version info');
        }

        const data = await response.json();
        
        console.log('Latest Version:', data.version);
        console.log('Download Link:', data.downloadLink);
        
        return data;
    } catch (error) {
        console.error('Error fetching version:', error);
    }
}

// Example usage
getLatestVersion().then(data => {
    if (data) {
        // Update your UI with the version and download button
        const versionEl = document.getElementById('version-display');
        const downloadBtn = document.getElementById('download-btn');
        
        if (versionEl) versionEl.innerText = `v${data.version}`;
        if (downloadBtn) downloadBtn.href = data.downloadLink;
    }
});
```

## 4. How it works
- The API connects to your MongoDB database.
- It searches the `downloads` collection for the most recent entry where `isFeatured` is set to `true`.
- It returns only the fields needed for a download page, keeping the payload small and clean.

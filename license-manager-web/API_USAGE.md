# API Integration Guide

This guide explains how to fetch the latest product version and download links from your Next.js app in other applications.

## 1. CORS Configuration
CORS has been enabled globally for all `/api` routes via `src/middleware.ts`. This allows any external domain to call your API.

## 2. The Version Endpoint
We created a clean endpoint specifically for external consumption:

**Endpoint:** `GET /api/public/version`

**Response Example:**
```json
[
    {
        "version": "1.2.1",
        "downloadLink": "https://example.com/download/v1.2.1.exe",
        "os": "Windows",
        "type": "Installer",
        "releaseDate": "2026-04-09T18:00:00Z",
        "description": "Latest stable release for Windows."
    },
    {
        "version": "1.2.1",
        "downloadLink": "https://example.com/download/v1.2.1.tar.gz",
        "os": "Linux",
        "type": "Archive",
        "releaseDate": "2026-04-09T18:00:00Z",
        "description": "Latest stable release for Linux."
    }
]
```

## 3. How to use in another App (Javascript/React)

You can use the following code in any other application to fetch the versions:

```javascript
async function getFeaturedVersions() {
    try {
        const response = await fetch('https://your-domain.com/api/public/version');
        
        if (!response.ok) {
            throw new Error('Failed to fetch versions');
        }

        const versions = await response.json(); // This is now an Array
        
        versions.forEach(v => {
            console.log(`${v.os} Version:`, v.version);
            console.log(`Download:`, v.downloadLink);
        });
        
        return versions;
    } catch (error) {
        console.error('Error fetching version:', error);
    }
}

// Example usage: Displaying a list of downloads
getFeaturedVersions().then(versions => {
    if (versions) {
        const listContainer = document.getElementById('download-list');
        if (!listContainer) return;

        listContainer.innerHTML = versions.map(v => `
            <div class="download-item">
                <h3>${v.os} (v${v.version})</h3>
                <p>${v.description}</p>
                <a href="${v.downloadLink}" class="btn">Download for ${v.os}</a>
            </div>
        `).join('');
    }
});
```

## 4. How it works
- The API connects to your MongoDB database.
- It searches the `downloads` collection for the most recent entry where `isFeatured` is set to `true`.
- It returns only the fields needed for a download page, keeping the payload small and clean.

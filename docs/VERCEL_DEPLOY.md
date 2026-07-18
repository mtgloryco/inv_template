# Deploy Glory Desk license portal on Vercel

## 1. Import project

- Push this repo to GitHub
- [vercel.com/new](https://vercel.com/new) → import repo
- Set **Root Directory** to `web`
- Framework: **Next.js** (auto-detected)

## 2. Database — Neon (recommended) or Vercel Postgres

The old `license-manager-web` used **MongoDB**. The new `web/` app uses **Postgres** only.

### Option A: Neon (free tier)

1. Go to [neon.tech](https://neon.tech) and create a project
2. Copy the **connection string** (starts with `postgresql://...`)
3. Paste it as `POSTGRES_URL` (and optionally `DATABASE_URL`) in Vercel env vars

Tables are created automatically on first API request (`ensureSchema()` in `web/lib/db.ts`).

### Option B: Vercel Postgres

- Project → Storage → Create **Postgres**
- Vercel sets `POSTGRES_URL` automatically

**Note:** Old MongoDB license records are **not** migrated automatically. New requests go into Postgres.

## 3. Pull env from license-manager-web

From the repo root:

```bash
cd web
node scripts/import-legacy-env.mjs
```

This reads `../license-manager-web/.env.local` and writes `web/.env.local` with mapped variables:

| Old (`license-manager-web`) | New (`web/`) |
|-----------------------------|--------------|
| `MONGODB_URI` | **Not used** — use `POSTGRES_URL` instead |
| `JWT_SECRET` | `JWT_SECRET` |
| `ADMIN_PASSWORD` | `ADMIN_API_KEY` |
| `LICENSE_PRIVATE_KEY` | `LICENSE_PRIVATE_KEY` (same PEM base64) |
| `SMTP_USER` / `SMTP_PASSWORD` | `SMTP_USER` / `SMTP_PASS` |
| `NEXT_PUBLIC_DOWNLOAD_URL_WINDOWS` | `GITHUB_RELEASE_URL` |

Then edit `web/.env.local` and set your **Neon** `POSTGRES_URL`.

## 4. Push env vars to Vercel

After `vercel link` in the `web/` folder:

```bash
cd web
chmod +x scripts/push-vercel-env.sh
./scripts/push-vercel-env.sh
```

Or paste manually in Vercel → Project → Settings → Environment Variables.

| Variable | Required | Notes |
|----------|----------|-------|
| `POSTGRES_URL` | Yes | Neon or Vercel Postgres connection string |
| `JWT_SECRET` | Yes | From old app |
| `ADMIN_EMAIL` | Yes | Admin login email for `/admin` |
| `ADMIN_PASSWORD` | Yes | Admin login password (seeded into Postgres on first login) |
| `ADMIN_API_KEY` | No | Optional API key (`X-Admin-Key` header) for scripts |
| `LICENSE_PRIVATE_KEY` | Yes | PEM base64 from old app (pairs with desktop public key) |
| `SMTP_HOST` | For email | `smtp.gmail.com` for Gmail |
| `SMTP_USER` / `SMTP_PASS` | For email | Gmail app password |
| `GITHUB_RELEASE_URL` | No | Windows installer / MediaFire link |

**Never commit** `.env.local` — it is gitignored.

## 5. Domain

- Add `glorydesk.mtglory.com` in Vercel → Domains

## 6. Local dev

```bash
cd web
node scripts/import-legacy-env.mjs   # once
# add POSTGRES_URL from Neon to .env.local
npm install
npm run dev
```

Open http://localhost:3000

## Note

- **License portal** → Vercel (`web/`)
- **Desktop cloud sync API** → `InventoryManagementSystem.Cloud` (deploy separately on Railway if needed)
- Desktop app public key matches `LICENSE_PRIVATE_KEY` from your old license web (same key pair)

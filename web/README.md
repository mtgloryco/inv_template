# Which folder to use?

| Folder | Purpose | Vercel |
|--------|---------|--------|
| **`web/`** | **Glory Desk** license portal (new — use this) | Set Root Directory to `web` |
| `license-manager-web/` | Old IMS project (different app) | Linked as project "ims" |

## Run Glory Desk locally

```bash
cd web
npm install
npm run dev
```

Open **http://localhost:3000**

## Admin portal (`/admin`)

Same workflow as old `license-manager-web`:

1. Customer submits at `/activate`
2. Admin → `/admin` → enter `ADMIN_API_KEY`
3. **Approve** → RSA key + email (if SMTP configured)
4. **Manual issue** → assign key without a prior request
5. Customer gets key via email or `/account/dashboard`

Old IMS admin is in `license-manager-web/` (MongoDB). **Use `web/` for Glory Desk.**

## Deploy Glory Desk to Vercel

1. Vercel → Project Settings → **Root Directory** → `web`
2. Add env vars from `web/.env.example`
3. Redeploy

Do **not** run `npm run dev` inside `license-manager-web` for Glory Desk.

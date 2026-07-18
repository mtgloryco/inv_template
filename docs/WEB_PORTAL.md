# Glory Desk license portal (separate repo)

The public website and admin portal (**glorydesk.mtglory.com**) is **not** in this repository.

| | |
|---|---|
| **Repo** | `mtgloryco/glorydesk-web` (private) |
| **Local folder** | Sibling directory: `../glorydesk-web/` |
| **Deploy** | Vercel — root directory `/` |
| **This repo** | Desktop app + Cloud API only |

## Why separate?

- Admin APIs and license signing stay out of the public desktop repo
- Private source on GitHub; public site still works for customers via Vercel
- Secrets only in Vercel env vars, never committed

## Setup (one time)

```bash
cd ../glorydesk-web
gh repo create mtgloryco/glorydesk-web --private --source=. --remote=origin --push
```

Full instructions: see `../glorydesk-web/docs/REPO_SETUP.md`

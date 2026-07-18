# Writing legal pages for Glory Desk

Use a generator to draft **Privacy Policy**, **Terms of Service**, and **EULA**, then paste the final text into `web/content/legal/` (see README there) or directly into `web/app/legal/*/page.tsx`.

**Have a lawyer review** anything before you rely on it in production — generators are starting points, not legal advice.

## Recommended generators (industry-standard templates)

### All-in-one (Privacy + Terms + Cookies + EULA)

| Site | Best for | URL |
|------|----------|-----|
| **Termly** | SaaS & software; GDPR/CCPA; EULA | https://termly.io |
| **TermsFeed** | Free/low-cost generators | https://www.termsfeed.com |
| **Iubenda** | Multi-country compliance, cookie consent | https://www.iubenda.com |
| **PrivacyPolicies.com** | Privacy + Terms bundles | https://www.privacypolicies.com |
| **GetTerms.io** | Quick Terms & Privacy | https://getterms.io |

### Privacy Policy only

| Site | URL |
|------|-----|
| Termly Privacy Generator | https://termly.io/products/privacy-policy-generator/ |
| TermsFeed Privacy Generator | https://www.termsfeed.com/privacy-policy-generator/ |
| FreePrivacyPolicy.com | https://www.freeprivacypolicy.com |

### Terms of Service

| Site | URL |
|------|-----|
| Termly Terms Generator | https://termly.io/products/terms-and-conditions-generator/ |
| TermsFeed T&C Generator | https://www.termsfeed.com/terms-conditions/generator/ |

### EULA (desktop / licensed software)

| Site | URL |
|------|-----|
| Termly EULA Generator | https://termly.io/products/end-user-license-agreement-generator/ |
| TermsFeed EULA Generator | https://www.termsfeed.com/eula-generator/ |
| Rocket Lawyer (paid, lawyer-backed) | https://www.rocketlawyer.com |

### Reference templates (read-only)

| Resource | URL |
|----------|-----|
| Basecamp open-source policies | https://basecamp.com/about/policies |
| GitHub `awesome-legal` list | https://github.com/kdeldycke/awesome-legal |

## What to select in the generator

When answering the questionnaire, choose options that match Glory Desk:

- **Product type:** Desktop software / licensed application (not pure SaaS)
- **Data collected:** Email, name/company, Hardware ID for licensing
- **Business data:** Stored locally on customer PC; optional cloud sync for Enterprise
- **Payments:** If you sell licenses online, mention payment processor
- **Hosting:** Vercel (website), Neon Postgres (license portal database)
- **Email:** SMTP for license delivery
- **Company:** MT GLORY CO, contact support@mtglory.com
- **Jurisdiction:** Rwanda (Law No. 058/2021 on personal data) + GDPR if you sell to EU

## Where to put finished policies in this repo

```
web/content/legal/privacy.md   → /legal/privacy
web/content/legal/terms.md     → /legal/terms
web/content/legal/eula.md      → /legal/eula
```

After you paste content, wire the pages to read those files (or copy HTML into the legal page components).

## Pages on the live site

- https://yoursite.com/legal/privacy
- https://yoursite.com/legal/terms
- https://yoursite.com/legal/eula

Footer links already point to these routes.

# Task 04 — auth lifecycle contract (target, 2026-10-03)

All routes use `/web/auth`, same origin FE proxy, JSON `ApiResponse<T>` (`success/data/message`). All auth POSTs require `Origin` in the configured allowlist and `X-CSRF-TOKEN` from `GET /web/auth/csrf`; missing/invalid checks return 403. `GET /csrf` returns `{csrfToken}` and sets the ASP.NET antiforgery cookie, Secure/HttpOnly/SameSite=Lax, Path `/web/auth`. FE keeps the returned token in memory and sends the header; no token is stored in localStorage/sessionStorage.

| Route | Request | Response | Cookies |
|---|---|---|---|
| `POST /register` | `{fullName,userName,email,password,phoneNumber?,address,dateOfBirth,sex}`; no role/permission fields | 202 acknowledgement; duplicate/invalid details 409; Student level resolved from active `Levels` row; email unverified | none |
| `POST /verify-email` | `{token}` | 200 verified; invalid/expired/used token 400 | none |
| `POST /resend-verification` | `{email}` | 200 generic acknowledgement for existent/nonexistent email; 429 for rate limit | none |
| `POST /login` | `{keyword,password}` | 200 `{accessToken,userId,userName,email,levelId,permissionIds}`; unverified/invalid login 401 | `__Secure-dacn-refresh`, HttpOnly, Secure, SameSite=Lax, Path `/web/auth`, finite MaxAge |
| `POST /refresh` | empty JSON body | 200 same access response; absent/expired/revoked/reused cookie 401 | rotate the same refresh cookie; never include refresh in JSON |
| `POST /logout` | empty JSON body + Bearer | 200 acknowledgement; unauthenticated 401 | revoke refresh family, deny current access jti until expiry, delete cookie using same Path/SameSite/Secure |

`GET /me` remains Bearer. Other BE endpoints remain Bearer. FE sets Axios `withCredentials` for same-origin `/web` calls and refreshes once on 401, with one in-tab refresh promise. Refresh token values are random; SQL stores SHA-256 hashes only. On rotation, a conditional update makes old token single use; reuse revokes its family. Existing plaintext refresh rows are hashed and revoked in the migration, so users reauthenticate after deployment.

Email verification tokens are random single-use values; SQL stores SHA-256 hash, expiry, consumed time and send time. Rate limits apply to register/login/resend/refresh and resend cooldown per account; per-IP limiter is local to an API instance. Development without SMTP writes verification messages to a local ignored maildrop; production requires SMTP configuration and public FE URL. Existing Users have no verified-email evidence: migration leaves `EmailVerifiedAt` NULL, so rollout must verify/backfill only records with trusted evidence or require verification before their next login. No automatic assumption that existing users are verified.

Verification links put the token in a URL fragment (`/auth/verify-email#token=...`), so the token is not sent in the FE page request. FE posts it in JSON to `/verify-email`. `GET /csrf` and the cookie are scoped to `/web/auth`; HTTPS Vite proxy serves the FE origin in development.

Production prerequisites: HTTPS same-origin reverse proxy preserving host/origin; explicit allowed origin; SMTP host/from/credentials via environment or secret store; DB migration and existing-account verification plan. Access TTL capped at 15 minutes. No Redis introduced.

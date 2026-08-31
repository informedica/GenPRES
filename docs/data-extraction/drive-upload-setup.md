# Google Drive upload setup for the FTK extraction pipeline

The FTK extraction FSI script exposes a `FTK_EXTRACT_UPLOAD=1` gate that uploads the Pass-1 TSV to Google Drive and converts it server-side to a Google Sheet at `My Drive/GenPRES/data/extraction/ftk_extract_pass1_<UTC>`. This runbook documents the one-time setup that gets that working from a personal `@gmail.com` account.

The script authenticates via Application Default Credentials (`GoogleCredential.GetApplicationDefault()`), which natively honours both gcloud user creds and a service-account JSON. On a personal Gmail account, the only path that actually *uploads* (not just creates folders) is gcloud user creds backed by a **custom OAuth client** — see "Why not a service account?" below for the reasoning.

> **No Drive folder pre-creation or sharing is needed for the user-creds path.** Files and folders are owned by you, written into your own My Drive. The "share folder with `xxx@…iam.gserviceaccount.com` as Editor" step you may have seen in earlier service-account guides applies to the SA path only and is irrelevant here.

## Prerequisites

- A Google Cloud project. The examples below use one named `genpres`; substitute your own.
- `gcloud` CLI installed and `gcloud auth login` (regular user login, separate from ADC) already done with the same Google account that owns the target Drive.
- The `Google.Apis.Drive.v3` NuGet ref is already declared in the script — no manual install needed.

## One-time setup

### 1. Enable the Drive (and Sheets) APIs on the project

```sh
gcloud services enable drive.googleapis.com sheets.googleapis.com --project=genpres
gcloud services list --enabled --project=genpres | grep -E 'drive|sheets'
```

Expected:

```text
drive.googleapis.com   Google Drive API
sheets.googleapis.com  Google Sheets API
```

### 2. Configure the OAuth consent screen

In the Cloud Console: **APIs & Services → OAuth consent screen**.

- **User type**: External (required for personal Gmail).
- **App name**: anything (e.g. `GenPRES`). The browser shows this on the consent page.
- **User support email**: your email.
- **Scopes** step: add `https://www.googleapis.com/auth/drive`. (You can leave the
  Sheets scope off — Drive is enough; converting TSV → Sheet happens via the Drive
  API's `mimeType = application/vnd.google-apps.spreadsheet` flag, not via the
  Sheets API.)
- **Test users** step: add **your own Gmail address**.
  In `Testing` publishing status, only listed test users can sign in. Skipping
  this gives `Error 403: access_denied — App has not completed Google
  verification` on login.

Leave the consent screen in `Testing` status — there's no need to publish for
verification when only you (the developer-tester) sign in.

### 3. Create a custom OAuth client ID

In the Cloud Console: **APIs & Services → Credentials → Create credentials → OAuth client ID**.

- **Application type**: Desktop app.
- **Name**: e.g. `genpres-ftk-extract`.
- After Create, click **Download JSON** and save the file to e.g.
  `~/secrets/genpres-oauth-client.json`. Treat it like a secret (don't commit it).

Why a custom client: gcloud's *default* OAuth client is being deprecated for
"wide" scopes like Drive, and re-running ADC login with `--scopes=…/drive`
against the default client triggers a Google warning ("scopes will be blocked
soon"). A custom client owned by your project avoids that.

### 4. Run `gcloud auth application-default login` against the custom client

Single line (zsh-safe — no continuations):

```sh
gcloud auth application-default login --client-id-file=$HOME/secrets/genpres-oauth-client.json --scopes=openid,https://www.googleapis.com/auth/userinfo.email,https://www.googleapis.com/auth/cloud-platform,https://www.googleapis.com/auth/drive
```

Notes:

- `cloud-platform` is **required** by the `gcloud auth application-default
  login` command itself — leaving it out gives `Invalid value for [--scopes]:
  https://www.googleapis.com/auth/cloud-platform scope is required`.
- Browser will open the consent flow. You'll see a yellow **"Google hasn't
  verified this app"** warning — click **Advanced → Go to GenPRES (unsafe)**.
  This warning is normal for an unverified app being used by its own
  developer-tester; it's not a sign-off on going to a malicious site.
- On success: `Credentials saved to file:
  [/Users/<you>/.config/gcloud/application_default_credentials.json]`. That's
  what the script reads.

### 5. Make sure the env doesn't override ADC with a service account

`GoogleCredential.GetApplicationDefault()` will prefer
`GOOGLE_APPLICATION_CREDENTIALS=<path-to-sa.json>` over the gcloud user creds
when set. Remove that line from `.env` if it's there:

```sh
sed -i.bak '/^GOOGLE_APPLICATION_CREDENTIALS=/d' /path/to/GenPRES/.env
```

(If you also created a service account during exploration, you can leave the
key file in place — the script just won't pick it up while the env var is
unset. To delete the SA itself: `gcloud iam service-accounts delete
<name>@<project>.iam.gserviceaccount.com`.)

### 6. (First run only) clean up any service-account-owned folders

If a prior pipeline run with a service account succeeded at the
folder-creation step but failed at file upload (the SA-quota error), you'll
have orphan `GenPRES/data/extraction` folders on Drive owned by the SA, not
you. They'll show under **Shared with me** rather than **My Drive**. Trash
them so the next run creates them fresh under your own ownership.

## Smoke test

Run from the directory containing the extraction script:

```sh
FTK_EXTRACT_RUN=1 FTK_EXTRACT_UPLOAD=1 dotnet fsi Informedica.NLP.Lib.fsx
```

Expected tail:

```text
[ftk_extract] DONE 1 generics in … s
[ftk_extract] deleted project sprj_…
[drive] using Application Default Credentials (UserCredential)
[drive] created folder 'GenPRES' under 'root' (id=…)
[drive] created folder 'data' under '…' (id=…)
[drive] created folder 'extraction' under '…' (id=…)
[ftk_extract] uploaded as Google Sheet 'ftk_extract_pass1_<UTC>' (id=…)
```

Subsequent runs reuse the existing folders (no `created folder` lines).

## Why not a service account?

A service account is the conventional automation pattern for Drive uploads, and
the script supports it (just point `GOOGLE_APPLICATION_CREDENTIALS` at the
service-account JSON — `GetApplicationDefault()` picks it up). But on a
personal `@gmail.com` account it hits a hard wall:

- Service accounts have **zero Drive storage quota** of their own.
- Files newly created by an SA are owned by the SA → they count against the
  SA's quota → upload fails with `Forbidden — The user's Drive storage quota
  has been exceeded`. Folders take no storage so SA folder creation succeeds,
  which makes the failure mode confusing on first hit.
- The standard fix is putting the SA in a **Shared Drive** (which has its own
  quota), but Shared Drives are a Workspace-only feature and aren't available
  on personal Gmail.

The SA path also requires an extra Drive-side step that the user-creds path
does not: you have to manually create a parent `GenPRES` folder in My Drive
and share it with the SA's email (`xxx@xxx.iam.gserviceaccount.com`) as
Editor, otherwise the SA can't see anywhere to write. With user creds, files
land in your own My Drive — no sharing dance needed.

So on a personal account: user OAuth creds are the realistic path, and a
custom OAuth client (steps 2–4) avoids the gcloud-default-client deprecation
warning.

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `Error 403: access_denied — App has not completed Google verification` on the consent page | OAuth consent screen is in `Testing` status and your account isn't in **Test users** | Add your Gmail under **Test users** (step 2) |
| `WARNING: scopes will be blocked soon for the default client ID` | Using gcloud's default OAuth client with `--scopes=…/drive` | Create a custom OAuth client (step 3) and pass `--client-id-file=…` |
| `Invalid value for [--scopes]: https://www.googleapis.com/auth/cloud-platform scope is required` | gcloud's ADC login command requires `cloud-platform` even if you don't need it | Add `cloud-platform` to `--scopes` |
| `(UserCredential)` printed but upload says `insufficient authentication scopes` | The user creds were minted without the Drive scope (e.g. plain `gcloud auth application-default login` without `--scopes`) | Re-run step 4 with the full `--scopes` list |
| `(ServiceAccountCredential)` printed and upload says `The user's Drive storage quota has been exceeded` | Authenticated as a service account, but personal Gmail doesn't give SAs storage | Switch to user creds — see step 5 |
| Upload fails with `invalid_grant — Token has been expired or revoked` | ADC refresh token is dead. On an OAuth consent screen left in `Testing` status, Google expires refresh tokens after **7 days**; also caused by password change or manual revoke | Re-run step 4 to mint fresh ADC creds. If it recurs weekly, the 7-day Testing-status cap is the root cause — move the consent screen to `In production` to stop the expiry |
| `[drive] created folder 'GenPRES' under 'root'` on every run, despite previous runs succeeding | The folders from earlier runs are owned by a different identity (service account) and aren't visible from the current user creds | Trash the SA-owned folders from **Shared with me** so the next run creates a fresh user-owned tree |
| Local TSV present, no upload attempted, no Drive log lines | `FTK_EXTRACT_UPLOAD` not set | `FTK_EXTRACT_UPLOAD=1` alongside `FTK_EXTRACT_RUN=1` |

## Reference

- Pipeline doc: [`doserule-extraction-flowchart.md`](doserule-extraction-flowchart.md)
- Drive API folder model: <https://developers.google.com/drive/api/guides/folder>
- TSV → Sheet conversion via metadata `mimeType`: <https://developers.google.com/drive/api/reference/rest/v3/files/create>

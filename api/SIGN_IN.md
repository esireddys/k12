# Sign-in

Parents sign in with Google. The browser gets a Google ID token, the API checks it with Google, and from then on
the API keeps the parent signed in with its own HttpOnly cookie (30 days, renewed while used). No token is kept in
the browser and no Google client secret is needed. Kids never sign in: they are profiles under the parent's account
and hold only a first name or nickname, a picture colour and a grade.

Guests pick a state and grade and practice with nothing saved.

Owners sign in the same way. Owner rights come only from `Access:Owners` (a list of emails), never from being a parent,
and only when Google owns that email: a Gmail address, or a Google Workspace account in its own domain (the ID token's
`hd` claim). Any other address on a Google account was verified once and could have changed hands since, so it can be a
parent but never an owner.

In the cloud this moves to Entra External ID (with Google as one of its providers); the API's `IExternalLoginVerifier`
port is where that plugs in. A verifier reports whether its provider owns the email, and the rest stays as it is.

## Turn on Google sign-in locally (once, about 10 minutes)

You need a Google account; your own Gmail is fine. Nothing here costs money and no billing account is needed.

### 1. Create a Google Cloud project

1. Open https://console.cloud.google.com/ and sign in.
2. Click the project picker at the top left (next to "Google Cloud"), then **New project**.
3. Project name `K12 Practice`. Leave Organization as "No organization". Click **Create**.
4. When the bell says it is ready, click **Select project**. The top bar now shows `K12 Practice`.

### 2. Set up the consent screen (Google Auth Platform)

1. In the search bar at the top, type `Google Auth Platform` and open it. (In older menus it is
   **APIs & Services > OAuth consent screen**.)
2. Click **Get started**.
3. **App information**: App name `K12 Practice`, User support email: your Gmail. **Next**.
4. **Audience**: choose **External**. **Next**.
5. **Contact information**: your Gmail. **Next**.
6. **Finish**: tick "I agree to the Google API Services: User Data Policy". **Continue**, then **Create**.
7. In the left menu click **Audience**. Under **Test users** click **Add users**, type your Gmail (and the Gmail of
   anyone else who will sign in, up to 100), then **Save**. While the app's status is "Testing", only these people can
   sign in. Leave it in Testing for local work; you never need to click "Publish app" for localhost.

You do not need to add any scopes under **Data Access**: sign-in asks only for name, email and the basic profile,
which every app gets.

### 3. Create the Web client ID

1. In the left menu click **Clients**, then **Create client**.
2. Application type: **Web application**. Name: `K12 local`.
3. Under **Authorized JavaScript origins** click **Add URI** twice and enter exactly:
   - `http://localhost:5173`
   - `http://localhost`

   No trailing slash, `http` not `https`, and `localhost` not `127.0.0.1`.
4. Leave **Authorized redirect URIs** empty: the button uses a popup, not a redirect.
5. Click **Create**. A box shows **Client ID** (it ends in `.apps.googleusercontent.com`). Copy it. Ignore the client
   secret: this app never uses it, so you do not need to download or store it.
   You can see the ID again any time under **Clients**.

Google says changes can take from 5 minutes to a few hours to apply; in practice a few minutes.

### 4. Give the ID to the API (Command Prompt)

```bat
cd C:\code\k12\api
dotnet user-secrets set "Auth:Google:ClientId" "PASTE-YOUR-ID.apps.googleusercontent.com" --project src\K12.Api
rem Make your Gmail an owner too (index 0 is owner@localhost from appsettings.Development.json):
dotnet user-secrets set "Access:Owners:1" "you@gmail.com" --project src\K12.Api
dotnet user-secrets list --project src\K12.Api
```

The client ID is public (the browser sees it), so user-secrets is for tidiness, not secrecy. Nothing goes in the web
app's `.env`: the web app asks the API for the ID at `/api/auth/options`.

### 5. Try it

1. Restart the API: `dotnet run --project src\K12.Api` (Ctrl+C first if it is running).
2. Start the web app if needed: `cd C:\code\k12\web` then `npm run dev`.
3. Open http://localhost:5173 (not 127.0.0.1). The Parents card shows **Continue with Google**.
4. Click it, pick your Gmail in the popup, and you land on the kid picker. Add a kid to check the family is saved.
5. If your Gmail is in `Access:Owners`, the welcome screen shows **Content review (owners)**.

Your Google family is a new family: kids you added earlier with the Developer sign-in belong to that developer
account, not to your Google account. Add them again under Google (or keep using the Developer sign-in for them).

### If it does not work

| What you see | Fix |
| --- | --- |
| "Google sign-in is not set up on this server yet" | The API did not get the ID. Check `dotnet user-secrets list --project src\K12.Api` shows `Auth:Google:ClientId`, then restart the API. |
| Popup says "The given origin is not allowed for the given client ID" or `origin_mismatch` | The origin list is missing `http://localhost:5173`, has a typo or trailing slash, or the page is on `127.0.0.1`. Fix it under **Clients > K12 local**, wait 5 minutes, hard refresh (Ctrl+F5). |
| Popup says "Access blocked: Authorization Error ... no registered origin", Error 401: invalid_client | The Web client has no JavaScript origins (or they were typed under redirect URIs instead). Under **Clients > K12 local**, add `http://localhost:5173` and `http://localhost` to **Authorized JavaScript origins**, Save, wait 5 minutes, hard refresh. |
| "Access blocked: K12 Practice has not completed the Google verification process" | That Gmail is not a test user. Add it under **Audience > Test users**. |
| "That sign-in could not be verified" | The token was for another client ID (the API and the Google client disagree), or the PC clock is far off. Compare the ID in user-secrets with **Clients**, and sync the Windows clock. |
| The button never appears, or the popup closes at once | A blocker or strict privacy setting is stopping `accounts.google.com`. Allow it for localhost, or try a normal Chrome window. |
| Signed in, but no **Content review** link | Your email is not in `Access:Owners`, or it is not a Gmail or Workspace address (see above). |

## Without Google (Development only)

The welcome screen shows a **Developer sign-in** when the API runs in Development: type any email and you are signed
in as that parent, with a real session cookie. Sign in as `owner@localhost` to see the owner review screens. Outside
Development this sign-in does not exist.

Scripts and tests can also act as a user with the `X-Dev-User` header (Development only). The AI reviewer
(`k12-pipeline review`) sends `X-Dev-User: review-agent@localhost`, which `Access:ReviewAgents` lets read its queue.

## Settings

| Key | What | Where |
| --- | --- | --- |
| `Auth:Google:ClientId` | Google client id (public, not a secret) | user-secrets locally, app settings in Azure |
| `Access:Owners` | Emails of app owners (Gmail or Google Workspace addresses when signing in with Google) | appsettings / user-secrets / Key Vault |
| `Access:ReviewAgents` | Names the AI reviewer job signs in as | appsettings |
| `Auth:Cookie:SameSite` | `Lax` (default) when the web app and API share a site; `None` if they are on different sites | app settings |

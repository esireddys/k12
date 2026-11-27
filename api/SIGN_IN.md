# Sign-in

Parents sign in with Google. The browser gets a Google ID token, the API checks it with Google, and from then on
the API keeps the parent signed in with its own HttpOnly cookie (30 days, renewed while used). No token is kept in
the browser and no Google client secret is needed. Kids never sign in: they are profiles under the parent's account
and hold only a first name or nickname, a picture colour and a grade.

Guests pick a state and grade and practice with nothing saved.

Owners sign in the same way. Owner rights come only from `Access:Owners` (a list of emails), never from being a parent.

In the cloud this moves to Entra External ID (with Google as one of its providers); the API's `IExternalLoginVerifier`
port is where that plugs in.

## Turn on Google sign-in locally (once)

1. Open https://console.cloud.google.com/ and create a project, for example "K12 Practice".
2. Open **Google Auth Platform** (in older menus: APIs & Services > OAuth consent screen) and click **Get started**.
   App name "K12 Practice", your email as support and contact email, Audience **External**. Create.
3. Under **Audience**, add your Gmail address (and anyone else who will sign in) as a **test user**. While the app is in
   Testing, only test users can sign in.
4. Under **Clients**, click **Create client**:
   - Application type: **Web application**, name "K12 local".
   - Authorized JavaScript origins: `http://localhost:5173` and `http://localhost`.
   - No redirect URIs are needed.
   - Create, then copy the **Client ID** (it ends in `.apps.googleusercontent.com`). You do not need the client secret.
5. In Command Prompt:

```bat
cd C:\code\k12\api
dotnet user-secrets set "Auth:Google:ClientId" "PASTE-YOUR-ID.apps.googleusercontent.com" --project src\K12.Api
rem Make your Gmail an owner too (index 0 is owner@localhost from appsettings.Development.json):
dotnet user-secrets set "Access:Owners:1" "you@gmail.com" --project src\K12.Api
dotnet user-secrets list --project src\K12.Api
```

6. Restart the API (`dotnet run --project src\K12.Api`) and refresh http://localhost:5173. The Parents card now shows
   **Continue with Google**.

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
| `Access:Owners` | Emails of app owners | appsettings / user-secrets / Key Vault |
| `Access:ReviewAgents` | Names the AI reviewer job signs in as | appsettings |
| `Auth:Cookie:SameSite` | `Lax` (default) when the web app and API share a site; `None` if they are on different sites | app settings |

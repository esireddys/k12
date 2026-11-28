# Deploying K12 Practice to Azure

Everything in Azure is Bicep in this folder, and GitHub Actions does the deploying. You do a one-time setup (below),
then every release is a button in GitHub, or automatic on merge to `main`.

## What gets created

One resource group per environment (`dev` now on the Visual Studio subscription, `prod` later on pay-as-you-go):

| Resource | Why | Rough cost a month |
| --- | --- | --- |
| Container App `k12<env>-api` | The API, with the web app served from the same address. Sleeps when nobody uses it. | $0 to $5 (inside the monthly free grant at family use) |
| Azure SQL Database `k12` (serverless) | All data. Pauses after an hour idle. Uses the Azure SQL free offer. | $0 within the free allowance |
| Storage account | `items` (question pictures), `raw` (released-test PDFs, private), `dataprotection` (sign-in cookie keys) | under $1 |
| Container Registry (Basic) | The app's images | about $5 |
| Key Vault | The import key, and the key that protects the sign-in cookie keys | under $1 |
| Log Analytics | App logs, capped at 1 GB a day | $0 to $3 |
| Managed identity `k12<env>-api-id` | How the app signs in to SQL, Storage and Key Vault. No passwords anywhere. | free |

Expect about **$6 to $15 a month** for dev, well inside the $150 Visual Studio credit. The Visual Studio subscription
has a spending limit, so Azure pauses services rather than billing you if credits ever run out.

Things that cost more were left out on purpose: Front Door (about $35 a month) and private networking. A custom domain
can be added to the Container App later at no cost.

Two things to know about the cheap setup:

- After the app has slept, the first visit takes a few seconds. After the database has paused (an hour with no use),
  the first page that needs data can take up to a minute. Setting `K12_MIN_REPLICAS` to `1` keeps the app warm (about
  $10 a month more); the database pause is what the free offer needs.
- The free SQL allowance is 100,000 vCore-seconds a month. In `dev`, if it runs out, the database pauses until the next
  month. In `prod` it keeps running and bills the extra.

## How the pieces fit

- **Infrastructure** workflow runs `main.bicep`: everything above except the app. Run it once, and again after a change
  in this folder.
- **Deploy** workflow builds the container image (API + web app), applies the EF Core migrations to Azure SQL, then
  runs `app.bicep` to roll out the new version, and checks `/health`.
- **Import content** workflow loads curriculum and released tests: it downloads your PDFs from the private `raw`
  container, renders the question pictures and uploads them to `items`, then sends everything to the API with the
  import key. It is the same `k12-pipeline import` you run locally, so released questions go Live and a repeat run
  refreshes forms without touching kids' progress.

All names, ids and settings are GitHub environment variables. Moving to another subscription means a new GitHub
environment with that subscription's values; no code changes.

## One-time setup (Windows Command Prompt)

You need: the Azure CLI (`winget install -e --id Microsoft.AzureCLI`), and optionally the GitHub CLI
(`winget install -e --id GitHub.cli`, then `gh auth login`) to set GitHub settings from cmd. Open a **new** Command
Prompt after installing.

Lines starting with `for /f` save a command's output in a variable. Type them as shown (single `%`) at the prompt.
Keep the same window open from step 1 to step 6 so the variables stay set.

### 1. Sign in and pick the subscription

```bat
az login
az account list -o table
az account set --subscription "<your Visual Studio subscription name>"
for /f "delims=" %i in ('az account show --query id -o tsv') do set SUB=%i
for /f "delims=" %i in ('az account show --query tenantId -o tsv') do set TENANT=%i
set RG=k12-dev
set LOC=southcentralus
echo %SUB% %TENANT%
```

`southcentralus` is in Texas. Any region with Container Apps works.

### 2. Register the services and make the resource group

```bat
for %p in (Microsoft.App Microsoft.OperationalInsights Microsoft.ContainerRegistry Microsoft.Sql Microsoft.KeyVault Microsoft.Storage Microsoft.ManagedIdentity) do az provider register --namespace %p
az group create -n %RG% -l %LOC%
```

### 3. Make the admin group and join it

Members of this group administer the database and can upload PDFs.

```bat
for /f "delims=" %i in ('az ad group create --display-name k12-admins --mail-nickname k12-admins --query id -o tsv') do set ADMIN_GROUP=%i
for /f "delims=" %i in ('az ad signed-in-user show --query id -o tsv') do set ME=%i
az ad group member add --group %ADMIN_GROUP% --member-id %ME%
echo %ADMIN_GROUP%
```

### 4. Let GitHub Actions sign in to Azure (OIDC, no stored password)

```bat
for /f "delims=" %i in ('az ad app create --display-name k12-github-dev --query appId -o tsv') do set APP_ID=%i
for /f "delims=" %i in ('az ad sp create --id %APP_ID% --query id -o tsv') do set SP_ID=%i
echo {"name":"github-env-dev","issuer":"https://token.actions.githubusercontent.com","subject":"repo:esireddys/k12:environment:dev","audiences":["api://AzureADTokenExchange"]} > fic-dev.json
az ad app federated-credential create --id %APP_ID% --parameters fic-dev.json
del fic-dev.json
az role assignment create --assignee-object-id %SP_ID% --assignee-principal-type ServicePrincipal --role Owner --scope /subscriptions/%SUB%/resourceGroups/%RG%
az ad group member add --group %ADMIN_GROUP% --member-id %SP_ID%
echo %APP_ID%
```

Owner is limited to this one resource group; it is needed because the Bicep grants the app's identity its access.
Joining the admin group lets the Deploy workflow apply database migrations.

### 5. Make the import key

```bat
for /f "delims=" %i in ('python -c "import secrets; print(secrets.token_hex(32))"') do set IMPORT_KEY=%i
```

### 6. Put the settings in GitHub

With the GitHub CLI, from `C:\code\k12`:

```bat
gh api -X PUT repos/esireddys/k12/environments/dev
gh variable set AZURE_CLIENT_ID --env dev --body %APP_ID%
gh variable set AZURE_TENANT_ID --env dev --body %TENANT%
gh variable set AZURE_SUBSCRIPTION_ID --env dev --body %SUB%
gh variable set AZURE_RESOURCE_GROUP --env dev --body %RG%
gh variable set K12_ADMIN_GROUP_NAME --env dev --body k12-admins
gh variable set K12_ADMIN_GROUP_ID --env dev --body %ADMIN_GROUP%
gh variable set K12_OWNERS --env dev --body "you@gmail.com"
gh variable set K12_GOOGLE_CLIENT_ID --env dev --body "<your Google web client id>.apps.googleusercontent.com"
gh secret set K12_IMPORT_KEY --env dev --body %IMPORT_KEY%
```

Or in the browser: repository **Settings > Environments > New environment** `dev`, then add the same names under
**Environment variables** and **Environment secrets**.

Optional variables on the environment:

| Variable | Default | Meaning |
| --- | --- | --- |
| `K12_INVITE_ONLY` | `true` | Only owners and `K12_ALLOWED_EMAILS` can sign in. Guests can still practice. Set `false` at launch. |
| `K12_ALLOWED_EMAILS` | empty | Comma-separated emails of invited families, during private testing |
| `K12_MIN_REPLICAS` | `0` | `1` keeps the app warm |
| `K12_SQL_FREE_OFFER` | `true` | Set `false` if this subscription already uses its free SQL database elsewhere |

### 7. Create the platform

GitHub > **Actions > Infrastructure > Run workflow**, environment `dev`. It takes about 5 minutes. Its summary lists
the storage account and SQL server names; you need them below.

### 8. Let the app's identity use the database (once)

The app signs in to SQL as its managed identity, which has to be added as a database user once. Open the firewall for
your PC, then use SSMS:

```bat
for /f "delims=" %i in ('curl -s https://api.ipify.org') do set MYIP=%i
az sql server firewall-rule create -g %RG% -s <sql server name> -n my-pc --start-ip-address %MYIP% --end-ip-address %MYIP%
```

In SSMS, connect to `<sql server name>.database.windows.net`, Authentication **Microsoft Entra MFA**, your Azure sign-in
as user name. Under **Options > Connection Properties**, set database to `k12`. If sign-in says your account is not in
the tenant, put the tenant id (`echo %TENANT%`) in **Options > Advanced > AD domain name or tenant ID**. Then open
`infra\sql\grant-api-identity.sql` and run it.

### 9. Deploy the app

GitHub > **Actions > Deploy > Run workflow**, environment `dev`. The summary shows the app's address, like
`https://k12dev-api.<something>.southcentralus.azurecontainerapps.io`.

### 10. Tell Google about the address

In Google Cloud Console > **Google Auth Platform > Clients**, open your web client and add the app's address (no
trailing slash) to **Authorized JavaScript origins**. Keep the localhost ones. It can take a few minutes to apply.

### 11. Upload the PDFs and import

```bat
az storage blob upload-batch --auth-mode login --account-name <storage account name> -d raw -s C:\code\k12\pipeline\data\raw --pattern "*.pdf"
```

The folders (`tx\math\...`) are kept, as the importer expects. If it says you lack permission, wait five minutes for
the new role to apply and try again. Then run GitHub > **Actions > Import content**, environment `dev`.

Run the upload and the import again whenever you download new PDFs.

### 12. Deploy on every merge (optional)

```bat
gh variable set K12_DEPLOY_DEV_ON_PUSH --body true
```

From then on, a merge to `main` that changes `api/`, `web/` or the app Bicep deploys to dev.

## Moving to pay-as-you-go later

Do steps 1 to 11 again with the pay-as-you-go subscription, `RG=k12-prod`, `k12-github-prod`, the subject
`repo:esireddys/k12:environment:prod`, and a GitHub environment named `prod`. Add required reviewers to the `prod`
environment so a production deploy waits for your click. Content comes over with Import content; family accounts made
during testing stay in dev unless we plan a data move.

## Doing it by hand

The workflows only call the Azure CLI, so each step can run from cmd too, for example:

```bat
az deployment group what-if -g %RG% -n k12-platform -f infra\main.bicep -p env=dev sqlAdminGroupName=k12-admins sqlAdminGroupObjectId=%ADMIN_GROUP% importKey=%IMPORT_KEY%
```

To run the pipeline from your PC against the deployed API instead of GitHub (the pictures still have to be uploaded to
`items`, as the Import content workflow does):

```bat
set K12_IMPORT_KEY=%IMPORT_KEY%
k12-pipeline import --api https://<app address>
```

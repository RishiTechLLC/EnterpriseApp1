# Permify on Azure App Service

`main.bicep` deploys Permify as a Linux container on Azure App Service, with its data in Azure Database for PostgreSQL.

| Resource | Purpose |
|----------|---------|
| App Service plan (Linux) + Web App | Runs `ghcr.io/permify/permify` with `serve`; HTTPS on 443 → container port 3476 |
| PostgreSQL Flexible Server 16 + `permify` database | Stores schemas, relationships and attributes (Permify auto-migrates on start) |
| Key Vault (RBAC) | Holds the database URI and the Permify API key; the web app reads them through Key Vault references using its managed identity |
| Log Analytics workspace | Container console, HTTP and platform logs |

Permify runs with **pre-shared key authentication**: every call except `/healthz` must send `Authorization: Bearer <key>`.

## Deploy

```bash
az login
az group create -n rg-permify -l eastus

export PERMIFY_PG_PASSWORD='<strong password>'
export PERMIFY_API_KEY=$(openssl rand -hex 32)   # keep this; the School Calendar API needs it

cd infra/permify
az deployment group create -g rg-permify -f main.bicep -p main.bicepparam
```

Check it:
```bash
URL=$(az deployment group show -g rg-permify -n main --query properties.outputs.permifyUrl.value -o tsv)
curl $URL/healthz                                                  # 200
curl -X POST $URL/v1/tenants/t1/schemas/list -d '{}'               # 401: missing bearer token
curl -X POST $URL/v1/tenants/t1/schemas/list -d '{}' \
     -H "Authorization: Bearer $PERMIFY_API_KEY"                    # 200
```
The first start pulls the image and runs database migrations, so it can take a few minutes.

## Connect the School Calendar API

Set these on the API's App Service (or in `appsettings.Production.json`):

```
Permify__BaseUrl = <permifyUrl output>
Permify__ApiKey  = @Microsoft.KeyVault(SecretUri=<apiKeySecretUri output>)
```
For the Key Vault reference, give the API's managed identity the **Key Vault Secrets User** role on the vault. Or set the key value directly.
The API writes the Permify schema (`permify/schema.perm`) itself at startup.

## Parameters

| Parameter | Default | Notes |
|-----------|---------|-------|
| `appServiceSkuName` | `B1` | Use `P0v3`+ for production |
| `instanceCount` | `1` | Permify is stateless (state is in Postgres), so it can scale out |
| `postgresSkuName` / `postgresSkuTier` | `Standard_B1ms` / `Burstable` | Use `GeneralPurpose` for production |
| `permifyImage` | `ghcr.io/permify/permify:v1.7.5` | Pin a version |
| `allowedIpRanges` | `[]` | e.g. `['20.1.2.3/32']` (the API's outbound IPs). Everything else gets 403 at the App Service front end |

## Production hardening

- **Private networking:** put the web app on a VNet (regional VNet integration). Give PostgreSQL private access and turn off its public access, then remove the `AllowAllAzureServices…` firewall rule. That rule allows any Azure-hosted client to try to connect, though the password still protects the server.
- **High availability:** set PostgreSQL `highAvailability.mode` to `ZoneRedundant` (General Purpose tier) and run 2+ App Service instances.
- **Key rotation:** Permify accepts several keys separated by commas, so you can add the new key alongside the old one, roll the clients over, then remove the old key.
- **gRPC:** App Service only exposes the REST port. If you need gRPC (port 3478), use Azure Container Apps instead.

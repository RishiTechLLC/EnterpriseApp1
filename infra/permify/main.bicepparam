using 'main.bicep'

param baseName = 'permify'
param appServiceSkuName = 'B1'
param instanceCount = 1
param permifyImage = 'ghcr.io/permify/permify:v1.7.5'

// Secrets are read from environment variables so they never land in source control:
//   export PERMIFY_PG_PASSWORD='<strong password>'
//   export PERMIFY_API_KEY=$(openssl rand -hex 32)
param postgresAdminPassword = readEnvironmentVariable('PERMIFY_PG_PASSWORD')
param permifyApiKey = readEnvironmentVariable('PERMIFY_API_KEY')

// Lock Permify down to the callers that need it (e.g. the School Calendar API's outbound IPs). Empty = API key only.
param allowedIpRanges = []

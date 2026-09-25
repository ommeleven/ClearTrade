#!/usr/bin/env bash
# Provisions the ClearTrade live demo on a PERSONAL Azure subscription at $0/month and wires up
# GitHub Actions (OIDC) so pushes to main redeploy automatically. Safe to re-run.
#
# Usage:
#   az login                                   # your personal account
#   export DATABASE_URL='postgresql://user:pass@host/db?sslmode=require'   # optional (e.g. Neon free tier)
#   (or put DATABASE_URL=... in .azure-secrets.env, which is git-ignored)
#   ./scripts/provision.sh
#
# Optional env: EXPECTED_SUBSCRIPTION (default "Azure for Students"), LOCATION (default centralus), RG (default rg-cleartrade), IMAGE, GITHUB_REPO, BUDGET_EMAIL
set -euo pipefail

LOCATION="${LOCATION:-centralus}"
RG="${RG:-rg-cleartrade}"
GITHUB_REPO="${GITHUB_REPO:-ommeleven/ClearTrade}"
IMAGE="${IMAGE:-ghcr.io/ommeleven/cleartrade-api:latest}"
BLOCKED_DOMAIN="perccent.com"
EXPECTED_SUBSCRIPTION="${EXPECTED_SUBSCRIPTION:-Azure for Students}"   # refuse to touch any other subscription
SECRETS_FILE="$(dirname "$0")/../.azure-secrets.env"   # git-ignored; keeps generated secrets stable across runs

say() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31mERROR: %s\033[0m\n' "$*" >&2; exit 1; }

command -v az >/dev/null || die "Azure CLI is required."
command -v gh >/dev/null || die "GitHub CLI is required."

# --- 1. Guard: never deploy to a work subscription ---------------------------------------------
ACCOUNT_USER="$(az account show --query user.name -o tsv 2>/dev/null)" || die "Run 'az login' with your personal account first."
SUBSCRIPTION_ID="$(az account show --query id -o tsv)"
SUBSCRIPTION_NAME="$(az account show --query name -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
if [[ "$(printf %s "$ACCOUNT_USER" | tr "[:upper:]" "[:lower:]")" == *"@${BLOCKED_DOMAIN}" ]]; then
  die "Signed in as ${ACCOUNT_USER}. This project must not use ${BLOCKED_DOMAIN} cloud resources. Run 'az logout' and 'az login' with your personal account."
fi
[[ "$SUBSCRIPTION_NAME" == "$EXPECTED_SUBSCRIPTION" ]] || die "Active subscription is '${SUBSCRIPTION_NAME}', expected '${EXPECTED_SUBSCRIPTION}'. Run: az account set -s \"${EXPECTED_SUBSCRIPTION}\""
say "Using subscription '${SUBSCRIPTION_NAME}' (${SUBSCRIPTION_ID}) as ${ACCOUNT_USER}"
BUDGET_EMAIL="${BUDGET_EMAIL:-$ACCOUNT_USER}"

# --- 2. The image must be publicly pullable (Container Apps pulls it without credentials) -------
say "Checking that ${IMAGE} is public on GHCR"
REPO_PATH="${IMAGE#ghcr.io/}"; REPO_PATH="${REPO_PATH%%:*}"; TAG="${IMAGE##*:}"
TOKEN="$(curl -fsS "https://ghcr.io/token?scope=repository:${REPO_PATH}:pull" | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')" \
  || die "Could not get an anonymous GHCR token for ${REPO_PATH}."
curl -fsS -o /dev/null -H "Authorization: Bearer ${TOKEN}" \
  -H "Accept: application/vnd.oci.image.index.v1+json,application/vnd.docker.distribution.manifest.v2+json" \
  "https://ghcr.io/v2/${REPO_PATH}/manifests/${TAG}" \
  || die "${IMAGE} is not publicly pullable. Let CI publish it, then set the package visibility to Public at https://github.com/users/${GITHUB_REPO%%/*}/packages/container/package/${REPO_PATH#*/}/settings"

# --- 3. Secrets (generated once, stored locally in a git-ignored file) --------------------------
ENV_DATABASE_URL="${DATABASE_URL:-}"   # an exported value wins over the saved one
[[ -f "$SECRETS_FILE" ]] && source "$SECRETS_FILE"
DATABASE_URL="${ENV_DATABASE_URL:-${DATABASE_URL:-}}"
JWT_KEY="${JWT_KEY:-$(openssl rand -base64 48 | tr -d '\n')}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-$(openssl rand -base64 18 | tr -d '\n/+=')A1!}"
printf 'JWT_KEY=%q\nADMIN_PASSWORD=%q\nDATABASE_URL=%q\n' "$JWT_KEY" "$ADMIN_PASSWORD" "${DATABASE_URL:-}" > "$SECRETS_FILE"
chmod 600 "$SECRETS_FILE"

# Npgsql needs key/value syntax; accept the postgres:// URI that Neon/Supabase hand out.
DB_CONNECTION=""
if [[ -n "${DATABASE_URL:-}" ]]; then
  DB_CONNECTION="$(python3 - "$DATABASE_URL" <<'PY'
import sys
from urllib.parse import urlparse, unquote, parse_qs
raw = sys.argv[1]
if not raw.startswith(("postgres://", "postgresql://")):
    print(raw); sys.exit()
u = urlparse(raw)
q = parse_qs(u.query)
parts = [f"Host={u.hostname}", f"Port={u.port or 5432}", f"Database={u.path.lstrip('/') or 'postgres'}",
         f"Username={unquote(u.username or '')}", f"Password={unquote(u.password or '')}",
         "SSL Mode=Require", "Trust Server Certificate=false", "Maximum Pool Size=10"]
print(";".join(parts))
PY
)"
  say "Database: external PostgreSQL configured"
else
  say "Database: none (DATABASE_URL not set) - the API will run in in-memory mode"
fi

# --- 4. Infrastructure ------------------------------------------------------------------------
say "Registering resource providers (first run only)"
for ns in Microsoft.App Microsoft.OperationalInsights Microsoft.Insights Microsoft.Consumption; do
  az provider register --namespace "$ns" --wait >/dev/null
done

say "Deploying infra/main.bicep to ${RG} (${LOCATION})"
az group create -n "$RG" -l "$LOCATION" --tags project=cleartrade cost=free-tier -o none
OUTPUTS="$(az deployment group create -g "$RG" -n cleartrade --template-file "$(dirname "$0")/../infra/main.bicep" \
  --parameters image="$IMAGE" jwtKey="$JWT_KEY" adminPassword="$ADMIN_PASSWORD" \
               databaseConnectionString="$DB_CONNECTION" budgetAlertEmail="$BUDGET_EMAIL" \
               budgetStartDate="$(date -u +%Y-%m-01)" githubRepo="$GITHUB_REPO" \
  --query properties.outputs -o json)"
URL="$(echo "$OUTPUTS" | python3 -c 'import sys,json;print(json.load(sys.stdin)["url"]["value"])')"
APP_NAME="$(echo "$OUTPUTS" | python3 -c 'import sys,json;print(json.load(sys.stdin)["containerAppName"]["value"])')"

# --- 5. GitHub Actions -> Azure via OIDC (managed identity created by the Bicep) ---------------
APP_ID="$(echo "$OUTPUTS" | python3 -c 'import sys,json;print(json.load(sys.stdin)["deployClientId"]["value"])')"

say "Setting GitHub repository variables on ${GITHUB_REPO}"
gh variable set AZURE_CLIENT_ID --repo "$GITHUB_REPO" --body "$APP_ID"
gh variable set AZURE_TENANT_ID --repo "$GITHUB_REPO" --body "$TENANT_ID"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$GITHUB_REPO" --body "$SUBSCRIPTION_ID"
gh variable set AZURE_RESOURCE_GROUP --repo "$GITHUB_REPO" --body "$RG"
gh variable set AZURE_CONTAINERAPP --repo "$GITHUB_REPO" --body "$APP_NAME"
gh variable set LIVE_URL --repo "$GITHUB_REPO" --body "$URL"

# --- 6. Smoke test ------------------------------------------------------------------------------
say "Waiting for ${URL}/health/ready (cold start can take ~30s)"
for _ in $(seq 1 30); do
  if curl -fsS -o /dev/null "${URL}/health/ready"; then
    say "Live: ${URL}   (Swagger: ${URL}/swagger)"
    echo "Admin password is in ${SECRETS_FILE} (git-ignored)."
    exit 0
  fi
  sleep 10
done
die "The app did not become ready. Check: az containerapp logs show -g ${RG} -n ${APP_NAME} --tail 100"

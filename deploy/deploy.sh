#!/usr/bin/env bash
# =======================================================================
#  ESGImpact - deploy de um ambiente (staging ou production)
# -----------------------------------------------------------------------
#  Equivalente Linux/macOS/WSL de deploy/deploy.ps1. Usado pelo pipeline
#  quando o runner self-hosted nao e Windows, ou manualmente:
#
#    export API_IMAGE=ghcr.io/jaowilqui/esgimpact-api:sha-abc1234
#    export MONGO_ROOT_PASSWORD=...  JWT_KEY=...
#    ./deploy/deploy.sh staging
#
#  Etapas: compose pull + up --wait -> smoke tests HTTP -> resumo.
#  Requer: docker (com compose v2), curl e jq.
# =======================================================================
set -euo pipefail

ENVIRONMENT="${1:-}"
case "$ENVIRONMENT" in staging|production) ;; *) echo "uso: $0 staging|production" >&2; exit 2;; esac

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
COMPOSE_FILE="$ROOT/deploy/docker-compose.deploy.yml"
ENV_FILE="$ROOT/deploy/$ENVIRONMENT.env"
PROJECT="esgimpact-$ENVIRONMENT"
EXPECTED_COMMIT="${GIT_SHA:-}"

: "${API_IMAGE:?Variavel API_IMAGE nao definida}"
: "${MONGO_ROOT_PASSWORD:?Segredo MONGO_ROOT_PASSWORD nao definido}"
: "${JWT_KEY:?Segredo JWT_KEY nao definido}"

cfg() { grep -E "^$1=" "$ENV_FILE" | head -1 | cut -d= -f2- | tr -d '\r'; }
PORT="$(cfg API_PORT)"
ENV_NAME="$(cfg ASPNETCORE_ENVIRONMENT)"
BASE_URL="http://localhost:$PORT"

compose() { docker compose -p "$PROJECT" --env-file "$ENV_FILE" -f "$COMPOSE_FILE" "$@"; }

diagnostics() {
  echo "::error::Deploy de $ENVIRONMENT falhou"
  echo "::group::Estado dos containers"; compose ps || true; echo "::endgroup::"
  echo "::group::Logs (ultimas 120 linhas)"; compose logs --tail 120 || true; echo "::endgroup::"
}
trap diagnostics ERR

retry() { # retry <tentativas> <comando...>
  local n=$1; shift
  for i in $(seq 1 "$n"); do
    if "$@"; then return 0; fi
    echo "  tentativa $i/$n falhou - aguardando 5s"; sleep 5
  done
  return 1
}

SUMMARY=""
ok() { echo "  OK  $1 - $2"; SUMMARY+="| $1 | $2 |"$'\n'; }

echo "== Deploy de $ENVIRONMENT =="
echo "   imagem : $API_IMAGE"
echo "   projeto: $PROJECT"
echo "   url    : $BASE_URL"

compose pull api
compose up -d --remove-orphans --wait --wait-timeout 240

echo "== Smoke tests =="

health_ok() { curl -fsS "$BASE_URL/api/health" | jq -e '.status=="ok" and .mongo=="up"' >/dev/null; }
retry 12 health_ok
ok "GET /api/health" "status=ok, mongo=up"

INFO="$(curl -fsS "$BASE_URL/api/info")"
[ "$(jq -r .environment <<<"$INFO")" = "$ENV_NAME" ] || { echo "ambiente inesperado: $INFO"; false; }
if [ -n "$EXPECTED_COMMIT" ]; then
  [ "$(jq -r .commit <<<"$INFO")" = "$EXPECTED_COMMIT" ] || { echo "commit inesperado: $INFO"; false; }
fi
ok "GET /api/info" "$(jq -r '"environment=\(.environment), version=\(.version), commit=\(.commit)"' <<<"$INFO")"

seed_ok() { curl -fsS "$BASE_URL/api/collections/stats" | jq -e '.collections.emission_factors >= 12' >/dev/null; }
retry 12 seed_ok
STATS="$(curl -fsS "$BASE_URL/api/collections/stats")"
ok "GET /api/collections/stats" "$(jq -r '.collections | to_entries | map("\(.key)=\(.value)") | join(", ")' <<<"$STATS")"

TOKEN="$(curl -fsS -X POST "$BASE_URL/api/auth/login" -H 'Content-Type: application/json' \
  -d '{"username":"analista","password":"Esg@2026"}' | jq -r .accessToken)"
[ -n "$TOKEN" ] && [ "$TOKEN" != "null" ]
ok "POST /api/auth/login" "token emitido"

DASH="$(curl -fsS "$BASE_URL/api/analytics/dashboard" -H "Authorization: Bearer $TOKEN")"
ok "GET /api/analytics/dashboard" "$(jq -r '"totalSimulations=\(.totalSimulations), totalKgCo2=\(.totalKgCo2)"' <<<"$DASH")"

trap - ERR
echo "== $ENVIRONMENT implantado com sucesso em $BASE_URL =="

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### Deploy em **$ENVIRONMENT** concluido"
    echo
    echo "| Item | Valor |"; echo "|---|---|"
    echo "| Imagem | \`$API_IMAGE\` |"
    echo "| URL | $BASE_URL/swagger |"
    echo
    echo "| Smoke test | Resultado |"; echo "|---|---|"
    printf '%s' "$SUMMARY"
  } >> "$GITHUB_STEP_SUMMARY"
fi

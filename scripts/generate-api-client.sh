#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
task_temp="$(mktemp -d)"
api_pid=""

cleanup() {
  if [[ -n "${api_pid}" ]] && kill -0 "${api_pid}" 2>/dev/null; then
    kill "${api_pid}"
    wait "${api_pid}" || true
  fi
  rm -rf "${task_temp}"
}
trap cleanup EXIT

cd "${repository_root}"
ASPNETCORE_ENVIRONMENT=Development \
ConnectionStrings__DirectiveDrift="Data Source=${task_temp}/openapi.db" \
TurnWorker__Enabled=false \
dotnet run --project src/DirectiveDrift.Api/DirectiveDrift.Api.csproj \
  --no-build --urls http://127.0.0.1:5078 >"${task_temp}/api.log" 2>&1 &
api_pid="$!"

generated=false
for _ in {1..80}; do
  if curl --fail --silent --show-error \
      http://127.0.0.1:5078/openapi/v1.json \
      --output "${task_temp}/api-v1.json" 2>/dev/null; then
    generated=true
    break
  fi
  if ! kill -0 "${api_pid}" 2>/dev/null; then
    cat "${task_temp}/api.log"
    exit 1
  fi
  sleep 0.25
done

if [[ "${generated}" != true ]] || [[ ! -s "${task_temp}/api-v1.json" ]]; then
  cat "${task_temp}/api.log"
  exit 1
fi

mv "${task_temp}/api-v1.json" openapi/api-v1.json
npm run generate:api --prefix src/DirectiveDrift.Web

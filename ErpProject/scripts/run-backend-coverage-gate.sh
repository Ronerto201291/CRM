#!/usr/bin/env bash
# Backend coverage gate: run tests with XPlat collector, merge reports, enforce thresholds.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BACKEND="$ROOT/backend"
UNIT_OUT="$BACKEND/TestResults/coverage-gate-unit"
INT_OUT="$BACKEND/TestResults/coverage-gate-int"

echo "==> Restore & build backend (Release)"
dotnet restore "$BACKEND/Erp.slnx" -v q
dotnet build "$BACKEND/Erp.slnx" -c Release --no-restore

echo "==> Architecture tests (no coverage)"
dotnet test "$BACKEND/tests/Erp.ArchitectureTests/Erp.ArchitectureTests.csproj" \
  -c Release --no-build --verbosity minimal

echo "==> Unit tests + XPlat coverage"
dotnet test "$BACKEND/tests/Erp.Tests/Erp.Tests.csproj" \
  -c Release --no-build \
  --results-directory "$UNIT_OUT" \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo "==> Integration tests + XPlat coverage"
dotnet test "$BACKEND/tests/Erp.IntegrationTests/Erp.IntegrationTests.csproj" \
  -c Release --no-build \
  --results-directory "$INT_OUT" \
  --collect:"XPlat Code Coverage" \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

UNIT_XML="$(find "$UNIT_OUT" -name 'coverage.cobertura.xml' | head -1)"
INT_XML="$(find "$INT_OUT" -name 'coverage.cobertura.xml' | head -1)"

if [[ -z "$UNIT_XML" || -z "$INT_XML" ]]; then
  echo "ERROR: coverage.cobertura.xml not found"
  exit 1
fi

echo "==> Coverage gate (unit: $UNIT_XML)"
echo "==> Coverage gate (integration: $INT_XML)"
python3 "$ROOT/scripts/check-coverage.py" \
  --unit "$UNIT_XML" \
  --integration "$INT_XML" \
  --thresholds "$ROOT/scripts/coverage-thresholds.json"

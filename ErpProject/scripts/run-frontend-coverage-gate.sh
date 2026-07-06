#!/usr/bin/env bash
# Frontend coverage gate: Vitest with v8 coverage + threshold enforcement.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FRONTEND="$ROOT/frontend"

cd "$FRONTEND"
npm ci
npm run test:coverage

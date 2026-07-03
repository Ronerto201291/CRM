#!/bin/bash
# ERP SaaS - Deploy script for Hetzner VPS (Ubuntu 22.04)
# Usage: ./deploy.sh

set -e

echo "🚀 Deploying ERP SaaS..."

# Pull latest code
echo "📥 Pulling latest code..."
cd /opt/erp
git pull origin main

# Build and restart containers
echo "🔨 Building Docker images..."
docker compose -f docker-compose.yml -f docker-compose.prod.yml down
docker compose -f docker-compose.yml -f docker-compose.prod.yml build --no-cache
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# Wait for services (backend runs migrations on startup — allow extra time on first deploy)
echo "⏳ Waiting for services to start..."
sleep 20

# Migrations run automatically via MigrateAsync() in Program.cs startup

# Health check — uses /health/ready (DB + Redis) and /health/live (liveness)
echo "🏥 Health check..."
curl -sf http://localhost:5000/health/live > /dev/null  && echo "✅ Backend live"  || echo "❌ Backend FAILED"
curl -sf http://localhost:5000/health/ready > /dev/null && echo "✅ Backend ready" || echo "⚠️  Backend not ready (DB/Redis may still be starting)"
curl -sf http://localhost:3000 > /dev/null              && echo "✅ Frontend OK"   || echo "❌ Frontend FAILED"

echo "🎉 Deployment complete!"

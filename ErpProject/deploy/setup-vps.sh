#!/bin/bash
# ERP SaaS - Initial VPS Setup (Ubuntu 22.04)
# Run once on fresh Hetzner VPS

set -e

echo "🔧 Setting up ERP SaaS VPS..."

# Update system
apt update && apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com | sh
systemctl enable docker

# Install Docker Compose
apt install -y docker-compose-plugin

# Install Nginx
apt install -y nginx

# Install Certbot for SSL
apt install -y certbot python3-certbot-nginx

# Install Tesseract OCR (para procesamiento de gastos)
apt install -y tesseract-ocr tesseract-ocr-spa

# Firewall
ufw allow 22/tcp     # SSH
ufw allow 80/tcp     # HTTP
ufw allow 443/tcp    # HTTPS
ufw --force enable

# Create project directory
mkdir -p /opt/erp
mkdir -p /opt/erp/backups
mkdir -p /opt/erp/uploads

# Set permissions
chown -R $USER:$USER /opt/erp

echo ""
echo "✅ VPS Setup complete!"
echo ""
echo "Next steps:"
echo "1. Clone your repo to /opt/erp"
echo "2. Copy deploy/nginx/erp.conf to /etc/nginx/sites-available/"
echo "3. ln -s /etc/nginx/sites-available/erp.conf /etc/nginx/sites-enabled/"
echo "4. Update erp.conf with your domain"
echo "5. certbot --nginx -d erp.tudominio.com"
echo "6. docker compose up -d"
echo "7. Add backup cron: crontab -e → 0 3 * * * /opt/erp/deploy/backup.sh"

#!/bin/bash

set -e

echo "========================================"
echo " ActualProductionManager Startup"
echo "========================================"

echo "[INFO] Update package list..."
apt-get -y update

echo "[INFO] Upgrade installed packages..."
apt-get -y upgrade

echo "[INFO] Check .NET SDK 8.0..."

if ! dpkg -l | grep -q 'dotnet-sdk-8.0'; then
    echo "[INFO] Install .NET SDK 8.0..."
    apt-get install -y dotnet-sdk-8.0
else
    echo "[INFO] .NET SDK 8.0 is already installed."
fi

echo "[INFO] Check nginx..."

if ! dpkg -l | grep -q 'nginx'; then
    echo "[INFO] Install nginx..."
    apt-get install -y nginx
else
    echo "[INFO] nginx is already installed."
fi

echo "[INFO] Start nginx..."
service nginx start

echo "[INFO] Check application file..."

if [ -f /var/app/ActualProductionManager.dll ]; then
    echo "[INFO] Starting ActualProductionManager..."
    cd /var/app || exit 1
    dotnet ActualProductionManager.dll
else
    echo "[ERROR] /var/app/ActualProductionManager.dll not found."
    echo "[ERROR] Please publish application files to /var/app."
    exit 1
fi
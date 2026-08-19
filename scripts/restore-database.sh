#!/usr/bin/env bash
# Restores a .bacpac backup into a NEW database (never overwrites the live one directly — see
# docs/06-runbook.md §3 for the full validation procedure before cutting over).
#
# Usage: ./restore-database.sh <resource-group> <sql-server-name> <new-database-name> <source-blob-sas-url>
set -euo pipefail

RESOURCE_GROUP="${1:?Uso: restore-database.sh <resource-group> <sql-server> <new-database> <blob-sas-url>}"
SQL_SERVER="${2:?}"
NEW_DATABASE="${3:?}"
SOURCE_BLOB_URL="${4:?}"

echo "Restaurando ${SOURCE_BLOB_URL} como nueva base ${NEW_DATABASE} en ${SQL_SERVER}..."
az sql db import \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name "$NEW_DATABASE" \
  --storage-uri "$SOURCE_BLOB_URL" \
  --admin-user "${SQL_ADMIN_LOGIN:?Defina SQL_ADMIN_LOGIN}" \
  --admin-password "${SQL_ADMIN_PASSWORD:?Defina SQL_ADMIN_PASSWORD}"

echo "Restauración solicitada en una base nueva. Valide su integridad antes de promoverla a producción."

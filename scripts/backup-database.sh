#!/usr/bin/env bash
# Exports the Azure SQL database to a .bacpac file in the given Storage Account container.
# Requires: az cli logged in, Contributor on the SQL server, a SAS URL to the destination blob.
#
# Usage: ./backup-database.sh <resource-group> <sql-server-name> <database-name> <destination-blob-sas-url>
set -euo pipefail

RESOURCE_GROUP="${1:?Uso: backup-database.sh <resource-group> <sql-server> <database> <blob-sas-url>}"
SQL_SERVER="${2:?}"
DATABASE="${3:?}"
DEST_BLOB_URL="${4:?}"

echo "Exportando ${DATABASE} de ${SQL_SERVER} a ${DEST_BLOB_URL}..."
az sql db export \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name "$DATABASE" \
  --storage-uri "$DEST_BLOB_URL" \
  --admin-user "${SQL_ADMIN_LOGIN:?Defina SQL_ADMIN_LOGIN}" \
  --admin-password "${SQL_ADMIN_PASSWORD:?Defina SQL_ADMIN_PASSWORD}"

echo "Exportación solicitada. Consulte el estado con: az sql db op-list --resource-group $RESOURCE_GROUP --server $SQL_SERVER --database $DATABASE"

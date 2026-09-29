#!/bin/sh
set -eu
# Creates each per-service database and grants the app user access to it.
# Validate identifiers before inserting them into SQL. Never drop existing data.
case "$MYSQL_USER" in ''|*[!a-zA-Z0-9_]*) echo "Invalid MySQL user name" >&2; exit 1;; esac
for db in $SERVICE_DB_NAMES; do
  case "$db" in *[!a-zA-Z0-9_]*) echo "Invalid database name: $db" >&2; exit 1;; esac
  printf 'CREATE DATABASE IF NOT EXISTS `%s`;\nGRANT ALL PRIVILEGES ON `%s`.* TO '\''%s'\''@'\''%%'\'';\n' \
    "$db" "$db" "$MYSQL_USER" |
    mysql --host=mysql --user=root
  echo "Ready: $db"
done

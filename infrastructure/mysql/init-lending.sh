#!/bin/sh
set -eu
# Validate identifiers before inserting them into SQL. Never drop existing data.
case "$LENDING_DB_NAME" in ''|*[!a-zA-Z0-9_]*) echo "Invalid lending database name" >&2; exit 1;; esac
case "$MYSQL_USER" in ''|*[!a-zA-Z0-9_]*) echo "Invalid MySQL user name" >&2; exit 1;; esac
printf 'CREATE DATABASE IF NOT EXISTS `%s`;\nGRANT ALL PRIVILEGES ON `%s`.* TO '\''%s'\''@'\''%%'\'';\n' \
  "$LENDING_DB_NAME" "$LENDING_DB_NAME" "$MYSQL_USER" |
  mysql --host=mysql --user=root

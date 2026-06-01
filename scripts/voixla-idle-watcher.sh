#!/bin/bash
# Stops the Voixla container after IDLE_MINUTES with no traffic, so the
# .NET + piper stack only runs while someone is actually using it.
#
# Activity is detected from the mtime of the dedicated nginx access log:
# nginx writes a line (and bumps the mtime) on every request to /voixla/.
# The wake server (voixla-wake-server.py) handles starting it back up.

CONTAINER="voixla"
IDLE_MINUTES=15
ACCESS_LOG="/var/log/nginx/voixla.access.log"
CHECK_INTERVAL=60

while true; do
    STATUS=$(docker inspect -f '{{.State.Running}}' "$CONTAINER" 2>/dev/null)
    if [ "$STATUS" = "true" ]; then
        now=$(date +%s)
        if [ -f "$ACCESS_LOG" ]; then
            last=$(stat -c %Y "$ACCESS_LOG")
        else
            last=0
        fi
        idle=$(( now - last ))

        if [ "$idle" -ge $(( IDLE_MINUTES * 60 )) ]; then
            echo "[$(date)] No /voixla requests for ${IDLE_MINUTES}m (idle ${idle}s), stopping container..."
            docker stop "$CONTAINER" >/dev/null 2>&1
        fi
    fi

    sleep "$CHECK_INTERVAL"
done

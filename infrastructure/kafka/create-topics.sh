#!/bin/sh
# Creates the Kafka topics Reading Pal needs, if they are missing.
# Run by the kafka-init service every time the stack starts. Existing topics and their
# messages are left untouched (--if-not-exists), so it is safe to run again and again.
# The broker has automatic topic creation switched off, so without this a fresh setup has
# no topics: publishers fail with "Unknown topic or partition" and no notifications arrive.

BOOTSTRAP="${KAFKA_BOOTSTRAP:-kafka:19092}"
TOPICS_CMD=/opt/kafka/bin/kafka-topics.sh
MAX_ATTEMPTS="${KAFKA_INIT_MAX_ATTEMPTS:-30}"
RETRY_SECONDS="${KAFKA_INIT_RETRY_SECONDS:-5}"

# Inventory events, then Lending events.
TOPICS="
book-created
book-updated
book-deleted
reservation-created
reservation-accepted
reservation-cancelled
book-returned
fine-recorded
book-due-soon
reservation-snapshot
"

attempt=1
until "$TOPICS_CMD" --bootstrap-server "$BOOTSTRAP" --list > /dev/null 2>&1; do
    if [ "$attempt" -ge "$MAX_ATTEMPTS" ]; then
        echo "kafka-init: ERROR - Kafka at $BOOTSTRAP did not answer after $MAX_ATTEMPTS attempts. No topics were created." >&2
        exit 1
    fi
    echo "kafka-init: Kafka at $BOOTSTRAP is not ready yet (attempt $attempt of $MAX_ATTEMPTS); retrying in ${RETRY_SECONDS}s."
    attempt=$((attempt + 1))
    sleep "$RETRY_SECONDS"
done

failed=""
for topic in $TOPICS; do
    # A single topic can fail while the broker is still settling, so each one gets a few tries of its own.
    tries=1
    until output=$("$TOPICS_CMD" --bootstrap-server "$BOOTSTRAP" --create --if-not-exists \
            --topic "$topic" --partitions 1 --replication-factor 1 2>&1); do
        if [ "$tries" -ge 5 ]; then
            echo "kafka-init: ERROR - could not create topic '$topic': $output" >&2
            failed="$failed $topic"
            break
        fi
        tries=$((tries + 1))
        sleep "$RETRY_SECONDS"
    done
    case " $failed " in
        *" $topic "*) ;;
        *) echo "kafka-init: topic '$topic' is ready." ;;
    esac
done

if [ -n "$failed" ]; then
    echo "kafka-init: ERROR - these topics are missing:$failed" >&2
    exit 1
fi

echo "kafka-init: all required topics exist."

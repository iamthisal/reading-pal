#!/bin/bash
# Startup command for the Azure Container Instances Kafka broker (readingpal-kafka).
# The broker keeps its data in the container's own filesystem, so every restart starts it
# empty: no topics, and automatic topic creation is switched off. This starts Kafka as the
# image normally would, then (re)creates the topics Reading Pal needs once the broker answers.
# It is the Azure counterpart of create-topics.sh, which the local kafka-init service runs;
# keep the two topic lists identical.

TOPICS="book-created book-updated book-deleted reservation-created reservation-accepted reservation-cancelled book-returned fine-recorded book-due-soon reservation-snapshot"
BOOTSTRAP=localhost:19092

/etc/confluent/docker/run &
KAFKA_PID=$!
trap 'kill -TERM "$KAFKA_PID"; wait "$KAFKA_PID"' TERM INT

until kafka-topics --bootstrap-server "$BOOTSTRAP" --list > /dev/null 2>&1; do
    kill -0 "$KAFKA_PID" 2> /dev/null || { echo "topic-init: Kafka exited before it was ready." >&2; exit 1; }
    echo "topic-init: waiting for Kafka at $BOOTSTRAP..."
    sleep 5
done

for topic in $TOPICS; do
    kafka-topics --bootstrap-server "$BOOTSTRAP" --create --if-not-exists \
        --topic "$topic" --partitions 1 --replication-factor 1 \
        || echo "topic-init: ERROR - could not create $topic" >&2
done
echo "topic-init: done - $(kafka-topics --bootstrap-server "$BOOTSTRAP" --list | grep -vc '^__') application topics present."

wait "$KAFKA_PID"

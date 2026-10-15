CREATE DATABASE IF NOT EXISTS semantic_engine;

-- Granular reasoning trace of the agent loop: one row per reasoning step
CREATE TABLE IF NOT EXISTS semantic_engine.thought_logs
(
    timestamp   DateTime64(3, 'UTC'),
    session_id  UUID,
    step_type   LowCardinality(String),
    tool_name   LowCardinality(String),
    content     String,
    duration_ms UInt32
)
ENGINE = MergeTree
PARTITION BY toYYYYMM(timestamp)
ORDER BY (session_id, timestamp);

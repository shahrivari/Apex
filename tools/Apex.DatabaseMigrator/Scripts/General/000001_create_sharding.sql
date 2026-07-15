CREATE TABLE Shards (
    id VARCHAR(50) NOT NULL,
    connection_name VARCHAR(100) NOT NULL,
    status VARCHAR(30) NOT NULL,
    schema_version VARCHAR(100) NULL,
    created_at DATETIME2(3) NOT NULL,
    modified_at DATETIME2(3) NOT NULL,
    version ROWVERSION NOT NULL,
    CONSTRAINT pk_shard PRIMARY KEY (id),
    CONSTRAINT ux_shard_connection_name UNIQUE (connection_name),
    CONSTRAINT ck_shard_status
        CHECK (status IN ('ACTIVE', 'DRAINING', 'SUSPENDED', 'FAILED'))
);

CREATE TABLE ShardAssignments (
    entity_type VARCHAR(100) NOT NULL,
    discriminator VARCHAR(200) NOT NULL,
    shard_id VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL,
    created_at DATETIME2(3) NOT NULL,
    modified_at DATETIME2(3) NOT NULL,
    version ROWVERSION NOT NULL,
    CONSTRAINT pk_shard_assignment PRIMARY KEY (entity_type, discriminator),
    CONSTRAINT fk_shard_assignment_shard FOREIGN KEY (shard_id) REFERENCES Shards(id),
    CONSTRAINT ck_shard_assignment_status
        CHECK (status IN ('ACTIVE', 'SUSPENDED'))
);

CREATE INDEX ix_shard_assignment_shard_status
ON ShardAssignments (shard_id, status);

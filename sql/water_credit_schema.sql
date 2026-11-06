CREATE TABLE dbo.water_account (
    account_id        VARCHAR(20)  NOT NULL PRIMARY KEY,
    property_id       VARCHAR(20)  NOT NULL,
    ratepayer_name    NVARCHAR(200) NOT NULL,
    meter_serial      VARCHAR(40)  NOT NULL,
    is_active         BIT          NOT NULL DEFAULT 1,
    updated_at        DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE dbo.water_meter_read (
    read_id           BIGINT IDENTITY PRIMARY KEY,
    account_id        VARCHAR(20)  NOT NULL,
    property_id       VARCHAR(20)  NOT NULL,
    meter_serial      VARCHAR(40)  NOT NULL,
    read_date         DATE         NOT NULL,
    cubic_metres      DECIMAL(12,2) NOT NULL,
    source            VARCHAR(40)  NOT NULL,
    is_estimated      BIT          NOT NULL DEFAULT 0,
    meter_fault       BIT          NOT NULL DEFAULT 0,
    council_leak      BIT          NOT NULL DEFAULT 0,
    ingested_at       DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT fk_read_account FOREIGN KEY (account_id) REFERENCES dbo.water_account(account_id)
);

CREATE TABLE dbo.water_credit_assessment (
    assessment_id     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    account_id        VARCHAR(20)  NOT NULL,
    read_id           BIGINT       NOT NULL,
    policy_reason     VARCHAR(30)  NOT NULL,
    status            VARCHAR(20)  NOT NULL,
    baseline_m3       DECIMAL(12,2) NOT NULL,
    actual_m3         DECIMAL(12,2) NOT NULL,
    excess_m3         DECIMAL(12,2) NOT NULL,
    credit_ex_gst     DECIMAL(12,2) NOT NULL,
    gst               DECIMAL(12,2) NOT NULL,
    credit_inc_gst    DECIMAL(12,2) NOT NULL,
    confidence        DECIMAL(5,4) NOT NULL,
    evidence_json     NVARCHAR(MAX) NOT NULL,
    created_at        DATETIME2    NOT NULL DEFAULT SYSUTCDATETIME(),
    approved_at       DATETIME2    NULL,
    approved_by       NVARCHAR(120) NULL
);

CREATE INDEX ix_water_read_account_date ON dbo.water_meter_read(account_id, read_date DESC);
CREATE INDEX ix_water_credit_status ON dbo.water_credit_assessment(status, created_at DESC);

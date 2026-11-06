CREATE TABLE IF NOT EXISTS lakehouse.water_credit_assessment (
    assessment_id STRING,
    account_id STRING,
    property_id STRING,
    policy_reason STRING,
    status STRING,
    baseline_m3 DECIMAL(12,2),
    actual_m3 DECIMAL(12,2),
    excess_m3 DECIMAL(12,2),
    credit_inc_gst DECIMAL(12,2),
    confidence DECIMAL(5,4),
    created_at TIMESTAMP
) USING DELTA;

CREATE OR REPLACE VIEW lakehouse.vw_water_credit_daily AS
SELECT
    date_trunc('day', created_at) AS credit_date,
    policy_reason,
    count(*) AS assessments,
    sum(CASE WHEN status = 'AutoCredit' THEN 1 ELSE 0 END) AS auto_credited,
    sum(CASE WHEN status = 'Review' THEN 1 ELSE 0 END) AS review_required,
    sum(credit_inc_gst) AS credit_value
FROM lakehouse.water_credit_assessment
GROUP BY 1, 2;

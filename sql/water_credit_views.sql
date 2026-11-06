CREATE OR ALTER VIEW dbo.vw_water_credit_candidates
AS
SELECT
    a.account_id,
    a.property_id,
    a.meter_serial,
    r.read_id,
    r.read_date,
    r.cubic_metres,
    r.source,
    r.is_estimated,
    r.meter_fault,
    r.council_leak,
    CAST(
        CASE
            WHEN r.property_id <> a.property_id OR r.meter_serial <> a.meter_serial THEN 'WrongAccount'
            WHEN r.council_leak = 1 THEN 'CouncilLeak'
            WHEN r.is_estimated = 1 OR r.source LIKE '%manual%' OR r.source LIKE '%correction%' THEN 'DataProcessingError'
            WHEN r.meter_fault = 1 THEN 'FaultyMeter'
            WHEN r.cubic_metres >= 4 * COALESCE(b.baseline_m3, 0) THEN 'Misread'
            WHEN r.cubic_metres <= 0.1 * COALESCE(b.baseline_m3, 0) THEN 'Misread'
            ELSE 'None'
        END AS VARCHAR(30)) AS policy_reason,
    COALESCE(b.baseline_m3, 0) AS baseline_m3
FROM dbo.water_account a
JOIN dbo.water_meter_read r ON r.account_id = a.account_id
OUTER APPLY (
    SELECT TOP 1
        AVG(prior.cubic_metres) OVER () AS baseline_m3
    FROM (
        SELECT TOP 6 cubic_metres
        FROM dbo.water_meter_read h
        WHERE h.account_id = a.account_id
          AND h.read_date < r.read_date
          AND h.is_estimated = 0
        ORDER BY h.read_date DESC
    ) prior
) b
WHERE a.is_active = 1;

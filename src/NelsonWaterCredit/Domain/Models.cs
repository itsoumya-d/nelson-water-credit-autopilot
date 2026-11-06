namespace NelsonWaterCredit.Domain;

public enum CreditReason
{
    None,
    Misread,
    FaultyMeter,
    DataProcessingError,
    WrongAccount,
    CouncilLeak
}

public enum CreditStatus
{
    NoCredit,
    Review,
    AutoCredit
}

public sealed record Account(
    string AccountId,
    string PropertyId,
    string RatepayerName,
    string MeterSerial,
    bool Active);

public sealed record MeterRead(
    string AccountId,
    string PropertyId,
    string MeterSerial,
    DateOnly ReadDate,
    decimal CubicMetres,
    string Source,
    bool IsEstimated = false,
    bool MeterFaultReported = false,
    bool CouncilLeak = false);

public sealed record WaterCreditPolicy(
    decimal VolumetricRatePerM3 = 2.80m,
    decimal GstRate = 0.15m,
    decimal ExcessMultiplier = 1.5m,
    int MinimumHistory = 3,
    int BaselineWindow = 6,
    decimal AutoCreditConfidence = 0.85m)
{
    public void Validate()
    {
        if (VolumetricRatePerM3 <= 0) throw new ArgumentOutOfRangeException(nameof(VolumetricRatePerM3));
        if (GstRate < 0 || GstRate >= 1) throw new ArgumentOutOfRangeException(nameof(GstRate));
        if (ExcessMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(ExcessMultiplier));
        if (MinimumHistory < 1) throw new ArgumentOutOfRangeException(nameof(MinimumHistory));
        if (BaselineWindow < MinimumHistory) throw new ArgumentOutOfRangeException(nameof(BaselineWindow));
        if (AutoCreditConfidence < 0 || AutoCreditConfidence > 1) throw new ArgumentOutOfRangeException(nameof(AutoCreditConfidence));
    }
}

public sealed record CreditAssessment(
    string AccountId,
    string PropertyId,
    DateOnly ReadDate,
    CreditReason Reason,
    CreditStatus Status,
    decimal BaselineM3,
    decimal ActualM3,
    decimal ExcessM3,
    decimal CreditExcludingGst,
    decimal Gst,
    decimal CreditIncludingGst,
    decimal Confidence,
    IReadOnlyList<string> Evidence);

public sealed record CreditSummary(
    int Assessed,
    int AutoCredited,
    int ReviewRequired,
    int NoCredit,
    decimal TotalCreditIncludingGst);

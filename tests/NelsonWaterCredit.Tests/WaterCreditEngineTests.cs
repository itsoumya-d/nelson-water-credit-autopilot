using Xunit;
using NelsonWaterCredit.Domain;

namespace NelsonWaterCredit.Tests;

public class WaterCreditEngineTests
{
    private static readonly Account Account = new("A100", "P100", "T. Kākahi", "M-100", true);
    private static readonly WaterCreditPolicy Policy = new();

    private static MeterRead Read(
        decimal usage,
        string propertyId = "P100",
        string meterSerial = "M-100",
        bool estimated = false,
        bool fault = false,
        bool leak = false,
        string source = "field-read") =>
        new("A100", propertyId, meterSerial, new DateOnly(2026, 11, 6), usage, source, estimated, fault, leak);

    private static List<MeterRead> History(params decimal[] values) =>
        values.Select((value, index) => new MeterRead(
            "A100", "P100", "M-100", new DateOnly(2026, 5 + index / 3, 1 + index % 3), value, "field-read"))
        .ToList();

    private static List<MeterRead> GoodHistory() => History(12, 14, 13, 15, 12, 13);

    [Fact]
    public void Median_OddCount() => Assert.Equal(13m, WaterCreditEngine.Median(new[] { 12m, 15m, 13m }));

    [Fact]
    public void Median_EvenCount() => Assert.Equal(13.5m, WaterCreditEngine.Median(new[] { 12m, 15m, 13m, 14m }));

    [Fact]
    public void Median_Empty() => Assert.Equal(0m, WaterCreditEngine.Median(Array.Empty<decimal>()));

    [Fact]
    public void Assess_UsesMedianBaseline()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Equal(13m, result.BaselineM3);
    }

    [Fact]
    public void Assess_WrongPropertyIsWrongAccount()
    {
        var result = WaterCreditEngine.Assess(Read(40, propertyId: "P999"), GoodHistory(), Account, Policy);
        Assert.Equal(CreditReason.WrongAccount, result.Reason);
    }

    [Fact]
    public void Assess_WrongMeterSerialIsWrongAccount()
    {
        var result = WaterCreditEngine.Assess(Read(40, meterSerial: "M-999"), GoodHistory(), Account, Policy);
        Assert.Equal(CreditReason.WrongAccount, result.Reason);
    }

    [Fact]
    public void Assess_CouncilLeakIsCredited()
    {
        var result = WaterCreditEngine.Assess(Read(90, leak: true), History(20, 22, 19, 21, 20, 21), Account, Policy);
        Assert.Equal(CreditReason.CouncilLeak, result.Reason);
    }

    [Fact]
    public void Assess_EstimatedReadIsDataProcessing()
    {
        var result = WaterCreditEngine.Assess(Read(30, estimated: true), History(10, 11, 9, 10, 12, 10), Account, Policy);
        Assert.Equal(CreditReason.DataProcessingError, result.Reason);
    }

    [Fact]
    public void Assess_ManualCorrectionIsDataProcessing()
    {
        var result = WaterCreditEngine.Assess(Read(30, source: "manual-correction"), History(10, 11, 9, 10, 12, 10), Account, Policy);
        Assert.Equal(CreditReason.DataProcessingError, result.Reason);
    }

    [Fact]
    public void Assess_MeterFaultIsCredited()
    {
        var result = WaterCreditEngine.Assess(Read(40, fault: true), History(10, 11, 9, 10, 12, 10), Account, Policy);
        Assert.Equal(CreditReason.FaultyMeter, result.Reason);
    }

    [Fact]
    public void Assess_HighReadIsMisread()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Equal(CreditReason.Misread, result.Reason);
    }

    [Fact]
    public void Assess_LowReadIsMisread()
    {
        var result = WaterCreditEngine.Assess(Read(1), History(10, 11, 9, 10, 12, 10), Account, Policy);
        Assert.Equal(CreditReason.Misread, result.Reason);
    }

    [Fact]
    public void Assess_NormalReadNoCredit()
    {
        var result = WaterCreditEngine.Assess(Read(16), History(15, 14, 16, 15, 14, 15), Account, Policy);
        Assert.Equal(CreditReason.None, result.Reason);
        Assert.Equal(0m, result.CreditIncludingGst);
    }

    [Fact]
    public void Assess_InactiveAccountNoCredit()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account with { Active = false }, Policy);
        Assert.Equal(CreditStatus.NoCredit, result.Status);
    }

    [Fact]
    public void Assess_CalculatesExcess()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Equal(55m, result.ExcessM3);
    }

    [Fact]
    public void Assess_CalculatesCreditExcludingGst()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Equal(154m, result.CreditExcludingGst);
    }

    [Fact]
    public void Assess_CalculatesGst()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Equal(23.10m, result.Gst);
    }

    [Fact]
    public void Assess_CalculatesCreditIncludingGst()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Equal(177.10m, result.CreditIncludingGst);
    }

    [Fact]
    public void Assess_HighConfidenceAutoCredits()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Equal(CreditStatus.AutoCredit, result.Status);
        Assert.Equal(0.95m, result.Confidence);
    }

    [Fact]
    public void Assess_LowHistorySendsToReview()
    {
        var result = WaterCreditEngine.Assess(Read(40), History(10, 10), Account, Policy);
        Assert.Equal(CreditStatus.Review, result.Status);
        Assert.Equal(0.65m, result.Confidence);
    }

    [Fact]
    public void Assess_ThreeHistoryIsReviewConfidence()
    {
        var result = WaterCreditEngine.Assess(Read(40), History(10, 10, 10), Account, Policy);
        Assert.Equal(0.85m, result.Confidence);
        Assert.Equal(CreditStatus.AutoCredit, result.Status);
    }

    [Fact]
    public void Assess_WrongAccountCreditsFullUsage()
    {
        var result = WaterCreditEngine.Assess(Read(40, propertyId: "P999"), GoodHistory(), Account, Policy);
        Assert.Equal(40m, result.ExcessM3);
        Assert.Equal(128.80m, result.CreditIncludingGst);
    }

    [Fact]
    public void Assess_DataProcessingCreditsFullUsage()
    {
        var result = WaterCreditEngine.Assess(Read(30, estimated: true), History(10, 11, 9, 10, 12, 10), Account, Policy);
        Assert.Equal(30m, result.ExcessM3);
        Assert.Equal(96.60m, result.CreditIncludingGst);
    }

    [Fact]
    public void Assess_NoReasonHasZeroExcess()
    {
        var result = WaterCreditEngine.Assess(Read(16), History(15, 14, 16, 15, 14, 15), Account, Policy);
        Assert.Equal(0m, result.ExcessM3);
    }

    [Fact]
    public void Assess_IgnoresHistoryForAnotherAccount()
    {
        var other = History(10, 10, 10).Select(read => read with { AccountId = "A999" }).ToList();
        var result = WaterCreditEngine.Assess(Read(68), other, Account, Policy);
        Assert.Equal(0m, result.BaselineM3);
        Assert.Equal(CreditReason.None, result.Reason);
    }

    [Fact]
    public void Assess_EvidenceIncludesBaseline()
    {
        var result = WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy);
        Assert.Contains(result.Evidence, item => item.Contains("Baseline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Summary_CountsStatuses()
    {
        var rows = new[]
        {
            WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy),
            WaterCreditEngine.Assess(Read(40), History(10, 10), Account, Policy),
            WaterCreditEngine.Assess(Read(16), History(15, 14, 16, 15, 14, 15), Account, Policy)
        };
        var summary = WaterCreditEngine.Summarise(rows);
        Assert.Equal(3, summary.Assessed);
        Assert.Equal(1, summary.AutoCredited);
        Assert.Equal(1, summary.ReviewRequired);
        Assert.Equal(1, summary.NoCredit);
    }

    [Fact]
    public void Summary_SumsCredits()
    {
        var summary = WaterCreditEngine.Summarise(new[]
        {
            WaterCreditEngine.Assess(Read(68), GoodHistory(), Account, Policy),
            WaterCreditEngine.Assess(Read(40, propertyId: "P999"), GoodHistory(), Account, Policy)
        });
        Assert.Equal(305.90m, summary.TotalCreditIncludingGst);
    }

    [Fact]
    public void Policy_RejectsNegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaterCreditPolicy(VolumetricRatePerM3: -1m).Validate());
    }

    [Fact]
    public void Policy_RejectsInvalidGst()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaterCreditPolicy(GstRate: 1m).Validate());
    }
}

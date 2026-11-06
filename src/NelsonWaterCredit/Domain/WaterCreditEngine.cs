namespace NelsonWaterCredit.Domain;

public static class WaterCreditEngine
{
    public static CreditAssessment Assess(
        MeterRead read,
        IReadOnlyList<MeterRead> history,
        Account account,
        WaterCreditPolicy? policy = null)
    {
        policy ??= new WaterCreditPolicy();
        policy.Validate();
        if (read.CubicMetres < 0) throw new ArgumentOutOfRangeException(nameof(read), "Meter read cannot be negative.");

        var orderedHistory = history
            .Where(item => item.AccountId == account.AccountId)
            .OrderBy(item => item.ReadDate)
            .TakeLast(policy.BaselineWindow)
            .ToList();

        var baseline = Baseline(orderedHistory);
        var reason = DetectReason(read, account, orderedHistory, baseline, policy);
        var confidence = Confidence(orderedHistory.Count, policy.MinimumHistory);
        var chargeable = reason is CreditReason.WrongAccount or CreditReason.DataProcessingError
            ? 0m
            : baseline;
        var excess = reason == CreditReason.None ? 0m : Math.Max(0m, read.CubicMetres - chargeable);
        var credit = RoundMoney(excess * policy.VolumetricRatePerM3);
        var gst = RoundMoney(credit * policy.GstRate);
        var status = reason == CreditReason.None
            ? CreditStatus.NoCredit
            : confidence >= policy.AutoCreditConfidence
                ? CreditStatus.AutoCredit
                : CreditStatus.Review;

        return new CreditAssessment(
            read.AccountId,
            read.PropertyId,
            read.ReadDate,
            reason,
            status,
            Round(baseline),
            Round(read.CubicMetres),
            Round(excess),
            credit,
            gst,
            RoundMoney(credit + gst),
            confidence,
            Evidence(read, account, orderedHistory, baseline, reason));
    }

    public static CreditSummary Summarise(IEnumerable<CreditAssessment> assessments)
    {
        var rows = assessments.ToList();
        return new CreditSummary(
            rows.Count,
            rows.Count(row => row.Status == CreditStatus.AutoCredit),
            rows.Count(row => row.Status == CreditStatus.Review),
            rows.Count(row => row.Status == CreditStatus.NoCredit),
            RoundMoney(rows.Sum(row => row.CreditIncludingGst)));
    }

    public static decimal Median(IEnumerable<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        if (ordered.Count == 0) return 0m;
        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2m
            : ordered[middle];
    }

    private static decimal Baseline(IReadOnlyList<MeterRead> history)
    {
        var usable = history
            .Where(read => !read.IsEstimated && read.CubicMetres >= 0)
            .Select(read => read.CubicMetres)
            .ToList();
        return usable.Count == 0 ? 0m : Median(usable);
    }

    private static CreditReason DetectReason(
        MeterRead read,
        Account account,
        IReadOnlyList<MeterRead> history,
        decimal baseline,
        WaterCreditPolicy policy)
    {
        if (!account.Active) return CreditReason.None;
        if (read.PropertyId != account.PropertyId || read.MeterSerial != account.MeterSerial)
            return CreditReason.WrongAccount;
        if (read.CouncilLeak)
            return CreditReason.CouncilLeak;
        if (read.IsEstimated || read.Source.Contains("manual", StringComparison.OrdinalIgnoreCase)
            || read.Source.Contains("correction", StringComparison.OrdinalIgnoreCase))
            return CreditReason.DataProcessingError;
        if (read.MeterFaultReported)
            return CreditReason.FaultyMeter;
        if (baseline > 0 && read.CubicMetres >= baseline * 4m)
            return CreditReason.Misread;
        if (baseline > 0 && read.CubicMetres <= baseline * 0.1m)
            return CreditReason.Misread;
        return CreditReason.None;
    }

    private static decimal Confidence(int historyCount, int minimumHistory)
    {
        if (historyCount >= 6) return 0.95m;
        if (historyCount >= minimumHistory) return 0.85m;
        return 0.65m;
    }

    private static IReadOnlyList<string> Evidence(
        MeterRead read,
        Account account,
        IReadOnlyList<MeterRead> history,
        decimal baseline,
        CreditReason reason)
    {
        var evidence = new List<string>
        {
            $"Read {read.CubicMetres:0.##} m3 on {read.ReadDate:yyyy-MM-dd} from {read.Source}.",
            $"Baseline {baseline:0.##} m3 from {history.Count} prior reads."
        };
        if (read.PropertyId != account.PropertyId)
            evidence.Add($"Read property {read.PropertyId} does not match account property {account.PropertyId}.");
        if (read.MeterSerial != account.MeterSerial)
            evidence.Add($"Meter serial {read.MeterSerial} does not match account meter {account.MeterSerial}.");
        if (read.CouncilLeak) evidence.Add("Council-owned leak was reported against this meter.");
        if (read.IsEstimated) evidence.Add("Read is estimated rather than measured.");
        if (read.MeterFaultReported) evidence.Add("Meter fault was reported by the field crew.");
        if (reason == CreditReason.Misread) evidence.Add("Read is outside the normal usage band and needs a field check.");
        if (reason == CreditReason.None) evidence.Add("No policy cause for a water credit was detected.");
        return evidence;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

using System.Text;
using System.Text.Json.Serialization;
using NelsonWaterCredit.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
var app = builder.Build();

var account = new Account("A100", "P100", "T. Kākahi", "M-100", true);
var history = History("A100", "P100", "M-100", 12, 14, 13, 15, 12, 13);
var read = new MeterRead("A100", "P100", "M-100", new DateOnly(2026, 11, 6), 68, "field-read");
var assessment = WaterCreditEngine.Assess(read, history, account);

var noCredit = WaterCreditEngine.Assess(
    new MeterRead("A101", "P101", "M-101", new DateOnly(2026, 11, 6), 16, "field-read"),
    History("A101", "P101", "M-101", 15, 14, 16, 15, 14, 15),
    new Account("A101", "P101", "R. Smith", "M-101", true));

var wrongAccount = WaterCreditEngine.Assess(
    new MeterRead("A102", "P999", "M-102", new DateOnly(2026, 11, 6), 40, "field-read"),
    History("A102", "P102", "M-102", 18, 19, 17, 18, 20, 18),
    new Account("A102", "P102", "M. Patel", "M-102", true));

var councilLeak = WaterCreditEngine.Assess(
    new MeterRead("A103", "P103", "M-103", new DateOnly(2026, 11, 6), 90, "field-read", CouncilLeak: true),
    History("A103", "P103", "M-103", 20, 22, 19, 21, 20, 21),
    new Account("A103", "P103", "W. Apiata", "M-103", true));

var estimated = WaterCreditEngine.Assess(
    new MeterRead("A104", "P104", "M-104", new DateOnly(2026, 11, 6), 30, "manual-correction", IsEstimated: true),
    History("A104", "P104", "M-104", 10, 11, 9, 10, 12, 10),
    new Account("A104", "P104", "L. Chen", "M-104", true));

var review = WaterCreditEngine.Assess(
    new MeterRead("A105", "P105", "M-105", new DateOnly(2026, 11, 6), 50, "field-read", CouncilLeak: true),
    History("A105", "P105", "M-105", 10, 11),
    new Account("A105", "P105", "S. Williams", "M-105", true));

var sessions = new[] { assessment, noCredit, wrongAccount, councilLeak, estimated, review };
var summary = WaterCreditEngine.Summarise(sessions);

app.MapGet("/", () => Results.Content(Dashboard(sessions, summary), "text/html"));
app.MapGet("/api/assessments", () => Results.Json(sessions));
app.MapGet("/api/summary", () => Results.Json(summary));
app.MapPost("/api/assess", (AssessmentRequest request) =>
{
    var result = WaterCreditEngine.Assess(request.Read, request.History, request.Account, request.Policy);
    return Results.Json(result);
});

app.Run();

static List<MeterRead> History(string accountId, string propertyId, string meterSerial, params int[] values) =>
    values.Select((value, index) => new MeterRead(
        accountId,
        propertyId,
        meterSerial,
        new DateOnly(2026, 5 + index / 3, 1 + index % 3),
        value,
        "field-read")).ToList();

static string Dashboard(IReadOnlyList<CreditAssessment> rows, CreditSummary summary)
{
    var cards = $"""
        <div class="metric"><span>Assessed</span><strong>{summary.Assessed}</strong></div>
        <div class="metric"><span>Auto-credited</span><strong>{summary.AutoCredited}</strong></div>
        <div class="metric accent"><span>Review required</span><strong>{summary.ReviewRequired}</strong></div>
        <div class="metric accent"><span>Credit value</span><strong>{Money(summary.TotalCreditIncludingGst)}</strong></div>
        """;
    var body = new StringBuilder();
    foreach (var row in rows)
    {
        var tone = row.Status == CreditStatus.AutoCredit ? "ok" : row.Status == CreditStatus.Review ? "warn" : "neutral";
        body.Append($"""
            <tr>
              <td><strong>{Escape(row.AccountId)}</strong><span class="sub">{Escape(row.PropertyId)}</span></td>
              <td>{row.ReadDate:dd MMM yyyy}</td>
              <td>{row.BaselineM3:0.##} m³</td>
              <td>{row.ActualM3:0.##} m³</td>
              <td>{Reason(row.Reason)}</td>
              <td><span class="badge {tone}">{Status(row.Status)}</span></td>
              <td class="num">{Money(row.CreditIncludingGst)}</td>
            </tr>
            """);
    }

    return $$$"""
        <!doctype html>
        <html lang="en-nz">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Nelson water credit autopilot</title>
          <style>
            :root{--blue:#0077c8;--teal:#174a5b;--navy:#3d5997;--text:#555;--ink:#333;--line:#dedede;--pale:#f5fcff;--white:#fff;--dark:#2e2e2e}
            *{box-sizing:border-box}body{margin:0;color:var(--text);font-family:"Open Sans",Arial,sans-serif;font-size:16px;line-height:1.5}
            a{color:var(--blue)}.shell{max-width:1180px;margin:0 auto;padding:32px 24px}
            .top{display:flex;justify-content:space-between;align-items:flex-start;gap:24px;border-bottom:1px solid var(--line);padding-bottom:20px}
            .kicker{color:var(--blue);font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;margin:0 0 8px}
            h1{font-size:48px;line-height:1.05;color:var(--blue);font-weight:700;margin:0;max-width:800px}
            .lede{max-width:720px;margin:14px 0 0;font-size:18px;line-height:1.55}
            .tag{background:var(--pale);border-top:4px solid var(--blue);padding:14px 18px;color:var(--teal);font-size:12px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;white-space:nowrap}
            .metrics{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:24px 0}
            .metric{border:1px solid var(--line);padding:16px;background:var(--white)}
            .metric span{display:block;color:var(--text);font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.08em}
            .metric strong{display:block;font-size:28px;line-height:1.1;color:var(--teal);margin-top:8px}
            .metric.accent{border-top:4px solid var(--blue)}.metric.accent strong{color:var(--blue)}
            .grid{display:grid;grid-template-columns:1.7fr 1fr;gap:18px}
            .panel{border:1px solid var(--line);background:var(--white)}
            .panel-head{display:flex;justify-content:space-between;align-items:baseline;padding:14px 18px;border-bottom:1px solid var(--line)}
            .panel-head h2{font-size:20px;color:var(--teal);margin:0}.panel-head span{font-size:10px;text-transform:uppercase;letter-spacing:.08em;font-weight:700}
            table{width:100%;border-collapse:collapse;text-align:left}th{font-size:10px;text-transform:uppercase;letter-spacing:.07em;color:var(--text);padding:10px 12px;border-bottom:1px solid var(--line);font-weight:700}
            td{padding:12px;border-bottom:1px solid var(--line);vertical-align:top;font-size:12px}tr:last-child td{border-bottom:0}.sub{display:block;color:#888;font-size:10px;margin-top:2px}
            .num{text-align:right;font-variant-numeric:tabular-nums}.badge{display:inline-block;padding:3px 7px;font-size:9px;font-weight:700;text-transform:uppercase;letter-spacing:.05em}
            .badge.ok{background:#e8f6ee;color:#087a2c}.badge.warn{background:#fff1d6;color:#8a5a00}.badge.neutral{background:#f1f1f1;color:#555}
            .side{padding:18px}.side h2{font-size:20px;color:var(--teal);margin:0 0 12px}.side p{margin:0 0 12px;font-size:13px}.side ul{margin:0;padding:0;list-style:none}.side li{border-top:1px solid var(--line);padding:10px 0;font-size:12px}
            .fix{background:var(--pale);border-left:4px solid var(--blue);padding:12px 14px;margin-top:14px;color:var(--teal);font-size:12px}
            footer{display:flex;justify-content:space-between;gap:16px;margin-top:24px;padding-top:14px;border-top:1px solid var(--line);font-size:11px;color:#777}
            @media(max-width:920px){.metrics{grid-template-columns:repeat(2,1fr)}.grid{grid-template-columns:1fr}.top{flex-direction:column}h1{font-size:36px}}
          </style>
        </head>
        <body>
          <div class="shell">
            <header class="top">
              <div><p class="kicker">Nelson City Council · Water credit autopilot</p><h1>Credit the ratepayer before they have to ask.</h1><p class="lede">Detect meter misreads, faulty meters, data-processing errors, wrong-account reads and council leaks — then calculate an explainable water credit and route the exceptions.</p></div>
              <div class="tag">C# .NET 10<br>Azure Functions<br>Power Automate</div>
            </header>
            <section class="metrics">{{{cards}}}</section>
            <main class="grid">
              <section class="panel">
                <div class="panel-head"><h2>Credit assessments</h2><span>Live demo</span></div>
                <table><thead><tr><th>Account</th><th>Read date</th><th>Baseline</th><th>Actual</th><th>Policy cause</th><th>Status</th><th class="num">Credit incl. GST</th></tr></thead><tbody>{{{body}}}</tbody></table>
              </section>
              <aside class="panel side">
                <h2>Why this exists</h2>
                <p>Council’s Utility Forms page says credits are processed for meter misreads, faulty meters, data-processing errors, wrong-account assignments and council leaks. The ratepayer still has to submit a Water Credit Application.</p>
                <ul>
                  <li><strong>Before:</strong> manual form, manual evidence, manual review.</li>
                  <li><strong>After:</strong> policy-coded detection, baseline math, credit calculation, audit evidence and a review queue.</li>
                </ul>
                <div class="fix"><strong>Entrepreneur fix</strong><br>Auto-credit high-confidence cases; send only ambiguous exceptions to a person.</div>
              </aside>
            </main>
            <footer><span>Open Sans · #0077C8 · #174A5B · #DEDEDE · 0px radius</span><span>Reference implementation for Nelson City Council</span></footer>
          </div>
        </body>
        </html>
        """;
}

static string Money(decimal value) => "$" + value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
static string Reason(CreditReason reason) => reason switch
{
    CreditReason.Misread => "Meter misread",
    CreditReason.FaultyMeter => "Faulty meter",
    CreditReason.DataProcessingError => "Data processing",
    CreditReason.WrongAccount => "Wrong account",
    CreditReason.CouncilLeak => "Council leak",
    _ => "No policy cause"
};
static string Status(CreditStatus status) => status switch
{
    CreditStatus.AutoCredit => "Auto-credit",
    CreditStatus.Review => "Review",
    _ => "No credit"
};
static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);

public sealed record AssessmentRequest(
    MeterRead Read,
    List<MeterRead> History,
    Account Account,
    WaterCreditPolicy? Policy);

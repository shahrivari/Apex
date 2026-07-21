using System.Net;
using Apex.Modules.Accounting.JournalEntries.UseCases;

namespace Apex.IntegrationTests.Accounting.Scenarios.Infrastructure;

/// <summary>
/// Reusable assertion helpers that prove the three-view agreement required by spec §7: public API
/// response, authoritative <c>journal_entry</c>/<c>journal_entry_line</c> state, and derived
/// <c>daily_account_turnover</c>/<c>daily_account_balance</c> projections. All numeric comparisons
/// are exact <c>decimal</c> equality — no floating-point tolerances.
/// </summary>
public sealed class ScenarioAssertions(ScenarioApiClient api, ScenarioDatabaseInspector inspector)
{
    /// <summary>Verifies a Journal Entry API response matches the authoritative header and ordered lines.</summary>
    public async Task AssertEntryMatchesAuthoritativeStateAsync(
        JournalEntryDetailResponse entry, CancellationToken cancellationToken = default)
    {
        var header = await inspector.GetHeaderByReferenceAsync(entry.FiscalYearId, entry.ReferenceNumber, cancellationToken);
        Assert.True(header is not null,
            $"No authoritative journal_entry row for fiscal year {entry.FiscalYearId}, " +
            $"reference number {entry.ReferenceNumber}.");
        Assert.Equal(entry.Id, header!.Id);
        Assert.Equal(entry.AccountingBookId, header.AccountingBookId);
        Assert.Equal(entry.JournalEntryNumber, header.JournalEntryNumber);
        Assert.Equal(entry.NumberFinalized, header.NumberFinalized);
        Assert.Equal(entry.AccountingDate, header.AccountingDate);
        Assert.Equal(entry.Description, header.Description);
        Assert.Equal(entry.DocumentType, header.DocumentType);
        Assert.Equal(entry.InsertionType, header.InsertionType);
        Assert.Equal(entry.Status, header.Status);
        Assert.Equal(entry.BalanceEffect, header.BalanceEffect);
        Assert.Equal(entry.ReversalOfReferenceNumber, header.ReversalOfReferenceNumber);
        Assert.Equal(entry.ReversedByReferenceNumber, header.ReversedByReferenceNumber);

        var lines = await inspector.GetOrderedLinesAsync(entry.Id, cancellationToken);
        var apiLines = entry.Lines.OrderBy(line => line.RowNumber).ToList();
        Assert.Equal(apiLines.Count, lines.Count);
        for (var index = 0; index < apiLines.Count; index++)
        {
            var apiLine = apiLines[index];
            var dbLine = lines[index];
            Assert.Equal(apiLine.RowNumber, dbLine.RowNumber);
            Assert.Equal(apiLine.Side, dbLine.Side);
            Assert.Equal(apiLine.Amount, dbLine.Amount);
            Assert.Equal(apiLine.AccountClassCode, dbLine.AccountClassCode);
            Assert.Equal(apiLine.GeneralAccountCode, dbLine.GeneralAccountCode);
            Assert.Equal(apiLine.SubsidiaryAccountCode, dbLine.SubsidiaryAccountCode);
            Assert.Equal(apiLine.DetailAccountCode, dbLine.DetailAccountCode);
            Assert.Equal(apiLine.Description, dbLine.Description);
        }
    }

    /// <summary>
    /// Verifies the closing balance for one account grain agrees across all three views: the
    /// public balances report, the <c>daily_account_balance</c> projection, and a movement
    /// recomputed directly from posted financial lines. The expected value must come from the
    /// test itself (spec §6.1: "Amount and balance expectations must be explicitly supplied by tests").
    /// </summary>
    public async Task AssertClosingBalanceAsync(
        long accountingBookId, long fiscalYearId, string accountClassCode, string generalAccountCode,
        string? subsidiaryAccountCode, string? detailAccountCode, DateOnly asOfDate, decimal expectedClosingBalance,
        CancellationToken cancellationToken = default)
    {
        var reportResult = await api.GetAccountBalancesAsync(accountingBookId, fiscalYearId, asOfDate, cancellationToken);
        Assert.True(reportResult.IsSuccess,
            $"Balance report failed: {reportResult.StatusCode} — {reportResult.RawBody}");
        var reportedClosing = reportResult.Value!
            .Where(item => item.AccountClassCode == accountClassCode
                && (generalAccountCode is null || item.GeneralAccountCode == generalAccountCode)
                && (subsidiaryAccountCode is null || item.SubsidiaryAccountCode == subsidiaryAccountCode)
                && (detailAccountCode is null || item.DetailAccountCode == detailAccountCode))
            .Sum(item => item.ClosingBalance);
        Assert.Equal(expectedClosingBalance, reportedClosing);

        var projectionBalance = await inspector.GetClosingBalanceAsync(
            accountingBookId, fiscalYearId, asOfDate, accountClassCode, generalAccountCode, subsidiaryAccountCode,
            detailAccountCode, cancellationToken);
        Assert.Equal(expectedClosingBalance, projectionBalance);

        var authoritative = await inspector.ComputeAuthoritativeMovementAsync(
            accountingBookId, fiscalYearId, asOfDate, accountClassCode, generalAccountCode, subsidiaryAccountCode,
            detailAccountCode, cancellationToken: cancellationToken);
        Assert.Equal(expectedClosingBalance, authoritative.Net);
    }

    /// <summary>
    /// Runs a write operation expected to fail, then verifies the complete rejected-write contract
    /// (spec §7): HTTP status, <c>application/problem+json</c> content type, stable error code,
    /// non-empty trace id, and a zero-diff <see cref="FiscalYearSnapshot"/> before vs after — proving
    /// no partial Journal Entry, counter, or projection change survived the rejection.
    /// </summary>
    public async Task<ScenarioApiResult<T>> AssertRejectedWithoutSideEffectsAsync<T>(
        long accountingBookId, long fiscalYearId, Func<Task<ScenarioApiResult<T>>> operation,
        HttpStatusCode expectedStatus, string expectedErrorCode, CancellationToken cancellationToken = default)
    {
        var before = await inspector.SnapshotAsync(accountingBookId, fiscalYearId, cancellationToken);
        var result = await operation();
        AssertRejected(result, expectedStatus, expectedErrorCode);
        var after = await inspector.SnapshotAsync(accountingBookId, fiscalYearId, cancellationToken);
        Assert.Equal(before, after);
        return result;
    }

    /// <summary>Asserts the stable rejected-write contract without the before/after snapshot diff.</summary>
    public static void AssertRejected<T>(
        ScenarioApiResult<T> result, HttpStatusCode expectedStatus, string expectedErrorCode)
    {
        Assert.True(result.StatusCode == expectedStatus,
            $"Expected {expectedStatus} but got {result.StatusCode}: {result.RawBody}");
        Assert.Equal("application/problem+json", result.ContentType);
        Assert.True(result.Problem is not null, $"No Problem Details payload in response: {result.RawBody}");
        Assert.Equal(expectedErrorCode, result.Problem!.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Problem.TraceId));
    }
}

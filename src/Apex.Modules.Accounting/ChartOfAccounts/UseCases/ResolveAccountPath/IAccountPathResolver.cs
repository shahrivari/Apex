namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ResolveAccountPath;

/// <summary>
/// The outcome of resolving a complete Account Class / General Account / Subsidiary Account
/// code path. <paramref name="RequiredDetailType"/> is the database value the Subsidiary Account
/// requires ("NONE", "BANK", "SYMBOL", "PERSON").
/// </summary>
public sealed record AccountPathResolution(
    bool Exists,
    bool PostingEligible,
    string RequiredDetailType);

/// <summary>
/// Cross-capability contract used by other Accounting capabilities (e.g. Journal Entries) to
/// resolve an account-code path to its posting eligibility and required detail-account type.
/// </summary>
public interface IAccountPathResolver
{
    Task<AccountPathResolution> ResolveAsync(
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        CancellationToken cancellationToken = default);
}

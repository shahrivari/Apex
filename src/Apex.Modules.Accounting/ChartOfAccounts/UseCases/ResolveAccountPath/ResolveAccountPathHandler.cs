using Apex.Modules.Accounting.ChartOfAccounts.Repositories;

namespace Apex.Modules.Accounting.ChartOfAccounts.UseCases.ResolveAccountPath;

internal sealed class ResolveAccountPathHandler(IAccountPathReadRepository repository)
    : IAccountPathResolver
{
    public async Task<AccountPathResolution> ResolveAsync(
        string accountClassCode, string generalAccountCode, string subsidiaryAccountCode,
        CancellationToken cancellationToken = default)
    {
        var row = await repository.ResolveAsync(
            accountClassCode?.Trim() ?? "",
            generalAccountCode?.Trim() ?? "",
            subsidiaryAccountCode?.Trim() ?? "",
            cancellationToken);

        if (row is null)
            return new AccountPathResolution(Exists: false, PostingEligible: false, RequiredDetailType: "");

        var eligible = row.ClassStatus == "ACTIVE"
            && row.GeneralStatus == "ACTIVE"
            && row.SubsidiaryStatus == "ACTIVE";
        return new AccountPathResolution(Exists: true, PostingEligible: eligible, row.DetailAccountType);
    }
}

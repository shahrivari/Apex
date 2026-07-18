namespace Apex.Modules.Accounting.DetailAccounts.UseCases.ValidateDetailAccountForPosting;

public interface IDetailAccountPostingValidator
{
    Task ValidateAsync(string? code, string? requiredType, CancellationToken ct = default);
}

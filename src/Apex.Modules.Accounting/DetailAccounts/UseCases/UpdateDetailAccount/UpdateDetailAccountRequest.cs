namespace Apex.Modules.Accounting.DetailAccounts.UseCases.UpdateDetailAccount;

public sealed record UpdateDetailAccountRequest(string Name, string Type, string? Code = null);

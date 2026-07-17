using Apex.Application.Abstractions.Exceptions;

namespace Apex.Modules.Accounting.ChartOfAccounts.Domain;

public sealed class SubsidiaryAccount
{
    public long Id { get; private init; }
    public long GeneralAccountId { get; private init; }
    public string Code { get; private init; } = null!;
    public string Name { get; private set; } = null!;
    public AccountNature Nature { get; private init; }
    public DetailAccountType DetailAccountType { get; private init; }
    public AccountStatus Status { get; private set; }
    public DateTime CreatedAt { get; private init; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? ArchivedAt { get; private set; }
    private SubsidiaryAccount() { }
    public static SubsidiaryAccount Create(long id,long parentId,string code,string name,AccountNature nature,DetailAccountType detailType,DateTime now) { Validate(parentId,code,name); return new(){Id=id,GeneralAccountId=parentId,Code=code.Trim().ToUpperInvariant(),Name=name.Trim(),Nature=nature,DetailAccountType=detailType,Status=AccountStatus.Active,CreatedAt=now}; }
    internal static SubsidiaryAccount Rehydrate(long id,long parentId,string code,string name,AccountNature nature,DetailAccountType detailType,AccountStatus status,DateTime createdAt,DateTime? updatedAt,DateTime? archivedAt)=>new(){Id=id,GeneralAccountId=parentId,Code=code,Name=name,Nature=nature,DetailAccountType=detailType,Status=status,CreatedAt=createdAt,UpdatedAt=updatedAt,ArchivedAt=archivedAt};
    public void Rename(string name,DateTime now){ValidateName(name);Name=name.Trim();UpdatedAt=now;}
    public void Archive(DateTime now){if(Status==AccountStatus.Archived)throw Rule("Subsidiary account is already archived.",ChartOfAccountsErrors.AlreadyArchived);Status=AccountStatus.Archived;UpdatedAt=now;ArchivedAt=now;}
    public void Reactivate(DateTime now){if(Status==AccountStatus.Active)throw Rule("Subsidiary account is already active.",ChartOfAccountsErrors.AlreadyActive);Status=AccountStatus.Active;UpdatedAt=now;ArchivedAt=null;}
    private static void Validate(long parentId,string code,string name){if(parentId<=0)throw Rule("General account is required.",ChartOfAccountsErrors.GeneralAccountNotFound);if(string.IsNullOrWhiteSpace(code)||code.Trim().Length>64)throw Rule("Account code must contain at most 64 characters.","invalid_account_code");ValidateName(name);}
    private static void ValidateName(string name){if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>255)throw Rule("Account name must contain at most 255 characters.","invalid_account_name");}
    private static BusinessRuleException Rule(string message,string code)=>new(message,code);
}

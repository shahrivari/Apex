using Apex.Modules.Accounting.ChartOfAccounts.Domain;
using Apex.Modules.Accounting.ChartOfAccounts.Repositories.Rows;

namespace Apex.Modules.Accounting.ChartOfAccounts.Repositories;

internal interface IAccountClassReadRepository { Task<AccountClassRow?> GetAsync(long id,CancellationToken ct=default); Task<IReadOnlyList<AccountClassRow>> ListAsync(bool includeArchived,CancellationToken ct=default); }
internal interface IGeneralAccountReadRepository { Task<GeneralAccountRow?> GetAsync(long id,CancellationToken ct=default); Task<IReadOnlyList<GeneralAccountRow>> ListAsync(bool includeArchived,CancellationToken ct=default); }
internal interface ISubsidiaryAccountReadRepository { Task<SubsidiaryAccountRow?> GetAsync(long id,CancellationToken ct=default); Task<IReadOnlyList<SubsidiaryAccountRow>> ListAsync(bool includeArchived,CancellationToken ct=default); }
internal interface IAccountClassWriteRepository { Task<AccountClass?> GetForUpdateAsync(long id,CancellationToken ct=default); Task<bool> CodeExistsAsync(string code,long? excludingId=null,CancellationToken ct=default); Task<bool> HasActiveChildrenAsync(long id,CancellationToken ct=default); Task InsertAsync(AccountClass value,CancellationToken ct=default); Task UpdateAsync(AccountClass value,CancellationToken ct=default); }
internal interface IGeneralAccountWriteRepository { Task<GeneralAccount?> GetForUpdateAsync(long id,CancellationToken ct=default); Task<bool> CodeExistsAsync(long parentId,string code,long? excludingId=null,CancellationToken ct=default); Task<bool> HasActiveChildrenAsync(long id,CancellationToken ct=default); Task InsertAsync(GeneralAccount value,CancellationToken ct=default); Task UpdateAsync(GeneralAccount value,CancellationToken ct=default); }
internal interface ISubsidiaryAccountWriteRepository { Task<SubsidiaryAccount?> GetForUpdateAsync(long id,CancellationToken ct=default); Task<bool> CodeExistsAsync(long parentId,string code,long? excludingId=null,CancellationToken ct=default); Task InsertAsync(SubsidiaryAccount value,CancellationToken ct=default); Task UpdateAsync(SubsidiaryAccount value,CancellationToken ct=default); }

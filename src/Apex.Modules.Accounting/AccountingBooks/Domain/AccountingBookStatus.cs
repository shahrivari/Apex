using System.Text.Json.Serialization;

namespace Apex.Modules.Accounting.AccountingBooks.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountingBookStatus
{
    Draft = 0,
    Active = 1,
    Suspended = 2,
    Archived = 3
}

public static class AccountingBookStatusExtensions
{
    public static string ToDatabaseValue(this AccountingBookStatus status)
    {
        return status switch
        {
            AccountingBookStatus.Draft => "DRAFT",
            AccountingBookStatus.Active => "ACTIVE",
            AccountingBookStatus.Suspended => "SUSPENDED",
            AccountingBookStatus.Archived => "ARCHIVED",
            _ => throw new InvalidOperationException($"Unknown AccountingBookStatus: {status}")
        };
    }

    public static AccountingBookStatus FromDatabaseValue(string value)
    {
        return value switch
        {
            "ACTIVE" => AccountingBookStatus.Active,
            "DRAFT" => AccountingBookStatus.Draft,
            "SUSPENDED" => AccountingBookStatus.Suspended,
            "ARCHIVED" => AccountingBookStatus.Archived,
            _ => throw new InvalidOperationException($"Unknown status value: {value}")
        };
    }
}

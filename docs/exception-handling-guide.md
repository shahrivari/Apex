# Apex Exception Handling Guide

This guide defines how exceptions are handled in Apex.

Apex uses centralized exception handling with RFC7807-style `ProblemDetails` responses.

The goal is to keep use cases clean, return stable API errors, and make production issues traceable through a short public `traceId`.

---

## 1. Core principles

Apex exception handling follows these rules:

1. Use cases throw meaningful application exceptions.
2. API middleware converts exceptions to `ProblemDetails`.
3. Clients receive stable `errorCode` values.
4. Clients receive a short NanoId `traceId`.
5. Logs include the same `traceId`.
6. Unexpected exceptions are not leaked to clients.
7. Validation errors are returned in a structured format.
8. Repositories generally do not hide infrastructure failures.

---

## 2. Exception flow

```text
Endpoint
  -> Handler
    -> Domain / Repository
      -> throws exception

GlobalExceptionHandlingMiddleware
  -> catches exception
  -> logs exception with traceId
  -> returns ProblemDetails JSON
```

Handlers should not manually create HTTP error responses.

Endpoints should not contain try/catch blocks for normal business errors.

---

## 3. Exception categories

Apex uses a small exception hierarchy.

```text
ApexException
  NotFoundException
  ConflictException
  BusinessRuleException
  ForbiddenException
```

FluentValidation uses:

```text
FluentValidation.ValidationException
```

Unexpected exceptions use normal .NET exceptions and are handled as internal server errors.

---

## 4. Recommended folder structure

```text
src/
  Apex.Application/
    Abstractions/
      Exceptions/
        ApexException.cs
        NotFoundException.cs
        ConflictException.cs
        BusinessRuleException.cs
        ForbiddenException.cs
        ErrorCodes.cs

  Apex.Api/
    Middleware/
      GlobalExceptionHandlingMiddleware.cs

    Extensions/
      ExceptionHandlingExtensions.cs
```

Exception classes live in `Apex.Application` because handlers and modules need to throw them.

The middleware lives in `Apex.Api` because HTTP response mapping is an API concern.

---

## 5. Base exception

All custom Apex application exceptions inherit from `ApexException`.

```csharp
public abstract class ApexException : Exception
{
    protected ApexException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
```

The exception message is human-readable.

The `ErrorCode` is machine-readable and stable.

---

## 6. Exception types

### `NotFoundException`

Use when a requested resource does not exist.

HTTP status:

```text
404 Not Found
```

Example:

```csharp
throw new NotFoundException(
    $"Fiscal year '{id}' was not found.",
    "fiscal_year_not_found");
```

### `ConflictException`

Use when the request conflicts with existing state.

HTTP status:

```text
409 Conflict
```

Examples:

```text
duplicate account code
overlapping fiscal year
duplicate idempotency key
already existing unique business key
```

Example:

```csharp
throw new ConflictException(
    "Fiscal year overlaps with an existing fiscal year.",
    "fiscal_year_overlaps_existing_year");
```

### `BusinessRuleException`

Use when the request is syntactically valid but violates a business rule.

HTTP status:

```text
422 Unprocessable Entity
```

Examples:

```text
journal entry is not balanced
cannot post journal entry in closed fiscal year
cannot close fiscal year with unposted journals
cannot reverse an already reversed journal
```

Example:

```csharp
throw new BusinessRuleException(
    "Journal entry debit and credit totals must be equal.",
    "journal_entry_not_balanced");
```

### `ForbiddenException`

Use when the authenticated user is not allowed to perform the action.

HTTP status:

```text
403 Forbidden
```

Example:

```csharp
throw new ForbiddenException(
    "You are not allowed to close this fiscal year.",
    "fiscal_year_close_forbidden");
```

### `UnauthorizedAccessException`

Use for authentication failures.

HTTP status:

```text
401 Unauthorized
```

In most cases, authentication middleware should handle this before the request reaches the handler.

---

## 7. Error code convention

Every business/application error should have a stable `errorCode`.

Use lowercase snake_case.

Good:

```text
fiscal_year_not_found
fiscal_year_overlaps_existing_year
fiscal_year_already_closed
account_code_already_exists
journal_entry_not_balanced
journal_entry_already_posted
journal_entry_cannot_be_reversed
```

Bad:

```text
Error1
FiscalYearError
SomethingWentWrong
Invalid
```

Do not rely on exception messages for client logic.

Clients should use `errorCode`.

---

## 8. Exception class vs error code

Use exception classes for broad HTTP/category behavior.

Use error codes for exact business reasons.

```text
Exception class -> HTTP status category
Error code      -> exact reason
Message         -> human-readable explanation
```

Example:

```csharp
throw new ConflictException(
    "Account code already exists.",
    "account_code_already_exists");
```

Do not create a new exception class for every business case unless it carries extra data or is reused heavily.

Prefer this:

```csharp
throw new BusinessRuleException(
    "Journal entry is already posted.",
    "journal_entry_already_posted");
```

Avoid early exception class explosion:

```csharp
throw new JournalEntryAlreadyPostedException();
```

---

## 9. HTTP status mapping

| Exception | HTTP status |
|---|---:|
| `FluentValidation.ValidationException` | 400 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `BusinessRuleException` | 422 |
| `ForbiddenException` | 403 |
| `UnauthorizedAccessException` | 401 |
| unknown `Exception` | 500 |

---

## 10. ProblemDetails response shape

Apex returns `application/problem+json`.

Example:

```json
{
  "type": "https://errors.apex.local/fiscal_year_overlaps_existing_year",
  "title": "Conflict",
  "status": 409,
  "detail": "Fiscal year overlaps with an existing fiscal year.",
  "instance": "/apex/v1/api/accounting/fiscal-years",
  "traceId": "V4x9aQm2Lk8p",
  "errorCode": "fiscal_year_overlaps_existing_year"
}
```

Fields:

```text
type       stable URL-like error type
title      short category title
status     HTTP status code
detail     human-readable message
instance   request path
traceId    short NanoId for support/log correlation
errorCode  stable machine-readable error code
```

---

## 11. Validation error response

Validation errors return status `400`.

Example:

```json
{
  "type": "https://errors.apex.local/validation_failed",
  "title": "Validation failed",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/apex/v1/api/accounting/fiscal-years",
  "traceId": "V4x9aQm2Lk8p",
  "errorCode": "validation_failed",
  "errors": {
    "Title": [
      "Title is required."
    ],
    "EndDate": [
      "EndDate must be greater than StartDate."
    ]
  }
}
```

Validation errors should come from FluentValidation.

---

## 12. Trace ID

Apex uses NanoId as the public `traceId`.

Example:

```text
V4x9aQm2Lk8p
```

The same `traceId` must be returned to the client and written to logs.

The public `traceId` should be short enough to share in support messages.

Do not expose the long W3C `Activity.Current.Id` to clients unless distributed tracing requires it.

---

## 13. Logging rule

The global middleware logs all handled exceptions.

Recommended log levels:

| Error type | Log level |
|---|---|
| validation error | Warning |
| not found | Warning |
| conflict | Warning |
| business rule violation | Warning |
| forbidden | Warning |
| unauthorized | Warning |
| unexpected exception | Error |

Every log entry should include:

```text
TraceId
Path
Exception
Message
```

Example structured log template:

```csharp
_logger.Log(
    logLevel,
    exception,
    "{Message} TraceId={TraceId} Path={Path}",
    message,
    traceId,
    context.Request.Path);
```

---

## 14. Where to throw exceptions

### Domain

Domain may throw business exceptions when a rule is purely domain-level and does not need infrastructure.

Example:

```csharp
if (Status == FiscalYearStatus.Closed)
{
    throw new BusinessRuleException(
        "Fiscal year is already closed.",
        "fiscal_year_already_closed");
}
```

### Handler

Handlers throw exceptions for use-case and orchestration rules.

Example:

```csharp
var overlaps = await _writeRepository.ExistsOverlappingAsync(
    request.StartDate,
    request.EndDate,
    cancellationToken);

if (overlaps)
{
    throw new ConflictException(
        "Fiscal year overlaps with an existing fiscal year.",
        "fiscal_year_overlaps_existing_year");
}
```

### Repository

Repositories should usually let infrastructure exceptions bubble up.

They may convert known database constraint violations later, but this should be done carefully.

---

## 15. Repository exception rule

Repositories should not hide unexpected database failures.

Good:

```text
SQL timeout bubbles up
connection failure bubbles up
unknown SqlException bubbles up
```

Later, repositories or infrastructure may translate known SQL errors:

```text
unique constraint violation -> ConflictException
foreign key violation       -> BusinessRuleException or ConflictException
deadlock                    -> retry or unexpected after retry failure
```

Do not return `false` or `null` for unexpected database errors.

---

## 16. Validation vs business rule

Use FluentValidation for input shape and simple request validity.

Examples:

```text
required field
max length
date range
positive amount
non-empty collection
```

Use business exceptions for rules requiring business state.

Examples:

```text
fiscal year overlaps existing fiscal year
account code already exists
journal entry cannot be posted in closed fiscal year
journal entry is not balanced
```

---

## 17. Conflict vs business rule

Use `ConflictException` when the request conflicts with existing system state or uniqueness.

Examples:

```text
duplicate account code
overlapping fiscal year
already existing idempotency key
```

Use `BusinessRuleException` when a domain rule rejects the operation.

Examples:

```text
journal entry is not balanced
fiscal year is closed
journal entry already posted
cannot reverse reversed journal
```

When unsure:

```text
409 Conflict -> state collision
422 BusinessRuleException -> domain rule violation
```

---

## 18. Endpoint rule

Endpoints should not catch business exceptions.

Good:

```csharp
group.MapPost("/", async (
    CreateFiscalYearRequest request,
    CreateFiscalYearHandler handler,
    CancellationToken cancellationToken) =>
{
    var response = await handler.HandleAsync(request, cancellationToken);
    return Results.Created($"/apex/v1/api/accounting/fiscal-years/{response.Id}", response);
});
```

Bad:

```csharp
try
{
    ...
}
catch (ConflictException exception)
{
    return Results.Conflict(...);
}
```

Centralized middleware owns error-to-HTTP mapping.

---

## 19. Handler rule

Handlers should throw meaningful exceptions and error codes.

Good:

```csharp
throw new NotFoundException(
    $"Fiscal year '{id}' was not found.",
    "fiscal_year_not_found");
```

Bad:

```csharp
throw new Exception("not found");
```

For query use cases where not found is an acceptable API result, either approach is acceptable:

```text
handler returns null and endpoint returns 404
handler throws NotFoundException
```

Pick one per use case and be consistent.

---

## 20. ErrorCodes class

Common error codes live in:

```text
Apex.Application/Abstractions/Exceptions/ErrorCodes.cs
```

Global/common codes:

```csharp
public static class ErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string UnexpectedError = "unexpected_error";

    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string Forbidden = "forbidden";
    public const string BusinessRuleViolation = "business_rule_violation";
}
```

Module-specific error codes may be placed near the capability if they grow.

Example:

```text
FiscalYears/
  FiscalYearErrorCodes.cs
```

---

## 21. Middleware order

Register middleware early in the HTTP pipeline.

Recommended order:

```csharp
app.UseSerilogRequestLogging();

app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapAccountingEndpoints();
```

The exception middleware should run before endpoint execution.

---

## 22. Testing exception handling

Test exception handling at two levels.

### Unit tests

Use unit tests for:

```text
handlers throwing correct exception
validators producing validation errors
domain rules throwing BusinessRuleException
```

### API/integration tests

Use API tests for:

```text
exception maps to correct HTTP status
ProblemDetails contains errorCode
ProblemDetails contains traceId
validation errors contain errors object
unexpected exception returns generic 500
```

Example assertions:

```text
status is 409
content-type is application/problem+json
errorCode is fiscal_year_overlaps_existing_year
traceId is not empty
```

---

## 23. Client contract

Clients should treat these fields as stable:

```text
status
errorCode
traceId
errors, for validation responses
```

Clients should not parse `detail`.

`detail` is human-readable and may change.

---

## 24. Security rule

Never expose internal exception details for unexpected errors.

Good 500 response:

```json
{
  "title": "Unexpected error",
  "status": 500,
  "detail": "An unexpected error occurred.",
  "traceId": "V4x9aQm2Lk8p",
  "errorCode": "unexpected_error"
}
```

Bad 500 response:

```json
{
  "detail": "SqlException: Login failed for user..."
}
```

Unexpected exception details belong in logs only.

---

## 25. Final rules

1. Throw `ApexException` subclasses for expected application errors.
2. Use FluentValidation for input validation.
3. Use stable snake_case `errorCode` values.
4. Return RFC7807-style `ProblemDetails`.
5. Use NanoId as public `traceId`.
6. Log the same `traceId`.
7. Do not expose internal exception details.
8. Do not catch business exceptions in endpoints.
9. Repositories should not hide unexpected infrastructure failures.
10. Keep exception classes few; use error codes for specificity.

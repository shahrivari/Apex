namespace Apex.UnitTests.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Apex.Infrastructure.Data;
using NSubstitute;

public sealed class SqlWriteTransactionRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Begin_And_Commit_Transaction()
    {
        var session = Substitute.For<IWriteDbSession>();
        session.HasActiveTransaction.Returns(false);

        var runner = new SqlWriteTransactionRunner(session);

        var result = await runner.ExecuteAsync(
            "Accounting",
            _ => Task.FromResult(42),
            CancellationToken.None);

        Assert.Equal(42, result);

        await session.Received(1).BeginTransactionAsync("Accounting", Arg.Any<CancellationToken>());
        await session.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await session.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Should_Rollback_When_Action_Throws()
    {
        var session = Substitute.For<IWriteDbSession>();
        session.HasActiveTransaction.Returns(false);

        var runner = new SqlWriteTransactionRunner(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ExecuteAsync<int>(
                "Accounting",
                _ => throw new InvalidOperationException("boom"),
                CancellationToken.None));

        await session.Received(1).BeginTransactionAsync("Accounting", Arg.Any<CancellationToken>());
        await session.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await session.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Should_Not_Start_Nested_Transaction_When_Already_Active()
    {
        var session = Substitute.For<IWriteDbSession>();
        session.HasActiveTransaction.Returns(true);

        var runner = new SqlWriteTransactionRunner(session);

        var result = await runner.ExecuteAsync(
            "Accounting",
            _ => Task.FromResult(42),
            CancellationToken.None);

        Assert.Equal(42, result);

        await session.DidNotReceive().BeginTransactionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await session.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await session.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }
}

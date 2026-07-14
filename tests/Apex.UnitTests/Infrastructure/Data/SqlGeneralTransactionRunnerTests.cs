namespace Apex.UnitTests.Infrastructure.Data;

using Apex.Application.Abstractions.Data;
using Apex.Infrastructure.Data;
using NSubstitute;

public sealed class SqlGeneralTransactionRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Begin_And_Commit()
    {
        var connectionFactory = Substitute.For<IGeneralConnectionFactory>();
        var runner = new SqlGeneralTransactionRunner(connectionFactory);

        var result = await runner.ExecuteAsync(_ => Task.FromResult(42));

        Assert.Equal(42, result);
        await connectionFactory.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await connectionFactory.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await connectionFactory.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_Should_Roll_Back_On_Failure()
    {
        var connectionFactory = Substitute.For<IGeneralConnectionFactory>();
        var runner = new SqlGeneralTransactionRunner(connectionFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ExecuteAsync<int>(_ => throw new InvalidOperationException("boom")));

        await connectionFactory.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await connectionFactory.Received(1).RollbackAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_Should_Reuse_Active_Transaction()
    {
        var connectionFactory = Substitute.For<IGeneralConnectionFactory>();
        connectionFactory.HasActiveTransaction.Returns(true);
        var runner = new SqlGeneralTransactionRunner(connectionFactory);

        var result = await runner.ExecuteAsync(_ => Task.FromResult(42));

        Assert.Equal(42, result);
        await connectionFactory.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
        await connectionFactory.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}

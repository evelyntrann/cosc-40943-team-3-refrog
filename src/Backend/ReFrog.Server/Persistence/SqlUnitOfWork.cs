using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ReFrog.Server.Persistence;

/// <summary>
/// One SQL Server connection per request scope, shared by the area stores whose writes must commit together
/// (architecture §8.2.8). A store opens the operation's transaction with <see cref="BeginAsync"/> (never
/// <c>DbContext.Database.BeginTransactionAsync</c>), and every area <see cref="DbContext"/> that takes part is
/// configured on <see cref="Connection"/> and calls <see cref="EnlistAsync"/> (or <see cref="JoinIfOpenAsync"/>
/// for a read) before it touches the database, so a lock one area takes (ADM's <c>LockForAssignmentAsync</c>)
/// lasts until another area (VOL) commits. The connection is not opened until first use.
/// </summary>
public sealed class SqlUnitOfWork(IConfiguration configuration) : IAsyncDisposable, IDisposable
{
    private readonly List<DbContext> enlisted = [];
    private SqlConnection? connection;

    public DbConnection Connection => connection ??= new SqlConnection(configuration.GetConnectionString("ReFrog"));

    /// <summary>The scope's open transaction, or null.</summary>
    public DbTransaction? Current { get; private set; }

    public async Task<SharedTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        if (Current is not null)
        {
            throw new InvalidOperationException("A transaction is already open in this scope.");
        }

        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken);
        }

        Current = await Connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return new SharedTransaction(this, Current);
    }

    /// <summary>Makes <paramref name="context"/> run its commands inside the scope's open transaction.</summary>
    public async Task EnlistAsync(DbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Current is null)
        {
            throw new InvalidOperationException(
                "No shared transaction is open in this scope. Open it with SqlUnitOfWork.BeginAsync, " +
                "not DbContext.Database.BeginTransactionAsync.");
        }

        if (!ReferenceEquals(context.Database.GetDbConnection(), Connection))
        {
            throw new InvalidOperationException("The context does not use the scope's shared connection.");
        }

        if (!ReferenceEquals(context.Database.CurrentTransaction?.GetDbTransaction(), Current))
        {
            await context.Database.UseTransactionAsync(Current, cancellationToken);
            enlisted.Add(context);
        }
    }

    /// <summary>
    /// For a read on the shared connection: joins the open transaction if there is one. SQL Server refuses a command
    /// outside the transaction while one is pending on its connection.
    /// </summary>
    public Task JoinIfOpenAsync(DbContext context, CancellationToken cancellationToken) =>
        Current is null ? Task.CompletedTask : EnlistAsync(context, cancellationToken);

    internal async Task EndAsync(DbTransaction transaction)
    {
        if (!ReferenceEquals(transaction, Current))
        {
            return;
        }

        foreach (var context in enlisted)
        {
            try
            {
                await context.Database.UseTransactionAsync(null);
            }
            catch (ObjectDisposedException)
            {
                // The scope already disposed this context; it holds nothing to release.
            }
        }

        enlisted.Clear();
        Current = null;
        await transaction.DisposeAsync();
    }

    /// <summary>
    /// Releases a transaction a caller left open (disposing it rolls it back) and the connection. Area contexts
    /// may already be disposed by the scope, so they are not touched.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var open = Current;
        Current = null;
        enlisted.Clear();
        if (open is not null)
        {
            await open.DisposeAsync();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }
    }

    public void Dispose()
    {
        var open = Current;
        Current = null;
        enlisted.Clear();
        open?.Dispose();
        connection?.Dispose();
    }
}

/// <summary>The scope's open transaction. Disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public sealed class SharedTransaction : IAsyncDisposable
{
    private readonly SqlUnitOfWork owner;
    private readonly DbTransaction transaction;
    private bool ended;

    internal SharedTransaction(SqlUnitOfWork owner, DbTransaction transaction)
    {
        this.owner = owner;
        this.transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken);
        ended = true;
        await owner.EndAsync(transaction);
    }

    public async ValueTask DisposeAsync()
    {
        if (ended)
        {
            return;
        }

        ended = true;
        try
        {
            await transaction.RollbackAsync();
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException)
        {
            // The server already ended the transaction; there is nothing left to roll back.
        }

        await owner.EndAsync(transaction);
    }
}

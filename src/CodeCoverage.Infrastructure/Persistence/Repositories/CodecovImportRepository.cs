// ------------------------------------------------------------------------------------------------
// <copyright file="CodecovImportRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Codecov;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Jobs;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>CodecovImports</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
/// <param name="jobQueue">The queue that takes a job in the transaction of the change.</param>
/// <param name="wakeUpSignal">The signal that wakes the worker of this process once a job is committed.</param>
/// <remarks>
///   <see cref="FindAsync" /> tracks what it returns: a row can only be written by the context that read it (ADR-013). A
///   deletion or a purge only reaches the rows of the organization of the scope.
/// </remarks>
internal sealed class CodecovImportRepository(CodeCoverageDbContext context, SqlJobQueue jobQueue, JobWakeUpSignal wakeUpSignal) : ICodecovImportRepository
{
  #region Fields

  /// <summary>The message of the refusal of an import that this context did not read.</summary>
  private const string NotReadMessage = "An import must be read in this scope before it can be updated.";

  /// <summary>The most rows one purge deletes.</summary>
  private const int PurgeBatchSize = 1000;

  #endregion

  #region Implementation of ICodecovImportRepository

  /// <inheritdoc />
  public async Task StartAsync(CodecovImport import, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(import);

    long jobId;
    var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    await using (transaction.ConfigureAwait(false))
    {
      context.CodecovImports.Add(import);

      try
      {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        jobId = await jobQueue.EnqueueInTransactionAsync(context, CodecovImportJobs.Create(import.Id, import.RepositoryId), cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }
      catch
      {
        // Nothing is left for a later save of the scope, which would write the token without its job.
        context.Entry(import).State = EntityState.Detached;

        throw;
      }
    }

    wakeUpSignal.Notify(jobId);
  }

  /// <inheritdoc />
  public async Task<CodecovImport?> FindAsync(long id, CancellationToken cancellationToken) =>
    await context.CodecovImports.SingleOrDefaultAsync(i => i.Id == id, cancellationToken).ConfigureAwait(false);

  /// <inheritdoc />
  public async Task UpdateAsync(CodecovImport import, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(import);

    if (context.Entry(import).State is EntityState.Detached)
    {
      throw new InvalidOperationException(NotReadMessage);
    }

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  /// <inheritdoc />
  public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
  {
    var deleted = await context.CodecovImports.Where(i => i.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false) > 0;

    // A tracked copy would be saved again by a later save of the scope.
    if (context.CodecovImports.Local.FirstOrDefault(i => i.Id == id) is { } tracked)
    {
      context.Entry(tracked).State = EntityState.Detached;
    }

    return deleted;
  }

  /// <inheritdoc />
  public async Task<int> PurgeAsync(DateTimeOffset before, CancellationToken cancellationToken)
  {
    var limit = before.ToUniversalTime();
    var ids = await context.CodecovImports
                           .Where(i => i.CreatedAt < limit)
                           .OrderBy(static i => i.CreatedAt)
                           .Select(static i => i.Id)
                           .Take(PurgeBatchSize)
                           .ToListAsync(cancellationToken)
                           .ConfigureAwait(false);

    return ids.Count == 0
             ? 0
             : await context.CodecovImports.Where(i => ids.Contains(i.Id)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion
}

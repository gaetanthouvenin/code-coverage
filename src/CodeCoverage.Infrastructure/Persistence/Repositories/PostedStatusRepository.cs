// ------------------------------------------------------------------------------------------------
// <copyright file="PostedStatusRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Notifications;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Notifications;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>PostedStatuses</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class PostedStatusRepository(CodeCoverageDbContext context) : IPostedStatusRepository
{
  #region Fields

  private const string CommitNotFoundMessage = "The commit was changed or removed in the meantime; reload it and try again.";

  #endregion

  #region Implementation of IPostedStatusRepository

  /// <inheritdoc />
  public async Task<IReadOnlyList<PostedStatus>> ListByCommitAsync(long commitId, CancellationToken cancellationToken)
  {
    var rows = await context.PostedStatuses
                            .AsNoTracking()
                            .Where(s => s.CommitId == commitId)
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

    // Sorted here: the order of the database follows its collation, which differs between the two engines.
    return
    [
      .. rows.OrderBy(static s => s.Context, StringComparer.Ordinal)
             .Select(static s => new PostedStatus(s.CommitId, new CommitStatus(s.Context, s.State, s.Description), s.TargetUrl, s.PostedAt) { CheckRunId = s.CheckRunId }),
    ];
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">The commit does not exist in the organization of the scope.</exception>
  public async Task UpsertAsync(PostedStatus status, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(status);

    var commitId = status.CommitId;
    var statusContext = status.Status.Context;

    // The primary key does not hold the organization: a row for the commit of another organization would take its key.
    if (!await context.Commits.AnyAsync(c => c.Id == commitId, cancellationToken).ConfigureAwait(false))
    {
      throw new ConflictException(GeneralErrors.ConcurrentChange, CommitNotFoundMessage);
    }

    // Twice at most: a row a concurrent save added between the reading and the insert is then found and replaced.
    for (var attempt = 0; ; attempt++)
    {
      var row = await context.PostedStatuses
                             .SingleOrDefaultAsync(s => s.CommitId == commitId && s.Context == statusContext, cancellationToken)
                             .ConfigureAwait(false);
      var added = row is null;

      row ??= context.PostedStatuses.Add(new PostedStatusRecord { CommitId = commitId, Context = statusContext }).Entity;
      row.State = status.Status.State;
      row.Description = status.Status.Description;
      row.TargetUrl = status.TargetUrl;
      row.PostedAt = status.PostedAt;
      row.CheckRunId = status.CheckRunId;

      try
      {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        context.Entry(row).State = EntityState.Detached;

        return;
      }
      catch (DbUpdateException exception) when (added && attempt == 0 && UniqueIndexViolation.Is(exception, PostedStatusRecordConfiguration.PrimaryKeyName))
      {
        context.Entry(row).State = EntityState.Detached;
      }
      catch
      {
        context.Entry(row).State = EntityState.Detached;

        throw;
      }
    }
  }

  #endregion
}
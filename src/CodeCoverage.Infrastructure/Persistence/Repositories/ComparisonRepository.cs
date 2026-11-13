// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Domain.Comparisons;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Comparisons;
using CodeCoverage.Infrastructure.Persistence.Configurations;

namespace CodeCoverage.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes the <c>Comparisons</c> table, under the query filter of the organization.</summary>
/// <param name="context">The database context of the scope.</param>
internal sealed class ComparisonRepository(CodeCoverageDbContext context) : IComparisonRepository
{
  #region Fields

  private const string CommitNotFoundMessage = "The commit was changed or removed in the meantime; reload it and try again.";

  #endregion

  #region Methods

  /// <summary>Converts a row into the comparison it stores.</summary>
  /// <param name="row">The row.</param>
  /// <returns>The comparison.</returns>
  private static CommitComparison ToComparison(ComparisonRecord row) =>
    new(
      row.CommitId,
      row.PullNumber == 0 ? null : row.PullNumber,
      row.ForkPointSha,
      row.ComparedToSha,
      row.HeadRevision,
      row.BaseRevision,
      row.DiffSource,
      ComparisonDataSerializer.Deserialize(row.Data),
      row.ComputedAt,
      row.BehindBy,
      row.Hops
    );

  /// <summary>Copies a comparison to the columns of its row.</summary>
  /// <param name="row">The row.</param>
  /// <param name="comparison">The comparison.</param>
  private static void Fill(ComparisonRecord row, CommitComparison comparison)
  {
    var result = comparison.Result;

    row.ForkPointSha = comparison.ForkPointSha;
    row.ComparedToSha = comparison.ComparedToSha;
    row.HeadRevision = comparison.HeadRevision;
    row.BaseRevision = comparison.BaseRevision;
    row.BehindBy = comparison.BehindBy;
    row.Hops = comparison.Hops;
    row.HeadCoverage = result.Head.Coverage;
    row.BaseCoverage = result.Base?.Coverage;
    row.PatchLines = result.Patch.Totals.Lines;
    row.PatchHits = result.Patch.Totals.Hits;
    row.PatchMisses = result.Patch.Totals.Misses;
    row.PatchPartials = result.Patch.Totals.Partials;
    row.IndirectChangeCount = result.IndirectChanges.Count;
    row.DiffSource = comparison.DiffSource;
    row.DiffIncomplete = result.DiffIncomplete;
    row.Data = ComparisonDataSerializer.Serialize(result);
    row.ComputedAt = comparison.ComputedAt.ToUniversalTime();
  }

  #endregion

  #region Implementation of IComparisonRepository

  /// <inheritdoc />
  public async Task<CommitComparison?> FindAsync(long commitId, int? pullNumber, CancellationToken cancellationToken)
  {
    var number = pullNumber ?? 0;
    var row = await context.Comparisons
                           .AsNoTracking()
                           .SingleOrDefaultAsync(c => c.CommitId == commitId && c.PullNumber == number, cancellationToken)
                           .ConfigureAwait(false);

    return row is null ? null : ToComparison(row);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<CommitComparison>> ListByCommitsAsync(IReadOnlyCollection<long> commitIds, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(commitIds);

    if (commitIds.Count == 0)
    {
      return [];
    }

    var rows = await context.Comparisons
                            .AsNoTracking()
                            .Where(c => commitIds.Contains(c.CommitId))
                            .ToListAsync(cancellationToken)
                            .ConfigureAwait(false);

    return [.. rows.Select(ToComparison)];
  }

  /// <inheritdoc />
  /// <exception cref="ConflictException">The commit does not exist in the organization of the scope.</exception>
  public async Task SaveAsync(CommitComparison comparison, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(comparison);

    var commitId = comparison.CommitId;
    var number = comparison.PullNumber ?? 0;

    // The unique index does not hold the organization: a row for the commit of another organization would take its key.
    if (!await context.Commits.AnyAsync(c => c.Id == commitId, cancellationToken).ConfigureAwait(false))
    {
      throw new ConflictException(GeneralErrors.ConcurrentChange, CommitNotFoundMessage);
    }

    // Twice at most: a row a concurrent save added between the reading and the insert is then found and replaced.
    for (var attempt = 0; ; attempt++)
    {
      var row = await context.Comparisons
                             .SingleOrDefaultAsync(c => c.CommitId == commitId && c.PullNumber == number, cancellationToken)
                             .ConfigureAwait(false);
      var added = row is null;

      row ??= context.Comparisons.Add(new ComparisonRecord { CommitId = commitId, PullNumber = number }).Entity;
      Fill(row, comparison);

      try
      {
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The blob has no reason to stay in the context of the scope.
        context.Entry(row).State = EntityState.Detached;

        return;
      }
      catch (DbUpdateException exception) when (added && attempt == 0 && UniqueIndexViolation.Is(exception, ComparisonRecordConfiguration.CommitPullIndexName))
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
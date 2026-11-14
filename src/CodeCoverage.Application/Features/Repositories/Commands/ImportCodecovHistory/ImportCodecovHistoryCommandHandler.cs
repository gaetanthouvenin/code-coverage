// ------------------------------------------------------------------------------------------------
// <copyright file="ImportCodecovHistoryCommandHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.Logging;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Features.Repositories.Commands.ImportCodecovHistory;

/// <summary>Handler for the <see cref="ImportCodecovHistoryCommand" /> command.</summary>
/// <param name="imports">The imports of the history of Codecov.</param>
/// <param name="repositories">The repositories of the organization.</param>
/// <param name="directory">The directory of the organizations, which gives the forge of the organization of the job.</param>
/// <param name="protector">Reads the token of the import.</param>
/// <param name="codecov">The API of Codecov.</param>
/// <param name="reports">Writes the imported commits.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
/// <remarks>
///   <para>
///     Each page of commits is written, then the import records the next page: a retry goes on where the last run
///     stopped. The row, and its credential with it, is deleted when the last page is read, or when Codecov refuses for
///     good (the token, or the repository on Codecov); a job that dies deletes it too (<c>AbandonCodecovImport</c>).
///   </para>
///   <para>
///     One run reads pages for <see cref="RunBudget" /> at most, well within the timeout of a job: once spent, the
///     next page is recorded and the job runs again shortly, without using an attempt (<see cref="RetryLaterException" />).
///     A long history is read over several runs instead of dying of its timeouts.
///   </para>
///   <para>
///     An imported commit is written complete, without blob, files or sessions, dated by Codecov, with the coverage
///     computed again from its counters. A SHA already known is never overwritten. Nothing is woken nor notified: an
///     imported commit is never a base nor a head of a comparison.
///   </para>
/// </remarks>
internal sealed partial class ImportCodecovHistoryCommandHandler(
  ICodecovImportRepository imports,
  IRepositoryRepository repositories,
  IOrganizationDirectory directory,
  ISecretProtector protector,
  ICodecovApiClient codecov,
  ICommitReportRepository reports,
  IClock clock,
  ILogger<ImportCodecovHistoryCommandHandler> logger) : ICommandHandler<ImportCodecovHistoryCommand>
{
  #region Fields

  /// <summary>The most pages one import reads: 100 000 commits at 100 a page.</summary>
  internal const int MaxPages = 1000;

  /// <summary>How long one run reads pages before it hands over to the next run, within the 10 minutes of a job.</summary>
  internal static readonly TimeSpan RunBudget = TimeSpan.FromMinutes(7);

  /// <summary>The delay before the next run of an import whose run spent its budget.</summary>
  internal static readonly TimeSpan ContinueDelay = TimeSpan.FromSeconds(5);

  #endregion

  #region Methods

  [LoggerMessage(EventId = 1800, Level = LogLevel.Information, Message = "The import {ImportId} of the history of Codecov ended: {Commits} commits written")]
  private static partial void LogEnded(ILogger logger, long importId, int commits);

  [LoggerMessage(EventId = 1801, Level = LogLevel.Warning, Message = "Codecov refused the import {ImportId}: the token, or the repository on Codecov; its credential is deleted")]
  private static partial void LogRefused(ILogger logger, long importId);

  [LoggerMessage(EventId = 1802, Level = LogLevel.Information, Message = "The import {ImportId} of the history of Codecov is gone: nothing to do")]
  private static partial void LogGone(ILogger logger, long importId);

  [LoggerMessage(EventId = 1803, Level = LogLevel.Information, Message = "The import {ImportId} of the history of Codecov spent the budget of its run: it goes on from page {Page}")]
  private static partial void LogContinued(ILogger logger, long importId, int page);

  #endregion

  #region Implementation of ICommandHandler<ImportCodecovHistoryCommand>

  /// <inheritdoc />
  /// <exception cref="UnprocessableEntityException">Codecov refused the token or does not have the repository.</exception>
  /// <exception cref="RetryLaterException">The run spent its budget: the next run goes on from the recorded page.</exception>
  public async ValueTask<Unit> Handle(ImportCodecovHistoryCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var import = await imports.FindAsync(command.ImportId, cancellationToken).ConfigureAwait(false);

    // Purged after seven days, or abandoned: nothing holds the token any more.
    if (import is null)
    {
      LogGone(logger, command.ImportId);

      return Unit.Value;
    }

    var repository = await repositories.FindAsync(import.RepositoryId, cancellationToken).ConfigureAwait(false);
    var organization = await directory.FindCurrentAsync(cancellationToken).ConfigureAwait(false);

    if (repository is null || organization is null)
    {
      await imports.DeleteAsync(import.Id, cancellationToken).ConfigureAwait(false);
      LogGone(logger, import.Id);

      return Unit.Value;
    }

    var token = protector.Unprotect(import.ProtectedToken);
    var started = clock.UtcNow;

    while (import.NextPage <= MaxPages)
    {
      var page = await codecov.ListCommitsAsync(organization.Forge, repository.Owner, repository.Namespace, repository.Name, token, import.NextPage, cancellationToken)
                              .ConfigureAwait(false);

      if (page is null)
      {
        await imports.DeleteAsync(import.Id, cancellationToken).ConfigureAwait(false);
        LogRefused(logger, import.Id);
        UnprocessableEntityException.ThrowIfCodecovRefused(page);
      }

      var written = 0;

      foreach (var imported in page.Commits)
      {
        var commit = Commit.Import(repository.Id, imported.Sha, imported.Branch, imported.Files, imported.Totals, imported.Timestamp, clock.UtcNow);

        if (await reports.SaveImportedAsync(commit, cancellationToken).ConfigureAwait(false))
        {
          written++;
        }
      }

      import.RecordPage(written);

      if (!page.HasNext)
      {
        break;
      }

      await imports.UpdateAsync(import, cancellationToken).ConfigureAwait(false);

      // The budget of this run is spent: the next page is recorded, the next run goes on from it.
      if (clock.UtcNow - started >= RunBudget)
      {
        LogContinued(logger, import.Id, import.NextPage);

        throw new RetryLaterException(ContinueDelay, $"The import {import.Id} of the history of Codecov goes on from page {import.NextPage}.");
      }
    }

    await imports.DeleteAsync(import.Id, cancellationToken).ConfigureAwait(false);
    LogEnded(logger, import.Id, import.ImportedCommits);

    return Unit.Value;
  }

  #endregion
}

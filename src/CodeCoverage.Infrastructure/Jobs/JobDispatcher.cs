// ------------------------------------------------------------------------------------------------
// <copyright file="JobDispatcher.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.Json;

using Mediator;

using CodeCoverage.Application.Features.Access.Commands.RefreshUserAccess;
using CodeCoverage.Application.Features.Access.Commands.RefreshUserMemberships;
using CodeCoverage.Application.Features.Commits.Commands.FinalizeCommit;
using CodeCoverage.Application.Features.Commits.Commands.RequeueCommitUploads;
using CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;
using CodeCoverage.Application.Features.GitHubEvents.Commands.HandleWebhookDelivery;
using CodeCoverage.Application.Features.GitHubEvents.Commands.ReconcileRepositoryTransfer;
using CodeCoverage.Application.Features.GitHubEvents.Commands.RewriteRepositoryOwner;
using CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallationRepositories;
using CodeCoverage.Application.Features.GitHubEvents.Commands.SyncInstallations;
using CodeCoverage.Application.Features.Notifications.Commands.NotifyCommit;
using CodeCoverage.Application.Features.Notifications.Commands.SweepPendingCommits;
using CodeCoverage.Application.Features.Operations.Commands.PurgeExpiredData;
using CodeCoverage.Application.Features.PullRequests.Commands.SyncPullRequest;
using CodeCoverage.Application.Features.Repositories.Commands.AbandonCodecovImport;
using CodeCoverage.Application.Features.Repositories.Commands.ImportCodecovHistory;
using CodeCoverage.Application.Features.Uploads.Commands.ProcessUpload;
using CodeCoverage.Domain.Contracts.Jobs;

namespace CodeCoverage.Infrastructure.Jobs;

/// <summary>
///   <see cref="IJobDispatcher" /> that maps each type of <see cref="JobTypes" /> to the command of the
///   Application it runs, built from the JSON payload of the job.
/// </summary>
/// <param name="sender">The mediator of the scope of the job.</param>
/// <remarks>
///   Each feature that runs work in the background adds its case here, next to its constant in
///   <see cref="JobTypes" /> (job pattern, PLAN §1.4). The commands go through the whole pipeline of the
///   mediator (logging, authorization, validation): the worker sends them as the system principal, which
///   the <c>ISystemOnly</c> commands require.
/// </remarks>
internal sealed class JobDispatcher(ISender sender) : IJobDispatcher
{
  #region Methods

  /// <summary>Reads a value of the payload of a job.</summary>
  /// <typeparam name="T">The type of the value.</typeparam>
  /// <param name="job">The job.</param>
  /// <param name="read">What reads the value from the root of the payload.</param>
  /// <returns>The value.</returns>
  /// <exception cref="InvalidJobException">The payload is not valid.</exception>
  private static T Read<T>(JobRecord job, Func<JsonElement, T> read)
  {
    try
    {
      using var payload = JsonDocument.Parse(job.PayloadJson);

      return read(payload.RootElement);
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
    {
      throw new InvalidJobException($"The payload of the {job.Type} job {job.Id} is not valid.");
    }
  }

  /// <summary>Reads a text property that the payload must hold.</summary>
  /// <param name="payload">The root of the payload.</param>
  /// <param name="name">The name of the property.</param>
  /// <returns>The text.</returns>
  /// <exception cref="FormatException">The property is empty.</exception>
  private static string ReadText(JsonElement payload, string name) =>
    payload.GetProperty(name).GetString() is { Length: > 0 } text ? text : throw new FormatException($"'{name}' is empty.");

  /// <summary>Reads a text property that the payload may hold.</summary>
  /// <param name="payload">The root of the payload.</param>
  /// <param name="name">The name of the property.</param>
  /// <returns>The text, or <see langword="null" />.</returns>
  private static string? ReadOptionalText(JsonElement payload, string name) =>
    payload.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.String ? value.GetString() : null;

  /// <summary>Reads the commit of a job.</summary>
  /// <param name="job">The job.</param>
  /// <returns>The identifier of the commit.</returns>
  /// <exception cref="InvalidJobException">The payload is not valid.</exception>
  private static long ReadCommitId(JobRecord job)
  {
    try
    {
      using var payload = JsonDocument.Parse(job.PayloadJson);

      return payload.RootElement.GetProperty("CommitId").GetInt64();
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
    {
      throw new InvalidJobException($"The payload of the {job.Type} job {job.Id} is not valid.");
    }
  }

  /// <summary>Reads the import of the history of Codecov of a job.</summary>
  /// <param name="job">The job.</param>
  /// <returns>The identifier of the <c>CodecovImports</c> row.</returns>
  /// <exception cref="InvalidJobException">The payload is not valid.</exception>
  private static long ReadImportId(JobRecord job)
  {
    try
    {
      return CodecovImportJobs.ReadImportId(job.PayloadJson);
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
    {
      throw new InvalidJobException($"The payload of the {job.Type} job {job.Id} is not valid.");
    }
  }

  /// <summary>Reads the upload of a job.</summary>
  /// <param name="job">The job.</param>
  /// <returns>The public identifier of the upload.</returns>
  /// <exception cref="InvalidJobException">The payload is not valid.</exception>
  private static Guid ReadUploadId(JobRecord job)
  {
    try
    {
      using var payload = JsonDocument.Parse(job.PayloadJson);

      return payload.RootElement.GetProperty("UploadId").GetGuid();
    }
    catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
    {
      throw new InvalidJobException($"The payload of the {job.Type} job {job.Id} is not valid.");
    }
  }

  #endregion

  #region Implementation of IJobDispatcher

  /// <inheritdoc />
  public async Task AbandonAsync(JobRecord job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(job);

    if (job.Type is JobTypes.ImportCodecovHistory)
    {
      await sender.Send(new AbandonCodecovImportCommand(ReadImportId(job)), cancellationToken).ConfigureAwait(false);
    }
  }

  /// <inheritdoc />
  public async Task DispatchAsync(JobRecord job, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(job);

    switch (job.Type)
    {
      case JobTypes.Ping:
        await sender.Send(new RunDiagnosticJobCommand(job.Id), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.ProcessUpload:
        await sender.Send(new ProcessUploadCommand(ReadUploadId(job), job.LockedBy ?? job.Type), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.FinalizeCommit:
        await sender.Send(new FinalizeCommitCommand(ReadCommitId(job)), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.HandleWebhook:
        await sender.Send(new HandleWebhookDeliveryCommand(Read(job, static p => ReadText(p, "deliveryId"))), cancellationToken).ConfigureAwait(false);

        break;
      case JobTypes.SyncInstallations:
        await sender.Send(Read(job, static p => new SyncInstallationsCommand(ReadOptionalText(p, "TransferredRepositoryId"), ReadOptionalText(p, "PreviousOwnerAccountId"))), cancellationToken)
                    .ConfigureAwait(false);

        break;
      case JobTypes.SyncInstallationRepositories:
        await sender.Send(new SyncInstallationRepositoriesCommand(Read(job, static p => p.GetProperty("InstallationId").GetInt64())), cancellationToken)
                    .ConfigureAwait(false);

        break;
      case JobTypes.ReconcileRepositoryTransfer:
        await sender.Send(new ReconcileRepositoryTransferCommand(Read(job, static p => ReadText(p, "RepositoryExternalId"))), cancellationToken)
                    .ConfigureAwait(false);

        break;
      case JobTypes.NotifyCommit:
        await sender.Send(
                      Read(
                        job,
                        static p => new NotifyCommitCommand(
                          p.GetProperty("CommitId").GetInt64(),
                          p.TryGetProperty("Force", out var force) && force.GetBoolean(),
                          p.TryGetProperty("Partial", out var partial) && partial.GetBoolean()
                        )
                      ),
                      cancellationToken
                    )
                    .ConfigureAwait(false);

        break;
      case JobTypes.RefreshUserMemberships:
        await sender.Send(new RefreshUserMembershipsCommand(Read(job, static p => p.GetProperty("UserId").GetInt64())), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.RefreshUserAccess:
        await sender.Send(new RefreshUserAccessCommand(Read(job, static p => p.GetProperty("UserId").GetInt64())), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.RewriteRepositoryOwner:
        await sender.Send(new RewriteRepositoryOwnerCommand(), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.PurgeExpiredData:
        await sender.Send(new PurgeExpiredDataCommand(), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.SweepPendingCommits:
        await sender.Send(new SweepPendingCommitsCommand(), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.SyncPullRequest:
        await sender.Send(
                      Read(job, static p => new SyncPullRequestCommand(p.GetProperty("RepositoryId").GetInt64(), p.GetProperty("Number").GetInt32())),
                      cancellationToken
                    )
                    .ConfigureAwait(false);

        break;
      case JobTypes.ReprocessCommit:
        await sender.Send(new RequeueCommitUploadsCommand(ReadCommitId(job)), cancellationToken).ConfigureAwait(false);
        break;

      case JobTypes.ImportCodecovHistory:
        await sender.Send(new ImportCodecovHistoryCommand(ReadImportId(job)), cancellationToken).ConfigureAwait(false);
        break;

      default:
        throw new InvalidJobException($"The job type '{job.Type}' is unknown: no command runs it.");
    }
  }

  #endregion
}

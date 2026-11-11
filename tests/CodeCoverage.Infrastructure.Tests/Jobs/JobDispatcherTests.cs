// ------------------------------------------------------------------------------------------------
// <copyright file="JobDispatcherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

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
using CodeCoverage.Application.Features.PullRequests.Commands.SyncPullRequest;
using CodeCoverage.Application.Features.Uploads.Commands.ProcessUpload;
using CodeCoverage.Domain.Contracts.Jobs;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Infrastructure.Jobs;

namespace CodeCoverage.Infrastructure.Tests.Jobs;

public sealed class JobDispatcherTests
{
  #region Fields

  private readonly ISender _sender = A.Fake<ISender>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Dispatch_Ping_ShouldSendTheDiagnosticCommand()
  {
    var dispatcher = new JobDispatcher(_sender);

    await dispatcher.DispatchAsync(Job(JobTypes.Ping, 42), Ct);

    A.CallTo(() => _sender.Send(new RunDiagnosticJobCommand(42), Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Dispatch_ProcessUpload_ShouldSendTheCommandOfTheStoredUpload()
  {
    var uploadId = Guid.NewGuid();
    var job = Job(JobTypes.ProcessUpload, 42);
    job.PayloadJson = $$"""{"UploadId":"{{uploadId}}"}""";
    job.LockedBy = "worker-1";

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new ProcessUploadCommand(uploadId, "worker-1"), Ct)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("not json")]
  [InlineData("""{"UploadId":"not-a-guid"}""")]
  public async Task Dispatch_ProcessUploadWithInvalidPayload_ShouldThrowInvalidJobException(string payload)
  {
    var job = Job(JobTypes.ProcessUpload, 42);
    job.PayloadJson = payload;

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
  }

  [Fact]
  public async Task Dispatch_FinalizeCommit_ShouldSendTheCommandOfTheCommit()
  {
    var job = Job(JobTypes.FinalizeCommit, 42);
    job.PayloadJson = """{"CommitId":9}""";

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new FinalizeCommitCommand(9), Ct)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("not json")]
  [InlineData("""{"CommitId":"abc"}""")]
  public async Task Dispatch_FinalizeCommitWithInvalidPayload_ShouldThrowInvalidJobException(string payload)
  {
    var job = Job(JobTypes.FinalizeCommit, 42);
    job.PayloadJson = payload;

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
  }

  [Fact]
  public async Task Dispatch_ReprocessCommit_ShouldSendTheRequeueOfTheCommit()
  {
    var job = Job(JobTypes.ReprocessCommit, 42);
    job.PayloadJson = ReprocessCommitJobs.Create(9).PayloadJson;

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new RequeueCommitUploadsCommand(9), Ct)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("not json")]
  [InlineData("""{"CommitId":"abc"}""")]
  public async Task Dispatch_ReprocessCommitWithInvalidPayload_ShouldThrowInvalidJobException(string payload)
  {
    var job = Job(JobTypes.ReprocessCommit, 42);
    job.PayloadJson = payload;

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Fact]
  public async Task Dispatch_HandleWebhook_ShouldSendTheCommandOfTheStoredDelivery()
  {
    var job = Job(JobTypes.HandleWebhook, 42);
    job.PayloadJson = """{"deliveryId":"d-1"}""";

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new HandleWebhookDeliveryCommand("d-1"), Ct)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("{}", null, null)]
  [InlineData("""{"TransferredRepositoryId":"7","PreviousOwnerAccountId":"2002"}""", "7", "2002")]
  [InlineData("""{"TransferredRepositoryId":7}""", null, null)]
  public async Task Dispatch_SyncInstallations_ShouldSendTheCoordinator(string payload, string? repository, string? owner)
  {
    var job = Job(JobTypes.SyncInstallations, 42);
    job.PayloadJson = payload;

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new SyncInstallationsCommand(repository, owner), Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Dispatch_RewriteRepositoryOwner_ShouldSendTheRewrite()
  {
    var job = Job(JobTypes.RewriteRepositoryOwner, 42);
    job.PayloadJson = RewriteRepositoryOwnerJobs.Create().PayloadJson;

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new RewriteRepositoryOwnerCommand(), Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Dispatch_RefreshUserMemberships_ShouldSendTheRefreshOfThatUser()
  {
    var job = Job(JobTypes.RefreshUserMemberships, 42);
    job.PayloadJson = RefreshUserMembershipsJobs.Create(7).PayloadJson;

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new RefreshUserMembershipsCommand(7), Ct)).MustHaveHappenedOnceExactly();
  }

  // What the factories of the Domain write is what the dispatcher reads.
  [Fact]
  public async Task Dispatch_PayloadsOfTheFactories_ShouldBeRead()
  {
    var transfer = Job(JobTypes.SyncInstallations, 1);
    transfer.PayloadJson = SyncInstallationsJobs.CreateForTransfer("7", "2002").PayloadJson;
    var listing = Job(JobTypes.SyncInstallationRepositories, 2);
    listing.PayloadJson = RepositoryReconciliationJobs.InstallationPayload(42);
    var closing = Job(JobTypes.ReconcileRepositoryTransfer, 3);
    closing.PayloadJson = RepositoryReconciliationJobs.TransferPayload("7");
    var dispatcher = new JobDispatcher(_sender);

    await dispatcher.DispatchAsync(transfer, Ct);
    await dispatcher.DispatchAsync(listing, Ct);
    await dispatcher.DispatchAsync(closing, Ct);

    A.CallTo(() => _sender.Send(new SyncInstallationsCommand("7", "2002"), Ct)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(new SyncInstallationRepositoriesCommand(42), Ct)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(new ReconcileRepositoryTransferCommand("7"), Ct)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData(JobTypes.HandleWebhook, "{}")]
  [InlineData(JobTypes.HandleWebhook, """{"deliveryId":""}""")]
  [InlineData(JobTypes.HandleWebhook, """{"deliveryId":7}""")]
  [InlineData(JobTypes.HandleWebhook, "not json")]
  [InlineData(JobTypes.SyncInstallations, "not json")]
  [InlineData(JobTypes.RefreshUserMemberships, "{}")]
  [InlineData(JobTypes.RefreshUserMemberships, """{"UserId":"x"}""")]
  [InlineData(JobTypes.SyncInstallationRepositories, "{}")]
  [InlineData(JobTypes.SyncInstallationRepositories, """{"InstallationId":"x"}""")]
  [InlineData(JobTypes.ReconcileRepositoryTransfer, "{}")]
  [InlineData(JobTypes.ReconcileRepositoryTransfer, """{"RepositoryExternalId":""}""")]
  public async Task Dispatch_InvalidPayloadOfAReconciliation_ShouldThrowInvalidJobException(string type, string payload)
  {
    var job = Job(type, 42);
    job.PayloadJson = payload;

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("""{"CommitId":9,"Force":true}""", true)]
  [InlineData("""{"CommitId":9,"Force":false}""", false)]
  [InlineData("""{"CommitId":9}""", false)]
  public async Task Dispatch_NotifyCommit_ShouldSendTheNotificationOfTheCommit(string payload, bool force)
  {
    var job = Job(JobTypes.NotifyCommit, 42);
    job.PayloadJson = payload;

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new NotifyCommitCommand(9, force), Ct)).MustHaveHappenedOnceExactly();
  }

  // What the factories of the Domain write is what the dispatcher reads.
  [Fact]
  public async Task Dispatch_NotifyCommitPayloadsOfTheFactories_ShouldBeRead()
  {
    var debounced = Job(JobTypes.NotifyCommit, 1);
    debounced.PayloadJson = NotifyCommitJobs.Create(9, DateTimeOffset.UnixEpoch, TimeSpan.Zero, TimeSpan.FromSeconds(1)).PayloadJson;
    var forced = Job(JobTypes.NotifyCommit, 2);
    forced.PayloadJson = NotifyCommitJobs.CreateForced(9).PayloadJson;
    var dispatcher = new JobDispatcher(_sender);

    await dispatcher.DispatchAsync(debounced, Ct);
    await dispatcher.DispatchAsync(forced, Ct);

    A.CallTo(() => _sender.Send(new NotifyCommitCommand(9, false), Ct)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _sender.Send(new NotifyCommitCommand(9, true), Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Dispatch_NotifyCommitOfTheSweep_ShouldBeForcedAndPartial()
  {
    var job = Job(JobTypes.NotifyCommit, 42);
    job.PayloadJson = NotifyCommitJobs.CreateForSweep(9).PayloadJson;

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new NotifyCommitCommand(9, true, true), Ct)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Dispatch_NotifyCommitWithAnInvalidPartialFlag_ShouldThrowInvalidJobException()
  {
    var job = Job(JobTypes.NotifyCommit, 42);
    job.PayloadJson = """{"CommitId":9,"Force":true,"Partial":"yes"}""";

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
  }

  // The coordinator and the sweep of an organization are the same job: the lane of the worker tells them apart.
  [Fact]
  public async Task Dispatch_SweepPendingCommits_ShouldSendTheSweep()
  {
    await new JobDispatcher(_sender).DispatchAsync(Job(JobTypes.SweepPendingCommits, 42), Ct);

    A.CallTo(() => _sender.Send(new SweepPendingCommitsCommand(), Ct)).MustHaveHappenedOnceExactly();
  }

  // What the factory of the Domain writes is what the dispatcher reads.
  [Fact]
  public async Task Dispatch_SyncPullRequest_ShouldSendTheSynchronizationOfThePullRequest()
  {
    var job = Job(JobTypes.SyncPullRequest, 42);
    job.PayloadJson = SyncPullRequestJobs.Create(5, 12).PayloadJson;

    await new JobDispatcher(_sender).DispatchAsync(job, Ct);

    A.CallTo(() => _sender.Send(new SyncPullRequestCommand(5, 12), Ct)).MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("not json")]
  [InlineData("""{"RepositoryId":5}""")]
  [InlineData("""{"Number":12}""")]
  [InlineData("""{"RepositoryId":"five","Number":12}""")]
  [InlineData("""{"RepositoryId":5,"Number":12.5}""")]
  public async Task Dispatch_SyncPullRequestWithInvalidPayload_ShouldThrowInvalidJobException(string payload)
  {
    var job = Job(JobTypes.SyncPullRequest, 42);
    job.PayloadJson = payload;

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
  }

  [Theory]
  [InlineData("{}")]
  [InlineData("not json")]
  [InlineData("""{"CommitId":"nine"}""")]
  [InlineData("""{"CommitId":9,"Force":"yes"}""")]
  public async Task Dispatch_NotifyCommitWithInvalidPayload_ShouldThrowInvalidJobException(string payload)
  {
    var job = Job(JobTypes.NotifyCommit, 42);
    job.PayloadJson = payload;

    await Should.ThrowAsync<InvalidJobException>(() => new JobDispatcher(_sender).DispatchAsync(job, Ct));
  }

  [Fact]
  public async Task Dispatch_UnknownType_ShouldThrowInvalidJobException()
  {
    var dispatcher = new JobDispatcher(_sender);

    var exception = await Should.ThrowAsync<InvalidJobException>(() => dispatcher.DispatchAsync(Job("Nope", 7), Ct));

    exception.Message.ShouldContain("Nope");
    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Fact]
  public async Task Dispatch_WithNullJob_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(() => new JobDispatcher(_sender).DispatchAsync(null!, Ct));
  }

  private static JobRecord Job(string type, long id) =>
    new()
    {
      Id = id,
      Type = type,
      PayloadJson = "{}",
      State = JobState.Running,
      CorrelationId = "test",
    };

  #endregion
}

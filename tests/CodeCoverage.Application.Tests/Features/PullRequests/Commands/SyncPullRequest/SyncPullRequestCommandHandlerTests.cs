// ------------------------------------------------------------------------------------------------
// <copyright file="SyncPullRequestCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.PullRequests.Commands.SyncPullRequest;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.PullRequests;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.PullRequests.Commands.SyncPullRequest;

public sealed class SyncPullRequestCommandHandlerTests
{
  #region Fields

  private const long RepositoryId = 5;

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha Base = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha MergeCommit = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IPullRequestRepository _pullRequests = A.Fake<IPullRequestRepository>();

  private readonly TrackedRepository _repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/repo"), "main", false, "badge", Now.AddDays(-30));

  private readonly SyncPullRequestCommandHandler _handler;

  private PullRequest? _stored;

  #endregion

  #region Ctors

  public SyncPullRequestCommandHandlerTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var clock = A.Fake<IClock>();

    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_repository, RepositoryId);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => _repositories.FindAsync(RepositoryId, A<CancellationToken>._)).Returns(_repository);
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "1001", "octo", OrganizationAccountType.Organization, Now));

    // As the real repository does: the row is created, or refreshed from what the forge reports.
    A.CallTo(() => _pullRequests.UpsertAsync(RepositoryId, A<ForgePullInfo>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .ReturnsLazily((long repositoryId, ForgePullInfo pull, DateTimeOffset at, CancellationToken _) =>
       {
         if (_stored is null)
         {
           _stored = PullRequest.Create(repositoryId, pull, at);
         }
         else
         {
           _stored.Refresh(pull, at);
         }

         return Task.FromResult(_stored);
       });

    _handler = new SyncPullRequestCommandHandler(_repositories, _directory, gateways, _pullRequests, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The command only names the pull request: whatever the delivery said about it is never seen here.
  [Fact]
  public async Task Handle_ShouldUseGitHubApiNotPayload()
  {
    Reports(Pull(ForgePullState.Open) with { MergeCommitSha = MergeCommit });

    await HandleAsync();

    var stored = _stored.ShouldNotBeNull();
    stored.Number.ShouldBe(12);
    stored.State.ShouldBe(PullRequestState.Open);
    stored.Title.ShouldBe("From the forge");
    // The head of the pull request, never the synthetic merge commit.
    stored.HeadSha.ShouldBe(Head);
    stored.HeadBranch.ShouldBe("feature/x");
    stored.BaseBranch.ShouldBe("main");
    stored.BaseShaGitHub.ShouldBe(Base);
    stored.UpdatedAt.ShouldBe(Now);
    A.CallTo(() => _gateway.GetPullAsync(new ForgeRepositoryRef(Forge.GitHub, "42", "7"), 12, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ClosedMerged_ShouldSetMerged()
  {
    _stored = PullRequest.Create(RepositoryId, Pull(ForgePullState.Open), Now.AddHours(-1));
    Reports(Pull(ForgePullState.Merged));

    await HandleAsync();

    _stored.State.ShouldBe(PullRequestState.Merged);
    _stored.AcceptsComments.ShouldBeFalse();
  }

  [Fact]
  public async Task Handle_ClosedWithoutMerge_ShouldSetClosed()
  {
    _stored = PullRequest.Create(RepositoryId, Pull(ForgePullState.Open), Now.AddHours(-1));
    Reports(Pull(ForgePullState.Closed));

    await HandleAsync();

    _stored.State.ShouldBe(PullRequestState.Closed);
  }

  [Fact]
  public async Task Handle_NewHead_ShouldFollowIt()
  {
    _stored = PullRequest.Create(RepositoryId, Pull(ForgePullState.Open), Now.AddHours(-1));
    Reports(Pull(ForgePullState.Open) with { HeadSha = MergeCommit, BaseBranch = "release" });

    await HandleAsync();

    _stored.HeadSha.ShouldBe(MergeCommit);
    _stored.BaseBranch.ShouldBe("release");
  }

  [Fact]
  public async Task Handle_Twice_ShouldChangeNothingMore()
  {
    Reports(Pull(ForgePullState.Open));

    await HandleAsync();
    var version = _stored!.Version;
    await HandleAsync();

    _stored.Version.ShouldBe(version);
  }

  [Fact]
  public async Task Handle_PullRequestUnknownToTheForge_ShouldRecordNothing()
  {
    Reports(null);

    await HandleAsync();

    A.CallTo(_pullRequests).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Handle_RepositoryNoLongerTracked_ShouldReadNothing(bool known)
  {
    if (known)
    {
      _repository.Deactivate(Now);
    }
    else
    {
      A.CallTo(() => _repositories.FindAsync(RepositoryId, A<CancellationToken>._)).Returns((TrackedRepository?)null);
    }

    await HandleAsync();

    A.CallTo(_gateway).MustNotHaveHappened();
    A.CallTo(_pullRequests).MustNotHaveHappened();
  }

  // A branch is never cut to fit, and the job must end: replayed, it would fail the same way eight times.
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Handle_BranchLongerThanABranchCanBe_ShouldRecordNothingAndEnd(bool head)
  {
    var tooLong = new string('b', PullRequest.BranchMaxLength + 1);
    Reports(head ? Pull(ForgePullState.Open) with { HeadBranch = tooLong } : Pull(ForgePullState.Open) with { BaseBranch = tooLong });

    await HandleAsync();

    A.CallTo(_pullRequests).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_LongestBranch_ShouldBeRecorded()
  {
    Reports(Pull(ForgePullState.Open) with { HeadBranch = new string('b', PullRequest.BranchMaxLength) });

    await HandleAsync();

    _stored.ShouldNotBeNull().HeadBranch.Length.ShouldBe(PullRequest.BranchMaxLength);
  }

  // The worker replays these: nothing is swallowed.
  [Fact]
  public async Task Handle_ForgeUnavailable_ShouldThrow()
  {
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._))
     .Throws(new BadGatewayException("GitHub.Unavailable", "GitHub does not answer."));

    await Should.ThrowAsync<BadGatewayException>(async () => await HandleAsync());
  }

  [Fact]
  public async Task Handle_ConcurrentSave_ShouldThrow()
  {
    Reports(Pull(ForgePullState.Open));
    A.CallTo(() => _pullRequests.UpsertAsync(RepositoryId, A<ForgePullInfo>._, A<DateTimeOffset>._, A<CancellationToken>._))
     .Throws(new ConflictException("General.ConcurrentChange", "changed in the meantime"));

    await Should.ThrowAsync<ConflictException>(async () => await HandleAsync());
  }

  [Fact]
  public async Task Handle_Null_ShouldThrow()
  {
    await Should.ThrowAsync<ArgumentNullException>(async () => await _handler.Handle(null!, Ct));
  }

  private static ForgePullInfo Pull(ForgePullState state) => new(12, state, "From the forge", "octocat", Head, "feature/x", "main", Base);

  private async Task HandleAsync() => await _handler.Handle(new SyncPullRequestCommand(RepositoryId, 12), Ct);

  private void Reports(ForgePullInfo? pull)
  {
    A.CallTo(() => _gateway.GetPullAsync(A<ForgeRepositoryRef>._, 12, A<CancellationToken>._)).Returns(pull);
  }

  #endregion
}

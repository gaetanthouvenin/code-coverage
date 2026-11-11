// ------------------------------------------------------------------------------------------------
// <copyright file="PushProcessorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Contracts.GitHub;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Services;

public sealed class PushProcessorTests
{
  #region Fields

  private const long RepositoryId = 5;

  private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha Parent = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly ForgeRepositoryRef Address = new(Forge.GitHub, "42", "7");

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IGitCommitRepository _git = A.Fake<IGitCommitRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly List<Commit> _stored = [];

  private readonly TrackedRepository _repository = TrackedRepository.Create(42, "7", RepositorySlug.Parse("octo/repo"), "main", false, "badge", Now.AddDays(-30));

  private readonly PushProcessor _processor;

  private Branch? _added;

  #endregion

  #region Ctors

  public PushProcessorTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    var clock = A.Fake<IClock>();

    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(_repository, RepositoryId);
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _git.FindAsync(RepositoryId, A<GitSha>._, A<CancellationToken>._)).Returns((GitCommit?)null);
    A.CallTo(() => _git.FindAsync(RepositoryId, Head, A<CancellationToken>._))
     .Returns(GitCommit.Create(RepositoryId, new ForgeCommitInfo(Head, [Parent], "head", null, Now), Now));
    A.CallTo(() => _git.FindAsync(RepositoryId, Parent, A<CancellationToken>._))
     .Returns(GitCommit.Create(RepositoryId, new ForgeCommitInfo(Parent, [], "parent", null, Now), Now));
    A.CallTo(() => _commits.ListByShasAsync(RepositoryId, A<IReadOnlyCollection<GitSha>>._, A<CancellationToken>._))
     .ReturnsLazily((long _, IReadOnlyCollection<GitSha> shas, CancellationToken _) =>
                      Task.FromResult<IReadOnlyList<Commit>>([.. _stored.Where(commit => shas.Contains(commit.Sha))]));
    A.CallTo(() => _branches.FindAsync(RepositoryId, A<string>._, A<CancellationToken>._)).Returns((Branch?)null);
    A.CallTo(() => _branches.AddAsync(A<Branch>._, A<CancellationToken>._)).Invokes((Branch branch, CancellationToken _) => _added = branch);

    var ancestry = new CommitAncestry(_git, gateways, clock);
    var configs = new ConfigResolver(
      A.Fake<ICommitConfigRepository>(),
      gateways,
      A.Fake<ICoverageConfigParser>(),
      A.Fake<IOrganizationSettingsProvider>(),
      clock
    );

    _processor = new PushProcessor(_branches, _commits, _repositories, new BranchHeadTracker(_commits, ancestry), configs, gateways, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Apply_NewBranch_ShouldCreateItAtTheHeadWithItsCoveredHead()
  {
    var covered = Complete(Parent);

    await ApplyAsync(new GitHubPushEvent("feature/x", Head, false, false));

    var branch = _added.ShouldNotBeNull();
    branch.RepositoryId.ShouldBe(RepositoryId);
    branch.Name.ShouldBe("feature/x");
    branch.HeadSha.ShouldBe(Head);
    branch.HeadUpdatedAt.ShouldBe(Now);
    branch.CoveredCommitId.ShouldBe(covered.Id);
    // The head is known before any upload names it.
    A.CallTo(() => _commits.GetOrCreateAsync(RepositoryId, Head, Now, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Apply_KnownBranch_ShouldMoveItsHeadAndRecomputeItsCoveredHead()
  {
    var former = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    var branch = Branch.Create(RepositoryId, "main", former, Now.AddDays(-1));
    branch.Cover(77, Now.AddDays(-1));
    A.CallTo(() => _branches.FindAsync(RepositoryId, "main", A<CancellationToken>._)).Returns(branch);

    await ApplyAsync(new GitHubPushEvent("main", Head, false, false, former));

    branch.HeadSha.ShouldBe(Head);
    // The commit covered before the push is no longer in the ancestry of the head, and nothing else is complete.
    branch.CoveredCommitId.ShouldBeNull();
    branch.CoveredAt.ShouldBeNull();
    A.CallTo(() => _branches.UpdateAsync(branch, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _branches.AddAsync(A<Branch>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // F14. A push replayed after a later one, its job having failed once: the branch is not where it started, so the
  // forge says where it is now, and the head never moves back.
  [Fact]
  public async Task Apply_PushReplayedAfterALaterOne_ShouldNotMoveTheHeadBack()
  {
    var started = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    var later = GitSha.Parse("d94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    var branch = Branch.Create(RepositoryId, "main", later, Now.AddMinutes(-1));
    A.CallTo(() => _branches.FindAsync(RepositoryId, "main", A<CancellationToken>._)).Returns(branch);
    A.CallTo(() => _gateway.GetBranchHeadAsync(Address, "main", A<CancellationToken>._)).Returns(later);

    await ApplyAsync(new GitHubPushEvent("main", Head, false, false, started));

    branch.HeadSha.ShouldBe(later);
    A.CallTo(() => _commits.GetOrCreateAsync(RepositoryId, Head, A<DateTimeOffset>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // F14. The usual case: the branch is where the push started, and the forge is not asked.
  [Fact]
  public async Task Apply_PushThatStartsWhereTheBranchIs_ShouldTrustItWithoutAskingTheForge()
  {
    var started = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    var branch = Branch.Create(RepositoryId, "main", started, Now.AddMinutes(-1));
    A.CallTo(() => _branches.FindAsync(RepositoryId, "main", A<CancellationToken>._)).Returns(branch);

    await ApplyAsync(new GitHubPushEvent("main", Head, false, false, started));

    branch.HeadSha.ShouldBe(Head);
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // F14. A push that creates the branch: nothing to compare, nothing to ask.
  [Fact]
  public async Task Apply_PushThatCreatesTheBranch_ShouldCreateItAtTheHeadWithoutAskingTheForge()
  {
    await ApplyAsync(new GitHubPushEvent("feature/x", Head, false, false));

    _added.ShouldNotBeNull().HeadSha.ShouldBe(Head);
    A.CallTo(() => _gateway.GetBranchHeadAsync(A<ForgeRepositoryRef>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // F14. Replayed after the branch was deleted: nothing is created again.
  [Fact]
  public async Task Apply_PushReplayedAfterTheBranchWasDeleted_ShouldCreateNothing()
  {
    var started = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    A.CallTo(() => _gateway.GetBranchHeadAsync(Address, "feature/x", A<CancellationToken>._)).Returns((GitSha?)null);

    await ApplyAsync(new GitHubPushEvent("feature/x", Head, false, true, started));

    _added.ShouldBeNull();
    A.CallTo(() => _branches.UpdateAsync(A<Branch>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _repositories.UpdateAsync(A<TrackedRepository>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // F15. A finalization saves the branch while the push computes its covered head: the push reads the row first, so
  // that its version refuses the save, and the job runs again on what the finalization wrote.
  [Fact]
  public async Task Apply_FinalizationSavesTheBranchMeanwhile_ShouldBeRefused()
  {
    var started = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");
    var branch = Branch.Create(RepositoryId, "main", started, Now.AddMinutes(-1));
    var stored = 0;
    var read = -1;
    A.CallTo(() => _branches.FindAsync(RepositoryId, "main", A<CancellationToken>._))
     .ReturnsLazily(
       () =>
       {
         read = stored;

         return branch;
       }
     );
    // The tracker reads the commits of the ancestry: the finalization saves the branch at that moment.
    A.CallTo(() => _commits.ListByShasAsync(RepositoryId, A<IReadOnlyCollection<GitSha>>._, A<CancellationToken>._))
     .ReturnsLazily(
       () =>
       {
         stored++;

         return Task.FromResult<IReadOnlyList<Commit>>([]);
       }
     );
    A.CallTo(() => _branches.UpdateAsync(branch, A<CancellationToken>._))
     .ReturnsLazily(() => read == stored ? Task.CompletedTask : throw new ConflictException("General.ConcurrentChange", "changed in the meantime"));

    await Should.ThrowAsync<ConflictException>(() => ApplyAsync(new GitHubPushEvent("main", Head, false, false, started)));
  }

  [Fact]
  public async Task Apply_Deleted_ShouldRemoveTheBranchAndNothingElse()
  {
    await ApplyAsync(new GitHubPushEvent("feature/x", null, true, false));

    A.CallTo(() => _branches.RemoveAsync(RepositoryId, "feature/x", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(_commits).MustNotHaveHappened();
    A.CallTo(_gateway).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public async Task Apply_NoBranch_ShouldDoNothing(string? name)
  {
    await ApplyAsync(new GitHubPushEvent(name, Head, false, true));

    A.CallTo(_branches).MustNotHaveHappened();
    A.CallTo(_commits).MustNotHaveHappened();
  }

  [Fact]
  public async Task Apply_NameLongerThanABranchCanHold_ShouldDoNothing()
  {
    await ApplyAsync(new GitHubPushEvent(new string('b', Branch.NameMaxLength + 1), Head, false, false));

    A.CallTo(_branches).MustNotHaveHappened();
  }

  [Fact]
  public async Task Apply_DefaultBranchTouchingTheConfiguration_ShouldReadItFromTheForgeAtTheHead()
  {
    A.CallTo(() => _gateway.GetTreeAsync(Address, Head, false, A<CancellationToken>._))
     .Returns(new ForgeTree(Head, [new ForgeTreeEntry("codecov.yml", Parent, false)], false));
    A.CallTo(() => _gateway.GetContentAsync(Address, "codecov.yml", Head, A<CancellationToken>._)).Returns("coverage:\n  precision: 1\n");

    await ApplyAsync(new GitHubPushEvent("main", Head, false, true));

    _repository.DefaultBranchYaml.ShouldBe("coverage:\n  precision: 1\n");
    _repository.DefaultBranchYamlSha.ShouldBe(Head);
    A.CallTo(() => _repositories.UpdateAsync(_repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Apply_SamePushTwice_ShouldSaveTheConfigurationOnce()
  {
    A.CallTo(() => _gateway.GetTreeAsync(Address, Head, false, A<CancellationToken>._))
     .Returns(new ForgeTree(Head, [new ForgeTreeEntry("codecov.yml", Parent, false)], false));
    A.CallTo(() => _gateway.GetContentAsync(Address, "codecov.yml", Head, A<CancellationToken>._)).Returns("yaml");
    var push = new GitHubPushEvent("main", Head, false, true);

    await ApplyAsync(push);
    await ApplyAsync(push);

    A.CallTo(() => _repositories.UpdateAsync(_repository, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Apply_DefaultBranchWithoutAConfigurationChange_ShouldNotReadTheForge()
  {
    await ApplyAsync(new GitHubPushEvent("main", Head, false, false));

    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Apply_OtherBranchTouchingTheConfiguration_ShouldNotRefreshTheDefault()
  {
    await ApplyAsync(new GitHubPushEvent("feature/x", Head, false, true));

    A.CallTo(() => _gateway.GetTreeAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<bool>._, A<CancellationToken>._)).MustNotHaveHappened();
    _repository.DefaultBranchYaml.ShouldBeNull();
  }

  [Fact]
  public async Task Apply_ConfigurationNoLongerAtTheHead_ShouldKeepTheRecordedOne()
  {
    _repository.RefreshDefaultBranchYaml("kept", Parent, Now.AddDays(-1));
    A.CallTo(() => _gateway.GetTreeAsync(Address, Head, false, A<CancellationToken>._)).Returns(new ForgeTree(Head, [], false));

    await ApplyAsync(new GitHubPushEvent("main", Head, false, true));

    _repository.DefaultBranchYaml.ShouldBe("kept");
    A.CallTo(_repositories).MustNotHaveHappened();
  }

  [Fact]
  public async Task Apply_NullArguments_ShouldThrow()
  {
    var push = new GitHubPushEvent("main", Head, false, false);

    await Should.ThrowAsync<ArgumentNullException>(() => _processor.ApplyAsync(null!, Address, push, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _processor.ApplyAsync(_repository, null!, push, Ct));
    await Should.ThrowAsync<ArgumentNullException>(() => _processor.ApplyAsync(_repository, Address, null!, Ct));
  }

  private Task ApplyAsync(GitHubPushEvent push) => _processor.ApplyAsync(_repository, Address, push, Ct);

  private Commit Complete(GitSha sha)
  {
    var commit = Commit.Create(RepositoryId, sha, Now);

    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(commit, 900L + _stored.Count);
    typeof(Commit).GetProperty(nameof(Commit.State))!.SetValue(commit, CommitState.Complete);
    _stored.Add(commit);

    return commit;
  }

  #endregion
}

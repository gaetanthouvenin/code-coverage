// ------------------------------------------------------------------------------------------------
// <copyright file="ImportCodecovHistoryCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Features.Repositories.Commands.ImportCodecovHistory;
using CodeCoverage.Domain.Codecov;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Codecov;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Repositories.Commands.ImportCodecovHistory;

public sealed class ImportCodecovHistoryCommandHandlerTests
{
  #region Fields

  private const long ImportId = 7;

  private const long RepositoryId = 5;

  private static readonly DateTimeOffset Now = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha First = GitSha.Parse(new string('1', GitSha.Length));

  private static readonly GitSha Second = GitSha.Parse(new string('2', GitSha.Length));

  private static readonly GitSha Known = GitSha.Parse(new string('3', GitSha.Length));

  private readonly ICodecovImportRepository _imports = A.Fake<ICodecovImportRepository>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly ISecretProtector _protector = A.Fake<ISecretProtector>();

  private readonly ICodecovApiClient _codecov = A.Fake<ICodecovApiClient>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly List<Commit> _saved = [];

  private readonly IClock _clock = A.Fake<IClock>();

  private DateTimeOffset _now = Now;

  private readonly CodecovImport _import = CodecovImport.Start(RepositoryId, "protected", Now.AddMinutes(-1));

  private readonly ImportCodecovHistoryCommandHandler _handler;

  #endregion

  #region Ctors

  public ImportCodecovHistoryCommandHandlerTests()
  {
    typeof(CodecovImport).GetProperty(nameof(CodecovImport.Id))!.SetValue(_import, ImportId);
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "badge", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, RepositoryId);

    A.CallTo(() => _clock.UtcNow).ReturnsLazily(() => _now);
    A.CallTo(() => _imports.FindAsync(ImportId, A<CancellationToken>._)).Returns(_import);
    A.CallTo(() => _repositories.FindAsync(RepositoryId, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitHub, "1001", "octo-org", OrganizationAccountType.Organization, Now));
    A.CallTo(() => _protector.Unprotect("protected")).Returns("plain-token");
    A.CallTo(() => _reports.SaveImportedAsync(A<Commit>._, A<CancellationToken>._))
     .ReturnsLazily((Commit commit, CancellationToken _) =>
       {
         if (commit.Sha == Known)
         {
           return Task.FromResult(false);
         }

         _saved.Add(commit);

         return Task.FromResult(true);
       });

    _handler = new ImportCodecovHistoryCommandHandler(
      _imports,
      _repositories,
      _directory,
      _protector,
      _codecov,
      _reports,
      _clock,
      NullLogger<ImportCodecovHistoryCommandHandler>.Instance
    );
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldCreateCompleteCommitsWithoutBlob()
  {
    Pages(new CodecovCommitPage([Imported(First, "main", 150, 40, 10), Imported(Second, "feature/x", 1, 1, 1)], HasNext: false));

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    _saved.Count.ShouldBe(2);
    var commit = _saved[0];
    commit.RepositoryId.ShouldBe(RepositoryId);
    commit.Sha.ShouldBe(First);
    commit.State.ShouldBe(CommitState.Complete);
    commit.IsImported.ShouldBeTrue();
    commit.ConfigSource.ShouldBe(Commit.ImportedConfigSource);
    commit.Branch.ShouldBe("main");
    commit.Files.ShouldBe(12);
    commit.SessionCount.ShouldBe(0);
    commit.LastUploadAt.ShouldBeNull();
    // Dated by Codecov, so that the point of the history falls where the commit was made.
    commit.CompletedAt.ShouldBe(new DateTimeOffset(2026, 10, 2, 10, 15, 0, TimeSpan.Zero));
    // The coverage is computed again from the counters.
    commit.Coverage.ShouldBe(new CoverageTotals(150, 40, 10, 0, 0).Coverage);
    A.CallTo(() => _codecov.ListCommitsAsync(Forge.GitHub, "octo-org", "", "app", "plain-token", 1, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_KnownSha_ShouldKeepExistingCommit()
  {
    Pages(new CodecovCommitPage([Imported(Known, "main", 1, 0, 0), Imported(First, "main", 1, 0, 0)], HasNext: false));

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    _saved.ShouldHaveSingleItem().Sha.ShouldBe(First);
    _import.ImportedCommits.ShouldBe(1);
  }

  [Fact]
  public async Task Handle_ShouldReadEveryPageAndRecordTheProgressThenDeleteTheCredential()
  {
    Pages(
      new CodecovCommitPage([Imported(First, "main", 1, 0, 0)], HasNext: true),
      new CodecovCommitPage([Imported(Second, "main", 1, 0, 0)], HasNext: false)
    );

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    _saved.Count.ShouldBe(2);
    A.CallTo(() => _codecov.ListCommitsAsync(Forge.GitHub, "octo-org", "", "app", "plain-token", 2, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    // The page before the last is recorded, so that a retry goes on from page 2.
    A.CallTo(() => _imports.UpdateAsync(_import, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(() => _imports.DeleteAsync(ImportId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_ShouldDeleteCredential()
  {
    Pages(new CodecovCommitPage([], HasNext: false));

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    A.CallTo(() => _imports.DeleteAsync(ImportId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // A retry goes on from the page the last run recorded.
  [Fact]
  public async Task Handle_Resumed_ShouldStartFromTheRecordedPage()
  {
    _import.RecordPage(3);
    Pages(new CodecovCommitPage([], HasNext: false));

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    A.CallTo(() => _codecov.ListCommitsAsync(A<Forge>._, A<string>._, A<string>._, A<string>._, A<string>._, 1, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _codecov.ListCommitsAsync(Forge.GitHub, "octo-org", "", "app", "plain-token", 2, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_CodecovRefuses_ShouldDeleteTheCredentialAndFailForGood()
  {
    A.CallTo(() => _codecov.ListCommitsAsync(A<Forge>._, A<string>._, A<string>._, A<string>._, A<string>._, A<int>._, A<CancellationToken>._)).Returns((CodecovCommitPage?)null);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(() => _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct).AsTask());

    exception.ErrorCode.ShouldBe(RepositoryErrors.CodecovRefused);
    exception.Message.ShouldNotContain("plain-token");
    A.CallTo(() => _imports.DeleteAsync(ImportId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    _saved.ShouldBeEmpty();
  }

  // A long history: once the run spent its budget, the next page is recorded and the job runs again shortly.
  [Fact]
  public async Task Handle_BudgetSpent_ShouldRecordTheNextPageAndRunAgain()
  {
    Pages(
      new CodecovCommitPage([Imported(First, "main", 1, 0, 0)], HasNext: true),
      new CodecovCommitPage([Imported(Second, "main", 1, 0, 0)], HasNext: true),
      new CodecovCommitPage([], HasNext: false)
    );
    A.CallTo(() => _reports.SaveImportedAsync(A<Commit>._, A<CancellationToken>._))
     .Invokes(() => _now = _now.AddMinutes(4))
     .Returns(true);

    var exception = await Should.ThrowAsync<RetryLaterException>(() => _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct).AsTask());

    exception.Delay.ShouldBe(ImportCodecovHistoryCommandHandler.ContinueDelay);
    // Two pages in 8 minutes: past the 7 minutes of a run, page 3 is recorded for the next run.
    _import.NextPage.ShouldBe(3);
    A.CallTo(() => _imports.UpdateAsync(_import, A<CancellationToken>._)).MustHaveHappenedTwiceExactly();
    A.CallTo(() => _imports.DeleteAsync(A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _codecov.ListCommitsAsync(A<Forge>._, A<string>._, A<string>._, A<string>._, A<string>._, 3, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // A repository of a GitLab subgroup: its namespace reaches Codecov.
  [Fact]
  public async Task Handle_Subgroup_ShouldPassTheNamespace()
  {
    var nested = TrackedRepository.Create(1, "3003", RepositorySlug.Parse("group/sub/app"), "main", true, "badge", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(nested, RepositoryId);
    A.CallTo(() => _repositories.FindAsync(RepositoryId, A<CancellationToken>._)).Returns(nested);
    A.CallTo(() => _directory.FindCurrentAsync(A<CancellationToken>._))
     .Returns(Organization.CreatePending(Forge.GitLab, "1001", "group", OrganizationAccountType.Organization, Now));
    A.CallTo(() => _codecov.ListCommitsAsync(Forge.GitLab, "group", "sub", "app", "plain-token", 1, A<CancellationToken>._))
     .Returns(new CodecovCommitPage([], HasNext: false));

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    A.CallTo(() => _codecov.ListCommitsAsync(Forge.GitLab, "group", "sub", "app", "plain-token", 1, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // Purged or abandoned: nothing holds the token any more, and nothing is read.
  [Fact]
  public async Task Handle_ImportGone_ShouldDoNothing()
  {
    A.CallTo(() => _imports.FindAsync(ImportId, A<CancellationToken>._)).Returns((CodecovImport?)null);

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    A.CallTo(_codecov).MustNotHaveHappened();
    A.CallTo(_protector).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_RepositoryGone_ShouldDeleteTheCredential()
  {
    A.CallTo(() => _repositories.FindAsync(RepositoryId, A<CancellationToken>._)).Returns((TrackedRepository?)null);

    await _handler.Handle(new ImportCodecovHistoryCommand(ImportId), Ct);

    A.CallTo(() => _imports.DeleteAsync(ImportId, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    A.CallTo(_codecov).MustNotHaveHappened();
  }

  private static CodecovCommit Imported(GitSha sha, string branch, int hits, int misses, int partials) =>
    new(sha, branch, new DateTimeOffset(2026, 10, 2, 10, 15, 0, TimeSpan.Zero), 12, new CoverageTotals(hits, misses, partials, 0, 0));

  private void Pages(params CodecovCommitPage[] pages)
  {
    for (var i = 0; i < pages.Length; i++)
    {
      var page = pages[i];
      var number = i + 1;

      A.CallTo(() => _codecov.ListCommitsAsync(Forge.GitHub, "octo-org", "", "app", "plain-token", number, A<CancellationToken>._)).Returns(page);
    }
  }

  #endregion
}

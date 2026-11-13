// ------------------------------------------------------------------------------------------------
// <copyright file="GetAnnotatedSourceQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Behaviors;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Queries.GetAnnotatedSource;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Security;
using CodeCoverage.Application.Services;
using CodeCoverage.Application.Tests.Behaviors.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Source;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Application.Tests.Features.Commits.Queries.GetAnnotatedSource;

public sealed class GetAnnotatedSourceQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IReportViewCache _views = A.Fake<IReportViewCache>();

  private readonly ISourceContentCache _sources = A.Fake<ISourceContentCache>();

  private readonly Commit _commit;

  #endregion

  #region Ctors

  public GetAnnotatedSourceQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    _commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(_commit);
    A.CallTo(() => _reports.GetReportAsync(9, A<CancellationToken>._)).Returns(new byte[] { 4 });
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._))
     .Returns(CoverageReport.Create([FileCoverage.Create("src/A.cs", [new LineCoverage(1, 2, 0, 0, false, null), new LineCoverage(2, 0, 0, 0, false, null)])]));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldMergeLinesWithCoverage()
  {
    A.CallTo(() => _sources.GetSourceAsync(A<ForgeRepositoryRef>._, Sha, "src/A.cs", A<CancellationToken>._)).Returns("covered();\nmissed();\n");

    var source = await Handler().Handle(Query("/src/A.cs"), Ct);

    source.IsSourceAvailable.ShouldBeTrue();
    source.Lines.Select(static l => (l.Text, l.State)).ShouldBe([("covered();", LineState.Hit), ("missed();", LineState.Miss)]);
    source.Totals.Hits.ShouldBe(1);
    A.CallTo(() => _sources.GetSourceAsync(new ForgeRepositoryRef(Forge.GitHub, "1", "2002"), Sha, "src/A.cs", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  // The visitor's path must be one of the report: an unknown one costs the forge nothing.
  [Fact]
  public async Task Handle_FileNotInReport_ShouldThrowNotFoundWithoutCallingTheForge()
  {
    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Query("src/../../secrets"), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.FileNotFound);
    A.CallTo(_sources).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_Compacted_ShouldThrowUnprocessableWithoutCallingTheForge()
  {
    typeof(Commit).GetProperty(nameof(Commit.CompletedAt))!.SetValue(_commit, Now);
    A.CallTo(() => _reports.GetReportAsync(9, A<CancellationToken>._)).Returns((byte[]?)null);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await Handler().Handle(Query("src/A.cs"), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.Compacted);
    A.CallTo(_sources).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldThrowNotFoundWithoutCallingTheForge()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns((Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await Handler().Handle(Query("src/A.cs"), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
    A.CallTo(_sources).MustNotHaveHappened();
  }

  // Unavailable keeps the totals: the page still shows the figures of the file.
  [Fact]
  public async Task Handle_SourceUnavailable_ShouldKeepTotals()
  {
    A.CallTo(() => _sources.GetSourceAsync(A<ForgeRepositoryRef>._, A<GitSha>._, A<string>._, A<CancellationToken>._)).Returns((string?)null);

    var source = await Handler().Handle(Query("src/A.cs"), Ct);

    source.IsSourceAvailable.ShouldBeFalse();
    source.Lines.ShouldBeEmpty();
    source.Totals.Lines.ShouldBe(2);
  }

  // In the pipeline the refusal comes from AuthorizationBehavior: neither the report nor the forge is ever reached.
  [Fact]
  public async Task Pipeline_NoAccess_ShouldThrowNotFoundWithoutCallingTheForge()
  {
    var services = A.Fake<IServiceProvider>();
    var scope = A.Fake<IOrganizationScope>();
    var access = A.Fake<IRepositoryAccessService>();
    var organization = A.Fake<ICurrentOrganization>();
    A.CallTo(() => services.GetService(typeof(IOrganizationScope))).Returns(scope);
    A.CallTo(() => services.GetService(typeof(IRepositoryRepository))).Returns(_repositories);
    A.CallTo(() => services.GetService(typeof(IRepositoryAccessService))).Returns(access);
    A.CallTo(() => services.GetService(typeof(IOrganizationRoleResolver))).Returns(A.Fake<IOrganizationRoleResolver>());
    A.CallTo(() => services.GetService(typeof(ICurrentOrganization))).Returns(organization);
    A.CallTo(() => scope.TryEnterAsync(Forge.GitHub, "octo-org", A<CancellationToken>._)).Returns(true);
    A.CallTo(() => access.CanAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<CancellationToken>._)).Returns(false);
    var behavior = new AuthorizationBehavior<GetAnnotatedSourceQuery, AnnotatedSource>(Principals.User(), services);
    var handler = Handler();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      async () => await behavior.Handle(Query("src/A.cs"), (message, ct) => handler.Handle(message, ct), Ct));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
    A.CallTo(_sources).MustNotHaveHappened();
    A.CallTo(_reports).MustNotHaveHappened();
  }

  private static GetAnnotatedSourceQuery Query(string path) => new(Forge.GitHub, "octo-org", "app", Sha, path, null);

  private GetAnnotatedSourceQueryHandler Handler() =>
    new(
      new CommitLocator(_repositories, _commits),
      _commits,
      new CommitFileReader(_reports, _codec, new CommitFlagView(new CommitReportReader(_reports, _codec), _views)),
      _sources
    );

  #endregion
}

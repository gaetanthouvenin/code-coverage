// ------------------------------------------------------------------------------------------------
// <copyright file="GetCommitFilesQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Commits;
using CodeCoverage.Application.Features.Commits.Queries.GetCommitFiles;
using CodeCoverage.Application.Features.Commits.Queries.GetFileCoverage;
using CodeCoverage.Application.Features.Repositories;
using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Application.Tests.Features.Commits.Queries.GetCommitFiles;

public sealed class GetCommitFilesQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse(new string('a', GitSha.Length));

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICoverageReportCodec _codec = A.Fake<ICoverageReportCodec>();

  private readonly IReportViewCache _views = A.Fake<IReportViewCache>();

  private readonly Commit _commit;

  #endregion

  #region Ctors

  public GetCommitFilesQueryHandlerTests()
  {
    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    _commit = Commit.Create(5, Sha, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(_commit, 9L);

    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns(_commit);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The index holds the counters of each file: listing the files decodes no line.
  [Fact]
  public async Task Handle_ShouldReadFileIndexWithoutDecodingLines()
  {
    var index = new byte[] { 1, 2, 3 };
    A.CallTo(() => _reports.GetFileIndexAsync(9, A<CancellationToken>._)).Returns(index);
    A.CallTo(() => _codec.DecodeFileIndex(A<ReadOnlyMemory<byte>>._))
     .Returns([new FileTotals("src/b.cs", default), new FileTotals("src/a.cs", default), new FileTotals("tests/t.cs", default)]);

    var files = await FilesHandler().Handle(Query(flag: null, path: "/src/"), Ct);

    files.Select(static f => f.Path).ShouldBe(["src/a.cs", "src/b.cs"]);
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).MustNotHaveHappened();
    A.CallTo(() => _reports.GetReportAsync(A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
    A.CallTo(() => _reports.ListSessionsAsync(A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_WithFlag_ShouldMergeTheSessionsOfThatFlag()
  {
    var report = CoverageReport.Create([FileCoverage.Create("src/a.cs", [new LineCoverage(1, 1, 0, 0, false, null)])]);
    A.CallTo(() => _reports.ListSessionsAsync(9, A<CancellationToken>._)).Returns([]);
    A.CallTo(() => _views.GetOrCreate(9, _commit.ReportRevision, A<IReadOnlyList<SessionReport>>._, A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(new[] { "unit" }), A<PathPatternSet>._))
     .Returns(ReportView.Build([new SessionReport(["unit"], report)], ["unit"], new PathPatternSet([])));

    var files = await FilesHandler().Handle(Query(flag: "unit", path: null), Ct);

    files.ShouldHaveSingleItem().Path.ShouldBe("src/a.cs");
    A.CallTo(() => _reports.GetFileIndexAsync(A<long>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  // A session whose report is gone: the commit was compacted, only its totals are kept.
  [Fact]
  public async Task Handle_WithFlagOnACompactedCommit_ShouldThrowCompacted()
  {
    var session = Session.FromUpload(9, 1, 77, ["unit"], null, null, null, default);
    A.CallTo(() => _reports.ListSessionsAsync(9, A<CancellationToken>._)).Returns([session]);
    A.CallTo(() => _reports.GetUploadCoverageAsync(77, A<CancellationToken>._)).Returns((byte[]?)null);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await FilesHandler().Handle(Query(flag: "unit", path: null), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.Compacted);
  }

  [Fact]
  public async Task Handle_UnknownCommit_ShouldThrowCommitNotFound()
  {
    A.CallTo(() => _commits.FindByShaAsync(5, Sha, A<CancellationToken>._)).Returns((Commit?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await FilesHandler().Handle(Query(flag: null, path: null), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.NotFound);
  }

  [Fact]
  public async Task Handle_UnknownRepository_ShouldThrowRepositoryNotFound()
  {
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns((TrackedRepository?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await FilesHandler().Handle(Query(flag: null, path: null), Ct));

    exception.ErrorCode.ShouldBe(RepositoryErrors.NotFound);
  }

  [Fact]
  public async Task FileCoverage_FileOfTheMergedReport_ShouldReturnItsLines()
  {
    var blob = new byte[] { 4 };
    A.CallTo(() => _reports.GetReportAsync(9, A<CancellationToken>._)).Returns(blob);
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._))
     .Returns(CoverageReport.Create([FileCoverage.Create("src/a.cs", [new LineCoverage(3, 2, 0, 0, false, null)])]));

    var view = await CoverageHandler().Handle(new GetFileCoverageQuery(Forge.GitHub, "octo-org", "app", Sha, "/src/a.cs", null), Ct);

    view.Flag.ShouldBeNull();
    view.File.Lines.ShouldHaveSingleItem().Line.ShouldBe(3);
  }

  [Fact]
  public async Task FileCoverage_FileNotInTheReport_ShouldThrowFileNotFound()
  {
    A.CallTo(() => _reports.GetReportAsync(9, A<CancellationToken>._)).Returns(new byte[] { 4 });
    A.CallTo(() => _codec.Decode(A<ReadOnlyMemory<byte>>._)).Returns(CoverageReport.Create([]));

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await CoverageHandler().Handle(new GetFileCoverageQuery(Forge.GitHub, "octo-org", "app", Sha, "src/missing.cs", null), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.FileNotFound);
  }

  [Fact]
  public async Task FileCoverage_FinalizedCommitWithoutReport_ShouldThrowCompacted()
  {
    typeof(Commit).GetProperty(nameof(Commit.CompletedAt))!.SetValue(_commit, Now);
    A.CallTo(() => _reports.GetReportAsync(9, A<CancellationToken>._)).Returns((byte[]?)null);

    var exception = await Should.ThrowAsync<UnprocessableEntityException>(async () => await CoverageHandler().Handle(new GetFileCoverageQuery(Forge.GitHub, "octo-org", "app", Sha, "src/a.cs", null), Ct));

    exception.ErrorCode.ShouldBe(CommitErrors.Compacted);
  }

  private static GetCommitFilesQuery Query(string? flag, string? path) => new(Forge.GitHub, "octo-org", "app", Sha, flag, path);

  private CommitFlagView FlagView() => new(new CommitReportReader(_reports, _codec), _views);

  private GetCommitFilesQueryHandler FilesHandler() => new(new CommitLocator(_repositories, _commits), _reports, _codec, FlagView());

  private GetFileCoverageQueryHandler CoverageHandler() => new(new CommitLocator(_repositories, _commits), new CommitFileReader(_reports, _codec, FlagView()));

  #endregion
}

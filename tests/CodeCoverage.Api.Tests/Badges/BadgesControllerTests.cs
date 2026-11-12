// ------------------------------------------------------------------------------------------------
// <copyright file="BadgesControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;

namespace CodeCoverage.Api.Tests.Badges;

// The badges, anonymous, at the routes of the API and of Codecov; the database is faked behind its ports.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class BadgesControllerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitReportRepository _reports = A.Fake<ICommitReportRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  #endregion

  #region Ctors

  public BadgesControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", false, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    var head = Commit.Create(5, GitSha.Parse(new string('a', GitSha.Length)), Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(head, 9L);
    head.MarkPending("main", null, Now);
    head.RecordFinalization(CommitState.Complete, 1, new CoverageTotals(85678, 14322, 0, 0, 0), 1, 1, CarryforwardStatus.None, "default", Now);
    var main = Branch.Create(5, "main", head.Sha, Now);
    main.Cover(9, Now);
    var feature = Branch.Create(5, "feature/x", head.Sha, Now);
    feature.Cover(9, Now);

    A.CallTo(() => _scope.TryEnterAsync(Forge.GitHub, "octo-org", A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(RepositorySlug.Parse("octo-org/app"), A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(main);
    A.CallTo(() => _branches.FindAsync(5, "feature/x", A<CancellationToken>._)).Returns(feature);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(head);
    A.CallTo(() => _reports.ListFlagTotalsAsync(9, A<CancellationToken>._))
     .Returns([CommitFlagTotals.Create(head, CommitFlagTotals.ProjectFlag, new CoverageTotals(85678, 14322, 0, 0, 0), false, null)]);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The acceptance of M3.15: a Codecov URL whose host alone changed works.
  [Fact]
  public async Task Get_CodecovStyleBranchUrl_ShouldServeSvg()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/gh/octo-org/app/branch/feature%2Fx/graph/badge.svg", UriKind.Relative), Ct);
    var svg = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
    response.Headers.CacheControl!.NoCache.ShouldBeTrue();
    response.Headers.ETag.ShouldNotBeNull();
    svg.ShouldContain(">86%<");
    A.CallTo(() => _branches.FindAsync(5, "feature/x", A<CancellationToken>._)).MustHaveHappened();
  }

  // Review R3: the route is decoded once; only an encoded slash is left to decode (fix%2541 is the branch fix%41).
  [Fact]
  public async Task Get_BranchWithAPercentSign_ShouldNotBeDecodedTwice()
  {
    var head = Commit.Create(5, GitSha.Parse(new string('a', GitSha.Length)), Now);
    var percent = Branch.Create(5, "fix%41", head.Sha, Now);
    percent.Cover(9, Now);
    A.CallTo(() => _branches.FindAsync(5, "fix%41", A<CancellationToken>._)).Returns(percent);
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/gh/octo-org/app/branch/fix%2541/graph/badge.svg", UriKind.Relative), Ct);

    (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(">86%<");
    A.CallTo(() => _branches.FindAsync(5, "fixA", A<CancellationToken>._)).MustNotHaveHappened();
  }

  // Review R4: a NUL in the branch, the flag or the address is an unknown badge, never a server error.
  [Theory]
  [InlineData("/gh/octo-org/app/graph/badge.svg?branch=a%00b")]
  [InlineData("/gh/octo-org/app/graph/badge.svg?flag=a%00b")]
  [InlineData("/api/v1/badges/github/octo-org/app.svg?branch=a%00b")]
  public async Task Get_ControlCharacter_ShouldReturn200Grey(string path)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(">unknown<");
    A.CallTo(() => _branches.FindAsync(A<long>._, A<string>.That.Contains("\0"), A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Theory]
  [InlineData("/gh/octo-org/app/graph/badge.svg")]
  [InlineData("/github/octo-org/app/graphs/badge.svg")]
  [InlineData("/api/v1/badges/github/octo-org/app.svg")]
  public async Task Get_EachRoute_ShouldServeTheBadge(string path)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(">86%<");
  }

  // Rounded to the nearest as Codecov; the text is the number alone, as the badge.txt of Codecov (review R10); an invalid
  // precision falls back to 0 (a named difference with Codecov).
  [Theory]
  [InlineData("", "86")]
  [InlineData("?precision=1", "85.7")]
  [InlineData("?precision=2", "85.68")]
  [InlineData("?precision=7", "86")]
  [InlineData("?precision=abc", "86")]
  public async Task Get_Text_ShouldRoundToTheNearest(string query, string expected)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/gh/octo-org/app/graph/badge.txt" + query, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
    (await response.Content.ReadAsStringAsync(Ct)).ShouldBe(expected);
  }

  [Fact]
  public async Task Get_IfNoneMatch_ShouldReturn304()
  {
    using var client = Client();
    using var first = await client.GetAsync(new Uri("/gh/octo-org/app/graph/badge.svg", UriKind.Relative), Ct);
    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/gh/octo-org/app/graph/badge.svg", UriKind.Relative));
    request.Headers.IfNoneMatch.Add(first.Headers.ETag!);

    using var response = await client.SendAsync(request, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    response.Headers.CacheControl!.NoCache.ShouldBeTrue();
  }

  // The SVG and the text never share an entity tag.
  [Fact]
  public async Task Get_SvgAndText_ShouldHaveDifferentEntityTags()
  {
    using var client = Client();

    using var svg = await client.GetAsync(new Uri("/gh/octo-org/app/graph/badge.svg", UriKind.Relative), Ct);
    using var text = await client.GetAsync(new Uri("/gh/octo-org/app/graph/badge.txt", UriKind.Relative), Ct);

    svg.Headers.ETag!.Tag.ShouldNotBe(text.Headers.ETag!.Tag);
  }

  [Theory]
  [InlineData("/gh/octo-org/unknown/graph/badge.svg")]
  [InlineData("/gh/unknown-org/app/graph/badge.svg")]
  [InlineData("/api/v1/badges/svn/octo-org/app.svg")]
  [InlineData("/gh/octo-org/app/branch/missing/graph/badge.svg")]
  public async Task Get_Unknown_ShouldReturn200Grey(string path)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
    var svg = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    svg.ShouldContain(">unknown<");
    svg.ShouldContain("#9f9f9f");
  }

  [Fact]
  public async Task Get_UnknownText_ShouldSayUnknown()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/gh/octo-org/unknown/graph/badge.txt", UriKind.Relative), Ct);

    (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("unknown");
  }

  // Decision 13: another extension is not a badge address.
  [Theory]
  [InlineData("/gh/octo-org/app/graph/badge.png")]
  [InlineData("/gh/octo-org/app/branch/main/graph/badge.png")]
  public async Task Get_OtherExtension_ShouldReturn404(string path)
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  [Fact]
  public async Task Get_Flag_ShouldUseItAsLabel()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri("/gh/octo-org/app/graph/badge.svg?flag=unit", UriKind.Relative), Ct);

    (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(">unit<");
  }

  [Fact]
  public async Task Get_Label_ShouldComeFromTheSettings()
  {
    using var client = Client(label: "codecov");

    using var response = await client.GetAsync(new Uri("/gh/octo-org/app/graph/badge.svg", UriKind.Relative), Ct);

    (await response.Content.ReadAsStringAsync(Ct)).ShouldContain(">codecov<");
  }

  private HttpClient Client(string? label = null) =>
    _factory.WithWebHostBuilder(b =>
             {
               if (label is not null)
               {
                 b.UseSetting("Badges:Label", label);
               }

               b.ConfigureTestServices(s =>
               {
                 s.AddScoped(_ => _scope);
                 s.AddScoped(_ => _organization);
                 s.AddScoped(_ => _repositories);
                 s.AddScoped(_ => _branches);
                 s.AddScoped(_ => _reports);
                 s.AddScoped(_ => _commits);
               });
             })
            .CreateClient();

  #endregion
}

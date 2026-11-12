// ------------------------------------------------------------------------------------------------
// <copyright file="EffectiveConfigTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Domain.Users;

namespace CodeCoverage.Api.Tests.Repositories;

// GET …/config through the ApiToken scheme and the gate: read-only, the forge is never called.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class EffectiveConfigTests
{
  #region Fields

  private const string Address = "/api/v1/repos/github/octo-org/app/config";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Head = GitSha.Parse(new string('c', GitSha.Length));

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IRepositoryRepository _repositories = A.Fake<IRepositoryRepository>();

  private readonly IRepositoryAccessService _access = A.Fake<IRepositoryAccessService>();

  private readonly IBranchRepository _branches = A.Fake<IBranchRepository>();

  private readonly ICommitRepository _commits = A.Fake<ICommitRepository>();

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IOrganizationSettingsProvider _settings = A.Fake<IOrganizationSettingsProvider>();

  private readonly IForgeGatewayProvider _gateways = A.Fake<IForgeGatewayProvider>();

  #endregion

  #region Ctors

  public EffectiveConfigTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    var repository = TrackedRepository.Create(1, "2002", RepositorySlug.Parse("octo-org/app"), "main", true, "protected", Now);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    repository.RefreshDefaultBranchYaml("coverage:\n  precision: 1\n", Head, Now);
    var head = Commit.Create(5, Head, Now);
    typeof(Commit).GetProperty(nameof(Commit.Id))!.SetValue(head, 9L);
    var branch = Branch.Create(5, "main", Head, Now);
    branch.Cover(9, Now);

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _repositories.FindBySlugAsync(A<RepositorySlug>._, A<CancellationToken>._)).Returns(repository);
    A.CallTo(() => _access.CanAsync(ApiTokens.UserId, "2002", RepositoryPermission.Read, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _branches.FindAsync(5, "main", A<CancellationToken>._)).Returns(branch);
    A.CallTo(() => _commits.FindAsync(9, A<CancellationToken>._)).Returns(head);
    A.CallTo(() => _settings.GetAsync(A<CancellationToken>._)).Returns(new OrganizationSettingsSnapshot("coverage", true, true, "coverage:\n  round: up\n"));
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_KeptForTheCoveredHead_ShouldReturnItAtThatSha()
  {
    A.CallTo(() => _configs.FindAsync(5, Head, A<CancellationToken>._))
     .Returns(CommitConfig.Create(5, Head, ConfigSource.Commit, "codecov.yml", "coverage:\n  precision: 3\n", [], [], Now));
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Address, UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var root = body.RootElement;
    root.GetProperty("source").GetString().ShouldBe("Commit");
    root.GetProperty("isResolvedAtSha").GetBoolean().ShouldBeTrue();
    root.GetProperty("sha").GetString().ShouldBe(Head.Value);
    root.GetProperty("path").GetString().ShouldBe("codecov.yml");
    root.GetProperty("yaml").GetString().ShouldBe("coverage:\n  precision: 3\n");
    var coverage = root.GetProperty("config").GetProperty("coverage");
    coverage.GetProperty("precision").GetDecimal().ShouldBe(3m);
    coverage.GetProperty("round").GetString().ShouldBe("up");
    A.CallTo(_gateways).MustNotHaveHappened();
  }

  [Fact]
  public async Task Get_NothingKept_ShouldMergeTheDefaultBranchWithoutTheForge()
  {
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Address, UriKind.Relative), Ct);
    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.RootElement.GetProperty("source").GetString().ShouldBe("RepositoryDefault");
    body.RootElement.GetProperty("isResolvedAtSha").GetBoolean().ShouldBeFalse();
    body.RootElement.GetProperty("sha").ValueKind.ShouldBe(JsonValueKind.Null);
    body.RootElement.GetProperty("config").GetProperty("coverage").GetProperty("precision").GetDecimal().ShouldBe(1m);
    A.CallTo(_gateways).MustNotHaveHappened();
    A.CallTo(() => _configs.AddAsync(A<CommitConfig>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Get_UnreadableRepository_ShouldReturn404()
  {
    A.CallTo(() => _access.CanAsync(A<long>._, A<string>._, A<RepositoryPermission>._, A<CancellationToken>._)).Returns(false);
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Address, UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _repositories);
                                  s.AddScoped(_ => _access);
                                  s.AddScoped(_ => _branches);
                                  s.AddScoped(_ => _commits);
                                  s.AddScoped(_ => _configs);
                                  s.AddScoped(_ => _settings);
                                  s.AddScoped(_ => _gateways);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

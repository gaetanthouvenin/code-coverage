// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigResolverTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Repositories;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Services;

// The fake parser reads a text as the value of codecov.strict_yaml_branch, which shows the layer that won;
// a text starting with "shared:" sets codecov.branch instead, so that two layers can both show through.
public sealed class ConfigResolverTests
{
  #region Fields

  private const string Invalid = "!invalid";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly GitSha Sha = GitSha.Parse("a94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha GithubTree = GitSha.Parse("b94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly GitSha DevTree = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  private static readonly ForgeRepositoryRef Address = new(Forge.GitHub, "101", "1");

  private readonly Dictionary<GitSha, CommitConfig> _cache = [];

  private readonly ICommitConfigRepository _configs = A.Fake<ICommitConfigRepository>();

  private readonly IForgeGateway _gateway = A.Fake<IForgeGateway>();

  private readonly IOrganizationSettingsProvider _settings = A.Fake<IOrganizationSettingsProvider>();

  private readonly ConfigResolver _resolver;

  private string? _organizationYaml;

  #endregion

  #region Ctors

  public ConfigResolverTests()
  {
    var gateways = A.Fake<IForgeGatewayProvider>();
    A.CallTo(() => gateways.GetGateway(Forge.GitHub)).Returns(_gateway);

    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    var parser = A.Fake<ICoverageConfigParser>();
    A.CallTo(() => parser.Parse(A<string>._)).ReturnsLazily((string yaml) => Parse(yaml));

    A.CallTo(() => _settings.GetAsync(A<CancellationToken>._))
     .ReturnsLazily(() => Task.FromResult(new OrganizationSettingsSnapshot("coverage", true, true, _organizationYaml)));

    A.CallTo(() => _configs.FindAsync(A<long>._, A<GitSha>._, A<CancellationToken>._))
     .ReturnsLazily((long _, GitSha sha, CancellationToken _) => _cache.GetValueOrDefault(sha));

    A.CallTo(() => _configs.AddAsync(A<CommitConfig>._, A<CancellationToken>._))
     .Invokes((CommitConfig config, CancellationToken _) => _cache[config.Sha] = config);

    A.CallTo(() => _gateway.GetTreeAsync(Address, Sha, false, A<CancellationToken>._)).Returns(new ForgeTree(Sha, [], false));

    _resolver = new ConfigResolver(_configs, gateways, parser, _settings, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Resolve_ShouldPreferRootThenGithubThenDev()
  {
    SetTree(rootFile: "codecov.yml", githubFile: ".codecov.yml", devFile: "codecov.yaml");
    A.CallTo(() => _gateway.GetContentAsync(Address, "codecov.yml", Sha, A<CancellationToken>._)).Returns("root");

    var resolved = await ResolveAsync(Repository());

    resolved.Source.ShouldBe(ConfigSource.Commit);
    resolved.Config.StrictYamlBranch.ShouldBe("root");
    _cache[Sha].Path.ShouldBe("codecov.yml");
    A.CallTo(() => _gateway.GetTreeAsync(Address, GithubTree, false, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_NoRootFile_ShouldReadGithubBeforeDev()
  {
    SetTree(rootFile: null, githubFile: ".codecov.yml", devFile: "codecov.yaml");
    A.CallTo(() => _gateway.GetContentAsync(Address, ".github/.codecov.yml", Sha, A<CancellationToken>._)).Returns("github");

    (await ResolveAsync(Repository())).Config.StrictYamlBranch.ShouldBe("github");

    _cache[Sha].Path.ShouldBe(".github/.codecov.yml");
    A.CallTo(() => _gateway.GetTreeAsync(Address, DevTree, false, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Resolve_OnlyDevFile_ShouldTakeFourCallsAtMost()
  {
    SetTree(rootFile: null, githubFile: null, devFile: "codecov.yaml");
    A.CallTo(() => _gateway.GetContentAsync(Address, "dev/codecov.yaml", Sha, A<CancellationToken>._)).Returns("dev");

    (await ResolveAsync(Repository())).Config.StrictYamlBranch.ShouldBe("dev");

    Fake.GetCalls(_gateway).Count().ShouldBe(4);
  }

  [Fact]
  public async Task Resolve_RootNameOrder_ShouldFollowCodecov()
  {
    A.CallTo(() => _gateway.GetTreeAsync(Address, Sha, false, A<CancellationToken>._))
     .Returns(new ForgeTree(Sha, [new ForgeTreeEntry("codecov.yaml", GithubTree, false), new ForgeTreeEntry(".codecov.yml", GithubTree, false)], false));
    A.CallTo(() => _gateway.GetContentAsync(Address, ".codecov.yml", Sha, A<CancellationToken>._)).Returns("hidden-yml");

    (await ResolveAsync(Repository())).Config.StrictYamlBranch.ShouldBe("hidden-yml");
  }

  [Fact]
  public async Task Resolve_InvalidYaml_ShouldFallBackAndKeepError()
  {
    SetTree(rootFile: "codecov.yml", githubFile: null, devFile: null);
    A.CallTo(() => _gateway.GetContentAsync(Address, "codecov.yml", Sha, A<CancellationToken>._)).Returns(Invalid);

    var resolved = await ResolveAsync(Repository("default"));

    resolved.Source.ShouldBe(ConfigSource.RepositoryDefault);
    resolved.Config.StrictYamlBranch.ShouldBe("default");
    resolved.Errors.ShouldHaveSingleItem().ShouldContain("codecov.yml");
    _cache[Sha].Path.ShouldBeNull();
    _cache[Sha].RawYaml.ShouldBe("default");
  }

  [Fact]
  public async Task Resolve_InvalidDefaultYaml_ShouldFallBackOnTheOrganization()
  {
    _organizationYaml = "shared:main";

    var resolved = await ResolveAsync(Repository(Invalid));

    resolved.Source.ShouldBe(ConfigSource.Organization);
    resolved.Config.DefaultBranch.ShouldBe("main");
    resolved.Errors.ShouldHaveSingleItem();
  }

  [Fact]
  public async Task Resolve_NothingFound_ShouldUseTheDefaults()
  {
    var resolved = await ResolveAsync(Repository());

    resolved.Source.ShouldBe(ConfigSource.Builtin);
    resolved.Errors.ShouldBeEmpty();
  }

  [Fact]
  public async Task Resolve_Twice_ShouldHitCache()
  {
    SetTree(rootFile: "codecov.yml", githubFile: null, devFile: null);
    A.CallTo(() => _gateway.GetContentAsync(Address, "codecov.yml", Sha, A<CancellationToken>._)).Returns("root");

    await ResolveAsync(Repository());
    var calls = Fake.GetCalls(_gateway).Count();

    (await ResolveAsync(Repository())).Config.StrictYamlBranch.ShouldBe("root");

    Fake.GetCalls(_gateway).Count().ShouldBe(calls);
    A.CallTo(() => _configs.AddAsync(A<CommitConfig>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Resolve_ShouldMergeOrganizationYaml()
  {
    SetTree(rootFile: "codecov.yml", githubFile: null, devFile: null);
    A.CallTo(() => _gateway.GetContentAsync(Address, "codecov.yml", Sha, A<CancellationToken>._)).Returns("root");
    _organizationYaml = "shared:main";

    var resolved = await ResolveAsync(Repository());

    resolved.Config.StrictYamlBranch.ShouldBe("root");
    resolved.Config.DefaultBranch.ShouldBe("main");
  }

  [Fact]
  public async Task Resolve_RepositoryYamlOverOrganization_ShouldWin()
  {
    SetTree(rootFile: "codecov.yml", githubFile: null, devFile: null);
    A.CallTo(() => _gateway.GetContentAsync(Address, "codecov.yml", Sha, A<CancellationToken>._)).Returns("shared:repo");
    _organizationYaml = "shared:org";

    (await ResolveAsync(Repository())).Config.DefaultBranch.ShouldBe("repo");
  }

  // The cached row holds no text of the organization: another scope resolves with its own settings.
  [Fact]
  public async Task Resolve_ShouldNotUseOtherOrganizationYaml()
  {
    _organizationYaml = "shared:first-org";
    await ResolveAsync(Repository());

    _cache[Sha].RawYaml.ShouldBeNull();

    _organizationYaml = "shared:second-org";

    (await ResolveAsync(Repository())).Config.DefaultBranch.ShouldBe("second-org");
  }

  [Fact]
  public async Task Resolve_InvalidOrganizationYaml_ShouldKeepTheError()
  {
    _organizationYaml = Invalid;

    var resolved = await ResolveAsync(Repository());

    resolved.Source.ShouldBe(ConfigSource.Builtin);
    resolved.Errors.ShouldHaveSingleItem();
  }

  private static ConfigParseResult Parse(string yaml)
  {
    if (yaml == Invalid)
    {
      return new ConfigParseResult(null, "broken", []);
    }

    var isShared = yaml.StartsWith("shared:", StringComparison.Ordinal);
    var key = isShared ? "branch" : "strict_yaml_branch";
    var value = isShared ? yaml["shared:".Length..] : yaml;

    return new ConfigParseResult(ConfigNode.FromMap(("codecov", ConfigNode.FromMap((key, ConfigNode.FromText(value))))), null, []);
  }

  private static TrackedRepository Repository(string? defaultBranchYaml = null)
  {
    var repository = TrackedRepository.Create(101, "1", RepositorySlug.Parse("acme/repo"), "main", false, "protected-token", Now);

    // Assigned when the row is saved, and written by the push handling that comes later (M2.04).
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.Id))!.SetValue(repository, 5L);
    typeof(TrackedRepository).GetProperty(nameof(TrackedRepository.DefaultBranchYaml))!.SetValue(repository, defaultBranchYaml);

    return repository;
  }

  private Task<ResolvedConfig> ResolveAsync(TrackedRepository repository) => _resolver.ResolveAsync(repository, Address, Sha, Ct);

  private void SetTree(string? rootFile, string? githubFile, string? devFile)
  {
    List<ForgeTreeEntry> root = [];

    if (rootFile is not null)
    {
      root.Add(new ForgeTreeEntry(rootFile, GithubTree, false));
    }

    root.Add(new ForgeTreeEntry(".github", GithubTree, true));
    root.Add(new ForgeTreeEntry("dev", DevTree, true));

    A.CallTo(() => _gateway.GetTreeAsync(Address, Sha, false, A<CancellationToken>._)).Returns(new ForgeTree(Sha, root, false));
    A.CallTo(() => _gateway.GetTreeAsync(Address, GithubTree, false, A<CancellationToken>._)).Returns(Subtree(GithubTree, githubFile));
    A.CallTo(() => _gateway.GetTreeAsync(Address, DevTree, false, A<CancellationToken>._)).Returns(Subtree(DevTree, devFile));
  }

  private static ForgeTree Subtree(GitSha sha, string? file) => new(sha, file is null ? [] : [new ForgeTreeEntry(file, sha, false)], false);

  #endregion
}

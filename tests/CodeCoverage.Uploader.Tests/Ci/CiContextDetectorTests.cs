// ------------------------------------------------------------------------------------------------
// <copyright file="CiContextDetectorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Ci;

namespace CodeCoverage.Uploader.Tests.Ci;

public sealed class CiContextDetectorTests
{
  #region Fields

  private const string MergeSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
  private const string HeadSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
  private const string BranchSha = "dddddddddddddddddddddddddddddddddddddddd";
  private const string QueueSha = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
  private const string OtherSha = "ffffffffffffffffffffffffffffffffffffffff";
  private const string Slug = "octo-org/octo-repo";

  private static readonly Func<string[], string?> NoGit =
    _ => throw new InvalidOperationException("git must not run on a known CI service.");

  #endregion

  #region Properties

  public static TheoryData<string> MalformedPayloads =>
    new()
    {
      { "not json" },
      { string.Empty },
      { "[]" },
      { "null" },
      { "{}" },
      { """{"pull_request":null}""" },
      { """{"pull_request":{"number":12}}""" },
      { """{"pull_request":{"number":12,"head":"main"}}""" },
      { """{"pull_request":{"number":12,"head":{"ref":"feature/login"}}}""" },
      { """{"pull_request":{"number":12,"head":{"sha":12}}}""" },
      { """{"pull_request":{"number":12,"head":{"sha":"abc1234"}}}""" },
      { $$"""{ "pull_request": { "number": 12, "head": { "sha": "{{MergeSha}}0" } } }""" },
    };

  public static TheoryData<string, string> RemoteAddresses =>
    new()
    {
      { "https://github.com/octo-org/octo-repo.git", Slug },
      { "https://github.com/octo-org/octo-repo", Slug },
      { "https://github.com/octo-org/octo-repo/", Slug },
      { "https://x-access-token:s3cret@github.com/octo-org/octo-repo.git", Slug },
      { "https://github.com:443/octo-org/octo-repo.git", Slug },
      { "git@github.com:octo-org/octo-repo.git", Slug },
      { "git@github.com:octo-org/octo-repo", Slug },
      { "ssh://git@github.com/octo-org/octo-repo.git", Slug },
      { "ssh://git@github.com:22/octo-org/octo-repo.git", Slug },
      { "ssh://git@github.com:octo-org/octo-repo.git", Slug },
    };

  public static TheoryData<string> UnusableRemoteAddresses =>
    new()
    {
      { @"C:\sources\octo-repo" },
      { "/srv/git/octo-org/octo-repo.git" },
      { "../octo-org/octo-repo.git" },
      { "file:///srv/git/octo-repo.git" },
      { "https://dev.azure.com/fabrikam/Octo/_git/octo-repo" },
      { "git@ssh.dev.azure.com:v3/fabrikam/Octo/octo-repo" },
    };

  #endregion

  #region Methods

  [Fact]
  public void Detect_GhaPullRequest_ShouldUseHeadSha()
  {
    var context = DetectFixture("gha-pull_request.json");

    context.Sha.ShouldBe(HeadSha);
    context.Sha.ShouldNotBe(MergeSha);
    context.Service.ShouldBe("github-actions");
    context.Slug.ShouldBe(Slug);
    context.Branch.ShouldBe("feature/login");
    context.PullRequest.ShouldBe(12);
    context.BuildUrl.ShouldBe("https://github.com/octo-org/octo-repo/actions/runs/1658821493/attempts/2");
    context.BuildId.ShouldBe("1658821493");
    context.JobId.ShouldBe("backend");
    context.Root.ShouldBe("/home/runner/work/octo-repo/octo-repo");
    context.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Detect_GhaPullRequestTarget_ShouldUseHeadSha()
  {
    var fixture = CiEnvironmentFixture.Load("gha-pull_request.json")
                         .With("GITHUB_EVENT_NAME", "pull_request_target")
                         .With("GITHUB_SHA", OtherSha)
                         .With("GITHUB_REF", "refs/heads/main")
                         .With("GITHUB_REF_NAME", "main");

    var context = Detect(fixture);

    context.Sha.ShouldBe(HeadSha);
    context.Branch.ShouldBe("feature/login");
    context.PullRequest.ShouldBe(12);
    context.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Detect_GhaForkPullRequest_ShouldKeepTheBaseSlugAndUseTheForkHead()
  {
    var fixture = CiEnvironmentFixture.Load("gha-pull_request.json")
                         .With("GITHUB_HEAD_REF", "main")
                         .WithEvent(
                           $$"""
                             {
                               "pull_request": {
                                 "number": 12,
                                 "head": {
                                   "label": "contributor:main",
                                   "ref": "main",
                                   "sha": "{{OtherSha}}",
                                   "repo": { "full_name": "contributor/octo-repo" }
                                 }
                               }
                             }
                             """);

    var context = Detect(fixture);

    context.Sha.ShouldBe(OtherSha);
    context.Slug.ShouldBe(Slug);
    context.Branch.ShouldBe("main");
    context.PullRequest.ShouldBe(12);
    context.Warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("pull_request_review")]
  [InlineData("pull_request_review_comment")]
  public void Detect_GhaReviewEvent_ShouldReadTheHeadFromThePayload(string eventName)
  {
    var fixture = CiEnvironmentFixture.Load("gha-pull_request.json")
                         .With("GITHUB_EVENT_NAME", eventName)
                         .With("GITHUB_HEAD_REF", string.Empty);

    var context = Detect(fixture);

    context.Sha.ShouldBe(HeadSha);
    context.Branch.ShouldBe("feature/login");
    context.PullRequest.ShouldBe(12);
  }

  [Fact]
  public void Detect_GhaPullRequest_MissingEventPath_ShouldWarnAndNeverUseGithubSha()
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-pull_request.json").With("GITHUB_EVENT_PATH", string.Empty));

    context.Sha.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain("GITHUB_EVENT_PATH");
    context.Warnings[0].ShouldContain("--sha");
    context.Warnings[0].ShouldNotContain(MergeSha);
  }

  [Theory]
  [MemberData(nameof(MalformedPayloads))]
  public void Detect_GhaPullRequest_MalformedPayload_ShouldWarnAndNeverUseGithubSha(string payload)
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-pull_request.json").WithEvent(payload));

    context.Sha.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain("head of the pull request");
  }

  [Theory]
  [InlineData(typeof(FileNotFoundException))]
  [InlineData(typeof(DirectoryNotFoundException))]
  [InlineData(typeof(UnauthorizedAccessException))]
  [InlineData(typeof(ArgumentException))]
  [InlineData(typeof(NotSupportedException))]
  public void Detect_GhaPullRequest_UnreadablePayload_ShouldWarnAndNeverUseGithubSha(Type exceptionType)
  {
    var fixture = CiEnvironmentFixture.Load("gha-pull_request.json");

    var context = CiContextDetector.Detect(
      Settings(),
      fixture.Environment,
      _ => throw (Exception)Activator.CreateInstance(exceptionType)!,
      NoGit);

    context.Sha.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain(exceptionType.Name);
  }

  [Fact]
  public void Detect_GhaPullRequest_UnreadablePayload_ShouldKeepWhatTheEnvironmentGives()
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-pull_request.json").WithEvent("not json"));

    context.PullRequest.ShouldBe(12);
    context.Branch.ShouldBe("feature/login");
    context.Slug.ShouldBe(Slug);
  }

  [Fact]
  public void Detect_GhaPullRequest_UnreadablePayload_WithSha_ShouldNotWarn()
  {
    var context = Detect(
      CiEnvironmentFixture.Load("gha-pull_request.json").WithEvent("not json"),
      Settings(sha: OtherSha));

    context.Sha.ShouldBe(OtherSha);
    context.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Detect_GhaPullRequest_UpperCaseHeadSha_ShouldBeLowered()
  {
    var fixture = CiEnvironmentFixture.Load("gha-pull_request.json")
                         .WithEvent($$"""{ "pull_request": { "number": 12, "head": { "sha": "{{HeadSha.ToUpperInvariant()}}" } } }""");

    Detect(fixture).Sha.ShouldBe(HeadSha);
  }

  [Fact]
  public void Detect_GhaPush_ShouldUseGithubShaAndRefName()
  {
    var context = DetectFixture("gha-push.json");

    context.Sha.ShouldBe(BranchSha);
    context.Branch.ShouldBe("release/1.0");
    context.PullRequest.ShouldBeNull();
    context.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Detect_GhaTag_ShouldHaveNoBranch()
  {
    var fixture = CiEnvironmentFixture.Load("gha-push.json")
                         .With("GITHUB_REF", "refs/tags/v1.0")
                         .With("GITHUB_REF_NAME", "v1.0");

    var context = Detect(fixture);

    context.Sha.ShouldBe(BranchSha);
    context.Branch.ShouldBeNull();
  }

  [Theory]
  [InlineData("")]
  [InlineData("abc1234")]
  public void Detect_GhaPush_MalformedGithubSha_ShouldWarn(string value)
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-push.json").With("GITHUB_SHA", value));

    context.Sha.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain("GITHUB_SHA");
  }

  [Fact]
  public void Detect_GhaWithoutRunAttempt_ShouldLeaveItOutOfTheBuildUrl()
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-push.json").With("GITHUB_RUN_ATTEMPT", string.Empty));

    context.BuildUrl.ShouldBe("https://github.com/octo-org/octo-repo/actions/runs/1658821494");
  }

  [Fact]
  public void Detect_GhaWithoutRepository_ShouldWarnAboutTheSlug()
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-push.json").With("GITHUB_REPOSITORY", string.Empty));

    context.Slug.ShouldBeNull();
    context.BuildUrl.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain("--slug");
  }

  [Fact]
  public void Detect_MergeGroup_ShouldExtractBranch()
  {
    var context = DetectFixture("gha-merge_group.json");

    context.Sha.ShouldBe(QueueSha);
    context.Branch.ShouldBe("release/1.0");
    context.PullRequest.ShouldBeNull();
    context.Warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("gh-readonly-queue/main/pr-12-cccccccccccccccccccccccccccccccccccccccc", "main")]
  [InlineData("gh-readonly-queue/main", "main")]
  public void Detect_MergeQueueBranch_ShouldBeTheTargetBranch(string refName, string expected)
  {
    var fixture = CiEnvironmentFixture.Load("gha-merge_group.json")
                         .With("GITHUB_REF", $"refs/heads/{refName}")
                         .With("GITHUB_REF_NAME", refName);

    Detect(fixture).Branch.ShouldBe(expected);
  }

  [Fact]
  public void Detect_AzurePr_ShouldUseSourceCommitId()
  {
    var context = DetectFixture("azure-pr.json");

    context.Sha.ShouldBe(HeadSha);
    context.Sha.ShouldNotBe(MergeSha);
    context.Service.ShouldBe("azure-pipelines");
    context.Slug.ShouldBe(Slug);
    context.Branch.ShouldBe("feature/login");
    context.PullRequest.ShouldBe(12);
    context.BuildUrl.ShouldBe("https://dev.azure.com/fabrikam/Octo%20Project/_build/results?buildId=4321");
    context.BuildId.ShouldBe("4321");
    context.JobId.ShouldBe("12f1170f-54f2-53f3-20dd-22fc7dff55f9");
    context.Root.ShouldBe("/home/vsts/work/1/s");
    context.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Detect_AzureCi_ShouldUseSourceVersion()
  {
    var context = DetectFixture("azure-ci.json");

    context.Sha.ShouldBe(BranchSha);
    context.Branch.ShouldBe("release/1.0");
    context.PullRequest.ShouldBeNull();
    context.Warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("")]
  [InlineData("abc1234")]
  public void Detect_AzurePr_WithoutValidSourceCommitId_ShouldWarnAndNeverUseSourceVersion(string value)
  {
    var context = Detect(CiEnvironmentFixture.Load("azure-pr.json").With("SYSTEM_PULLREQUEST_SOURCECOMMITID", value));

    context.Sha.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain("BUILD_SOURCEVERSION");
  }

  [Theory]
  [InlineData("PullRequest", "", "")]
  [InlineData("Manual", "987654321", "")]
  [InlineData("Manual", "", "12")]
  public void Detect_AzurePr_AnyPullRequestMarker_ShouldNeverUseSourceVersion(string reason, string id, string number)
  {
    var fixture = CiEnvironmentFixture.Load("azure-pr.json")
                         .With("BUILD_REASON", reason)
                         .With("SYSTEM_PULLREQUEST_PULLREQUESTID", id)
                         .With("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER", number)
                         .With("SYSTEM_PULLREQUEST_SOURCECOMMITID", string.Empty);

    Detect(fixture).Sha.ShouldBeNull();
  }

  [Fact]
  public void Detect_AzurePr_WithoutNumber_ShouldUseTheIdentifier()
  {
    var context = Detect(CiEnvironmentFixture.Load("azure-pr.json").With("SYSTEM_PULLREQUEST_PULLREQUESTNUMBER", string.Empty));

    context.PullRequest.ShouldBe(987654321);
  }

  [Fact]
  public void Detect_AzureReposPr_ShouldStripTheBranchRef()
  {
    var context = Detect(
      CiEnvironmentFixture.Load("azure-pr.json").With("SYSTEM_PULLREQUEST_SOURCEBRANCH", "refs/heads/users/raisa/new-feature"));

    context.Branch.ShouldBe("users/raisa/new-feature");
  }

  [Theory]
  [InlineData("refs/tags/v1.0")]
  [InlineData("refs/pull/12/merge")]
  [InlineData("$/teamproject/main")]
  public void Detect_AzureNonBranchRef_ShouldHaveNoBranch(string reference)
  {
    var context = Detect(CiEnvironmentFixture.Load("azure-ci.json").With("BUILD_SOURCEBRANCH", reference));

    context.Branch.ShouldBeNull();
  }

  [Theory]
  [InlineData("TfsGit")]
  [InlineData("Git")]
  [InlineData("")]
  public void Detect_AzureNonGitHubProvider_ShouldRequireSlug(string provider)
  {
    var fixture = CiEnvironmentFixture.Load("azure-ci.json")
                         .With("BUILD_REPOSITORY_PROVIDER", provider)
                         .With("BUILD_REPOSITORY_NAME", "octo-repo");

    var withoutOption = Detect(fixture);
    var withOption = Detect(fixture, Settings(slug: "fabrikam/octo-repo"));

    withoutOption.Slug.ShouldBeNull();
    withoutOption.Warnings.ShouldHaveSingleItem().ShouldContain("--slug");
    withOption.Slug.ShouldBe("fabrikam/octo-repo");
    withOption.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Detect_Local_ShouldAskGit()
  {
    var context = DetectLocal(sha: HeadSha, branch: "feature/login", remote: "git@github.com:octo-org/octo-repo.git");

    context.Service.ShouldBe("local");
    context.Sha.ShouldBe(HeadSha);
    context.Branch.ShouldBe("feature/login");
    context.Slug.ShouldBe(Slug);
    context.PullRequest.ShouldBeNull();
    context.Warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("true")]
  [InlineData("1")]
  public void Detect_UnknownCiService_ShouldBeOther(string value)
  {
    var context = DetectLocal(
      sha: HeadSha,
      branch: "main",
      remote: "git@github.com:octo-org/octo-repo.git",
      ("CI", value));

    context.Service.ShouldBe("other");
  }

  [Fact]
  public void Detect_LocalDetachedHead_ShouldHaveNoBranchAndNoWarning()
  {
    var context = DetectLocal(sha: HeadSha, branch: string.Empty, remote: "git@github.com:octo-org/octo-repo.git");

    context.Branch.ShouldBeNull();
    context.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Detect_LocalWithoutGit_ShouldWarnWithoutFailing()
  {
    var context = DetectLocal(sha: null, branch: null, remote: null);

    context.Sha.ShouldBeNull();
    context.Slug.ShouldBeNull();
    context.Branch.ShouldBeNull();
    context.Warnings.Count.ShouldBe(2);
    context.Warnings.ShouldContain(warning => warning.Contains("--sha", StringComparison.Ordinal));
    context.Warnings.ShouldContain(warning => warning.Contains("--slug", StringComparison.Ordinal));
  }

  [Theory]
  [MemberData(nameof(RemoteAddresses))]
  public void Detect_LocalRemote_ShouldGiveTheSlug(string remote, string expected)
  {
    DetectLocal(sha: HeadSha, branch: "main", remote: remote).Slug.ShouldBe(expected);
  }

  [Theory]
  [MemberData(nameof(UnusableRemoteAddresses))]
  public void Detect_LocalRemoteWithoutSlug_ShouldWarn(string remote)
  {
    var context = DetectLocal(sha: HeadSha, branch: "main", remote: remote);

    context.Slug.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain("--slug");
  }

  [Fact]
  public void Detect_LocalRemoteWithCredential_ShouldNotPrintIt()
  {
    var context = DetectLocal(sha: null, branch: "main", remote: "https://user:s3cret@host/not-a-slug");

    string.Join('\n', context.Warnings).ShouldNotContain("s3cret");
  }

  [Fact]
  public void Detect_Options_ShouldWinOverTheDetection()
  {
    var settings = Settings(
      sha: OtherSha.ToUpperInvariant(),
      slug: "other/repo",
      branch: "refs/heads/other",
      pullRequest: 99,
      root: "/other");

    var context = Detect(CiEnvironmentFixture.Load("gha-pull_request.json"), settings);

    context.Sha.ShouldBe(OtherSha);
    context.Slug.ShouldBe("other/repo");
    context.Branch.ShouldBe("other");
    context.PullRequest.ShouldBe(99);
    context.Root.ShouldBe("/other");
    context.Warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("abc1234")]
  [InlineData("not-a-sha")]
  public void Detect_MalformedShaOption_ShouldWarnAndNotUseTheDetectedCommit(string sha)
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-pull_request.json"), Settings(sha: sha));

    context.Sha.ShouldBeNull();
    context.Warnings.ShouldHaveSingleItem().ShouldContain("--sha");
  }

  [Theory]
  [InlineData("TRUE")]
  [InlineData(" true ")]
  public void Detect_ServiceVariable_ShouldIgnoreCaseAndSpaces(string value)
  {
    var context = Detect(CiEnvironmentFixture.Load("gha-push.json").With("GITHUB_ACTIONS", value));

    context.Service.ShouldBe("github-actions");
  }

  [Fact]
  public void RunGit_Version_ShouldReturnTheOutput()
  {
    LocalContext.RunGit(["--version"], workingDirectory: null).ShouldNotBeNull().ShouldStartWith("git version");
  }

  [Fact]
  public void RunGit_Failure_ShouldReturnNull()
  {
    LocalContext.RunGit(["not-a-git-command"], workingDirectory: null).ShouldBeNull();
  }

  [Fact]
  public void RunGit_MissingDirectory_ShouldReturnNull()
  {
    var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    LocalContext.RunGit(["--version"], missing).ShouldBeNull();
  }

  private static DetectedContext DetectFixture(string name)
  {
    return Detect(CiEnvironmentFixture.Load(name));
  }

  private static DetectedContext Detect(CiEnvironmentFixture fixture, UploaderSettings? settings = null)
  {
    return CiContextDetector.Detect(settings ?? Settings(), fixture.Environment, fixture.ReadFile, NoGit);
  }

  private static DetectedContext DetectLocal(
    string? sha,
    string? branch,
    string? remote,
    params (string Name, string Value)[] variables)
  {
    var values = variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);

    return CiContextDetector.Detect(
      Settings(),
      name => values.GetValueOrDefault(name),
      _ => throw new InvalidOperationException("No file is read locally."),
      arguments => arguments[0] switch
      {
        "rev-parse" => sha,
        "branch" => branch,
        "remote" => remote,
        _ => throw new InvalidOperationException($"Unexpected git command: {arguments[0]}."),
      });
  }

  private static UploaderSettings Settings(
    string? sha = null,
    string? slug = null,
    string? branch = null,
    int? pullRequest = null,
    string? root = null)
  {
    return new UploaderSettings
    {
      Url = new Uri("https://coverage.example.com"),
      Sha = sha,
      Slug = slug,
      Branch = branch,
      PullRequest = pullRequest,
      Root = root,
    };
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCommandFactoryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Commands;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Commands;

public sealed class UploadCommandFactoryTests
{
  #region Fields

  private const string Url = "https://coverage.example.com/";
  private const string Secret = "cvc_secret_value";

  private readonly StringWriter _output = new();
  private UploaderSettings? _captured;

  #endregion

  #region Properties

  public static TheoryData<string[]> InvalidCommandLines =>
    new()
    {
      { Array.Empty<string>() },
      { new[] { "--url", "not-an-address" } },
      { new[] { "--url", "ftp://coverage.example.com" } },
      { new[] { "--url", Url, "--unknown" } },
      { new[] { "--url", Url, "--pr", "abc" } },
    };

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Parse_EnvironmentFallbacks_ShouldApply()
  {
    var environment = EnvironmentOf(
      ("COVERAGE_URL", Url),
      ("COVERAGE_TOKEN", Secret),
      ("COVERAGE_FILE", "a/lcov.info; b/**/*.xml"),
      ("COVERAGE_FLAGS", "backend, net10"),
      ("COVERAGE_NAME", "session"),
      ("COVERAGE_SLUG", "owner/repo"),
      ("COVERAGE_SHA", "abc"),
      ("COVERAGE_BRANCH", "main"),
      ("COVERAGE_PR", "12"),
      ("COVERAGE_PARENT_SHA", "def"),
      ("COVERAGE_ROOT", "/src"),
      ("COVERAGE_DISABLE_SEARCH", "true"),
      ("COVERAGE_NO_NETWORK", "1"),
      ("COVERAGE_WAIT", "TRUE"),
      ("COVERAGE_FINALIZE", "true"),
      ("COVERAGE_DRY_RUN", "true"),
      ("COVERAGE_VERBOSE", "true"));

    await RunAsync([], environment);

    _captured.ShouldNotBeNull();
    _captured.Url.ShouldBe(new Uri(Url));
    _captured.Token.ShouldBe(Secret);
    _captured.Files.ShouldBe(["a/lcov.info", "b/**/*.xml"]);
    _captured.Flags.ShouldBe(["backend", "net10"]);
    _captured.Name.ShouldBe("session");
    _captured.Slug.ShouldBe("owner/repo");
    _captured.Sha.ShouldBe("abc");
    _captured.Branch.ShouldBe("main");
    _captured.PullRequest.ShouldBe(12);
    _captured.ParentSha.ShouldBe("def");
    _captured.Root.ShouldBe("/src");
    _captured.DisableSearch.ShouldBeTrue();
    _captured.NoNetwork.ShouldBeTrue();
    _captured.Wait.ShouldBeTrue();
    _captured.Finalize.ShouldBeTrue();
    _captured.DryRun.ShouldBeTrue();
    _captured.FailOnError.ShouldBeFalse();
    _captured.Verbose.ShouldBeTrue();
  }

  [Fact]
  public async Task Parse_CommandLine_ShouldWinOverEnvironment()
  {
    var environment = EnvironmentOf(
      ("COVERAGE_URL", "https://other.example.com"),
      ("COVERAGE_TOKEN", "from-environment"),
      ("COVERAGE_FILE", "other.xml"),
      ("COVERAGE_PR", "12"),
      ("COVERAGE_WAIT", "true"));

    await RunAsync(
      ["--url", Url, "--token", Secret, "-f", "a.xml", "--file", "b.xml", "--pr", "7", "--wait=false"],
      environment);

    _captured.ShouldNotBeNull();
    _captured.Url.ShouldBe(new Uri(Url));
    _captured.Token.ShouldBe(Secret);
    _captured.Files.ShouldBe(["a.xml", "b.xml"]);
    _captured.PullRequest.ShouldBe(7);
    _captured.Wait.ShouldBeFalse();
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task Parse_BlankCoverageToken_ShouldFallBackOnCodecovToken(string blank)
  {
    await RunAsync(
      ["--url", Url],
      EnvironmentOf(("COVERAGE_TOKEN", blank), ("CODECOV_TOKEN", Secret)));

    _captured.ShouldNotBeNull();
    _captured.Token.ShouldBe(Secret);
  }

  [Fact]
  public async Task Parse_ValueStartingWithAt_ShouldNotBeReadAsResponseFile()
  {
    await RunAsync(["--url", Url, "--name", "@session"], EnvironmentOf());

    _captured.ShouldNotBeNull();
    _captured.Name.ShouldBe("@session");
  }

  [Fact]
  public async Task Parse_MalformedPullRequestVariable_ShouldBeIgnored()
  {
    var exitCode = await RunAsync(
      ["--url", Url, "--fail-on-error"],
      EnvironmentOf(("COVERAGE_PR", "abc")));

    exitCode.ShouldBe(ExitCodePolicy.Success);
    _captured.ShouldNotBeNull();
    _captured.PullRequest.ShouldBeNull();
  }

  [Theory]
  [MemberData(nameof(InvalidCommandLines))]
  public async Task Run_AnyError_WithoutFailOnError_ShouldExitZero(string[] args)
  {
    var exitCode = await RunAsync(args, EnvironmentOf());

    exitCode.ShouldBe(ExitCodePolicy.Success);
    _output.ToString().ShouldContain("error: ");
  }

  [Theory]
  [MemberData(nameof(InvalidCommandLines))]
  public async Task Run_AnyError_WithFailOnError_ShouldExitOne(string[] args)
  {
    var exitCode = await RunAsync([.. args, "--fail-on-error"], EnvironmentOf());

    exitCode.ShouldBe(ExitCodePolicy.Failure);
    _output.ToString().ShouldContain("error: ");
  }

  [Theory]
  [InlineData(false, ExitCodePolicy.Success)]
  [InlineData(true, ExitCodePolicy.Failure)]
  public async Task Run_UploadThrows_ShouldFollowThePolicy(bool failOnError, int expected)
  {
    var exitCode = await UploadCommandFactory.RunAsync(
      ["--url", Url, .. failOnError ? new[] { "--fail-on-error" } : []],
      EnvironmentOf(),
      new ConsoleReporter(_output),
      (_, _) => throw new InvalidOperationException("boom"),
      Ct);

    exitCode.ShouldBe(expected);
    _output.ToString().ShouldContain("error: boom");
  }

  [Theory]
  [InlineData(false, ExitCodePolicy.Success)]
  [InlineData(true, ExitCodePolicy.Failure)]
  public async Task Run_UploadFails_ShouldFollowThePolicy(bool failOnError, int expected)
  {
    var exitCode = await RunAsync(
      ["--url", Url, .. failOnError ? new[] { "--fail-on-error" } : []],
      EnvironmentOf(),
      succeeds: false);

    exitCode.ShouldBe(expected);
  }

  [Fact]
  public async Task Run_UploadSucceeds_WithFailOnError_ShouldExitZero()
  {
    var exitCode = await RunAsync(["--url", Url, "--fail-on-error"], EnvironmentOf());

    exitCode.ShouldBe(ExitCodePolicy.Success);
  }

  [Theory]
  [InlineData("true")]
  [InlineData("1")]
  public async Task Run_FailOnErrorFromEnvironment_ShouldExitOne(string value)
  {
    var exitCode = await RunAsync([], EnvironmentOf(("COVERAGE_FAIL_ON_ERROR", value)));

    exitCode.ShouldBe(ExitCodePolicy.Failure);
  }

  [Fact]
  public async Task Run_FailOnErrorVariable_WithParseError_ShouldExitOne()
  {
    var exitCode = await RunAsync(
      ["--url", Url, "--unknown"],
      EnvironmentOf(("COVERAGE_FAIL_ON_ERROR", "true")));

    exitCode.ShouldBe(ExitCodePolicy.Failure);
  }

  [Fact]
  public async Task Run_FailOnErrorTurnedOffOnCommandLine_ShouldWinOverEnvironment()
  {
    var exitCode = await RunAsync(
      ["--fail-on-error=false"],
      EnvironmentOf(("COVERAGE_FAIL_ON_ERROR", "true")));

    exitCode.ShouldBe(ExitCodePolicy.Success);
  }

  [Fact]
  public async Task Run_MalformedFailOnErrorValue_ShouldExitOne()
  {
    var exitCode = await RunAsync(["--url", Url, "--fail-on-error=maybe"], EnvironmentOf());

    exitCode.ShouldBe(ExitCodePolicy.Failure);
    _output.ToString().ShouldContain("error: ");
  }

  [Fact]
  public async Task Run_Help_ShouldListEveryOptionWithoutPrintingTheToken()
  {
    var environment = EnvironmentOf(
      ("COVERAGE_TOKEN", Secret),
      ("CODECOV_TOKEN", Secret));

    var exitCode = await RunAsync(["--help"], environment);

    exitCode.ShouldBe(ExitCodePolicy.Success);
    _captured.ShouldBeNull();

    var help = _output.ToString();

    help.ShouldNotContain(Secret);

    foreach (var option in new[]
             {
               "--url", "--token", "-f", "--file", "--flags", "--name", "--slug", "--sha", "--branch",
               "--pr", "--parent-sha", "--root", "--disable-search", "--no-network", "--wait",
               "--finalize", "--dry-run", "--fail-on-error", "--verbose",
             })
    {
      help.ShouldContain(option);
    }
  }

  [Fact]
  public async Task Run_Failure_ShouldNotPrintTheToken()
  {
    await RunAsync(["--token", Secret, "--unknown"], EnvironmentOf());

    _output.ToString().ShouldNotContain(Secret);
  }

  private static Func<string, string?> EnvironmentOf(params (string Name, string Value)[] variables)
  {
    var values = variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);

    return name => values.GetValueOrDefault(name);
  }

  private Task<int> RunAsync(
    string[] args,
    Func<string, string?> environment,
    bool succeeds = true)
  {
    return UploadCommandFactory.RunAsync(
      args,
      environment,
      new ConsoleReporter(_output),
      (settings, _) =>
      {
        _captured = settings;
        return Task.FromResult(succeeds);
      },
      Ct);
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="DryRunPrinterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Uploader.Commands;
using CodeCoverage.Uploader.Http;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Commands;

public sealed class DryRunPrinterTests
{
  #region Fields

  private const string Token = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private readonly StringWriter _output = new();

  #endregion

  #region Methods

  [Fact]
  public void Print_ShouldListWhatTheUploadWouldSend()
  {
    Print(Settings(Token), Request(), hasOidc: false);

    var output = _output.ToString();

    output.ShouldContain("Dry run: nothing is sent to https://coverage.example.com/.");
    output.ShouldContain("  slug: octo-org/app");
    output.ShouldContain($"  sha: {new string('a', 40)}");
    output.ShouldContain("  branch: feature/x");
    output.ShouldContain("  pull request: 12");
    output.ShouldContain($"  parent sha: {new string('b', 40)}");
    output.ShouldContain("  flags: back,front");
    output.ShouldContain("  name: backend-linux");
    output.ShouldContain("  ci service: github-actions");
    output.ShouldContain("  build id: 99");
    output.ShouldContain("  job id: test");
    output.ShouldContain("  build url: https://example.test/build");
    output.ShouldContain("  root: /work");
    output.ShouldContain("  file list: 2 paths of git ls-files");
    output.ShouldContain("  reports (2), each sent as gzip:");
    output.ShouldContain("    /work/a/coverage.cobertura.xml (1234 bytes)");
    output.ShouldContain("    /work/b/lcov.info");
    output.ShouldNotContain("lcov.info (");
  }

  [Fact]
  public void Print_ShouldNeverPrintTheCredential()
  {
    Print(Settings(Token), Request(), hasOidc: true);

    _output.ToString().ShouldNotContain(Token);
    _output.ToString().ShouldContain("  credential: an upload token (not printed)");
  }

  [Theory]
  [InlineData(true, "  credential: none given: GitHub Actions would be asked for an OIDC token")]
  [InlineData(false, "  credential: none: the server would refuse the upload")]
  public void Print_WithoutToken_ShouldTellWhereTheCredentialWouldComeFrom(bool hasOidc, string expected)
  {
    Print(Settings(null), Request(), hasOidc);

    _output.ToString().ShouldContain(expected);
  }

  [Fact]
  public void Print_AbsentValues_ShouldBeMarked()
  {
    var request = new UploadRequest { Slug = "octo-org/app", Sha = new string('a', 40), Reports = [] };

    Print(Settings(Token), request, hasOidc: false);

    var output = _output.ToString();

    output.ShouldContain("  branch: (none)");
    output.ShouldContain("  pull request: (none)");
    output.ShouldContain("  flags: (none)");
    output.ShouldContain("  file list: not sent");
    output.ShouldContain("  reports (0), each sent as gzip:");
    output.ShouldNotContain("--wait");
  }

  [Fact]
  public void Print_WithWait_ShouldSayTheProcessingWouldBeWaitedFor()
  {
    Print(Settings(Token, wait: true), Request(), hasOidc: false);

    _output.ToString().ShouldContain("--wait: the processing would then be waited for.");
  }

  private static UploaderSettings Settings(string? token, bool wait = false) =>
    new() { Url = new Uri("https://coverage.example.com"), Token = token, DryRun = true, Wait = wait };

  private static UploadRequest Request() =>
    new()
    {
      Slug = "octo-org/app",
      Sha = new string('a', 40),
      Branch = "feature/x",
      PullRequest = 12,
      ParentSha = new string('b', 40),
      Flags = ["back", "front"],
      Name = "backend-linux",
      CiService = "github-actions",
      BuildUrl = "https://example.test/build",
      BuildId = "99",
      JobId = "test",
      Root = "/work",
      Network = ["src/A.cs", "src/B.cs"],
      Reports = ["/work/a/coverage.cobertura.xml", "/work/b/lcov.info"],
    };

  private void Print(UploaderSettings settings, UploadRequest request, bool hasOidc) =>
    DryRunPrinter.Print(new ConsoleReporter(_output), settings, request, hasOidc, static path => path.EndsWith(".xml", StringComparison.Ordinal) ? 1234 : null);

  #endregion
}

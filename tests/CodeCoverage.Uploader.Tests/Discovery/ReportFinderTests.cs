// ------------------------------------------------------------------------------------------------
// <copyright file="ReportFinderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Security.AccessControl;
using System.Security.Principal;

using CodeCoverage.Uploader.Discovery;
using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Tests.Discovery;

public sealed class ReportFinderTests : IDisposable
{
  #region Fields

  private const string NoReport = "warning: No coverage report was found.";

  private readonly string _sandbox =
    Directory.CreateTempSubdirectory("report-finder-").FullName;

  private readonly StringWriter _output = new();

  #endregion

  #region Properties

  private string Output => _output.ToString();

  #endregion

  #region Methods

  [Fact]
  public void Find_ShouldSkipNodeModulesAndObjExceptTestResults()
  {
    Write("src/web/lcov.info");
    Write("tests/App/TestResults/guid/coverage.cobertura.xml");
    Write("tests/App/bin/Debug/net10.0/TestResults/guid/coverage.cobertura.xml");
    Write("tests/App/obj/TestResults/guid/coverage.opencover.xml");
    Write("tests/App/bin/Debug/coverage.cobertura.xml");
    Write("tests/App/obj/coverage.cobertura.xml");
    Write("web/node_modules/pkg/lcov.info");
    Write("web/node_modules/pkg/TestResults/lcov.info");
    Write(".git/lcov.info");
    Write("vendor/lib/clover.xml");
    Write(".venv/lib/jacoco.xml");

    var reports = Find();

    reports.ShouldBe(
    [
      "src/web/lcov.info",
      "tests/App/TestResults/guid/coverage.cobertura.xml",
      "tests/App/bin/Debug/net10.0/TestResults/guid/coverage.cobertura.xml",
      "tests/App/obj/TestResults/guid/coverage.opencover.xml",
    ]);
  }

  [Fact]
  public void Find_WithAgentTempDirectory_ShouldSearchIt()
  {
    Write("repo/lcov.info");
    Write("agent/_temp/guid/coverage.cobertura.xml");
    Write("agent/_temp/obj/coverage.cobertura.xml");

    var reports = Find(root: "repo", agentTemp: Combine("agent/_temp"));

    reports.ShouldBe(["agent/_temp/guid/coverage.cobertura.xml", "repo/lcov.info"]);
  }

  [Fact]
  public void Find_None_ShouldWarn()
  {
    Write("readme.md");

    var reports = Find();

    reports.ShouldBeEmpty();
    Output.ShouldContain(NoReport);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Find_BlankAgentTempDirectory_ShouldSearchOnlyTheRoot(string blank)
  {
    Write("outside/lcov.info");
    Write("repo/lcov.info");

    var reports = Find(root: "repo", agentTemp: blank, workingDirectory: "outside");

    reports.ShouldBe(["repo/lcov.info"]);
    Output.ShouldNotContain("warning");
  }

  [Theory]
  [InlineData("coverage.cobertura.xml")]
  [InlineData("cobertura.xml")]
  [InlineData("web.cobertura.report.xml")]
  [InlineData("coverage.opencover.xml")]
  [InlineData("lcov.info")]
  [InlineData("app.lcov")]
  [InlineData("jacoco.xml")]
  [InlineData("jacocoTestReport.xml")]
  [InlineData("clover.xml")]
  public void Find_KnownName_ShouldBeFound(string name)
  {
    Write($"a/{name}");

    Find().ShouldBe([$"a/{name}"]);
  }

  [Theory]
  [InlineData("Coverage.xml")]
  [InlineData("my-coverage.xml")]
  [InlineData("coverage.xml.bak")]
  [InlineData("coverage.json")]
  [InlineData("clover.lcov.txt")]
  [InlineData("my-clover.xml")]
  [InlineData("JaCoCo.xml")]
  [InlineData("LCOV.INFO")]
  public void Find_OtherName_ShouldBeLeftOut(string name)
  {
    Write($"a/{name}");

    Find().ShouldBeEmpty();
  }

  [Fact]
  public void Find_CoverageXml_ShouldBeFoundByTheDefaultSearch()
  {
    Write("coverage.xml");
    Write("services/api/reports/coverage.xml");

    Find().ShouldBe(["coverage.xml", "services/api/reports/coverage.xml"]);
  }

  [Fact]
  public void Find_CoverageXml_ShouldStayUnderTheExclusions()
  {
    Write("src/coverage.xml");
    Write("tests/App/bin/Debug/net10.0/TestResults/guid/coverage.xml");
    Write("tests/App/obj/TestResults/guid/coverage.xml");
    Write("tests/App/bin/Debug/coverage.xml");
    Write("tests/App/obj/coverage.xml");
    Write("web/node_modules/pkg/coverage.xml");
    Write("web/node_modules/pkg/TestResults/coverage.xml");
    Write(".git/coverage.xml");
    Write("vendor/lib/coverage.xml");
    Write(".venv/lib/coverage.xml");

    Find().ShouldBe(
    [
      "src/coverage.xml",
      "tests/App/bin/Debug/net10.0/TestResults/guid/coverage.xml",
      "tests/App/obj/TestResults/guid/coverage.xml",
    ]);
  }

  [Fact]
  public void Find_ExcludedNamesAboveTheRoot_ShouldNotCount()
  {
    Write("vendor/obj/repo/lcov.info");

    Find(root: "vendor/obj/repo").ShouldBe(["vendor/obj/repo/lcov.info"]);
  }

  [Fact]
  public void Find_HiddenDirectoryAndFile_ShouldBeFound()
  {
    Write(".cache/lcov.info");
    Write("a/.hidden.lcov");

    Find().ShouldBe([".cache/lcov.info", "a/.hidden.lcov"]);
  }

  [Fact]
  public void Find_ExcludedNameOfAnotherCase_ShouldNotBeExcluded()
  {
    Write("Vendor/lcov.info");
    Write("OBJ/lcov.info");

    Find().ShouldBe(["OBJ/lcov.info", "Vendor/lcov.info"]);
  }

  [Fact]
  public void Find_DirectoryNamedLikeAReport_ShouldNotBeReturned()
  {
    Write("lcov.info/readme.md");

    Find().ShouldBeEmpty();
  }

  [Fact]
  public void Find_SameLocationTwice_ShouldReturnTheReportOnce()
  {
    Write("repo/lcov.info");

    Find(root: "repo", agentTemp: Combine("repo")).ShouldBe(["repo/lcov.info"]);
  }

  [Fact]
  public void Find_ShouldOrderThePathsAlikeOnEveryPlatform()
  {
    // In ordinal order, 0 falls after / but before \.
    Write("a/lcov.info");
    Write("a0/lcov.info");

    Find().ShouldBe(["a/lcov.info", "a0/lcov.info"]);
  }

  [Fact]
  public void Find_WithFile_ShouldNotSearch()
  {
    Write("given.xml");
    Write("other/lcov.info");

    Find(files: ["given.xml"]).ShouldBe(["given.xml"]);
  }

  [Fact]
  public void Find_WithFile_ShouldIgnoreTheExclusions()
  {
    Write("obj/a.xml");
    Write("node_modules/pkg/b.xml");

    Find(files: ["obj/a.xml", "node_modules/**/*.xml"])
      .ShouldBe(["node_modules/pkg/b.xml", "obj/a.xml"]);
  }

  [Fact]
  public void Find_WithBackslashes_ShouldTreatThemAsSeparators()
  {
    Write("a/b/one.xml");
    Write("a/c/d/two.xml");

    Find(files: [@"a\b\one.xml", @"a\c\**\*.xml"]).ShouldBe(["a/b/one.xml", "a/c/d/two.xml"]);
  }

  [Fact]
  public void Find_WithAbsoluteGlob_ShouldSearchBelowItsDirectory()
  {
    Write("agent/_temp/guid/coverage.cobertura.xml");
    Write("repo/coverage.cobertura.xml");

    Find(root: "repo", files: [Combine("agent/_temp") + "/**/coverage.cobertura.xml"])
      .ShouldBe(["agent/_temp/guid/coverage.cobertura.xml"]);
  }

  [Fact]
  public void Find_WithRelativeFile_ShouldStartFromTheWorkingDirectory()
  {
    Write("repo/web/coverage/lcov.info");
    Write("repo/coverage/lcov.info");

    Find(root: "repo", files: ["coverage/lcov.info"], workingDirectory: "repo/web")
      .ShouldBe(["repo/web/coverage/lcov.info"]);
  }

  [Fact]
  public void Find_WithParentGlob_ShouldLeaveTheWorkingDirectory()
  {
    Write("out/a.xml");
    Write("repo/b.xml");

    Find(files: ["../out/*.xml"], workingDirectory: "repo").ShouldBe(["out/a.xml"]);
  }

  [Fact]
  public void Find_WithFileNamedLikeAGlob_ShouldTakeItLiterally()
  {
    Write("a[1].xml");
    Write("a1.xml");

    Find(files: ["a[1].xml"]).ShouldBe(["a[1].xml"]);
  }

  [Fact]
  public void Find_WithOverlappingGlobs_ShouldReturnEachReportOnceAndNotWarn()
  {
    Write("a/one.xml");

    Find(files: ["a/*.xml", "a/one*"]).ShouldBe(["a/one.xml"]);
    Output.ShouldNotContain("warning");
  }

  [Fact]
  public void Find_WithMissingFile_ShouldWarnAndKeepTheOthers()
  {
    Write("a.xml");

    Find(files: ["missing.xml", "a.xml"]).ShouldBe(["a.xml"]);
    Output.ShouldContain("warning: The report 'missing.xml' was not found.");
    Output.ShouldNotContain(NoReport);
  }

  [Fact]
  public void Find_WithDirectoryAsFile_ShouldWarn()
  {
    Write("a/lcov.info");

    Find(files: ["a"]).ShouldBeEmpty();
    Output.ShouldContain("warning: The report 'a' was not found.");
    Output.ShouldContain(NoReport);
  }

  [Fact]
  public void Find_WithGlobMatchingNothing_ShouldWarn()
  {
    Find(files: ["**/nothing*.xml"]).ShouldBeEmpty();

    Output.ShouldContain("warning: No file matches '**/nothing*.xml'.");
    Output.ShouldContain(NoReport);
  }

  [Fact]
  public void Find_SearchDisabledWithoutFile_ShouldFindNothingAndWarn()
  {
    Write("lcov.info");

    Find(disableSearch: true).ShouldBeEmpty();
    Output.ShouldContain(NoReport);
  }

  [Fact]
  public void Find_SearchDisabledWithFile_ShouldUseTheFile()
  {
    Write("a.xml");

    Find(files: ["a.xml"], disableSearch: true).ShouldBe(["a.xml"]);
  }

  [Fact]
  public void Find_MissingRoot_ShouldWarn()
  {
    Find(root: "missing").ShouldBeEmpty();

    Output.ShouldContain("cannot be read");
    Output.ShouldContain(NoReport);
  }

  [Fact]
  public void Find_UnreadableFile_ShouldWarnAndKeepTheOthers()
  {
    SkipWhenPermissionsDoNotApply();

    var unreadable = Write("a/lcov.info");

    Write("b/lcov.info");

    using var holder = MakeFileUnreadable(unreadable);

    Find().ShouldBe(["b/lcov.info"]);
    Output.ShouldContain($"warning: '{unreadable}' cannot be read");
    Output.ShouldNotContain(NoReport);
  }

  [Fact]
  public void Find_UnreadableDirectory_ShouldWarnAndKeepTheOthers()
  {
    SkipWhenPermissionsDoNotApply();

    var unreadable = Path.GetDirectoryName(Write("a/lcov.info"))!;

    Write("b/lcov.info");
    SetDirectoryReadable(unreadable, readable: false);

    try
    {
      Find().ShouldBe(["b/lcov.info"]);
      Output.ShouldContain($"warning: '{unreadable}' cannot be read");
    }
    finally
    {
      SetDirectoryReadable(unreadable, readable: true);
    }
  }

  [Fact]
  public void Find_BrokenFileLink_ShouldWarn()
  {
    CreateLinkOrSkip(() => File.CreateSymbolicLink(Combine("lcov.info"), Combine("missing")));

    Find().ShouldBeEmpty();
    Output.ShouldContain("cannot be read");
  }

  [Fact]
  public void Find_FileLink_ShouldBeFound()
  {
    Write("real.txt");
    CreateLinkOrSkip(() => File.CreateSymbolicLink(Combine("lcov.info"), Combine("real.txt")));

    Find().ShouldBe(["lcov.info"]);
  }

  [Fact]
  public void Find_DirectoryLinkLoop_ShouldNotBeFollowed()
  {
    Write("a/lcov.info");
    CreateLinkOrSkip(() => Directory.CreateSymbolicLink(Combine("a/loop"), _sandbox));

    Find().ShouldBe(["a/lcov.info"]);
  }

  [Fact]
  public void Find_NullArguments_ShouldThrow()
  {
    var finder = new ReportFinder(_ => null, _sandbox, new ConsoleReporter(_output));

    Should.Throw<ArgumentNullException>(() => finder.Find(null!, [], disableSearch: false));
    Should.Throw<ArgumentNullException>(() => finder.Find(_sandbox, null!, disableSearch: false));
  }

  // Elevated on Linux, every permission is granted; a root container could not show the warning.
  private static void SkipWhenPermissionsDoNotApply()
  {
    if (!OperatingSystem.IsWindows() && Environment.IsPrivilegedProcess)
    {
      Assert.Skip("The process runs as root, to which file permissions do not apply.");
    }
  }

  private static FileStream? MakeFileUnreadable(string path)
  {
    if (OperatingSystem.IsWindows())
    {
      return new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    File.SetUnixFileMode(path, UnixFileMode.None);

    return null;
  }

  private static void SetDirectoryReadable(string path, bool readable)
  {
    if (OperatingSystem.IsWindows())
    {
      var directory = new DirectoryInfo(path);
      var security = directory.GetAccessControl();
      var denyListing = new FileSystemAccessRule(
        WindowsIdentity.GetCurrent().User!,
        FileSystemRights.ListDirectory,
        AccessControlType.Deny);

      if (readable)
      {
        security.RemoveAccessRule(denyListing);
      }
      else
      {
        security.AddAccessRule(denyListing);
      }

      directory.SetAccessControl(security);

      return;
    }

    File.SetUnixFileMode(
      path,
      readable
        ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        : UnixFileMode.None);
  }

  // Windows only grants the creation of a link to an administrator or in developer mode.
  private static void CreateLinkOrSkip(Action createLink)
  {
    try
    {
      createLink();
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      Assert.Skip($"This account cannot create a symbolic link: {exception.Message}");
    }
  }

  private string Combine(string relativePath)
  {
    return Path.GetFullPath(relativePath, _sandbox);
  }

  private string Write(string relativePath)
  {
    var path = Combine(relativePath);

    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, "report");

    return path;
  }

  // Returns the paths relative to the sandbox and with forward slashes, in the order of the finder.
  private string[] Find(
    string? root = null,
    string[]? files = null,
    bool disableSearch = false,
    string? agentTemp = null,
    string? workingDirectory = null)
  {
    var directory = workingDirectory is null ? _sandbox : Combine(workingDirectory);

    if (!Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var finder = new ReportFinder(
      name => name is "AGENT_TEMPDIRECTORY" ? agentTemp : null,
      directory,
      new ConsoleReporter(_output));

    return finder.Find(Combine(root ?? "."), files ?? [], disableSearch)
                 .Select(report => Path.GetRelativePath(_sandbox, report).Replace('\\', '/'))
                 .ToArray();
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    _output.Dispose();
    Directory.Delete(_sandbox, recursive: true);
  }

  #endregion
}

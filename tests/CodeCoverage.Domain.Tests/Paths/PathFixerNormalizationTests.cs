// ------------------------------------------------------------------------------------------------
// <copyright file="PathFixerNormalizationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Paths;

// The fixes cases come from test_user_path_fixes.py and test_path_fixer.py of Codecov
// (apps/worker/services/path_fixer/tests/unit); the expected values of the star and leading-slash
// quirks were checked against its _fixpaths_regs and CustomFixPathSchemaField.
public sealed class PathFixerNormalizationTests
{
  #region Fields

  private static readonly NetworkIndex Network = new(["src/a.cs"]);

  #endregion

  #region Methods

  [Fact]
  public void Normalize_WindowsDrive_ShouldStripAndSlash()
  {
    PathFixer.Normalize(@"C:\agent\src\a.cs").ShouldBe("agent/src/a.cs");
  }

  [Fact]
  public void Normalize_DeterministicSourcePrefix_ShouldStrip()
  {
    PathFixer.Normalize("/_/src/a.cs").ShouldBe("src/a.cs");
  }

  [Theory]
  [InlineData("  src/a.cs \r", "src/a.cs")]
  [InlineData("file:///home/ci/a.cs", "home/ci/a.cs")]
  [InlineData("FILE:///home/ci/a.cs", "home/ci/a.cs")]
  [InlineData("file:///C:/ci/a.cs", "ci/a.cs")]
  [InlineData("file://C:/ci/a.cs", "ci/a.cs")]
  [InlineData("/home/ci/a.cs", "home/ci/a.cs")]
  [InlineData("src//lib///a.cs", "src/lib/a.cs")]
  [InlineData("./src/./a.cs", "src/a.cs")]
  [InlineData("src/lib/../a.cs", "src/a.cs")]
  [InlineData("../../src/a.cs", "src/a.cs")]
  [InlineData(".github/a.cs", ".github/a.cs")]
  [InlineData("_/src/a.cs", "_/src/a.cs")]
  [InlineData("/_", "_")]
  [InlineData("   ", "")]
  public void Normalize_ShouldGiveCanonicalPath(string path, string expected)
  {
    PathFixer.Normalize(path).ShouldBe(expected);
  }

  [Fact]
  public void Normalize_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => PathFixer.Normalize(null!));
  }

  [Fact]
  public void ApplyFixes_FirstMatchingRuleShouldWin()
  {
    Fixer("src/::first/", "src/lib/::second/").ApplyFixes("src/lib/a.cs", false).ShouldBe("first/lib/a.cs");
    Fixer("src/lib/::second/", "src/::first/").ApplyFixes("src/lib/a.cs", false).ShouldBe("second/a.cs");
  }

  [Theory]
  [InlineData("before/::after/", "before/path/to/x.py", "after/path/to/x.py")]
  [InlineData("before/::after/", "after/path/to/x.py", "after/path/to/x.py")]
  [InlineData("before/::after/", "after/before/path/to/x.py", "after/before/path/to/x.py")]
  [InlineData("prefix_to_remove::", "prefix_to_remove/third_path.py", "third_path.py")]
  [InlineData("prefix_to_remove::", "thisisnot/prefix_to_remove/third_path.py", "thisisnot/prefix_to_remove/third_path.py")]
  [InlineData("prefix_to_remove::add", "prefix_to_remove/third_path.py", "add/third_path.py")]
  [InlineData(@"(?s:prefix_to_remove/test\-[^\/]+)::add", "prefix_to_remove/test-third_folder/path.py", "add/path.py")]
  [InlineData(@"(?s:prefix_to_remove/test\-[^\/]+)::add", "prefix_to_remove/test-fourth_oops.py", "add")]
  [InlineData(@"(?s:prefix_to_remove/test\-[^\/]+)::add", "thisisnot/prefix_to_remove/test-third_path.py", "thisisnot/prefix_to_remove/test-third_path.py")]
  [InlineData("a.b/::c/", "axb/d.cs", "c/d.cs")]
  [InlineData("a/**/b/::c/", "a/x/y/b/d.cs", "c/d.cs")]
  [InlineData("a/**/b/::", "a/x/y/b/d.cs", "d.cs")]
  [InlineData("a/::$1/", "a/d.cs", "$1/d.cs")]
  [InlineData("a::b::c", "a/d.cs", "b/d.cs")]
  public void ApplyFixes_ShouldFollowCodecov(string fix, string path, string expected)
  {
    Fixer(fix).ApplyFixes(path, false).ShouldBe(expected);
  }

  [Theory]
  [InlineData("before/tests-*::after/", "before/tests-a/x.cs", "before/tests-a/x.cs")]
  [InlineData("before/tests-*::after/", "before/tests-ab/x.cs", "after/x.cs")]
  [InlineData("before/tests-*::", "before/tests-a/x.cs", "x.cs")]
  [InlineData("before/tests-*::", "before/tests-/x.cs", "before/tests-/x.cs")]
  public void ApplyFixes_SingleStar_ShouldNeedAsManyCharactersAsCodecov(string fix, string path, string expected)
  {
    Fixer(fix).ApplyFixes(path, false).ShouldBe(expected);
  }

  [Fact]
  public void ApplyFixes_LeadingSlashOfRemoval_ShouldBeIgnored()
  {
    Fixer("/opt/x/::").ApplyFixes("opt/x/a.py", false).ShouldBe("a.py");
  }

  [Fact]
  public void ApplyFixes_LeadingSlashOfPrefixWithReplacement_ShouldNeverMatch()
  {
    Fixer("/opt/x/::project/").ApplyFixes("opt/x/a.py", false).ShouldBe("opt/x/a.py");
  }

  [Theory]
  [InlineData("C:/Users/ci/build/::project/")]
  [InlineData("c:/Users/ci/build/::project/")]
  public void ApplyFixes_RuleWithDriveLetter_ShouldMatchNormalizedPath(string fix)
  {
    Fixer(fix).ApplyFixes(PathFixer.Normalize(@"C:\Users\ci\build\a.c"), false).ShouldBe("project/a.c");
  }

  [Fact]
  public void ApplyFixes_EveryRule_ShouldBeAnchored()
  {
    Fixer("a/::x/", "b/::y/").ApplyFixes("z/b/c.cs", false).ShouldBe("z/b/c.cs");
  }

  [Fact]
  public void ApplyFixes_RuleWithCaptureGroup_ShouldUseItsOwnReplacement()
  {
    Fixer("(a|b)/::x/", "c/::y/").ApplyFixes("c/d.cs", false).ShouldBe("y/d.cs");
  }

  [Fact]
  public void ApplyFixes_ShouldSubstituteOnce()
  {
    Fixer("a/::a/a/").ApplyFixes("a/d.cs", false).ShouldBe("a/a/d.cs");
  }

  [Fact]
  public void ApplyFixes_WithoutPrefixes_ShouldNotAddThem()
  {
    Fixer("::added_prefix").ApplyFixes("simple/path.c", false).ShouldBe("simple/path.c");
  }

  [Theory]
  [InlineData("simple/path.c", "added_prefix/simple/path.c")]
  [InlineData("added_prefix/second_path.java", "added_prefix/added_prefix/second_path.java")]
  public void ApplyFixes_WithPrefixes_ShouldAddThem(string path, string expected)
  {
    Fixer("::added_prefix").ApplyFixes(path, true).ShouldBe(expected);
  }

  [Fact]
  public void ApplyFixes_WithPrefixes_ShouldJoinThemInOrderBeforeTheSubstitution()
  {
    Fixer("::one/", "::two", "one/two/::three/").ApplyFixes("a.c", true).ShouldBe("three/a.c");
  }

  [Theory]
  [InlineData("(?=x)y::z")]
  [InlineData("a(::z")]
  [InlineData("no separator")]
  public void ApplyFixes_RefusedEntry_ShouldBeReportedAndLeaveTheOthersRunning(string refused)
  {
    var rule = FixRule.Parse([refused, "a/::b/"]);

    rule.RefusedFixes.ShouldBe([refused]);
    new PathFixer(new PathFixContext { Fixes = rule }).ApplyFixes("a/c.cs", false).ShouldBe("b/c.cs");
  }

  [Fact]
  public void ApplyFixes_EntriesTooLargeTogether_ShouldAllBeRefused()
  {
    string[] fixes = ["a{6000}::x", "b{6000}::y"];
    var rule = FixRule.Parse(fixes);

    rule.RefusedFixes.ShouldBe(fixes);
    rule.Apply(new string('a', 6000), false).ShouldBe(new string('a', 6000));
  }

  [Fact]
  public void ApplyFixes_WithoutRule_ShouldKeepPath()
  {
    new PathFixer(new PathFixContext()).ApplyFixes("a/c.cs", true).ShouldBe("a/c.cs");
  }

  [Fact]
  public void Candidates_ShouldIncludeCoberturaSourcesAndCiPrefixes()
  {
    var fixer = new PathFixer(
      new PathFixContext
      {
        SourceRoots = ["/build/app", "lib", " "],
        ReportPath = @"coverage\out\report.xml",
        Network = Network,
      });

    fixer.Candidates("src/a.cs").ShouldBe(["src/a.cs", "coverage/out/src/a.cs", "build/app/src/a.cs", "lib/src/a.cs"]);
    fixer.Candidates("/home/runner/work/owner/repo/src/a.cs").ShouldBe(["home/runner/work/owner/repo/src/a.cs", "src/a.cs"]);
  }

  [Theory]
  [InlineData("/home/runner/work/owner/repo/src/a.cs")]
  [InlineData("/__w/owner/repo/src/a.cs")]
  [InlineData("/a/owner/repo/src/a.cs")]
  [InlineData(@"D:\a\1\s\src\a.cs")]
  [InlineData("/home/vsts/work/12/s/src/a.cs")]
  [InlineData("/agent/_work/3/s/src/a.cs")]
  [InlineData("/Users/me/src/a.cs")]
  [InlineData("/home/me/src/a.cs")]
  public void Candidates_KnownCiPrefix_ShouldBeStripped(string path)
  {
    new PathFixer(new PathFixContext()).Candidates(path).ShouldContain("src/a.cs");
  }

  [Fact]
  public void Candidates_WorkspacePrefix_ShouldWinOverUserHome()
  {
    new PathFixer(new PathFixContext()).Candidates("/home/runner/work/o/r/a.cs").ShouldNotContain("work/o/r/a.cs");
  }

  [Fact]
  public void Candidates_WithoutPrefix_ShouldOnlyGiveThePath()
  {
    new PathFixer(new PathFixContext()).Candidates("src/a.cs").ShouldBe(["src/a.cs"]);
  }

  [Fact]
  public void Candidates_RootPrefix_ShouldBeRemovedIgnoringCase()
  {
    new PathFixer(new PathFixContext { Root = @"C:\Build\App" }).Candidates(@"c:\build\app\src\a.cs").ShouldBe(["src/a.cs"]);
  }

  [Theory]
  [InlineData("/build/app2/a.cs", "build/app2/a.cs")]
  [InlineData("/build/app", "build/app")]
  public void Candidates_RootOnSegmentBoundaryOnly_ShouldKeepOtherPaths(string path, string expected)
  {
    new PathFixer(new PathFixContext { Root = "/build/app" }).Candidates(path).ShouldBe([expected]);
  }

  [Fact]
  public void Candidates_ShouldApplyFixesBeforeRemovingRoot()
  {
    var context = new PathFixContext { Fixes = FixRule.Parse(["src/::build/app/lib/"]), Root = "/build/app" };

    new PathFixer(context).Candidates("src/a.cs").ShouldBe(["lib/a.cs"]);
  }

  [Fact]
  public void Candidates_BaseDirectory_ShouldBeRelativeToRoot()
  {
    var context = new PathFixContext
    {
      Root = "/build",
      SourceRoots = ["/build/output", "/build/sub"],
      Network = Network,
    };

    new PathFixer(context).Candidates("a.cs").ShouldBe(["a.cs", "output/a.cs", "sub/a.cs"]);
  }

  [Fact]
  public void Candidates_RelativeBaseDirectory_ShouldApplyFixesOnTheJoinedPath()
  {
    var context = new PathFixContext
    {
      Fixes = FixRule.Parse(["output/::src/"]),
      SourceRoots = ["output"],
      Network = Network,
    };

    new PathFixer(context).Candidates("a.cs").ShouldBe(["a.cs", "src/a.cs"]);
  }

  [Theory]
  [InlineData("/src/a.cs")]
  [InlineData(@"C:\src\a.cs")]
  [InlineData("file:///src/a.cs")]
  public void Candidates_AbsolutePath_ShouldNotJoinBaseDirectories(string path)
  {
    var context = new PathFixContext { SourceRoots = ["lib"], ReportPath = "out/report.xml", Network = Network };

    new PathFixer(context).Candidates(path).ShouldBe(["src/a.cs"]);
  }

  [Fact]
  public void Candidates_WithoutNetwork_ShouldNotJoinBaseDirectories()
  {
    var context = new PathFixContext { SourceRoots = ["lib"], ReportPath = "out/report.xml" };

    new PathFixer(context).Candidates("src/a.cs").ShouldBe(["src/a.cs"]);
  }

  [Fact]
  public void Candidates_WithEmptyNetwork_ShouldBeLikeNoNetwork()
  {
    var context = new PathFixContext { SourceRoots = ["lib"], Network = new NetworkIndex([]) };

    new PathFixer(context).Candidates("src/a.cs").ShouldBe(["src/a.cs"]);
  }

  [Fact]
  public void Candidates_DefaultFixesDisabled_ShouldOnlyGiveThePath()
  {
    var context = new PathFixContext
    {
      SourceRoots = ["lib"],
      Network = Network,
      DisableDefaultPathFixes = true,
    };

    new PathFixer(context).Candidates("home/me/a.cs").ShouldBe(["home/me/a.cs"]);
  }

  [Fact]
  public void Candidates_DefaultFixesDisabledWithoutNetwork_ShouldStillStripCiPrefixes()
  {
    new PathFixer(new PathFixContext { DisableDefaultPathFixes = true })
      .Candidates("/home/me/a.cs")
      .ShouldBe(["home/me/a.cs", "a.cs"]);
  }

  [Fact]
  public void Candidates_ShouldNotRepeatAPath()
  {
    var context = new PathFixContext { SourceRoots = ["/_/", "."], Network = Network };

    new PathFixer(context).Candidates("src/a.cs").ShouldBe(["src/a.cs"]);
  }

  [Fact]
  public void Candidates_EmptyPath_ShouldBeEmpty()
  {
    new PathFixer(new PathFixContext()).Candidates(" ").ShouldBeEmpty();
  }

  [Fact]
  public void Candidates_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new PathFixer(new PathFixContext()).Candidates(null!));
  }

  [Fact]
  public void ApplyFixes_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new PathFixer(new PathFixContext()).ApplyFixes(null!, false));
  }

  [Fact]
  public void Ctor_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new PathFixer(null!));
  }

  private static PathFixer Fixer(params string[] fixes)
  {
    return new PathFixer(new PathFixContext { Fixes = FixRule.Parse(fixes) });
  }

  #endregion
}

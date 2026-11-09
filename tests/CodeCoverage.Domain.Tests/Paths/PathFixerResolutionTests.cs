// ------------------------------------------------------------------------------------------------
// <copyright file="PathFixerResolutionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Paths;

// The suffix cases come from test_pathmap.py of Codecov (apps/worker/helpers/tests/pathmap), read
// with ancestors=1, which is what its PathFixer passes.
public sealed class PathFixerResolutionTests
{
  #region Methods

  [Theory]
  [InlineData("/home/runner/work/app/app/src/App/Service.cs", "src/App/Service.cs")]
  [InlineData("/builds/team/app/src/App/Service.cs", "src/App/Service.cs")]
  [InlineData(@"D:\a\app\app\src\App\Service.cs", "src/App/Service.cs")]
  [InlineData(@"C:\Users\ci\repos\app\src\App\Service.cs", "src/App/Service.cs")]
  [InlineData("/_/src/App/Service.cs", "src/App/Service.cs")]
  [InlineData("/home/runner/work/web/web/packages/ui/src/Button.tsx", "packages/ui/src/Button.tsx")]
  [InlineData("/Users/dev/code/web/packages/ui/src/Button.tsx", "packages/ui/src/Button.tsx")]
  [InlineData("com/acme/shop/Cart.java", "services/shop/src/main/java/com/acme/shop/Cart.java")]
  [InlineData("Main.java", "services/shop/src/main/java/Main.java")]
  public void Fix_ReportPathShapes_ShouldResolve(string path, string expected)
  {
    var result = Fixer([
      "src/App/Service.cs",
      "tests/App/Tests.cs",
      "packages/ui/src/Button.tsx",
      "services/shop/src/main/java/com/acme/shop/Cart.java",
      "services/shop/src/main/java/Main.java",
    ]).Fix(path);

    ShouldBeResolved(result, expected);
  }

  [Fact]
  public void Fix_ExactMatch_ShouldPreferCase()
  {
    ShouldBeResolved(Fixer(["Aa/Bb/cc", "Aa/Bb/Cc"]).Fix("Aa/Bb/Cc"), "Aa/Bb/Cc");
  }

  [Fact]
  public void Fix_ExactMatchOnLaterCandidate_ShouldWinOverCaseInsensitiveMatch()
  {
    var result = Fixer(["SRC/a.cs", "lib/src/a.cs"], new PathFixContext { ReportPath = "lib/report.xml" })
      .Fix("src/a.cs");

    ShouldBeResolved(result, "lib/src/a.cs");
  }

  [Fact]
  public void Fix_CaseInsensitiveMatch_ShouldGiveRepositoryCase()
  {
    ShouldBeResolved(Fixer(["src/components/login.js"]).Fix("Src/components/login.js"), "src/components/login.js");
  }

  [Theory]
  [InlineData("C:/projects/perfview/src/heapDump/GCHeapDump.cs", "src/HeapDump/GCHeapDump.cs", "src/HeapDump/GCHeapDump.cs")]
  [InlineData("z/y/b/c", "a/b/c", "a/b/c")]
  [InlineData("b/c", "a/b/c", "a/b/c")]
  [InlineData("R/y/z", "x/y/z", "x/y/z")]
  [InlineData("w/x/y/z", "x/y/z", "x/y/z")]
  [InlineData("site-packages/package/__init__.py", "package/__init__.py", "package/__init__.py")]
  public void Fix_SuffixScoreTwoOrMore_ShouldResolve(string path, string file, string expected)
  {
    ShouldBeResolved(Fixer([file]).Fix(path), expected);
  }

  [Fact]
  public void Fix_HighestSuffixScore_ShouldWin()
  {
    ShouldBeResolved(Fixer(["a/b/c", "a/r/c", "c"]).Fix("r/c"), "a/r/c");
    ShouldBeResolved(Fixer(["a/b", "a/b/c/d", "x/y"]).Fix("c/d"), "a/b/c/d");
    ShouldBeResolved(Fixer(["b/c.py", "x/b/c.py"]).Fix("w/x/b/c.py"), "x/b/c.py");
  }

  [Theory]
  [InlineData("other/a.cs", "src/a.cs")]
  [InlineData("R/z", "x/y/z")]
  [InlineData("b/b", "a/b")]
  public void Fix_SuffixScoreOne_ShouldBeRejectedByDefault(string path, string file)
  {
    Fixer([file]).Fix(path).ShouldBe(PathFixResult.Unresolved);
  }

  [Fact]
  public void Fix_SameDirectoryOtherFileName_ShouldBeUnresolved()
  {
    Fixer(["four/six/three.py"]).Fix("four/six/seven.py").ShouldBe(PathFixResult.Unresolved);
  }

  [Fact]
  public void Fix_SuffixScoreOne_WithAllowFilenameOnlyAndUnique_ShouldResolve()
  {
    var result = Fixer(["src/a.cs"], new PathFixContext { AllowFilenameOnlyMatch = true }).Fix("other/a.cs");

    ShouldBeResolved(result, "src/a.cs");
  }

  [Fact]
  public void Fix_SuffixScoreOne_WithAllowFilenameOnlyAndSeveralFiles_ShouldBeUnresolved()
  {
    var result = Fixer(["src/a.cs", "lib/a.cs"], new PathFixContext { AllowFilenameOnlyMatch = true }).Fix("other/a.cs");

    result.ShouldBe(PathFixResult.Unresolved);
  }

  [Fact]
  public void Fix_BareFileNameUniqueInRepository_ShouldResolve()
  {
    ShouldBeResolved(Fixer(["x/y/z"]).Fix("z"), "x/y/z");
  }

  [Fact]
  public void Fix_BareFileNameWithSeveralFiles_ShouldBeUnresolved()
  {
    Fixer(["src/list.rs", "benches/list.rs"]).Fix("list.rs").ShouldBe(PathFixResult.Unresolved);
  }

  [Fact]
  public void Fix_FileNameLeftByCiPrefixRemoval_ShouldNotCountAsBareFileName()
  {
    Fixer(["src/a.cs"]).Fix("/home/runner/work/app/app/a.cs").ShouldBe(PathFixResult.Unresolved);
  }

  [Fact]
  public void Fix_LongerPathToRootFile_ShouldResolve()
  {
    ShouldBeResolved(Fixer(["shorter.h"]).Fix("a/long/path/shorter.h"), "shorter.h");
    ShouldBeResolved(Fixer(["shorter.h", "src/inner/shorter.h"]).Fix("a/long/path/shorter.h"), "shorter.h");
  }

  [Fact]
  public void Fix_Tie_ShouldPreferFlagPaths()
  {
    var context = new PathFixContext { FlagPaths = new PathPatternSet([PathPattern.Parse("backend/long/")]) };

    ShouldBeResolved(Fixer(["web/b/c.py", "backend/long/b/c.py"], context).Fix("q/b/c.py"), "backend/long/b/c.py");
  }

  [Fact]
  public void Fix_Tie_ShouldPreferReportDirectoryOverShortest()
  {
    var context = new PathFixContext { ReportPath = "backend/long/coverage.xml" };

    ShouldBeResolved(
      Fixer(["web/b/c.py", "backend/long/b/c.py"], context).Fix("/ci/q/b/c.py"),
      "backend/long/b/c.py");
  }

  [Fact]
  public void Fix_Tie_ShouldPreferFlagPathsOverReportDirectory()
  {
    var context = new PathFixContext
    {
      FlagPaths = new PathPatternSet([PathPattern.Parse("web/")]),
      ReportPath = "backend/coverage.xml",
    };

    ShouldBeResolved(Fixer(["web/x/b/c.py", "backend/b/c.py"], context).Fix("/ci/q/b/c.py"), "web/x/b/c.py");
  }

  [Fact]
  public void Fix_Tie_ShouldPreferShortestPath()
  {
    ShouldBeResolved(
      Fixer(["examples/Church.scala", "tests/src/examples/Church.scala"]).Fix("/ci/owner/repo/examples/Church.scala"),
      "examples/Church.scala");
  }

  [Fact]
  public void Fix_PersistentTie_ShouldBeAmbiguous()
  {
    Fixer(["x/b/c.py", "y/b/c.py"]).Fix("q/b/c.py").ShouldBe(PathFixResult.Ambiguous);
  }

  [Fact]
  public void Fix_CaseVariantsOfOneFile_ShouldBeAmbiguous()
  {
    Fixer(["Aa/Bb/cc", "Aa/Bb/Cc"]).Fix("aa/bb/CC").ShouldBe(PathFixResult.Ambiguous);
  }

  [Fact]
  public void Fix_WithoutNetwork_ShouldReturnUnverified()
  {
    var result = new PathFixer(new PathFixContext()).Fix("/home/runner/work/app/app/src/a.cs");

    result.Status.ShouldBe(PathFixStatus.Unverified);
    result.Path.ShouldBe("src/a.cs");
  }

  [Fact]
  public void Fix_EmptyNetwork_ShouldReturnUnverified()
  {
    var result = Fixer([]).Fix("src/a.cs");

    result.Status.ShouldBe(PathFixStatus.Unverified);
    result.Path.ShouldBe("src/a.cs");
  }

  [Fact]
  public void Fix_WithoutNetwork_ShouldAddPrefixes()
  {
    var result = new PathFixer(new PathFixContext { Fixes = FixRule.Parse(["::src"]) }).Fix("x/a.c");

    result.Status.ShouldBe(PathFixStatus.Unverified);
    result.Path.ShouldBe("src/x/a.c");
  }

  [Fact]
  public void Fix_DisableDefaultPathFixes_ShouldKeepPathUnknownToTheList()
  {
    var result = Fixer(["src/a.cs"], new PathFixContext { DisableDefaultPathFixes = true }).Fix("/ci/other/a.cs");

    ShouldBeResolved(result, "ci/other/a.cs");
  }

  [Fact]
  public void Fix_PrefixRule_ShouldApplyAfterResolution()
  {
    var result = Fixer(["src/x/a.c"], new PathFixContext { Fixes = FixRule.Parse(["::src"]) }).Fix("x/a.c");

    ShouldBeResolved(result, "src/src/x/a.c");
  }

  [Fact]
  public void Fix_SubstitutionRule_ShouldRunAgainOnRetainedPath()
  {
    var result = Fixer(["src/main/x/a.c"], new PathFixContext { Fixes = FixRule.Parse(["src::src/main"]) })
      .Fix("src/x/a.c");

    ShouldBeResolved(result, "src/main/main/x/a.c");
  }

  [Fact]
  public void Fix_RemovalRuleEmptyingThePath_ShouldBeUnresolved()
  {
    var result = Fixer(["src/a.cs"], new PathFixContext { Fixes = FixRule.Parse(["src/a.cs::"]) }).Fix("src/a.cs");

    result.ShouldBe(PathFixResult.Unresolved);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void Fix_NothingLeftOfThePath_ShouldBeUnresolved(string path)
  {
    Fixer(["src/a.cs"]).Fix(path).ShouldBe(PathFixResult.Unresolved);
    new PathFixer(new PathFixContext()).Fix(path).ShouldBe(PathFixResult.Unresolved);
  }

  [Fact]
  public void Fix_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => Fixer(["src/a.cs"]).Fix(null!));
  }

  [Fact]
  public void RefusedFixes_ShouldListEntriesLeftOut()
  {
    var fixer = new PathFixer(new PathFixContext { Fixes = FixRule.Parse(["before(?=x)::after/"]) });

    fixer.RefusedFixes.ShouldBe(["before(?=x)::after/"]);
  }

  private static PathFixer Fixer(string[] network, PathFixContext? context = null) =>
    new((context ?? new PathFixContext()) with { Network = new NetworkIndex(network) });

  private static void ShouldBeResolved(PathFixResult result, string expected)
  {
    result.Status.ShouldBe(PathFixStatus.Resolved);
    result.Path.ShouldBe(expected);
  }

  #endregion
}

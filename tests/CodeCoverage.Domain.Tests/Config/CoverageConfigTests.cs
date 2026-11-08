// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageConfigTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Coverage;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

public sealed class CoverageConfigTests
{
  #region Fields

  private const string RefusedPattern = "src/(?=a)";

  #endregion

  #region Methods

  [Fact]
  public void Create_OnDefaults_ShouldExposeTheBuiltInValues()
  {
    var config = CoverageConfig.Create(CoverageConfigDefaults.Root);

    config.Precision.ShouldBe(2);
    config.Rounding.ShouldBe(RoundingMode.Down);
    config.Range.ShouldBe(CoverageRange.Read(Text("60..80")));
    config.Ignore.ShouldBeEmpty();
    config.Fixes.ShouldBeEmpty();
    config.MaxReportAge.ShouldBeNull();
    config.NotifyAfterNBuilds.ShouldBe(0);
    config.Cobertura.ShouldBe(new ParserRule(PartialsAsHits: false, HandleMissingConditions: false));
  }

  [Fact]
  public void Create_OnEmptyConfiguration_ShouldUseTheBuiltInValuesWithoutWarning()
  {
    var config = CoverageConfig.Create(Map());

    config.Precision.ShouldBe(2);
    config.Range.ShouldBe(CoverageRange.Read(Text("60..80")));
    config.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Create_ShouldReadTheValuesOfTheFile()
  {
    var config = Effective(
      ("coverage", Map(("precision", Number(1)), ("round", Text("nearest")), ("range", Text("70...90")))),
      ("fixes", Texts("before/::after/", "::prefix")),
      ("parsers", Map(
        ("cobertura", Map(("partials_as_hits", Bool(true)), ("handle_missing_conditions", Bool(true)))),
        ("lcov", Map(("partials_as_hits", Bool(true)))))));

    config.Precision.ShouldBe(1);
    config.Rounding.ShouldBe(RoundingMode.Nearest);
    config.Range.Low.ShouldBe(70);
    config.Fixes.ShouldBe(["before/::after/", "::prefix"]);
    config.Cobertura.ShouldBe(new ParserRule(PartialsAsHits: true, HandleMissingConditions: true));
    config.Lcov.PartialsAsHits.ShouldBeTrue();
    config.Jacoco.PartialsAsHits.ShouldBeFalse();
    config.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Create_ShouldReadTheCodecovSection()
  {
    var config = Effective(("codecov", Map(
      ("strict_yaml_branch", Text("main")),
      ("disable_default_path_fixes", Bool(true)),
      ("allow_pseudo_compare", Bool(true)),
      ("require_ci_to_pass", Bool(true)),
      ("notify", Map(
        ("wait_for_ci", Bool(true)),
        ("after_n_builds", Number(3)),
        ("manual_trigger", Bool(true)),
        ("notify_error", Bool(true)))))));

    config.StrictYamlBranch.ShouldBe("main");
    config.DisableDefaultPathFixes.ShouldBeTrue();
    config.AllowPseudoCompare.ShouldBeTrue();
    config.RequireCiToPass.ShouldBeTrue();
    config.WaitForCi.ShouldBeTrue();
    config.NotifyAfterNBuilds.ShouldBe(3);
    config.ManualTrigger.ShouldBeTrue();
    config.NotifyError.ShouldBeTrue();
  }

  [Theory]
  [InlineData("main", "main")]
  [InlineData("origin/main", "main")]
  [InlineData("refs/heads/release/1", "release/1")]
  public void Create_Branch_ShouldBeNormalized(string written, string expected)
  {
    Effective(("codecov", Map(("branch", Text(written))))).DefaultBranch.ShouldBe(expected);
  }

  [Fact]
  public void Create_MaxReportAge_ShouldKeepTheText()
  {
    Effective(("codecov", Map(("max_report_age", Text("12h ago"))))).MaxReportAge.ShouldBe("12h ago");
  }

  [Fact]
  public void Create_MaxReportAgeNumber_ShouldBecomeText()
  {
    Effective(("codecov", Map(("max_report_age", Number(3600))))).MaxReportAge.ShouldBe("3600");
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Create_MaxReportAgeBoolean_ShouldBeDisabled(bool value)
  {
    Effective(("codecov", Map(("max_report_age", Bool(value))))).MaxReportAge.ShouldBeNull();
  }

  [Fact]
  public void Create_InvalidPrecision_ShouldWarnAndUseTheDefault()
  {
    var config = Effective(("coverage", Map(("precision", Text("abc")))));

    config.Precision.ShouldBe(2);
    config.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "coverage.precision", "abc")]);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(100)]
  [InlineData(1.5)]
  public void Create_PrecisionOutOfSchema_ShouldWarnAndUseTheDefault(decimal value)
  {
    var config = Effective(("coverage", Map(("precision", Number(value)))));

    config.Precision.ShouldBe(2);
    config.Warnings.Single().Code.ShouldBe(ConfigWarning.InvalidValue);
  }

  [Fact]
  public void Create_PrecisionAboveWhatIsSupported_ShouldClamp()
  {
    var config = Effective(("coverage", Map(("precision", Number(50)))));

    config.Precision.ShouldBe(CoverageMath.MaxPrecision);
    config.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.ClampedValue, "coverage.precision", "50")]);
  }

  [Fact]
  public void Create_InvalidRound_ShouldWarnAndUseTheDefault()
  {
    var config = Effective(("coverage", Map(("round", Text("Down")))));

    config.Rounding.ShouldBe(RoundingMode.Down);
    config.Warnings.ShouldHaveSingleItem().Key.ShouldBe("coverage.round");
  }

  [Fact]
  public void Create_InvalidRange_ShouldWarnAndUseTheDefault()
  {
    var config = Effective(("coverage", Map(("range", Text("90..60")))));

    config.Range.ShouldBe(CoverageRange.Read(Text("60..80")));
    config.Warnings.ShouldHaveSingleItem().Detail.ShouldBe("90..60");
  }

  [Fact]
  public void Create_InvalidBoolean_ShouldWarnAndUseTheDefault()
  {
    var config = Effective(("parsers", Map(("lcov", Map(("partials_as_hits", Text("maybe")))))));

    config.Lcov.PartialsAsHits.ShouldBeFalse();
    config.Warnings.ShouldHaveSingleItem().Key.ShouldBe("parsers.lcov.partials_as_hits");
  }

  [Fact]
  public void Create_NullValue_ShouldUseTheDefaultWithoutWarning()
  {
    var config = Effective(("coverage", Map(("precision", ConfigNode.Null))));

    config.Precision.ShouldBe(2);
    config.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Create_FixWithoutSeparator_ShouldWarnAndSkip()
  {
    var config = Effective(("fixes", Texts("a::b", "broken")));

    config.Fixes.ShouldBe(["a::b"]);
    config.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "fixes[1]", "broken")]);
  }

  [Fact]
  public void Create_FixesNotAList_ShouldWarn()
  {
    Effective(("fixes", Text("a::b"))).Warnings.ShouldHaveSingleItem().Key.ShouldBe("fixes");
  }

  [Fact]
  public void Create_IgnoreShouldBeInverted()
  {
    var config = Effective(("ignore", Texts("tests/", "!docs/")));

    config.Ignore.Select(pattern => pattern.IsNegative).ShouldBe([true, false]);
  }

  [Fact]
  public void Create_UnsupportedIgnorePattern_ShouldWarnAndStayInTheList()
  {
    var config = Effective(("ignore", Texts("tests/", RefusedPattern)));

    config.Ignore.Count.ShouldBe(2);
    config.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.UnsupportedPattern, "ignore[1]", RefusedPattern)]);
  }

  [Fact]
  public void Create_IgnoreEntryNotAText_ShouldWarnAndSkip()
  {
    var config = Effective(("ignore", List(Number(1), Text("tests/"))));

    config.Ignore.ShouldHaveSingleItem();
    config.Warnings.ShouldHaveSingleItem().Key.ShouldBe("ignore[0]");
  }

  [Fact]
  public void Create_UnsupportedLegacyFlagPattern_ShouldWarn()
  {
    var config = Effective(("flags", Map(("a", Map(("paths", Texts(RefusedPattern)))))));

    config.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.UnsupportedPattern, "flags.a.paths[0]", RefusedPattern)]);
  }

  [Fact]
  public void Create_UnsupportedManagementPatterns_ShouldWarnWhereTheyAreWritten()
  {
    var config = Effective(("flag_management", Map(
      ("default_rules", Map(("ignore", Texts(RefusedPattern)))),
      ("individual_flags", List(Map(("name", Text("a")), ("paths", Texts(RefusedPattern))))))));

    config.Warnings.Select(warning => warning.Key).ShouldBe(
      ["flag_management.default_rules.ignore[0]", "flag_management.individual_flags[0].paths[0]"]);
  }

  [Theory]
  [InlineData("after_n_builds")]
  [InlineData("carryforward")]
  [InlineData("joined")]
  public void Create_InvalidFlagOption_ShouldWarn(string option)
  {
    var config = Effective(("flags", Map(("a", Map((option, Text("x")))))));

    config.Warnings.ShouldHaveSingleItem().Key.ShouldBe($"flags.a.{option}");
  }

  [Fact]
  public void Create_FlagNotAMap_ShouldWarn()
  {
    Effective(("flags", Map(("a", Bool(true))))).Warnings.ShouldHaveSingleItem().Key.ShouldBe("flags.a");
  }

  [Fact]
  public void ResolveFlag_ShouldUseTheEffectiveConfiguration()
  {
    var config = Effective(("flags", Map(("a", Map(("after_n_builds", Number(2)))))));

    config.ResolveFlag("a").AfterNBuilds.ShouldBe(2);
  }

  [Fact]
  public void BuildPathFilter_WithoutFlag_ShouldApplyIgnore()
  {
    var filter = Effective(("ignore", Texts("tests/"))).BuildPathFilter([]);

    filter.Includes("src/a.cs").ShouldBeTrue();
    filter.Includes("tests/a.cs").ShouldBeFalse();
  }

  [Fact]
  public void BuildPathFilter_FlagWithoutPaths_ShouldCoverAllButIgnored()
  {
    var filter = Effective(("ignore", Texts("tests/")), ("flags", Map(("a", Map(("carryforward", Bool(true)))))))
      .BuildPathFilter(["a"]);

    filter.Includes("any/a.cs").ShouldBeTrue();
    filter.Includes("tests/a.cs").ShouldBeFalse();
  }

  [Fact]
  public void BuildPathFilter_ShouldJoinPathsAndIgnoreOfEveryFlag()
  {
    var filter = Effective(("flags", Map(
      ("a", Map(("paths", Texts("src/")), ("ignore", Texts("src/generated/")))),
      ("b", Map(("paths", Texts("lib/")))),
      ("c", Map(("carryforward", Bool(true)))))))
      .BuildPathFilter(["a", "b", "c"]);

    filter.Includes("src/a.cs").ShouldBeTrue();
    filter.Includes("lib/a.cs").ShouldBeTrue();
    filter.Includes("src/generated/a.cs").ShouldBeFalse();
    filter.Includes("other/a.cs").ShouldBeFalse();
  }

  private static CoverageConfig Effective(params (string Key, ConfigNode Value)[] entries) =>
    CoverageConfig.Create(ConfigMerger.Merge(CoverageConfigDefaults.Root, ConfigNode.FromMap(entries)));

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="FlagRuleTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Paths;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

// The expected results follow UserYaml.get_flag_configuration and flag_has_carryfoward of Codecov's
// libs/shared/shared/yaml/user_yaml.py.
public sealed class FlagRuleTests
{
  #region Methods

  [Fact]
  public void Resolve_LegacyFlagsShouldWinOverIndividualFlags()
  {
    var root = Map(
      ("flags", Map(("a", Map(("paths", Texts("legacy/")))))),
      ("flag_management", Map(
        ("default_rules", Map(("after_n_builds", Number(3)))),
        ("individual_flags", List(Map(("name", Text("a")), ("paths", Texts("individual/")), ("after_n_builds", Number(2))))))));

    var rule = FlagRule.Resolve(root, "a");

    SourcesOf(rule.Paths).ShouldBe(["legacy/"]);
    rule.AfterNBuilds.ShouldBeNull();
  }

  [Fact]
  public void Resolve_FlagWithoutPaths_ShouldCoverAll()
  {
    var root = Map(("flag_management", Map(("individual_flags", List(Map(("name", Text("a"))))))));

    var rule = FlagRule.Resolve(root, "a");

    rule.Paths.ShouldBeEmpty();
    new PathPatternSet(rule.Paths).Includes("any/path.cs").ShouldBeTrue();
  }

  [Fact]
  public void Resolve_ListedFlagWithoutPaths_ShouldInheritThePathsOfDefaultRules()
  {
    var root = Map(("flag_management", Map(
      ("default_rules", Map(("paths", Texts("shared/")))),
      ("individual_flags", List(Map(("name", Text("a"))))))));

    SourcesOf(FlagRule.Resolve(root, "a").Paths).ShouldBe(["shared/"]);
  }

  [Fact]
  public void Resolve_IndividualFlag_ShouldReplaceKeysWithoutMergingThem()
  {
    var root = Map(("flag_management", Map(
      ("default_rules", Map(("paths", Texts("shared/")), ("after_n_builds", Number(3)))),
      ("individual_flags", List(Map(("name", Text("a")), ("paths", Texts("own/"))))))));

    var rule = FlagRule.Resolve(root, "a");

    SourcesOf(rule.Paths).ShouldBe(["own/"]);
    rule.AfterNBuilds.ShouldBe(3);
  }

  [Fact]
  public void Resolve_IndividualFlagWithNullPaths_ShouldHideThePathsOfDefaultRules()
  {
    var root = Map(("flag_management", Map(
      ("default_rules", Map(("paths", Texts("shared/")))),
      ("individual_flags", List(Map(("name", Text("a")), ("paths", ConfigNode.Null)))))));

    FlagRule.Resolve(root, "a").Paths.ShouldBeEmpty();
  }

  [Fact]
  public void Resolve_UnlistedFlag_ShouldUseDefaultRules()
  {
    var root = Map(("flag_management", Map(("default_rules", Map(("after_n_builds", Number(4)))))));

    FlagRule.Resolve(root, "unlisted").AfterNBuilds.ShouldBe(4);
  }

  [Fact]
  public void Resolve_RepeatedIndividualFlag_ShouldUseTheFirstEntry()
  {
    var root = Map(("flag_management", Map(("individual_flags", List(
      Map(("name", Text("a")), ("paths", Texts("first/"))),
      Map(("name", Text("a")), ("paths", Texts("second/"))))))));

    SourcesOf(FlagRule.Resolve(root, "a").Paths).ShouldBe(["first/"]);
  }

  [Fact]
  public void Resolve_FlagNotMentioned_ShouldBeEmpty()
  {
    var rule = FlagRule.Resolve(Map(), "a");

    rule.Paths.ShouldBeEmpty();
    rule.Ignore.ShouldBeEmpty();
    rule.Statuses.ShouldBeEmpty();
    rule.AfterNBuilds.ShouldBeNull();
    rule.Carryforward.ShouldBeFalse();
    rule.Joined.ShouldBeTrue();
  }

  [Fact]
  public void Resolve_IgnoreShouldBeInverted()
  {
    var rule = FlagRule.Resolve(Map(("flags", Map(("a", Map(("ignore", Texts("tests/", "!docs/"))))))), "a");

    rule.Ignore.Select(pattern => pattern.IsNegative).ShouldBe([true, false]);
  }

  [Fact]
  public void Resolve_Statuses_ShouldComeFromDefaultRules()
  {
    var root = Map(("flag_management", Map(
      ("default_rules", Map(("statuses", List(Map(("type", Text("project"))))))),
      ("individual_flags", List(Map(("name", Text("a"))))))));

    FlagRule.Resolve(root, "a").Statuses.Single().Find("type")!.AsText().ShouldBe("project");
  }

  [Fact]
  public void Resolve_LegacyFlagWithoutCarryforward_ShouldNotInheritDefaultRules()
  {
    var root = Map(
      ("flags", Map(("a", Map(("paths", Texts("src/")))))),
      ("flag_management", Map(("default_rules", Map(("carryforward", Bool(true)))))));

    FlagRule.Resolve(root, "a").Carryforward.ShouldBeFalse();
  }

  [Fact]
  public void Resolve_EmptyLegacyFlag_ShouldHideThePathsButNotTheCarryforward()
  {
    var root = Map(
      ("flags", Map(("a", Map()))),
      ("flag_management", Map(
        ("default_rules", Map(("carryforward", Bool(true)), ("paths", Texts("shared/")))))));

    var rule = FlagRule.Resolve(root, "a");

    rule.Paths.ShouldBeEmpty();
    rule.Carryforward.ShouldBeTrue();
  }

  [Fact]
  public void Resolve_IndividualCarryforward_ShouldWinOverDefaultRules()
  {
    var root = Map(("flag_management", Map(
      ("default_rules", Map(("carryforward", Bool(true)))),
      ("individual_flags", List(Map(("name", Text("a")), ("carryforward", Bool(false))))))));

    FlagRule.Resolve(root, "a").Carryforward.ShouldBeFalse();
  }

  [Fact]
  public void Resolve_Joined_ShouldOnlyReadLegacyFlags()
  {
    var legacy = Map(("flags", Map(("a", Map(("joined", Bool(false)))))));
    var management = Map(("flag_management", Map(("default_rules", Map(("joined", Bool(false)))))));

    FlagRule.Resolve(legacy, "a").Joined.ShouldBeFalse();
    FlagRule.Resolve(management, "a").Joined.ShouldBeTrue();
  }

  private static IEnumerable<string> SourcesOf(IEnumerable<PathPattern> patterns) =>
    patterns.Select(pattern => pattern.Source);

  #endregion
}

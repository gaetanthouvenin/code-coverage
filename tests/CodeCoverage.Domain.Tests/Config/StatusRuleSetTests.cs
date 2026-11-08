// ------------------------------------------------------------------------------------------------
// <copyright file="StatusRuleSetTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

// The expected results follow NotificationService.get_statuses and default_if_true, the status notifiers
// and libs/shared/shared/validation/user_schema.py of Codecov.
public sealed class StatusRuleSetTests
{
  #region Methods

  [Fact]
  public void Build_Defaults_ShouldCreateTheProjectAndPatchStatuses()
  {
    var set = StatusRuleSet.Build(CoverageConfigDefaults.Root, []);

    set.Rules.Select(rule => (rule.Type, rule.Name)).ShouldBe(
      [(StatusType.Project, StatusRule.DefaultName), (StatusType.Patch, StatusRule.DefaultName)]);
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_ChangesDefault_ShouldBeDisabled()
  {
    StatusRuleSet.Build(CoverageConfigDefaults.Root, []).Rules.Select(rule => rule.Type).ShouldNotContain(StatusType.Changes);
  }

  [Fact]
  public void Build_ChangesEnabled_ShouldCreateTheStatus()
  {
    var set = Effective(("changes", Bool(true)));

    set.Rules.Select(rule => (rule.Type, rule.Name)).ShouldContain((StatusType.Changes, StatusRule.DefaultName));
  }

  [Fact]
  public void Build_ProjectWithoutValues_ShouldApplyTheDefaults()
  {
    var rule = Effective().Rules.First(status => status.Type is StatusType.Project);

    rule.Target.ShouldBe(Target.Auto);
    rule.Threshold.ShouldBe(0);
    rule.Base.ShouldBe(StatusBase.Auto);
    rule.Flags.ShouldBeEmpty();
    rule.Paths.ShouldBeEmpty();
    rule.Branches.ShouldBeEmpty();
    rule.OnlyPulls.ShouldBeFalse();
    rule.Informational.ShouldBeFalse();
    rule.IfNotFound.ShouldBe(IfNotFoundBehavior.Success);
    rule.RemovedCode.ShouldBe(RemovedCodeBehavior.AdjustBase);
    rule.NotUploaded.ShouldBe(NotUploadedBehavior.Include);
  }

  [Fact]
  public void Build_PatchWithoutValues_ShouldNotAdjustTheBase()
  {
    Effective().Rules.First(status => status.Type is StatusType.Patch).RemovedCode.ShouldBe(RemovedCodeBehavior.Off);
  }

  [Fact]
  public void Build_NamedEntry_ShouldReadEveryValue()
  {
    var set = Effective(("project", Map(("backend", Map(
      ("target", Text("85.5%")),
      ("threshold", Text("1%")),
      ("base", Text("parent")),
      ("flags", Texts("unit", "integration")),
      ("paths", Texts("src/")),
      ("branches", Texts("main", "release/.*")),
      ("only_pulls", Bool(true)),
      ("informational", Bool(true)),
      ("if_not_found", Text("failure")),
      ("removed_code_behavior", Text("removals_only")),
      ("flag_coverage_not_uploaded_behavior", Text("exclude")))))));

    var rule = set.Rules.Single(status => status.Type is StatusType.Project);

    rule.Name.ShouldBe("backend");
    rule.Target.Percentage.ShouldBe(85.5m);
    rule.Target.IsAuto.ShouldBeFalse();
    rule.Threshold.ShouldBe(1);
    rule.Base.ShouldBe(StatusBase.Parent);
    rule.Flags.ShouldBe(["unit", "integration"]);
    rule.Paths.Select(pattern => pattern.Source).ShouldBe(["src/"]);
    rule.Branches.Select(pattern => pattern.Source).ShouldBe(["main", "release/.*"]);
    rule.OnlyPulls.ShouldBeTrue();
    rule.Informational.ShouldBeTrue();
    rule.IfNotFound.ShouldBe(IfNotFoundBehavior.Failure);
    rule.RemovedCode.ShouldBe(RemovedCodeBehavior.RemovalsOnly);
    rule.NotUploaded.ShouldBe(NotUploadedBehavior.Exclude);
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_MapSection_ShouldReplaceTheBuiltInDefault()
  {
    var set = Effective(("project", Map(("backend", Map()), ("frontend", Bool(true)))));

    set.Rules.Where(rule => rule.Type is StatusType.Project).Select(rule => rule.Name).ShouldBe(["backend", "frontend"]);
  }

  [Fact]
  public void Build_SectionOrder_ShouldFollowTheConfiguration()
  {
    var status = Map(("patch", Bool(true)), ("project", Bool(true)));

    Build(status).Rules.Select(rule => rule.Type).ShouldBe([StatusType.Patch, StatusType.Project]);
  }

  [Fact]
  public void Build_SectionFalse_ShouldHaveNoStatus()
  {
    Effective(("project", Bool(false)), ("patch", ConfigNode.Null)).Rules.ShouldBeEmpty();
  }

  [Fact]
  public void Build_EntryFalseNullOrNotEnabled_ShouldBeSkippedWithoutWarning()
  {
    var set = Build(Map(("project", Map(
      ("off", Bool(false)),
      ("empty", ConfigNode.Null),
      ("disabled", Map(("enabled", Bool(false)))),
      ("kept", Map(("enabled", Bool(true))))))));

    set.Rules.ShouldHaveSingleItem().Name.ShouldBe("kept");
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_EntryNotAMap_ShouldWarnAndSkip()
  {
    var set = Build(Map(("project", Map(("backend", Text("x"))))));

    set.Rules.ShouldBeEmpty();
    set.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "coverage.status.project.backend", "x")]);
  }

  [Fact]
  public void Build_SectionNotAMap_ShouldWarn()
  {
    Build(Map(("project", Text("x")))).Warnings.ShouldHaveSingleItem().Key.ShouldBe("coverage.status.project");
  }

  [Fact]
  public void Build_InvalidEntryName_ShouldWarnAndSkip()
  {
    var set = Build(Map(("project", Map(("not valid", Map())))));

    set.Rules.ShouldBeEmpty();
    set.Warnings.ShouldHaveSingleItem().Detail.ShouldBe("not valid");
  }

  [Fact]
  public void Build_StatusFalse_ShouldHaveNoStatusWithoutWarning()
  {
    var set = StatusRuleSet.Build(Root(Bool(false)), []);

    set.Rules.ShouldBeEmpty();
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_StatusNull_ShouldHaveNoStatusWithoutWarning()
  {
    var set = StatusRuleSet.Build(Root(ConfigNode.Null), []);

    set.Rules.ShouldBeEmpty();
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_StatusNotAMap_ShouldWarnAndUseTheBuiltInStatuses()
  {
    var set = StatusRuleSet.Build(Root(Bool(true)), []);

    set.Rules.Select(rule => rule.Type).ShouldBe([StatusType.Project, StatusType.Patch]);
    set.Warnings.ShouldHaveSingleItem().Key.ShouldBe("coverage.status");
  }

  [Fact]
  public void Build_NoStatusSection_ShouldHaveNoStatus()
  {
    StatusRuleSet.Build(Map(), []).Rules.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("80%", 80)]
  [InlineData("80", 80)]
  [InlineData("12.5%", 12.5)]
  [InlineData("80 %", 80)]
  [InlineData("150%", 150)]
  public void Build_PercentTarget_ShouldBeRead(string written, decimal expected)
  {
    var set = Single(("target", Text(written)));

    set.Rules.Single().Target.Percentage.ShouldBe(expected);
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_NumericTarget_ShouldBeRead()
  {
    Single(("target", Number(75))).Rules.Single().Target.Percentage.ShouldBe(75);
  }

  [Fact]
  public void Build_AutoTarget_ShouldBeRead()
  {
    Single(("target", Text("auto"))).Rules.Single().Target.IsAuto.ShouldBeTrue();
  }

  [Fact]
  public void Build_NullTarget_ShouldBeAutoWithoutWarning()
  {
    var set = Single(("target", ConfigNode.Null));

    set.Rules.Single().Target.IsAuto.ShouldBeTrue();
    set.Warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("abc")]
  [InlineData("AUTO")]
  [InlineData(".5")]
  [InlineData("")]
  public void Build_InvalidTarget_ShouldWarnAndBeAuto(string written)
  {
    var set = Single(("target", Text(written)));

    set.Rules.Single().Target.IsAuto.ShouldBeTrue();
    set.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "coverage.status.project.default.target", written)]);
  }

  [Fact]
  public void Build_AutoThreshold_ShouldWarnAndBeZero()
  {
    var set = Single(("threshold", Text("auto")));

    set.Rules.Single().Threshold.ShouldBe(0);
    set.Warnings.ShouldHaveSingleItem().Key.ShouldEndWith(".threshold");
  }

  [Theory]
  [InlineData("adjust_base", RemovedCodeBehavior.AdjustBase)]
  [InlineData("removals_only", RemovedCodeBehavior.RemovalsOnly)]
  [InlineData("fully_covered_patch", RemovedCodeBehavior.FullyCoveredPatch)]
  [InlineData("off", RemovedCodeBehavior.Off)]
  public void Build_RemovedCodeBehavior_ShouldBeRead(string written, RemovedCodeBehavior expected)
  {
    Single(("removed_code_behavior", Text(written))).Rules.Single().RemovedCode.ShouldBe(expected);
  }

  [Fact]
  public void Build_RemovedCodeBehaviorFalse_ShouldBeOff()
  {
    Single(("removed_code_behavior", Bool(false))).Rules.Single().RemovedCode.ShouldBe(RemovedCodeBehavior.Off);
  }

  [Fact]
  public void Build_RemovedCodeBehaviorTrue_ShouldWarnAndUseTheDefault()
  {
    var set = Single(("removed_code_behavior", Bool(true)));

    set.Rules.Single().RemovedCode.ShouldBe(RemovedCodeBehavior.AdjustBase);
    set.Warnings.ShouldHaveSingleItem().Key.ShouldEndWith(".removed_code_behavior");
  }

  [Theory]
  [InlineData("base", "nearest")]
  [InlineData("if_not_found", "maybe")]
  [InlineData("flag_coverage_not_uploaded_behavior", "maybe")]
  [InlineData("carryforward_behavior", "maybe")]
  public void Build_InvalidChoice_ShouldWarnAndKeepTheRule(string key, string written)
  {
    var set = Single((key, Text(written)));

    set.Rules.ShouldHaveSingleItem();
    set.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, $"coverage.status.project.default.{key}", written)]);
  }

  [Theory]
  [InlineData("only_pulls")]
  [InlineData("informational")]
  [InlineData("enabled")]
  public void Build_InvalidBoolean_ShouldWarnAndKeepTheRule(string key)
  {
    var set = Single((key, Text("maybe")));

    set.Rules.ShouldHaveSingleItem();
    set.Warnings.ShouldHaveSingleItem().Key.ShouldBe($"coverage.status.project.default.{key}");
  }

  [Theory]
  [InlineData("if_ci_failed")]
  [InlineData("if_no_uploads")]
  [InlineData("measurement")]
  public void Build_KeyWithoutEffect_ShouldWarn(string key)
  {
    var set = Single((key, Text("x")));

    set.Rules.ShouldHaveSingleItem();
    set.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.WithoutEffectKey, $"coverage.status.project.default.{key}")]);
  }

  [Fact]
  public void Build_NullKeyWithoutEffect_ShouldNotWarn()
  {
    Single(("measurement", ConfigNode.Null)).Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_InvalidFlagNames_ShouldWarnAndSkipThem()
  {
    var set = Single(("flags", List(Text("unit"), Text("it's"), Number(1))));

    set.Rules.Single().Flags.ShouldBe(["unit"]);
    set.Warnings.Select(warning => warning.Key).ShouldBe(
      ["coverage.status.project.default.flags[1]", "coverage.status.project.default.flags[2]"]);
  }

  [Fact]
  public void Build_FlagsNotAList_ShouldWarn()
  {
    Single(("flags", Text("unit"))).Warnings.ShouldHaveSingleItem().Key.ShouldEndWith(".flags");
  }

  [Fact]
  public void Build_UnsupportedPathPattern_ShouldWarnWhereItIsWritten()
  {
    var set = Single(("paths", Texts("src/(?=a)")));

    set.Warnings.ShouldBe([new ConfigWarning(
      ConfigWarning.UnsupportedPattern,
      "coverage.status.project.default.paths[0]",
      "src/(?=a)")]);
  }

  [Fact]
  public void Build_DefaultRules_ShouldApplyToARuleWithoutBehavior()
  {
    var set = Build(Map(
      ("default_rules", Map(("flag_coverage_not_uploaded_behavior", Text("pass")))),
      ("project", Bool(true))));

    set.Rules.Single().NotUploaded.ShouldBe(NotUploadedBehavior.Pass);
  }

  [Fact]
  public void Build_RuleBehavior_ShouldWinOverDefaultRules()
  {
    var set = Build(Map(
      ("default_rules", Map(("flag_coverage_not_uploaded_behavior", Text("pass")))),
      ("project", Map(("default", Map(("flag_coverage_not_uploaded_behavior", Text("exclude"))))))));

    set.Rules.Single().NotUploaded.ShouldBe(NotUploadedBehavior.Exclude);
  }

  [Fact]
  public void Build_DefaultRulesNotAMap_ShouldWarn()
  {
    var set = Build(Map(("default_rules", Text("x")), ("project", Bool(true))));

    set.Rules.Single().NotUploaded.ShouldBe(NotUploadedBehavior.Include);
    set.Warnings.ShouldHaveSingleItem().Key.ShouldBe("coverage.status.default_rules");
  }

  [Fact]
  public void Build_CarryforwardBehaviorAlias_ShouldMap()
  {
    var set = Single(("carryforward_behavior", Text("exclude")));

    set.Rules.Single().NotUploaded.ShouldBe(NotUploadedBehavior.Exclude);
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_CarryforwardBehaviorAliasInDefaultRules_ShouldBeatTheBuiltInValue()
  {
    var set = Effective(("default_rules", Map(("carryforward_behavior", Text("pass")))));

    set.Rules.Select(rule => rule.NotUploaded).ShouldBe([NotUploadedBehavior.Pass, NotUploadedBehavior.Pass]);
  }

  [Fact]
  public void Build_BothNamesOnARule_ShouldPreferTheCurrentOne()
  {
    var set = Single(
      ("carryforward_behavior", Text("exclude")),
      ("flag_coverage_not_uploaded_behavior", Text("pass")));

    set.Rules.Single().NotUploaded.ShouldBe(NotUploadedBehavior.Pass);
  }

  [Fact]
  public void Build_FlagManagementStatuses_ShouldCreateFlagRules()
  {
    var root = Map(("flag_management", Map(("individual_flags", List(Map(
      ("name", Text("backend")),
      ("statuses", List(
        Map(("type", Text("project")), ("target", Text("auto")), ("threshold", Text("1%"))),
        Map(("type", Text("patch")), ("name_prefix", Text("strict-")), ("target", Number(90)))))))))));

    var set = StatusRuleSet.Build(root, ["backend"]);

    set.Rules.Select(rule => (rule.Type, rule.Name)).ShouldBe(
      [(StatusType.Project, "backend"), (StatusType.Patch, "strict-backend")]);
    set.Rules.ShouldAllBe(rule => rule.Flags.Single() == "backend");
    set.Rules[0].Threshold.ShouldBe(1);
    set.Rules[1].Target.Percentage.ShouldBe(90);
    set.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Build_FlagRules_ShouldFollowTheStatusesOfCoverage()
  {
    var root = Map(
      ("coverage", Map(("status", Map(("patch", Bool(true)))))),
      ("flag_management", Map(("individual_flags", List(Map(
        ("name", Text("a")),
        ("statuses", List(Map(("type", Text("project")))))))))));

    StatusRuleSet.Build(root, ["a"]).Rules.Select(rule => rule.Type).ShouldBe([StatusType.Patch, StatusType.Project]);
  }

  [Fact]
  public void Build_UnlistedFlag_ShouldUseTheStatusesOfDefaultRules()
  {
    var root = Map(("flag_management", Map(("default_rules", Map(("statuses", List(Map(("type", Text("project"))))))))));

    var rule = StatusRuleSet.Build(root, ["other"]).Rules.ShouldHaveSingleItem();

    rule.Name.ShouldBe("other");
    rule.Flags.ShouldBe(["other"]);
  }

  [Fact]
  public void Build_FlagNotKnownToTheRepository_ShouldHaveNoStatus()
  {
    var root = Map(("flag_management", Map(("individual_flags", List(Map(
      ("name", Text("a")),
      ("statuses", List(Map(("type", Text("project")))))))))));

    StatusRuleSet.Build(root, []).Rules.ShouldBeEmpty();
  }

  [Fact]
  public void Build_RepeatedFlag_ShouldCreateItsStatusesOnce()
  {
    var root = Map(("flag_management", Map(("default_rules", Map(("statuses", List(Map(("type", Text("project"))))))))));

    StatusRuleSet.Build(root, ["a", "a"]).Rules.ShouldHaveSingleItem();
  }

  [Fact]
  public void Build_FlagStatusNotEnabled_ShouldBeSkipped()
  {
    var root = Map(("flag_management", Map(("default_rules", Map(("statuses", List(
      Map(("type", Text("project")), ("enabled", Bool(false))))))))));

    StatusRuleSet.Build(root, ["a"]).Rules.ShouldBeEmpty();
  }

  [Fact]
  public void Build_FlagRules_ShouldUseDefaultRulesAndTheirOwnBehavior()
  {
    var root = Map(
      ("coverage", Map(("status", Map(("default_rules", Map(("flag_coverage_not_uploaded_behavior", Text("exclude")))))))),
      ("flag_management", Map(("default_rules", Map(("statuses", List(
        Map(("type", Text("project"))),
        Map(("type", Text("patch")), ("carryforward_behavior", Text("pass"))))))))));

    StatusRuleSet.Build(root, ["a"]).Rules.Select(rule => rule.NotUploaded).ShouldBe(
      [NotUploadedBehavior.Exclude, NotUploadedBehavior.Pass]);
  }

  [Fact]
  public void Build_FlagStatusProblems_ShouldWarnWhereTheyAreWrittenWhateverTheFlags()
  {
    var root = Map(("flag_management", Map(
      ("default_rules", Map(("statuses", List(Map(("type", Text("project")), ("measurement", Text("line"))))))),
      ("individual_flags", List(
        Map(("name", Text("a"))),
        Map(("name", Text("b")), ("statuses", List(
          Map(("type", Text("unknown"))),
          Text("x"),
          Map(("type", Text("patch")), ("name_prefix", Text("not valid")))))))))));

    var expected = new[]
    {
      new ConfigWarning(ConfigWarning.WithoutEffectKey, "flag_management.default_rules.statuses[0].measurement"),
      new ConfigWarning(ConfigWarning.InvalidValue, "flag_management.individual_flags[1].statuses[0].type", "unknown"),
      new ConfigWarning(ConfigWarning.InvalidValue, "flag_management.individual_flags[1].statuses[1]", "x"),
      new ConfigWarning(ConfigWarning.InvalidValue, "flag_management.individual_flags[1].statuses[2].name_prefix", "not valid"),
    };

    StatusRuleSet.Build(root, []).Warnings.ShouldBe(expected);
    StatusRuleSet.Build(root, ["a", "b", "c"]).Warnings.ShouldBe(expected);
  }

  [Fact]
  public void Build_FlagStatusesNotAList_ShouldWarn()
  {
    var root = Map(("flag_management", Map(("default_rules", Map(("statuses", Text("x")))))));

    StatusRuleSet.Build(root, ["a"]).Warnings.ShouldBe(
      [new ConfigWarning(ConfigWarning.InvalidValue, "flag_management.default_rules.statuses", "x")]);
  }

  [Fact]
  public void Build_InvalidPrefixOnAFlag_ShouldKeepTheStatusWithoutPrefix()
  {
    var root = Map(("flag_management", Map(("default_rules", Map(("statuses", List(
      Map(("type", Text("project")), ("name_prefix", Text("not valid"))))))))));

    StatusRuleSet.Build(root, ["a"]).Rules.ShouldHaveSingleItem().Name.ShouldBe("a");
  }

  [Fact]
  public void BuildStatusRules_ShouldUseTheEffectiveConfigurationAndTheFlagsGiven()
  {
    var config = CoverageConfig.Create(ConfigMerger.Merge(
      CoverageConfigDefaults.Root,
      Map(("flag_management", Map(("default_rules", Map(("statuses", List(Map(("type", Text("patch"))))))))))));

    config.BuildStatusRules(["a"]).Rules.Select(rule => rule.Name).ShouldBe(
      [StatusRule.DefaultName, StatusRule.DefaultName, "a"]);
  }

  [Fact]
  public void Create_KeyWithoutEffect_ShouldReachTheWarningsOfTheConfiguration()
  {
    var config = CoverageConfig.Create(ConfigMerger.Merge(
      CoverageConfigDefaults.Root,
      Root(Map(("project", Map(("default", Map(("if_ci_failed", Text("error"))))))))));

    config.Warnings.ShouldBe(
      [new ConfigWarning(ConfigWarning.WithoutEffectKey, "coverage.status.project.default.if_ci_failed")]);
  }

  private static ConfigNode Root(ConfigNode status) => Map(("coverage", Map(("status", status))));

  private static StatusRuleSet Build(ConfigNode status) => StatusRuleSet.Build(Root(status), []);

  private static StatusRuleSet Single(params (string Key, ConfigNode Value)[] entries) =>
    Build(Map(("project", Map(("default", Map(entries))))));

  private static StatusRuleSet Effective(params (string Key, ConfigNode Value)[] entries) =>
    StatusRuleSet.Build(ConfigMerger.Merge(CoverageConfigDefaults.Root, Root(Map(entries))), []);

  #endregion
}

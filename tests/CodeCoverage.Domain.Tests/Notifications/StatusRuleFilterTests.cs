// ------------------------------------------------------------------------------------------------
// <copyright file="StatusRuleFilterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Git;
using CodeCoverage.Domain.Notifications;

using static CodeCoverage.Domain.Tests.Notifications.StatusSamples;

namespace CodeCoverage.Domain.Tests.Notifications;

public sealed class StatusRuleFilterTests
{
  #region Methods

  [Fact]
  public void Skip_OnlyPullsWithoutPr_ShouldSkip()
  {
    var rule = Rule(StatusType.Project, ("only_pulls", Bool(true)));

    StatusRuleFilter.Skip(rule, Facts(pull: false)).ShouldBeTrue();
    StatusRuleFilter.Skip(rule, Facts(pull: true)).ShouldBeFalse();
  }

  [Fact]
  public void Skip_BasePrWithoutPr_ShouldSkip()
  {
    var rule = Rule(StatusType.Project, ("base", Text("pr")));

    StatusRuleFilter.Skip(rule, Facts(pull: false)).ShouldBeTrue();
    StatusRuleFilter.Skip(rule, Facts(pull: true)).ShouldBeFalse();
  }

  [Theory]
  [InlineData("auto")]
  [InlineData("parent")]
  public void Skip_OtherBaseWithoutPr_ShouldEvaluate(string @base)
  {
    StatusRuleFilter.Skip(Rule(StatusType.Project, ("base", Text(@base))), Facts(pull: false)).ShouldBeFalse();
  }

  [Theory]
  [InlineData("main", false)]
  [InlineData("release/1.2", false)]
  [InlineData("feature/x", true)]
  [InlineData(null, true)]
  public void Skip_Branches_ShouldKeepOnlyTheBranchesOfTheRule(string? branch, bool skipped)
  {
    var rule = Rule(StatusType.Project, ("branches", ConfigNode.FromList(Text("main"), Text("release/.*"))));

    StatusRuleFilter.Skip(rule, Facts(branch: branch)).ShouldBe(skipped);
  }

  // The `match` of Codecov: a negative pattern that matches refuses the branch, and without any positive pattern
  // every other branch is kept.
  [Theory]
  [InlineData("!main", "main", true)]
  [InlineData("!main", "feature/x", false)]
  [InlineData("^!main", "main", true)]
  [InlineData("^!main", "feature/x", false)]
  public void Skip_NegativeBranchPattern_ShouldRefuseTheBranchItNames(string pattern, string branch, bool skipped)
  {
    var rule = Rule(StatusType.Project, ("branches", ConfigNode.FromList(Text(pattern))));

    StatusRuleFilter.Skip(rule, Facts(branch: branch)).ShouldBe(skipped);
  }

  [Theory]
  [InlineData("main", true)]
  [InlineData("release/1.2", false)]
  [InlineData("feature/x", true)]
  public void Skip_NegativeAndPositiveBranchPatterns_ShouldNeedAPositiveAndNoNegative(string branch, bool skipped)
  {
    var rule = Rule(StatusType.Project, ("branches", ConfigNode.FromList(Text("!main"), Text("release/.*"))));

    StatusRuleFilter.Skip(rule, Facts(branch: branch)).ShouldBe(skipped);
  }

  [Fact]
  public void Skip_WithoutBranches_ShouldEvaluateWhateverTheBranch()
  {
    StatusRuleFilter.Skip(Rule(StatusType.Project), Facts(branch: null)).ShouldBeFalse();
  }

  [Fact]
  public void Skip_FlagAfterNBuildsNotReached_ShouldSkip()
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"))));

    StatusRuleFilter.Skip(rule, Facts(sessions: [("unit", 1)], waits: [("unit", 2)])).ShouldBeTrue();
    // Exactly reached.
    StatusRuleFilter.Skip(rule, Facts(sessions: [("unit", 2)], waits: [("unit", 2)])).ShouldBeFalse();
    StatusRuleFilter.Skip(rule, Facts(sessions: [("unit", 3)], waits: [("unit", 2)])).ShouldBeFalse();
  }

  [Fact]
  public void Skip_FlagWithoutAnySessionAndAnAfterNBuilds_ShouldSkip()
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"))));

    StatusRuleFilter.Skip(rule, Facts(waits: [("unit", 1)])).ShouldBeTrue();
  }

  // The wait of a flag only concerns the rules that select it, and flags are case-sensitive.
  [Fact]
  public void Skip_AfterNBuildsOfAnotherFlag_ShouldEvaluate()
  {
    StatusRuleFilter.Skip(Rule(StatusType.Project), Facts(waits: [("unit", 5)])).ShouldBeFalse();
    StatusRuleFilter.Skip(Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("Unit")))), Facts(sessions: [("Unit", 1)], waits: [("unit", 5)])).ShouldBeFalse();
  }

  [Fact]
  public void Skip_OneOfSeveralFlagsNotReached_ShouldSkip()
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"), Text("api"))));

    StatusRuleFilter.Skip(rule, Facts(sessions: [("unit", 3), ("api", 1)], waits: [("api", 2)])).ShouldBeTrue();
  }

  [Fact]
  public void NotUploaded_Pass_ShouldAutoPassWithSuffix()
  {
    // The flag was carried forward, not uploaded: a failing rule passes and says why.
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"))), ("flag_coverage_not_uploaded_behavior", Text("pass")), ("removed_code_behavior", Text("off")));
    var facts = Facts(sessions: [("unit", 1)]);

    StatusRuleFilter.NotUploaded(rule, facts).ShouldBe(NotUploadedBehavior.Pass);

    var status = StatusEvaluator.Evaluate(rule, Subject(View(70, 30), View(80, 20)), facts, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("70.00% (-10.00%) compared to bbbbbbb [Auto passed due to carriedforward or missing coverage]");
  }

  [Fact]
  public void NotUploaded_Exclude_ShouldPostNothing()
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"))), ("flag_coverage_not_uploaded_behavior", Text("exclude")));
    var facts = Facts(sessions: [("unit", 1)]);

    StatusRuleFilter.NotUploaded(rule, facts).ShouldBe(NotUploadedBehavior.Exclude);
    StatusEvaluator.Evaluate(rule, Subject(View(70, 30), View(80, 20)), facts, Format).ShouldBeNull();
  }

  // The default: the rule is evaluated on what was carried forward.
  [Fact]
  public void NotUploaded_Include_ShouldEvaluateTheRuleAsItIs()
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"))), ("removed_code_behavior", Text("off")));
    var facts = Facts(sessions: [("unit", 1)]);

    StatusRuleFilter.NotUploaded(rule, facts).ShouldBe(NotUploadedBehavior.Include);

    var status = StatusEvaluator.Evaluate(rule, Subject(View(70, 30), View(80, 20)), facts, Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Failure);
    status.Description.ShouldBe("70.00% (-10.00%) compared to bbbbbbb");
  }

  [Theory]
  [InlineData("pass")]
  [InlineData("exclude")]
  public void NotUploaded_FlagUploaded_ShouldEvaluateTheRuleAsItIs(string behavior)
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"), Text("api"))), ("flag_coverage_not_uploaded_behavior", Text(behavior)));

    // One uploaded flag among those of the rule is enough.
    StatusRuleFilter.NotUploaded(rule, Facts(sessions: [("unit", 1), ("api", 1)], uploaded: ["api"])).ShouldBe(NotUploadedBehavior.Include);
  }

  // A flag without any session on the commit was not uploaded either.
  [Theory]
  [InlineData("pass", NotUploadedBehavior.Pass)]
  [InlineData("exclude", NotUploadedBehavior.Exclude)]
  public void NotUploaded_FlagWithoutAnySession_ShouldFollowTheBehaviour(string behavior, NotUploadedBehavior expected)
  {
    var rule = Rule(StatusType.Patch, ("flags", ConfigNode.FromList(Text("unit"))), ("flag_coverage_not_uploaded_behavior", Text(behavior)));

    StatusRuleFilter.NotUploaded(rule, Facts()).ShouldBe(expected);
  }

  [Fact]
  public void NotUploaded_FlagWithoutAnySessionPassing_ShouldAutoPassTheNotFoundStatus()
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"))), ("flag_coverage_not_uploaded_behavior", Text("pass")), ("if_not_found", Text("failure")));

    var status = StatusEvaluator.Evaluate(rule, Subject(ViewOf(), View(80, 20)), Facts(), Format).ShouldNotBeNull();

    status.State.ShouldBe(StatusState.Success);
    status.Description.ShouldBe("No coverage information found on head [Auto passed due to carriedforward or missing coverage]");
  }

  [Theory]
  [InlineData("pass")]
  [InlineData("exclude")]
  public void NotUploaded_RuleWithoutFlag_ShouldEvaluateTheRuleAsItIs(string behavior)
  {
    var rule = Rule(StatusType.Project, ("flag_coverage_not_uploaded_behavior", Text(behavior)));

    StatusRuleFilter.NotUploaded(rule, Facts()).ShouldBe(NotUploadedBehavior.Include);
  }

  // Flags are case-sensitive: the upload of "Unit" is not an upload of "unit".
  [Fact]
  public void NotUploaded_SameFlagInAnotherCase_ShouldNotCountAsUploaded()
  {
    var rule = Rule(StatusType.Project, ("flags", ConfigNode.FromList(Text("unit"))), ("flag_coverage_not_uploaded_behavior", Text("exclude")));

    StatusRuleFilter.NotUploaded(rule, Facts(sessions: [("Unit", 1)], uploaded: ["Unit"])).ShouldBe(NotUploadedBehavior.Exclude);
  }

  [Fact]
  public void Evaluate_SkippedRule_ShouldPostNothing()
  {
    var rule = Rule(StatusType.Patch, ("only_pulls", Bool(true)));

    StatusEvaluator.Evaluate(rule, Subject(View(1, 1), View(1, 1)), Facts(pull: false), Format).ShouldBeNull();
  }

  // The status of a flag: named after the flag, with its prefix, and evaluated like any rule.
  [Fact]
  public void Evaluate_FlagStatus_ShouldBeNamedAfterItsPrefixAndItsFlag()
  {
    var root = ConfigNode.FromMap(
      (
        "flag_management",
        ConfigNode.FromMap(
          (
            "individual_flags",
            ConfigNode.FromList(
              ConfigNode.FromMap(
                ("name", Text("backend")),
                (
                  "statuses",
                  ConfigNode.FromList(
                    ConfigNode.FromMap(("type", Text("project")), ("target", Number(80))),
                    ConfigNode.FromMap(("type", Text("patch")), ("name_prefix", Text("strict-")), ("target", Number(90)))
                  )
                )
              )
            )
          )
        )
      )
    );
    var rules = StatusRuleSet.Build(root, ["backend"]).Rules.Where(static r => r.Flags.Count > 0).ToList();
    var facts = Facts(sessions: [("backend", 1)], uploaded: ["backend"]);
    var subject = Subject(View(85, 15), View(1, 1), Patch(8, 2));

    var statuses = rules.Select(rule => StatusEvaluator.Evaluate(rule, subject, facts, Format with { Prefix = "codecov" })).ToList();

    statuses.Select(static s => (s!.Context, s.State, s.Description))
            .ShouldBe(
              [
                ("codecov/project/backend", StatusState.Success, "85.00% (target 80.00%)"),
                ("codecov/patch/strict-backend", StatusState.Failure, "80.00% of diff hit (target 90.00%)"),
              ]
            );
  }

  [Fact]
  public void PathsOf_ShouldKeepTheIgnoredPathsOfTheConfigurationThenThoseOfTheRule()
  {
    var config = CoverageConfig.Create(ConfigNode.FromMap(("ignore", ConfigNode.FromList(Text("gen/")))));
    var rule = Rule(StatusType.Project, ("paths", ConfigNode.FromList(Text("src/"))));

    var paths = StatusRuleFilter.PathsOf(rule, config);

    paths.Includes("src/a.cs").ShouldBeTrue();
    paths.Includes("docs/a.md").ShouldBeFalse();
    paths.Includes("gen/a.cs").ShouldBeFalse();
    StatusRuleFilter.PathsOf(Rule(StatusType.Project), config).Includes("docs/a.md").ShouldBeTrue();
    StatusRuleFilter.PathsOf(Rule(StatusType.Project), config).Includes("gen/a.cs").ShouldBeFalse();
  }

  [Fact]
  public void Facts_From_ShouldCountCarriedForwardSessionsAndKeepUploadedFlagsApart()
  {
    var config = CoverageConfig.Create(
      ConfigNode.FromMap(("flags", ConfigNode.FromMap(("unit", ConfigNode.FromMap(("after_n_builds", Number(2)))), ("never-seen", ConfigNode.FromMap(("after_n_builds", Number(3)))))))
    );
    Session[] sessions =
    [
      Session.FromUpload(1, 1, 11, ["unit", "unit"], null, null, null, default),
      Session.CarriedForward(1, 2, HeadSha, 21, null, [1], ["unit", "api"], null, null, null, default),
      Session.FromUpload(1, 3, 13, [], null, null, null, default),
    ];

    var facts = StatusRuleFacts.From(true, "main", sessions, config, []);

    facts.HasPullRequest.ShouldBeTrue();
    facts.Branch.ShouldBe("main");
    facts.SessionsByFlag.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => (p.Key, p.Value)).ShouldBe([("api", 1), ("unit", 2)]);
    facts.UploadedFlags.ShouldBe(["unit"]);
    facts.AfterNBuildsByFlag.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => (p.Key, p.Value)).ShouldBe([("never-seen", 3), ("unit", 2)]);
  }

  // A flag that is not listed gets flag_management.default_rules, as get_flag_configuration of Codecov: the wait
  // of a flag a rule names is known even when no session carries it.
  [Fact]
  public void Facts_From_FlagOfARuleWithoutSessionNorEntry_ShouldTakeTheAfterNBuildsOfTheDefaultRules()
  {
    var backend = ConfigNode.FromMap(("flags", ConfigNode.FromList(Text("backend"))));
    var status = ConfigNode.FromMap(("project", ConfigNode.FromMap(("backend", backend))));
    var defaults = ConfigNode.FromMap(("after_n_builds", Number(2)));
    var config = CoverageConfig.Create(
      ConfigNode.FromMap(("flag_management", ConfigNode.FromMap(("default_rules", defaults))), ("coverage", ConfigNode.FromMap(("status", status))))
    );
    var rules = config.BuildStatusRules([]).Rules;
    var rule = rules.Single(static r => r.Name == "backend");
    Session[] sessions = [Session.FromUpload(1, 1, 11, ["frontend"], null, null, null, default)];

    var facts = StatusRuleFacts.From(true, "main", sessions, config, rules);

    facts.AfterNBuildsByFlag["backend"].ShouldBe(2);
    StatusRuleFilter.Skip(rule, facts).ShouldBeTrue();
  }

  [Fact]
  public void NullArguments_ShouldThrow()
  {
    var rule = Rule(StatusType.Project);
    var config = CoverageConfig.Create(ConfigNode.FromMap());

    Should.Throw<ArgumentNullException>(() => StatusRuleFilter.Skip(null!, Facts()));
    Should.Throw<ArgumentNullException>(() => StatusRuleFilter.Skip(rule, null!));
    Should.Throw<ArgumentNullException>(() => StatusRuleFilter.NotUploaded(null!, Facts()));
    Should.Throw<ArgumentNullException>(() => StatusRuleFilter.NotUploaded(rule, null!));
    Should.Throw<ArgumentNullException>(() => StatusRuleFilter.PathsOf(null!, config));
    Should.Throw<ArgumentNullException>(() => StatusRuleFilter.PathsOf(rule, null!));
    Should.Throw<ArgumentNullException>(() => StatusRuleFacts.From(true, null, null!, config, []));
    Should.Throw<ArgumentNullException>(() => StatusRuleFacts.From(true, null, [], null!, []));
    Should.Throw<ArgumentNullException>(() => StatusRuleFacts.From(true, null, [], config, null!));
    Should.Throw<ArgumentNullException>(() => StatusEvaluator.Evaluate(rule, Subject(View(1, 1), null), null!, Format));
  }

  private static StatusRuleFacts Facts(
    bool pull = true,
    string? branch = "main",
    (string Flag, int Count)[]? sessions = null,
    string[]? uploaded = null,
    (string Flag, int Wait)[]? waits = null) =>
    new(
      pull,
      branch,
      (sessions ?? []).ToDictionary(static s => s.Flag, static s => s.Count, StringComparer.Ordinal),
      new HashSet<string>(uploaded ?? [], StringComparer.Ordinal),
      (waits ?? []).ToDictionary(static w => w.Flag, static w => w.Wait, StringComparer.Ordinal)
    );

  #endregion
}
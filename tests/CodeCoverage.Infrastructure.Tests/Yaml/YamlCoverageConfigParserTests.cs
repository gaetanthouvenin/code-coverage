// ------------------------------------------------------------------------------------------------
// <copyright file="YamlCoverageConfigParserTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Infrastructure.Yaml;

namespace CodeCoverage.Infrastructure.Tests.Yaml;

public sealed class YamlCoverageConfigParserTests
{
  #region Fields

  private const string RepresentativeFile = """
    # Comments are not values.
    codecov:
      branch: main
      max_report_age: off
      require_ci_to_pass: false
      notify:
        after_n_builds: 3
        wait_for_ci: no
    coverage:
      precision: 2
      round: down
      range: "60...80"
      status:
        project:
          default: &strict
            target: 80%
            threshold: 1%
            if_not_found: success
          backend:
            <<: *strict
            target: auto
            flags: [backend]
            paths:
              - src/backend
        patch:
          default:
            target: 75.5
            informational: true
        changes: false
    comment:
      layout: "header, diff, flags, files"
      behavior: default
      require_changes: "coverage_drop OR uncovered_patch"
    ignore:
      - "**/*.Designer.cs"
      - tests
    fixes:
      - "before/::after/"
    parsers:
      cobertura:
        partials_as_hits: true
    flags:
      backend:
        paths: [src/backend]
        carryforward: true
    flag_management:
      default_rules:
        carryforward: true
        statuses:
          - type: project
            target: 90%
      individual_flags:
        - name: frontend
          paths: [src/frontend]
          statuses:
            - type: patch
              name_prefix: front-
              threshold: "2%"
    slack_app: false
    """;

  private static readonly YamlCoverageConfigParser Parser = new();

  #endregion

  #region Methods

  [Theory]
  [InlineData("coverage: [")]
  [InlineData("a: b: c")]
  [InlineData("a: *missing")]
  [InlineData("a: &x [*x]")]
  [InlineData("a: &x 1\nb: &x 2")]
  [InlineData("--- {a: 1}\n--- {b: 2}")]
  [InlineData("- a\n- b")]
  [InlineData("just a text")]
  [InlineData("a: !custom b")]
  [InlineData("a: !custom {b: 1}")]
  [InlineData("<<: 1")]
  [InlineData("<<: [1]")]
  [InlineData("? [a, b]\n: c")]
  [InlineData("a: !!s[r 5")]
  [InlineData("a: {c{ !!st  5}\nd: 1\n")]
  public void Parse_InvalidYaml_ShouldReturnError(string yaml)
  {
    var result = Parser.Parse(yaml);

    result.Root.ShouldBeNull();
    result.Error.ShouldNotBeNullOrWhiteSpace();
    result.Warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_NullText_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => Parser.Parse(null!));
  }

  [Fact]
  public void Parse_VeryDeepNesting_ShouldReturnErrorInsteadOfOverflowingTheStack()
  {
    var result = Parser.Parse("a: " + new string('[', 100_000));

    result.Root.ShouldBeNull();
    result.Error.ShouldNotBeNullOrWhiteSpace();
  }

  [Fact]
  public void Parse_ExponentialAliases_ShouldReturnError()
  {
    var yaml = new StringBuilder("level0: &level0 [x, x, x, x, x, x, x, x, x, x]\n");

    for (var level = 1; level < 12; level++)
    {
      var references = string.Join(", ", Enumerable.Repeat($"*level{level - 1}", 10));

      yaml.Append("level").Append(level).Append(": &level").Append(level).Append(" [").Append(references).AppendLine("]");
    }

    var result = Parser.Parse(yaml.ToString());

    result.Root.ShouldBeNull();
    result.Error.ShouldNotBeNullOrWhiteSpace();
  }

  [Theory]
  [InlineData("")]
  [InlineData("# only a comment\n")]
  [InlineData("---\n")]
  [InlineData("~")]
  public void Parse_EmptyFile_ShouldReturnAnEmptyMap(string yaml)
  {
    var result = Parser.Parse(yaml);

    result.Error.ShouldBeNull();
    result.Warnings.ShouldBeEmpty();
    result.Root!.Kind.ShouldBe(ConfigNodeKind.Map);
    result.Root.Entries.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_RepresentativeFile_ShouldBeReadWithoutErrorNorWarning()
  {
    var result = Parser.Parse(RepresentativeFile);

    result.Error.ShouldBeNull();
    result.Warnings.ShouldBeEmpty();
    CoverageConfig.Create(result.Root!).Warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData("unknown_key: 1", SupportedKeys.UnknownKey, "unknown_key")]
  [InlineData("coverage:\n  nope: 1", SupportedKeys.UnknownKey, "coverage.nope")]
  [InlineData("coverage: {status: {project: {backend: {nope: 1}}}}", SupportedKeys.UnknownKey, "coverage.status.project.backend.nope")]
  [InlineData("coverage: {status: {changes: {default: {target: 80}}}}", SupportedKeys.UnknownKey, "coverage.status.changes.default.target")]
  [InlineData("flag_management: {individual_flags: [{name: a}, {nope: 1}]}", SupportedKeys.UnknownKey, "flag_management.individual_flags[1].nope")]
  [InlineData("flag_management: {default_rules: {statuses: [{flags: [a]}]}}", SupportedKeys.UnknownKey, "flag_management.default_rules.statuses[0].flags")]
  [InlineData("coverage: {status: {project: {default: {if_ci_failed: error}}}}", SupportedKeys.WithoutEffectKey, "coverage.status.project.default.if_ci_failed")]
  [InlineData("coverage: {status: {patch: {default: {if_no_uploads: error}}}}", SupportedKeys.WithoutEffectKey, "coverage.status.patch.default.if_no_uploads")]
  [InlineData("flag_management: {individual_flags: [{statuses: [{measurement: line}]}]}", SupportedKeys.WithoutEffectKey, "flag_management.individual_flags[0].statuses[0].measurement")]
  [InlineData("component_management: {default_rules: {}}", SupportedKeys.WithoutEffectKey, "component_management")]
  [InlineData("codecov: {require_ci_to_pass: true}", SupportedKeys.WithoutEffectKey, "codecov.require_ci_to_pass")]
  [InlineData("codecov: {notify: {wait_for_ci: yes}}", SupportedKeys.WithoutEffectKey, "codecov.notify.wait_for_ci")]
  [InlineData("coverage: {notify: {slack: {default: {url: x}}}}", SupportedKeys.IgnoredKey, "coverage.notify")]
  [InlineData("flags: {a: {carryforward_mode: labels}}", SupportedKeys.IgnoredKey, "flags.a.carryforward_mode")]
  [InlineData("parsers: {gcov: {branch_detection: {loop: true}}}", SupportedKeys.IgnoredKey, "parsers.gcov")]
  [InlineData("comment: {show_critical_paths: true}", SupportedKeys.IgnoredKey, "comment.show_critical_paths")]
  public void Parse_UnsupportedKey_ShouldWarn(string yaml, string code, string key)
  {
    var result = Parser.Parse(yaml);

    result.Error.ShouldBeNull();
    result.Warnings.ShouldHaveSingleItem().ShouldBe(new ConfigWarning(code, key));
  }

  [Fact]
  public void Parse_UnsupportedKey_ShouldKeepItsValueAndWarnOnlyOnce()
  {
    var result = Parser.Parse("component_management: {nope: {deeper: 1}}");

    result.Warnings.ShouldHaveSingleItem();
    result.Root!.Find("component_management", "nope", "deeper")!.AsInteger().ShouldBe(1);
  }

  [Theory]
  [InlineData("slack_app: true")]
  [InlineData("slack_app: {enabled: true}")]
  [InlineData("codecov: {token: abc}")]
  [InlineData("codecov: {url: https://example.com}")]
  [InlineData("bundle_analysis: {status: informational}")]
  [InlineData("test_analytics: {flake_detection: true}")]
  [InlineData("ai_pr_review: {enabled: true}")]
  [InlineData("beta_groups: [a]")]
  [InlineData("codecov: {require_ci_to_pass: false}")]
  [InlineData("codecov: {notify: {wait_for_ci: off}}")]
  [InlineData("github_checks: false")]
  [InlineData("github_checks: {annotations: false}")]
  public void Parse_SilentlyIgnoredKey_ShouldNotWarn(string yaml)
  {
    var result = Parser.Parse(yaml);

    result.Error.ShouldBeNull();
    result.Warnings.ShouldBeEmpty();
  }

  // M5.05: github_checks is a rule now (Check Runs, behind Notifications:ChecksEnabled).
  [Theory]
  [InlineData("comment: false", true, true)]
  [InlineData("github_checks: false", false, false)]
  [InlineData("github_checks: true", true, true)]
  [InlineData("github_checks: {annotations: false}", true, false)]
  [InlineData("github_checks: {annotations: true}", true, true)]
  public void Parse_GitHubChecks_ShouldReadTheRule(string yaml, bool enabled, bool annotations)
  {
    var result = Parser.Parse(yaml);

    result.Error.ShouldBeNull();
    result.Warnings.ShouldBeEmpty();
    var rule = CoverageConfig.Create(result.Root!).GitHubChecks;
    rule.Enabled.ShouldBe(enabled);
    rule.Annotations.ShouldBe(annotations);
  }

  [Theory]
  [InlineData("80%", 80)]
  [InlineData("\"80%\"", 80)]
  [InlineData("'80.5%'", 80.5)]
  [InlineData("80", 80)]
  [InlineData("\"80\"", 80)]
  [InlineData("80.5", 80.5)]
  public void Parse_PercentStrings_ShouldParseTargets(string written, double expected)
  {
    var result = Parser.Parse($"coverage: {{status: {{project: {{default: {{target: {written}, threshold: {written}}}}}}}}}");

    result.Error.ShouldBeNull();
    result.Root!.Find("coverage", "status", "project", "default", "target")!.AsNumber().ShouldBe((decimal)expected);
    result.Root.Find("coverage", "status", "project", "default", "threshold")!.AsNumber().ShouldBe((decimal)expected);
  }

  [Fact]
  public void Parse_PercentStringsInFlagStatuses_ShouldParseTargets()
  {
    var result = Parser.Parse("flag_management: {individual_flags: [{statuses: [{target: 90%, threshold: 1.5%}]}]}");

    var status = result.Root!.Find("flag_management", "individual_flags")!.Items[0].Find("statuses")!.Items[0];

    status.Find("target")!.AsNumber().ShouldBe(90m);
    status.Find("threshold")!.AsNumber().ShouldBe(1.5m);
  }

  [Theory]
  [InlineData("auto")]
  [InlineData("high")]
  [InlineData("-5%")]
  public void Parse_TargetThatIsNotAPercentage_ShouldStayAText(string written)
  {
    var result = Parser.Parse($"coverage: {{status: {{patch: {{default: {{target: {written}}}}}}}}}");

    result.Root!.Find("coverage", "status", "patch", "default", "target")!.AsText().ShouldBe(written);
  }

  [Fact]
  public void Parse_PercentStringOutsideATarget_ShouldStayAText()
  {
    var result = Parser.Parse("coverage: {range: 60%}");

    result.Root!.Find("coverage", "range")!.AsText().ShouldBe("60%");
  }

  [Theory]
  [InlineData("true", true)]
  [InlineData("True", true)]
  [InlineData("yes", true)]
  [InlineData("ON", true)]
  [InlineData("false", false)]
  [InlineData("No", false)]
  [InlineData("off", false)]
  public void Parse_Yaml11Boolean_ShouldBeABoolean(string written, bool expected)
  {
    Value(written).AsBoolean().ShouldBe(expected);
  }

  [Theory]
  [InlineData("0", 0)]
  [InlineData("12", 12)]
  [InlineData("-12", -12)]
  [InlineData("+12", 12)]
  [InlineData("1_000", 1000)]
  [InlineData("0x1F", 31)]
  [InlineData("0b101", 5)]
  [InlineData("010", 8)]
  [InlineData("1:30", 90)]
  [InlineData("1.5", 1.5)]
  [InlineData(".5", 0.5)]
  [InlineData("80.", 80)]
  [InlineData("1.5e+2", 150)]
  [InlineData("1:30.5", 90.5)]
  public void Parse_Yaml11Number_ShouldBeANumber(string written, double expected)
  {
    Value(written).AsNumber().ShouldBe((decimal)expected);
  }

  [Theory]
  [InlineData("1e3")]
  [InlineData("-.5")]
  [InlineData("089")]
  [InlineData("60..80")]
  [InlineData(".inf")]
  [InlineData(".nan")]
  [InlineData("1.0e+400")]
  [InlineData("99999999999999999999999999999999")]
  [InlineData("y")]
  [InlineData("n")]
  [InlineData("80%")]
  public void Parse_PlainTextThatLooksLikeAValue_ShouldStayAText(string written)
  {
    Value(written).AsText().ShouldBe(written);
  }

  [Theory]
  [InlineData("\"true\"")]
  [InlineData("'12'")]
  [InlineData("!!str 12")]
  public void Parse_QuotedOrTaggedScalar_ShouldBeAText(string written)
  {
    Value(written).Kind.ShouldBe(ConfigNodeKind.Text);
  }

  [Theory]
  [InlineData("!!int '12'")]
  [InlineData("! '12'")]
  public void Parse_ScalarTaggedAsImplicit_ShouldBeResolved(string written)
  {
    Value(written).AsInteger().ShouldBe(12);
  }

  [Theory]
  [InlineData("")]
  [InlineData("~")]
  [InlineData("null")]
  [InlineData("NULL")]
  public void Parse_NullScalar_ShouldBeNull(string written)
  {
    Value(written).Kind.ShouldBe(ConfigNodeKind.Null);
  }

  [Fact]
  public void Parse_NonTextKey_ShouldKeepItsSpelling()
  {
    var root = Parser.Parse("on: 1\n12: 2\n~: 3").Root!;

    root.Entries.Select(static entry => entry.Key).ShouldBe(["on", "12", "~"]);
  }

  [Fact]
  public void Parse_RepeatedKey_ShouldKeepItsPlaceAndTakeTheLastValue()
  {
    var root = Parser.Parse("a: 1\nb: 2\na: 3").Root!;

    root.Entries.Select(static entry => entry.Key).ShouldBe(["a", "b"]);
    root.Find("a")!.AsInteger().ShouldBe(3);
  }

  [Fact]
  public void Parse_Alias_ShouldRepeatTheAnchoredValue()
  {
    var root = Parser.Parse("a: &name [x]\nb: *name\n&key c: 1\n*key : 2").Root!;

    root.Find("b")!.Items.ShouldHaveSingleItem().AsText().ShouldBe("x");
    root.Find("c")!.AsInteger().ShouldBe(2);
  }

  [Fact]
  public void Parse_MergeKey_ShouldPutMergedKeysFirstAndLetOwnKeysWin()
  {
    var root = Parser.Parse("base: &base {a: 1, b: 2}\nmap: {c: 3, <<: *base, b: 4}").Root!;
    var map = root.Find("map")!;

    map.Entries.Select(static entry => entry.Key).ShouldBe(["a", "b", "c"]);
    map.Find("a")!.AsInteger().ShouldBe(1);
    map.Find("b")!.AsInteger().ShouldBe(4);
  }

  [Fact]
  public void Parse_MergeKeyWithAList_ShouldLetTheFirstMapWin()
  {
    var root = Parser.Parse("a: &a {x: 1}\nb: &b {x: 2, y: 2}\nmap: {<<: [*a, *b]}").Root!;
    var map = root.Find("map")!;

    map.Find("x")!.AsInteger().ShouldBe(1);
    map.Find("y")!.AsInteger().ShouldBe(2);
  }

  [Fact]
  public void Parse_QuotedMergeKey_ShouldBeAnOrdinaryKey()
  {
    Parser.Parse("\"<<\": 1").Root!.Find("<<")!.AsInteger().ShouldBe(1);
  }

  [Theory]
  [InlineData("flags")]
  [InlineData("parsers")]
  [InlineData("ignore")]
  [InlineData("fixes")]
  public void Parse_RootKeyWrittenUnderCoverage_ShouldMoveToTheRoot(string key)
  {
    var root = Parser.Parse($"coverage: {{{key}: null, precision: 2}}").Root!;

    root.Find(key)!.Kind.ShouldBe(ConfigNodeKind.Null);
    root.Find("coverage", key).ShouldBeNull();
    root.Find("coverage", "precision")!.AsInteger().ShouldBe(2);
  }

  [Fact]
  public void Parse_RequireCiToPassUnderNotify_ShouldMoveUnderCodecov()
  {
    var result = Parser.Parse("codecov: {notify: {require_ci_to_pass: true, after_n_builds: 2}}");

    result.Root!.Find("codecov", "require_ci_to_pass")!.AsBoolean().ShouldBe(true);
    result.Root.Find("codecov", "notify", "require_ci_to_pass").ShouldBeNull();
    result.Root.Find("codecov", "notify", "after_n_builds")!.AsInteger().ShouldBe(2);
    result.Warnings.ShouldHaveSingleItem().Key.ShouldBe("codecov.require_ci_to_pass");
  }

  [Fact]
  public void Parse_ReservedToStringKey_ShouldBeRemoved()
  {
    var result = Parser.Parse("to_string: text\ncoverage: {precision: 2}");

    result.Warnings.ShouldBeEmpty();
    result.Root!.Find("to_string").ShouldBeNull();
  }

  private static ConfigNode Value(string written)
  {
    var result = Parser.Parse($"key: {written}");

    result.Error.ShouldBeNull();

    return result.Root!.Find("key")!;
  }

  #endregion
}

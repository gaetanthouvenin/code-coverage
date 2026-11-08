// ------------------------------------------------------------------------------------------------
// <copyright file="CommentRuleTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

// The layout follows LayoutStructure (libs/shared/shared/validation/helpers.py) and get_message_layout
// (apps/worker/services/notification/notifiers/mixins/message/sections.py) of Codecov.
public sealed class CommentRuleTests
{
  #region Fields

  private readonly List<ConfigWarning> _warnings = [];

  #endregion

  #region Methods

  [Theory]
  [InlineData("new")]
  [InlineData("spammy")]
  public void Parse_NewBehavior_ShouldFallBackToDefaultWithWarning(string behavior)
  {
    var rule = Parse(("behavior", Text(behavior)));

    rule.Behavior.ShouldBe(CommentBehavior.Default);
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.ClampedValue, "comment.behavior", behavior)]);
  }

  [Fact]
  public void Parse_BuiltInDefaults_ShouldMatchTheArchitecture()
  {
    var rule = CommentRule.Parse(CoverageConfigDefaults.Root, _warnings);

    rule.Enabled.ShouldBeTrue();
    rule.Layout.ShouldBe([CommentSection.Header, CommentSection.Diff, CommentSection.Flags, CommentSection.Files, CommentSection.Footer]);
    rule.Behavior.ShouldBe(CommentBehavior.Default);
    rule.RequireChanges.ShouldBeSameAs(RequireChangesExpression.None);
    rule.AfterNBuilds.ShouldBe(0);
    rule.FilesLimit.ShouldBe(10);
    rule.ShowCarryforwardFlags.ShouldBeFalse();
    rule.HideProjectCoverage.ShouldBeFalse();
    rule.RequireBase.ShouldBeFalse();
    rule.RequireHead.ShouldBeFalse();
    rule.Branches.ShouldBeEmpty();
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_EmptyConfiguration_ShouldUseTheBuiltInValues()
  {
    var rule = CommentRule.Parse(Map(), _warnings);

    rule.Enabled.ShouldBeTrue();
    rule.Layout.Count.ShouldBe(5);
    _warnings.ShouldBeEmpty();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void Parse_BooleanComment_ShouldDisableLikeCodecov(bool value)
  {
    var rule = CommentRule.Parse(Map(("comment", Bool(value))), _warnings);

    rule.Enabled.ShouldBeFalse();
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_NullComment_ShouldKeepTheDefaults()
  {
    var rule = CommentRule.Parse(Map(("comment", ConfigNode.Null)), _warnings);

    rule.Enabled.ShouldBeTrue();
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_ScalarComment_ShouldWarnAndKeepTheDefaults()
  {
    var rule = CommentRule.Parse(Map(("comment", Text("yes please"))), _warnings);

    rule.Enabled.ShouldBeTrue();
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "comment", "yes please")]);
  }

  [Fact]
  public void Parse_Layout_ShouldKeepTheOrderAndTheAliases()
  {
    var rule = Parse(("layout", Text(" footer,tree , flag, condensed_header,, diff, files")));

    rule.Layout.ShouldBe([
      CommentSection.Footer,
      CommentSection.Files,
      CommentSection.Flags,
      CommentSection.CondensedHeader,
      CommentSection.Diff,
      CommentSection.Files,
    ]);
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_EmptyLayout_ShouldStayEmpty()
  {
    Parse(("layout", Text(string.Empty))).Layout.ShouldBeEmpty();
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_LayoutNotAText_ShouldWarnAndUseTheDefault()
  {
    var rule = Parse(("layout", Number(3)));

    rule.Layout.Count.ShouldBe(5);
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "comment.layout")]);
  }

  [Fact]
  public void Parse_UnrenderedSection_ShouldBeDroppedWithAWarning()
  {
    var rule = Parse(("layout", Text("reach, diff")));

    rule.Layout.ShouldBe([CommentSection.Diff]);
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.ClampedValue, "comment.layout", "reach")]);
  }

  [Fact]
  public void Parse_UnknownSection_ShouldBeDroppedWithAWarning()
  {
    var rule = Parse(("layout", Text("sideways, diff")));

    rule.Layout.ShouldBe([CommentSection.Diff]);
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "comment.layout", "sideways")]);
  }

  [Theory]
  [InlineData("diff, files:5", 5)]
  [InlineData("tree: 7, footer", 7)]
  [InlineData("files:0", 0)]
  [InlineData("files", 10)]
  public void Parse_LayoutLimit_ShouldSetTheFilesLimit(string layout, int expected)
  {
    var rule = Parse(("layout", Text(layout)));

    rule.Layout.ShouldContain(CommentSection.Files);
    rule.FilesLimit.ShouldBe(expected);
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_FilesOption_ShouldSetTheFilesLimit()
  {
    Parse(("files", Number(3))).FilesLimit.ShouldBe(3);
  }

  [Fact]
  public void Parse_LayoutLimit_ShouldWinOverTheFilesOption()
  {
    Parse(("layout", Text("files:5")), ("files", Number(3))).FilesLimit.ShouldBe(5);
  }

  [Theory]
  [InlineData("files:many")]
  [InlineData("files:-1")]
  public void Parse_InvalidLayoutLimit_ShouldWarnAndKeepTheSection(string layout)
  {
    var rule = Parse(("layout", Text(layout)));

    rule.Layout.ShouldBe([CommentSection.Files]);
    rule.FilesLimit.ShouldBe(10);
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "comment.layout", layout)]);
  }

  [Fact]
  public void Parse_Once_ShouldBeRead()
  {
    Parse(("behavior", Text("once"))).Behavior.ShouldBe(CommentBehavior.Once);
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_UnknownBehavior_ShouldWarnAndUseTheDefault()
  {
    var rule = Parse(("behavior", Text("sometimes")));

    rule.Behavior.ShouldBe(CommentBehavior.Default);
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "comment.behavior", "sometimes")]);
  }

  [Fact]
  public void Parse_RequireChanges_ShouldReadTheExpression()
  {
    var rule = Parse(("require_changes", Text("uncovered_patch")));

    rule.RequireChanges.Evaluate(new RequireChangesFacts(false, 0, null, null, null, 0m)).ShouldBeFalse();
    rule.RequireChanges.Evaluate(new RequireChangesFacts(false, 1, 0m, null, null, 0m)).ShouldBeTrue();
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_InvalidRequireChanges_ShouldWarnAndNotRequireAnything()
  {
    var rule = Parse(("require_changes", Text("sometimes")));

    rule.RequireChanges.ShouldBeSameAs(RequireChangesExpression.None);
    _warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "comment.require_changes", "sometimes")]);
  }

  [Fact]
  public void Parse_Options_ShouldBeRead()
  {
    var rule = Parse(
      ("after_n_builds", Number(2)),
      ("hide_project_coverage", Bool(true)),
      ("show_carryforward_flags", Bool(true)),
      ("require_base", Bool(true)),
      ("require_head", Bool(true)),
      ("branches", Texts("main", "release/.*")));

    rule.AfterNBuilds.ShouldBe(2);
    rule.HideProjectCoverage.ShouldBeTrue();
    rule.ShowCarryforwardFlags.ShouldBeTrue();
    rule.RequireBase.ShouldBeTrue();
    rule.RequireHead.ShouldBeTrue();
    rule.Branches.Count.ShouldBe(2);
    _warnings.ShouldBeEmpty();
  }

  [Fact]
  public void Parse_InvalidOptions_ShouldWarnAndUseTheDefaults()
  {
    var rule = Parse(
      ("after_n_builds", Number(-1)),
      ("files", Text("many")),
      ("hide_project_coverage", Text("maybe")));

    rule.AfterNBuilds.ShouldBe(0);
    rule.FilesLimit.ShouldBe(10);
    rule.HideProjectCoverage.ShouldBeFalse();
    _warnings.Select(warning => warning.Key)
      .ShouldBe(["comment.after_n_builds", "comment.files", "comment.hide_project_coverage"], ignoreOrder: true);
  }

  private CommentRule Parse(params (string Key, ConfigNode Value)[] options) =>
    CommentRule.Parse(Map(("comment", Map(options))), _warnings);

  #endregion
}

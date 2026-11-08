// ------------------------------------------------------------------------------------------------
// <copyright file="CoverageConfigDefaultsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

namespace CodeCoverage.Domain.Tests.Config;

// The acceptance of M1.07: the built-in defaults are those of ARCHITECTURE section 9.2.
public sealed class CoverageConfigDefaultsTests
{
  #region Methods

  [Theory]
  [InlineData("codecov.require_ci_to_pass", false)]
  [InlineData("codecov.notify.wait_for_ci", false)]
  [InlineData("codecov.notify.manual_trigger", false)]
  [InlineData("codecov.max_report_age", false)]
  [InlineData("coverage.status.project", true)]
  [InlineData("coverage.status.patch", true)]
  [InlineData("coverage.status.changes", false)]
  [InlineData("comment.require_changes", false)]
  [InlineData("comment.show_carryforward_flags", false)]
  [InlineData("parsers.cobertura.partials_as_hits", false)]
  [InlineData("parsers.lcov.partials_as_hits", false)]
  [InlineData("parsers.jacoco.partials_as_hits", false)]
  public void Root_ShouldHoldBoolean(string key, bool expected)
  {
    Node(key).AsBoolean().ShouldBe(expected);
  }

  [Theory]
  [InlineData("codecov.notify.after_n_builds", 0)]
  [InlineData("coverage.precision", 2)]
  public void Root_ShouldHoldNumber(string key, int expected)
  {
    Node(key).AsInteger().ShouldBe(expected);
  }

  [Theory]
  [InlineData("coverage.round", "down")]
  [InlineData("coverage.status.default_rules.flag_coverage_not_uploaded_behavior", "include")]
  [InlineData("comment.layout", "header, diff, flags, files, footer")]
  [InlineData("comment.behavior", "default")]
  public void Root_ShouldHoldText(string key, string expected)
  {
    Node(key).AsText().ShouldBe(expected);
  }

  [Fact]
  public void Root_ShouldHoldTheRange()
  {
    Node("coverage.range").Items.Select(item => item.AsInteger()).ShouldBe([60, 80]);
  }

  [Fact]
  public void Create_OnDefaults_ShouldGiveNoWarning()
  {
    CoverageConfig.Create(CoverageConfigDefaults.Root).Warnings.ShouldBeEmpty();
  }

  private static ConfigNode Node(string key) => CoverageConfigDefaults.Root.Find(key.Split('.')).ShouldNotBeNull();

  #endregion
}

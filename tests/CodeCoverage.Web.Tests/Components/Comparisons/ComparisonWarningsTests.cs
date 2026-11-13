// ------------------------------------------------------------------------------------------------
// <copyright file="ComparisonWarningsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Components.Comparisons;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;
using CodeCoverage.Web.ViewModels.Comparisons;

namespace CodeCoverage.Web.Tests.Components.Comparisons;

public sealed class ComparisonWarningsTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_DiffIncomplete_ShouldWarn()
  {
    var cut = Render(Comparison(diffIncomplete: true, comparedTo: "9f8e7d6"));

    cut.Find(".cc-warning-diff").TextContent.ShouldContain(L("Comparison_DiffIncompleteTitle"));
    cut.FindAll(".cc-warning-base").ShouldBeEmpty();
  }

  // As the comment: the commit a base report is expected for is named.
  [Fact]
  public void Render_BaseMissing_ShouldWarn()
  {
    var cut = Render(Comparison(diffIncomplete: false, comparedTo: null));

    cut.Find(".cc-warning-base").TextContent.ShouldContain(L("Comparison_BaseMissingText", "a1b2c3d"));
    cut.FindAll(".cc-warning-diff").ShouldBeEmpty();
  }

  [Fact]
  public void Render_NothingToSay_ShouldWarnNothing()
  {
    var cut = Render(Comparison(diffIncomplete: false, comparedTo: "9f8e7d6"));

    cut.FindAll(".cc-banner").ShouldBeEmpty();
  }

  private static ComparisonViewModel Comparison(bool diffIncomplete, string? comparedTo) =>
    new(
      "81.23%",
      comparedTo is null ? null : "+0.33",
      1,
      comparedTo is null ? null : "80.90%",
      "72.50%",
      72.5,
      29,
      40,
      11,
      1,
      "Send.cs",
      11,
      0,
      new BaseExplanationViewModel("e4f5a6b", "a1b2c3d", "main", comparedTo, comparedTo is null ? "a1b2c3d" : null, null, null),
      [],
      false,
      new string('e', 40),
      diffIncomplete,
      [],
      [],
      []);

  private IRenderedComponent<ComparisonWarnings> Render(ComparisonViewModel comparison) =>
    Render<ComparisonWarnings>(p => p.Add(static c => c.Comparison, comparison));

  private string L(string key, params object[] arguments) => Services.GetRequiredService<IStringLocalizer<Common>>()[key, arguments];

  #endregion
}

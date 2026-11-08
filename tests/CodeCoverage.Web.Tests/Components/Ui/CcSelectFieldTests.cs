// ------------------------------------------------------------------------------------------------
// <copyright file="CcSelectFieldTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Components.Ui.Icons;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcSelectFieldTests : UiTestContext
{
  #region Fields

  private static readonly CcSelectOption[] Options = [new("main", "main"), new("develop", "develop")];

  #endregion

  #region Methods

  [Fact]
  public void Render_ShouldLinkTheLabelAndSelectTheValue()
  {
    var cut = Render<CcSelectField>(p => p.Add(f => f.Label, "Branch")
                                          .Add(f => f.Options, Options)
                                          .Add(f => f.Value, "develop")
                                          .Add(f => f.Help, "The default branch."));

    var select = cut.Find("select");
    var label = cut.Find("label");
    label.GetAttribute("for").ShouldBe(select.Id);
    label.ClassName.ShouldBe("cc-field__label");
    select.GetAttribute("aria-describedby").ShouldBe($"{select.Id}-help");
    cut.FindAll("option").Count.ShouldBe(2);
    cut.Find("option[value=develop]").HasAttribute("selected").ShouldBeTrue();
    cut.Find("option[value=main]").HasAttribute("selected").ShouldBeFalse();
    cut.Find(".cc-select__chevron").GetAttribute("aria-hidden").ShouldBe("true");
  }

  [Fact]
  public void Render_WithPlaceholder_ShouldAddAnEmptyFirstOption()
  {
    var cut = Render<CcSelectField>(p => p.Add(f => f.Options, Options).Add(f => f.Placeholder, "All branches"));

    var first = cut.FindAll("option")[0];
    first.GetAttribute("value").ShouldBe(string.Empty);
    first.TextContent.ShouldBe("All branches");
    first.HasAttribute("selected").ShouldBeTrue();
  }

  [Fact]
  public void Render_AsPillWithLeadIcon_ShouldKeepTheLabelForScreenReadersOnly()
  {
    var cut = Render<CcSelectField>(p => p.Add(f => f.Label, "Branch")
                                          .Add(f => f.Options, Options)
                                          .Add(f => f.Pill, true)
                                          .Add(f => f.LeadIcon, CcIconName.Commit));

    cut.Find("label").ClassName.ShouldBe("cc-sr-only");
    cut.Find(".cc-select").ClassName.ShouldBe("cc-select cc-select--pill cc-select--lead");
    cut.Find(".cc-select__lead").GetAttribute("aria-hidden").ShouldBe("true");
  }

  [Fact]
  public void Change_ShouldRaiseValueChanged()
  {
    string? value = null;
    var cut = Render<CcSelectField>(p => p.Add(f => f.Options, Options).Add(f => f.ValueChanged, v => value = v));

    cut.Find("select").Change("develop");

    value.ShouldBe("develop");
  }

  #endregion
}

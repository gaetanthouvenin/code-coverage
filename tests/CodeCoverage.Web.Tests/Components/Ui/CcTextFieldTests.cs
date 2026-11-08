// ------------------------------------------------------------------------------------------------
// <copyright file="CcTextFieldTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcTextFieldTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_WithLabel_ShouldLinkTheLabelToTheInput()
  {
    var cut = Render<CcTextField>(p => p.Add(f => f.Label, "Token name").Add(f => f.Value, "ci-main"));

    var input = cut.Find("input");
    cut.Find("label").GetAttribute("for").ShouldBe(input.Id);
    input.Id.ShouldNotBeNullOrEmpty();
    input.GetAttribute("value").ShouldBe("ci-main");
    input.ClassName.ShouldBe("cc-input");
    input.HasAttribute("aria-invalid").ShouldBeFalse();
    input.HasAttribute("aria-describedby").ShouldBeFalse();
  }

  [Fact]
  public void Render_WithHelpAndError_ShouldMarkInvalidAndDescribeTheInput()
  {
    var cut = Render<CcTextField>(p => p.Add(f => f.Id, "token")
                                        .Add(f => f.Help, "A GUID.")
                                        .Add(f => f.Error, "The token is not a GUID."));

    var input = cut.Find("input");
    input.GetAttribute("aria-invalid").ShouldBe("true");
    input.GetAttribute("aria-describedby").ShouldBe("token-help token-error");
    input.ClassList.ShouldContain("cc-input--invalid");
    cut.Find("#token-error").TextContent.ShouldBe("The token is not a GUID.");
    cut.Find("#token-help").TextContent.ShouldBe("A GUID.");
  }

  [Fact]
  public void Render_ReadOnlyDisabledRequired_ShouldSetTheNativeAttributes()
  {
    var cut = Render<CcTextField>(p => p.Add(f => f.ReadOnly, true).Add(f => f.Disabled, true).Add(f => f.Required, true));

    var input = cut.Find("input");
    input.HasAttribute("readonly").ShouldBeTrue();
    input.HasAttribute("disabled").ShouldBeTrue();
    input.HasAttribute("required").ShouldBeTrue();
  }

  [Fact]
  public void Input_ShouldRaiseValueChanged()
  {
    string? value = null;
    var cut = Render<CcTextField>(p => p.Add(f => f.ValueChanged, v => value = v));

    cut.Find("input").Input("ci-nightly");

    value.ShouldBe("ci-nightly");
  }

  #endregion
}

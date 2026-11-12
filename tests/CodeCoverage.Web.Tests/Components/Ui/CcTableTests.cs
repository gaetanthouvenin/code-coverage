// ------------------------------------------------------------------------------------------------
// <copyright file="CcTableTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Bunit;

using Microsoft.AspNetCore.Components;

using CodeCoverage.Web.Components.Ui;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Components.Ui;

public sealed class CcTableTests : UiTestContext
{
  #region Methods

  [Fact]
  public void Render_ShouldWrapTheHeadAndTheRowsInAScrolledTable()
  {
    var cut = Render<CcTable>(p => p.Add(t => t.AriaLabel, "Organizations")
                                    .Add(t => t.Head, (RenderFragment)(b => b.AddMarkupContent(0, "<th>Login</th>")))
                                    .Add(t => t.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<tr><td>acme</td></tr>"))));

    cut.Find(".cc-table-wrap > table.cc-table").GetAttribute("aria-label").ShouldBe("Organizations");
    cut.Find("thead th").TextContent.ShouldBe("Login");
    cut.Find("tbody td").TextContent.ShouldBe("acme");
  }

  #endregion
}

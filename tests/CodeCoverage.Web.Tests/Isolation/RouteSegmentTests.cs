// ------------------------------------------------------------------------------------------------
// <copyright file="RouteSegmentTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using CodeCoverage.Web.Components.Commits;

namespace CodeCoverage.Web.Tests.Isolation;

// The addresses of the new pages carry dots, escaped characters and spaces: they must reach the page through the whole
// host (static files, fallback, router), not only through bUnit.
[Collection(IsolationCollection.Name)]
public sealed class RouteSegmentTests(IsolationFixture fixture)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A path with a dot, a « # » and a space, escaped segment by segment as the file tree writes it, reaches the
  // annotated source page unescaped: its name is the title of the page.
  [Fact]
  public async Task Get_SourcePathWithDotsHashAndSpace_ShouldReachThePage()
  {
    var web = await fixture.WebAsync();
    using var client = web.Client();
    var address = FileTree.SourceHref($"github/{TwoOrganizationWeb.FirstOwner}/app", new string('c', 40), "src/My File#1.Tests.cs");

    address.ShouldEndWith("/blob/src/My%20File%231.Tests.cs");

    using var response = await client.GetAsync(new Uri("/" + address, UriKind.Relative), Ct);
    var markup = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    markup.ShouldContain("<title>My File#1.Tests.cs");
  }

  #endregion
}

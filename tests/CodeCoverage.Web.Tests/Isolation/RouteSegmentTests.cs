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

  // The range of a free comparison is one segment with three dots: it reaches the page, which reads both SHAs.
  [Fact]
  public async Task Get_CompareRange_ShouldReachThePage()
  {
    var web = await fixture.WebAsync();
    using var client = web.Client();
    var range = $"{new string('a', 40)}...{new string('b', 40)}";

    using var response = await client.GetAsync(new Uri($"/github/{TwoOrganizationWeb.FirstOwner}/app/compare/{range}", UriKind.Relative), Ct);
    var markup = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    markup.ShouldNotContain("data-state=\"invalid\"");
    markup.ShouldContain("data-state=\"missing\"", Case.Insensitive, "the two full SHAs reached the query, which found neither commit");
  }

  // A short range reaches the page too, which refuses it itself, before any request.
  [Fact]
  public async Task Get_ShortRange_ShouldShowTheForm()
  {
    var web = await fixture.WebAsync();
    using var client = web.Client();

    using var response = await client.GetAsync(new Uri($"/github/{TwoOrganizationWeb.FirstOwner}/app/compare/a1b2c3d...e4f5a6b", UriKind.Relative), Ct);
    var markup = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    markup.ShouldContain("data-state=\"invalid\"");
  }

  #endregion
}

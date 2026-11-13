// ------------------------------------------------------------------------------------------------
// <copyright file="CrossOrganizationPageTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.RegularExpressions;

namespace CodeCoverage.Web.Tests.Isolation;

// M3.43a: what a session of one organization receives from the pages of another. The console runs on a real database
// holding two organizations; the signed-in user belongs to the first one only.
[Collection(IsolationCollection.Name)]
public sealed partial class CrossOrganizationPageTests(IsolationFixture fixture)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A new page with no probe fails here: whoever adds a route says what an outsider receives from it.
  [Fact]
  public void EveryPage_ShouldDeclareProbe()
  {
    var declared = PageProbes.All.Select(static p => p.Route).ToHashSet(StringComparer.Ordinal);
    var pages = PageProbes.Pages();

    pages.ShouldNotBeEmpty();
    pages.Where(p => !declared.Contains(p))
         .ShouldBeEmpty("every routable page declares a probe in PageProbes.All, addressed to an organization or with the reason it addresses none");
    declared.Where(d => !pages.Contains(d))
            .ShouldBeEmpty("a probe names a page that no longer exists");
  }

  // Every page addressed to an organization: the one the user knows nothing about renders exactly as an owner that
  // never existed, so a page never tells one from the other.
  [Theory]
  [MemberData(nameof(Addressed))]
  public async Task Render_OtherOrganization_ShouldMatchNotFound(string route)
  {
    var probe = PageProbes.All.Single(p => string.Equals(p.Route, route, StringComparison.Ordinal));
    var web = await fixture.WebAsync();
    using var client = web.Client();

    var other = await ReadAsync(client, probe.Address!, TwoOrganizationWeb.SecondOwner);
    var unknown = await ReadAsync(client, probe.Address!, TwoOrganizationWeb.UnknownOwner);

    other.Status.ShouldBe(unknown.Status, $"{route}: the second organization answers {other.Status}, an unknown owner {unknown.Status}");
    other.Status.ShouldBe(HttpStatusCode.OK);

    // The page is rendered on the server: the console answers 200 and the page itself says what the visitor may see.
    other.Markup.ShouldBe(unknown.Markup, $"{route}: the pages differ once the owner of the address is set aside");
    other.Markup.ShouldContain("data-state=\"missing\"", Case.Insensitive, $"{route}: the page does not show its inaccessible state");
  }

  // The user reads the pages of its own organization: what proves the two answers above say « nothing here », rather
  // than the console being broken for everyone.
  [Fact]
  public async Task Render_OwnOrganization_ShouldShowIt()
  {
    var web = await fixture.WebAsync();
    using var client = web.Client();

    var own = await ReadAsync(client, "/github/{owner}/app", TwoOrganizationWeb.FirstOwner);

    own.Status.ShouldBe(HttpStatusCode.OK);
    own.Markup.ShouldNotContain("data-state=\"missing\"", Case.Insensitive);
  }

  // The source probe addresses a real file: an administrator of the second organization sees it, its text included.
  [Fact]
  public async Task Render_SourceOfTheSecondOrganization_ShouldShowItToItsAdministrator()
  {
    var web = await fixture.WebAsync();
    using var client = web.ClientOf(web.SecondUserId);
    var address = $"/github/{TwoOrganizationWeb.SecondOwner}/app/commit/{TwoOrganizationWeb.SecondSha}/blob/{TwoOrganizationWeb.SecondFile}";

    using var response = await client.GetAsync(new Uri(address, UriKind.Relative), Ct);
    var markup = await response.Content.ReadAsStringAsync(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    markup.ShouldNotContain("data-state=\"missing\"", Case.Insensitive);
    markup.ShouldContain(TwoOrganizationWeb.SecondFile);
    markup.ShouldContain(TwoOrganizationWeb.SourceText);
  }

  public static TheoryData<string> Addressed()
  {
    var data = new TheoryData<string>();

    foreach (var probe in PageProbes.All.Where(static p => p.IsCrossOrganization))
    {
      data.Add(probe.Route);
    }

    return data;
  }

  private static async Task<Answer> ReadAsync(HttpClient client, string address, string owner)
  {
    var asked = address.Replace("{owner}", owner, StringComparison.Ordinal);
    using var response = await client.GetAsync(new Uri(asked, UriKind.Relative), Ct);
    var markup = await response.Content.ReadAsStringAsync(Ct);

    return new Answer(response.StatusCode, Normalize(markup, owner));
  }

  // What belongs to the request, not to the answer: the markers Blazor draws for a render, the antiforgery token and
  // the owner the visitor typed, which the address of the page echoes.
  private static string Normalize(string markup, string owner)
  {
    var normalized = markup;

    foreach (var (start, end) in new[] { ("<!--Blazor", "-->"), ("<input type=\"hidden\" name=\"__RequestVerificationToken\"", ">") })
    {
      normalized = Erase(normalized, start, end);
    }

    // The identifiers Blazor draws for the elements of a render are long hexadecimal runs: they change at every
    // request and say nothing about what the page shows.
    return GeneratedId().Replace(normalized.Replace(owner, "{owner}", StringComparison.OrdinalIgnoreCase), "id");
  }

  private static string Erase(string text, string start, string end)
  {
    var result = text;
    var from = result.IndexOf(start, StringComparison.Ordinal);

    while (from >= 0)
    {
      var to = result.IndexOf(end, from, StringComparison.Ordinal);

      if (to < 0)
      {
        break;
      }

      result = string.Concat(result.AsSpan(0, from), start, end, result.AsSpan(to + end.Length));
      from = result.IndexOf(start, from + start.Length + end.Length, StringComparison.Ordinal);
    }

    return result;
  }

  #endregion

  [GeneratedRegex("[0-9a-f]{16,}")]
  private static partial Regex GeneratedId();

}

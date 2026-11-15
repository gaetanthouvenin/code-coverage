// ------------------------------------------------------------------------------------------------
// <copyright file="SetupFormPostTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;

using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

using Mediator;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Features.Settings.Commands.CompleteSetup;
using CodeCoverage.Application.Features.Settings.Commands.SaveGitHubApp;
using CodeCoverage.Application.Features.Settings.Commands.SavePublicUrls;
using CodeCoverage.Application.Features.Settings.Queries.GetSetupState;
using CodeCoverage.Application.Features.Settings.Queries.ResolveSetupAdministrators;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Setup;

// M4.02f: every form of /setup posted to the real host as a browser posts it: the page is read, each named field of
// the form is sent with what the page holds or what the operator typed, and the step reaches the server. A second
// antiforgery token or a field without a name fails here, as it failed in production.
[Collection(CodeCoverageWebCollection.Name)]
public sealed class SetupFormPostTests(CodeCoverageWebFactory factory)
{
  #region Fields

  private const string Token = "setup-token";

  private const string Pem = "-----BEGIN RSA PRIVATE KEY-----\nMIIE\n-----END RSA PRIVATE KEY-----";

  private readonly ISender _sender = A.Fake<ISender>();

  private readonly ISetupTokenIssuer _issuer = A.Fake<ISetupTokenIssuer>();

  private SetupState _state = new(false, false, false);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_Urls_ShouldSaveTheAddresses()
  {
    await using var host = Host();
    using var client = await OpenWizardAsync(host);

    using var response = await PostAsync(client, await ReadAsync(client), "urls", new()
    {
      ["Urls.WebPublicUrl"] = "https://coverage.example.com",
      ["Urls.ApiPublicUrl"] = "https://coverage-api.example.com",
    });

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    A.CallTo(() => _sender.Send(
                A<SavePublicUrlsCommand>.That.Matches(c => c.SetupToken == Token
                                                           && c.WebPublicUrl == "https://coverage.example.com"
                                                           && c.ApiPublicUrl == "https://coverage-api.example.com"),
                A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Post_ManifestStart_ShouldPrepareTheManifestUnderTheTypedName()
  {
    _state = new SetupState(false, true, false, "https://coverage.example.com", "https://coverage-api.example.com");
    await using var host = Host();
    using var client = await OpenWizardAsync(host);

    using var response = await PostAsync(client, await ReadAsync(client), "manifest-start", new() { ["appName"] = "CodeCoverage-octocat" });
    var page = await ParseAsync(response);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var manifest = page.QuerySelector("[data-form=manifest] input[name=manifest]").ShouldNotBeNull().GetAttribute("value").ShouldNotBeNull();
    manifest.ShouldContain("\"name\":\"CodeCoverage-octocat\"");
  }

  [Fact]
  public async Task Post_ManualApp_ShouldSaveTheApp()
  {
    _state = new SetupState(false, true, false, "https://coverage.example.com", "https://coverage-api.example.com");
    await using var host = Host();
    using var client = await OpenWizardAsync(host);

    using var response = await PostAsync(client, await ReadAsync(client), "manual-app", new()
    {
      ["App.AppId"] = "12345",
      ["App.ClientId"] = "Iv1.client",
      ["App.ClientSecret"] = "client-secret",
      ["App.WebhookSecret"] = "webhook-secret",
      ["App.PrivateKeyPem"] = Pem,
    });

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    A.CallTo(() => _sender.Send(
                A<SaveGitHubAppCommand>.That.Matches(c => c.SetupToken == Token && c.AppId == "12345" && c.ClientId == "Iv1.client" && c.PrivateKeyPem == Pem),
                A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // A login is shown with its identifier first; the form posted again as the page renders it ends the setup.
  [Fact]
  public async Task Post_Admins_WithALogin_ShouldConfirmThenCompleteWithTheIdentifiers()
  {
    _state = new SetupState(true, true, false);
    await using var host = Host();
    using var client = await OpenWizardAsync(host);

    using var first = await PostAsync(client, await ReadAsync(client), "admins", new() { ["Admins.AdminUserIds"] = "octocat, 42" });
    var confirmation = await ParseAsync(first);

    first.StatusCode.ShouldBe(HttpStatusCode.OK);
    confirmation.QuerySelector("[data-state=resolved] [data-admin='583231']").ShouldNotBeNull().TextContent.ShouldContain("octocat");
    A.CallTo(() => _sender.Send(A<CompleteSetupCommand>._, A<CancellationToken>._)).MustNotHaveHappened();

    using var second = await PostAsync(client, confirmation, "admins", []);

    second.StatusCode.ShouldBe(HttpStatusCode.OK);
    A.CallTo(() => _sender.Send(A<CompleteSetupCommand>.That.Matches(c => c.SetupToken == Token && c.AdminUserIds == "583231, 42"), A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  // The token moves from the address to the cookie of the wizard, as the operator's first visit does.
  private static async Task<HttpClient> OpenWizardAsync(WebApplicationFactory<Program> host)
  {
    var client = host.CreateClient();

    using var response = await client.GetAsync(new Uri($"/setup?token={Token}", UriKind.Relative), Ct);
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return client;
  }

  private static async Task<IHtmlDocument> ReadAsync(HttpClient client)
  {
    using var response = await client.GetAsync(new Uri("/setup", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await ParseAsync(response);
  }

  private static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response) =>
    await new HtmlParser().ParseDocumentAsync(await response.Content.ReadAsStringAsync(Ct));

  // Every named input of the form is sent, as a browser sends it: hidden ones (the handler, the antiforgery token) as
  // the page holds them, the others with what the operator typed. A value typed in a field without a name is lost.
  private static async Task<HttpResponseMessage> PostAsync(HttpClient client, IHtmlDocument page, string formName, Dictionary<string, string> typed)
  {
    var form = page.QuerySelectorAll<IHtmlFormElement>("form")
                   .SingleOrDefault(f => f.QuerySelector($"input[name=_handler][value='{formName}']") is not null)
                   .ShouldNotBeNull($"the page has no form '{formName}'");
    var inputs = form.QuerySelectorAll<IHtmlInputElement>("input[name]").ToList();

    typed.Keys.ShouldBeSubsetOf(inputs.Select(static i => i.Name!), "every field the operator types in must be named");

    var fields = inputs.Select(i => new KeyValuePair<string, string>(i.Name!, typed.TryGetValue(i.Name!, out var value) ? value : i.Value)).ToList();
    var action = string.IsNullOrEmpty(form.GetAttribute("action")) ? "/setup" : form.GetAttribute("action")!;

    using var content = new FormUrlEncodedContent(fields);

    return await client.PostAsync(new Uri(action, UriKind.RelativeOrAbsolute), content, Ct);
  }

  private static List<SetupAdministrator> Resolve(string? list) =>
  [
    .. (list ?? string.Empty).Split(',', StringSplitOptions.TrimEntries)
                             .Select(static e => e == "octocat" ? new SetupAdministrator("583231", "octocat") : new SetupAdministrator(e, null)),
  ];

  // The real host, whose requests reach a fake sender, with a setup token the issuer accepts.
  private WebApplicationFactory<Program> Host()
  {
    A.CallTo(() => _issuer.IsValid(Token)).Returns(true);
    A.CallTo(() => _sender.Send(A<GetSetupStateQuery>._, A<CancellationToken>._)).ReturnsLazily(() => new ValueTask<SetupState>(_state));
    A.CallTo(() => _sender.Send(A<ResolveSetupAdministratorsQuery>._, A<CancellationToken>._))
     .ReturnsLazily((IQuery<IReadOnlyList<SetupAdministrator>> q, CancellationToken _) => new ValueTask<IReadOnlyList<SetupAdministrator>>(Resolve(((ResolveSetupAdministratorsQuery)q).AdminUserIds)));

    return factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
    {
      s.AddSingleton(_issuer);
      s.AddFakeSenderRunner(_sender);
    }));
  }

  #endregion
}

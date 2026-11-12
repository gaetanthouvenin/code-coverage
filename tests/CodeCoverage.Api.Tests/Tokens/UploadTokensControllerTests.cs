// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokensControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Api.Tests.Tokens;

// The upload tokens of an organization through the API: the value in clear leaves once, at the creation.
[Collection(CodeCoverageApiCollection.Name)]
public sealed class UploadTokensControllerTests
{
  #region Fields

  private const string Value = "cvc_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private const string Path = "/api/v1/orgs/github/octo-org/upload-tokens";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly CodeCoverageApiFactory _factory;

  private readonly IOrganizationScope _scope = A.Fake<IOrganizationScope>();

  private readonly ICurrentOrganization _organization = A.Fake<ICurrentOrganization>();

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly IUploadTokenRepository _tokens = A.Fake<IUploadTokenRepository>();

  private readonly ITokenGenerator _generator = A.Fake<ITokenGenerator>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly List<UploadToken> _added = [];

  #endregion

  #region Ctors

  public UploadTokensControllerTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;

    A.CallTo(() => _scope.TryEnterAsync(A<Forge>._, A<string?>._, A<CancellationToken>._)).Returns(true);
    A.CallTo(() => _organization.Id).Returns(3L);
    A.CallTo(() => _organization.IsKnown).Returns(true);
    A.CallTo(() => _roles.ResolveAsync(ApiTokens.UserId, 3, A<CancellationToken>._)).Returns(OrganizationRole.Admin);
    A.CallTo(() => _generator.NewUploadToken()).Returns(Value);
    A.CallTo(() => _tokens.TryAddAsync(A<UploadToken>._, A<CancellationToken>._))
     .ReturnsLazily((UploadToken token, CancellationToken _) =>
     {
       typeof(UploadToken).GetProperty(nameof(UploadToken.Id))!.SetValue(token, 11L);
       _added.Add(token);
       return true;
     });
    A.CallTo(() => _tokens.ListOfOrganizationAsync(A<CancellationToken>._)).ReturnsLazily(() => _added);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Post_ShouldReturnValueOnce()
  {
    using var client = Client();

    using var created = await client.PostAsJsonAsync(new Uri(Path, UriKind.Relative), new { label = "ci" }, Ct);
    using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
    using var listed = await client.GetAsync(new Uri(Path, UriKind.Relative), Ct);
    var list = await listed.Content.ReadAsStringAsync(Ct);

    created.StatusCode.ShouldBe(HttpStatusCode.Created);
    body.RootElement.GetProperty("value").GetString().ShouldBe(Value);
    body.RootElement.GetProperty("id").GetInt64().ShouldBe(11);
    listed.StatusCode.ShouldBe(HttpStatusCode.OK);
    list.ShouldNotContain(Value[8..]);
  }

  [Fact]
  public async Task Get_ShouldNeverReturnValue()
  {
    _added.Add(UploadToken.Create(UploadTokenScope.Organization, null, "ci", "cvc_abcd", new byte[UploadToken.HashLength], false, "octocat", Now, null));
    using var client = Client();

    using var response = await client.GetAsync(new Uri(Path, UriKind.Relative), Ct);
    var json = await response.Content.ReadAsStringAsync(Ct);
    using var body = JsonDocument.Parse(json);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var item = body.RootElement.EnumerateArray().ShouldHaveSingleItem();
    item.TryGetProperty("value", out _).ShouldBeFalse();
    item.TryGetProperty("hash", out _).ShouldBeFalse();
    item.GetProperty("hint").GetString().ShouldBe("cvc_abcd");
  }

  // A participant cannot manage the tokens of the organization.
  [Fact]
  public async Task Post_AsParticipant_ShouldReturn403()
  {
    A.CallTo(() => _roles.ResolveAsync(A<long>._, A<long>._, A<CancellationToken>._)).Returns(OrganizationRole.Participant);
    using var client = Client();

    using var response = await client.PostAsJsonAsync(new Uri(Path, UriKind.Relative), new { label = "ci" }, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    _added.ShouldBeEmpty();
  }

  [Fact]
  public async Task Delete_UnknownToken_ShouldReturn404()
  {
    A.CallTo(() => _tokens.FindAsync(99, A<CancellationToken>._)).Returns((UploadToken?)null);
    using var client = Client();

    using var response = await client.DeleteAsync(new Uri($"{Path}/99", UriKind.Relative), Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  private HttpClient Client() =>
    _factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                                {
                                  s.AddApiToken(organizationId: 3);
                                  s.AddScoped(_ => _scope);
                                  s.AddScoped(_ => _organization);
                                  s.AddScoped(_ => _roles);
                                  s.AddScoped(_ => _tokens);
                                  s.AddScoped(_ => _generator);
                                  s.AddScoped(_ => _auditLog);
                                }))
            .CreateClient()
            .WithApiToken();

  #endregion
}

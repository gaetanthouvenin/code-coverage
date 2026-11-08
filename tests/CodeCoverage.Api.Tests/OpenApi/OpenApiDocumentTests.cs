// ------------------------------------------------------------------------------------------------
// <copyright file="OpenApiDocumentTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using CodeCoverage.Api.Configuration;
using CodeCoverage.Api.Tests.Support;

namespace CodeCoverage.Api.Tests.OpenApi;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class OpenApiDocumentTests(CodeCoverageApiFactory factory)
{
  #region Fields

  private const string PingPath = CodeCoverageApi.Prefix + "/diagnostics/ping";

  private static readonly WebApplicationFactoryClientOptions NoRedirect =
    new() { AllowAutoRedirect = false };

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_OpenApiJson_ShouldDescribeBearerScheme()
  {
    using var client = factory.CreateClient();

    using var document = await GetDocumentAsync(client);

    var root = document.RootElement;
    var scheme = root.GetProperty("components")
                     .GetProperty("securitySchemes")
                     .GetProperty(OpenApiConfiguration.BearerScheme);
    scheme.GetProperty("type").GetString().ShouldBe("http");
    scheme.GetProperty("scheme").GetString().ShouldBe("bearer");
    root.GetProperty("security")[0]
        .TryGetProperty(OpenApiConfiguration.BearerScheme, out var _)
        .ShouldBeTrue();
  }

  [Fact]
  public async Task Get_OpenApiJson_ShouldDocumentPingFromXmlComments()
  {
    using var client = factory.CreateClient();

    using var document = await GetDocumentAsync(client);

    var operation = document.RootElement.GetProperty("paths")
                                        .GetProperty(PingPath)
                                        .GetProperty("get");
    operation.GetProperty("operationId").GetString().ShouldBe("Ping");
    operation.GetProperty("summary").GetString().ShouldBe("Ping");
    operation.GetProperty("description").GetString().ShouldNotBeNullOrWhiteSpace();
    operation.GetProperty("tags")[0].GetString().ShouldBe(CodeCoverageApi.Tag);
  }

  [Fact]
  public async Task Get_SwaggerUi_ShouldReturnHtml()
  {
    using var client = factory.CreateClient();

    using var response =
      await client.GetAsync($"{OpenApiConfiguration.SwaggerPath}/index.html", Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
  }

  [Fact]
  public async Task Get_ScalarV1_ShouldReturnHtml()
  {
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(ScalarConfiguration.DocumentPath, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
  }

  [Fact]
  public async Task Get_Scalar_ShouldRedirectToV1()
  {
    using var client = factory.CreateClient(NoRedirect);

    using var response = await client.GetAsync(ScalarConfiguration.Path, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location?.OriginalString.ShouldBe(ScalarConfiguration.DocumentPath);
  }

  [Fact]
  public async Task Get_Swagger_InProduction_ShouldReturn404()
  {
    await using var production = factory.WithWebHostBuilder(b => b.UseEnvironment("Production"));
    using var client = production.CreateClient(NoRedirect);

    string[] paths =
    [
      OpenApiConfiguration.SwaggerPath,
      OpenApiConfiguration.DocumentPath,
      ScalarConfiguration.DocumentPath,
    ];

    foreach (var path in paths)
    {
      using var response = await client.GetAsync(path, Ct);

      response.StatusCode.ShouldBe(HttpStatusCode.NotFound, path);
    }
  }

  [Fact]
  public async Task Get_OpenApiJson_InProductionWithEnableApiDocs_ShouldReturn200()
  {
    await using var production = factory.WithWebHostBuilder(
      b => b.UseEnvironment("Production")
            .UseSetting(OpenApiConfiguration.EnableApiDocsKey, "true"));
    using var client = production.CreateClient();

    using var response = await client.GetAsync(OpenApiConfiguration.DocumentPath, Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
  }

  private static async Task<JsonDocument> GetDocumentAsync(HttpClient client)
  {
    using var response = await client.GetAsync(OpenApiConfiguration.DocumentPath, Ct);
    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
  }

  #endregion
}

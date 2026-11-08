// ------------------------------------------------------------------------------------------------
// <copyright file="PingControllerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Reflection;

using CodeCoverage.Api.Contracts.Diagnostics;
using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

namespace CodeCoverage.Api.Tests.Controllers;

[Collection(CodeCoverageApiCollection.Name)]
public sealed class PingControllerTests(CodeCoverageApiFactory factory)
{
  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Get_Ping_ShouldReturnVersion()
  {
    var expected = typeof(PingQuery).Assembly
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                                    .InformationalVersion;
    using var client = factory.CreateClient();

    using var response = await client.GetAsync(CodeCoverageApi.Prefix + "/diagnostics/ping", Ct);
    var body = await response.Content.ReadFromJsonAsync<PingResponse>(Ct);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    body.ShouldNotBeNull().Version.ShouldBe(expected);
  }

  #endregion
}

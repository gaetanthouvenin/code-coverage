// ------------------------------------------------------------------------------------------------
// <copyright file="MediatorWiringTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Smoke;

// The mediator generated in the Application resolves its internal sealed handlers from the
// Blazor Server host, without InternalsVisibleTo (ADR-001).
[Collection(CodeCoverageWebCollection.Name)]
public sealed class MediatorWiringTests
{
  #region Fields

  private readonly CodeCoverageWebFactory _factory;

  #endregion

  #region Ctors

  public MediatorWiringTests(CodeCoverageWebFactory factory)
  {
    _factory = factory;
  }

  #endregion

  #region Methods

  [Fact]
  public async Task Send_PingQuery_FromWebHost_ShouldReturnVersion()
  {
    var expected = typeof(PingQuery).Assembly
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                                    .InformationalVersion;
    await using var scope = _factory.Services.CreateAsyncScope();
    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

    var version = await sender.Send(new PingQuery(), TestContext.Current.CancellationToken);

    version.ShouldBe(expected);
  }

  #endregion
}

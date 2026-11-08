// ------------------------------------------------------------------------------------------------
// <copyright file="MediatorWiringTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Api.Tests.Support;
using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

namespace CodeCoverage.Api.Tests.Smoke;

// The mediator generated in the Application resolves its internal sealed handlers from the
// REST API host, without InternalsVisibleTo (ADR-001).
[Collection(CodeCoverageApiCollection.Name)]
public sealed class MediatorWiringTests
{
  #region Fields

  private readonly CodeCoverageApiFactory _factory;

  #endregion

  #region Ctors

  public MediatorWiringTests(CodeCoverageApiFactory factory)
  {
    _factory = factory;
  }

  #endregion

  #region Methods

  [Fact]
  public async Task Send_PingQuery_FromApiHost_ShouldReturnVersion()
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

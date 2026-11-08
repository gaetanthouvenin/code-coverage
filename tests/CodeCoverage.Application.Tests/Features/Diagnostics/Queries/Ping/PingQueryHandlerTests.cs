// ------------------------------------------------------------------------------------------------
// <copyright file="PingQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

namespace CodeCoverage.Application.Tests.Features.Diagnostics.Queries.Ping;

public sealed class PingQueryHandlerTests
{
  #region Methods

  [Fact]
  public async Task Handle_ShouldReturnTheInformationalVersionOfTheApplication()
  {
    var expected = typeof(PingQuery).Assembly
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                                    .InformationalVersion;

    var version = await new PingQueryHandler().Handle(new PingQuery(), TestContext.Current.CancellationToken);

    version.ShouldNotBeNullOrWhiteSpace();
    version.ShouldBe(expected);
  }

  [Fact]
  public async Task Handle_WithNullQuery_ShouldThrowArgumentNullException()
  {
    await Should.ThrowAsync<ArgumentNullException>(
      async () => await new PingQueryHandler().Handle(null!, TestContext.Current.CancellationToken));
  }

  #endregion
}

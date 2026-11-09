// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeGatewayProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure.Forges;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Forges;

public sealed class ForgeGatewayProviderTests : IDisposable
{
  #region Fields

  private readonly GitHubApiHarness _github = new();

  #endregion

  #region Methods

  [Fact]
  public void GetGateway_GitHub_ShouldReturnTheGitHubGateway()
  {
    var gateway = _github.Gateway;

    new ForgeGatewayProvider(gateway).GetGateway(Forge.GitHub).ShouldBeSameAs(gateway);
  }

  [Theory]
  [InlineData(Forge.AzureDevOps)]
  [InlineData(Forge.Bitbucket)]
  [InlineData(Forge.GitLab)]
  public void GetGateway_ForgeWithoutGateway_ShouldThrowNotSupported(Forge forge)
  {
    Should.Throw<NotSupportedException>(() => new ForgeGatewayProvider(_github.Gateway).GetGateway(forge));
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _github.Dispose();

  #endregion
}

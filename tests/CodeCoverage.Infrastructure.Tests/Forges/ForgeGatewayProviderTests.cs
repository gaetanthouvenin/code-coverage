// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeGatewayProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Forges;

namespace CodeCoverage.Infrastructure.Tests.Forges;

public sealed class ForgeGatewayProviderTests
{
  #region Methods

  [Theory]
  [InlineData(Forge.GitHub)]
  [InlineData(Forge.AzureDevOps)]
  [InlineData(Forge.Bitbucket)]
  [InlineData(Forge.GitLab)]
  public void GetGateway_RegisteredForge_ShouldReturnItsAdapter(Forge forge)
  {
    var adapter = A.Fake<IForgeGateway>();
    var other = A.Fake<IForgeGateway>();
    var otherForge = forge is Forge.GitHub ? Forge.GitLab : Forge.GitHub;
    var services = new ServiceCollection();
    services.AddKeyedScoped(forge, (_, _) => adapter);
    services.AddKeyedScoped(otherForge, (_, _) => other);
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    new ForgeGatewayProvider(scope.ServiceProvider).GetGateway(forge).ShouldBeSameAs(adapter);
  }

  [Theory]
  [InlineData(Forge.AzureDevOps)]
  [InlineData(Forge.Bitbucket)]
  [InlineData(Forge.GitLab)]
  public void GetGateway_UnregisteredForge_ShouldThrowNotSupported(Forge forge)
  {
    var services = new ServiceCollection();
    services.AddKeyedScoped(Forge.GitHub, (_, _) => A.Fake<IForgeGateway>());
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    Should.Throw<NotSupportedException>(() => new ForgeGatewayProvider(scope.ServiceProvider).GetGateway(forge));
  }

  [Theory]
  [InlineData(Forge.GitHub)]
  [InlineData(Forge.GitLab)]
  public void GetUserGateway_RegisteredForge_ShouldReturnItsAdapter(Forge forge)
  {
    var adapter = A.Fake<IForgeUserGateway>();
    var otherForge = forge is Forge.GitHub ? Forge.GitLab : Forge.GitHub;
    var services = new ServiceCollection();
    services.AddKeyedScoped(forge, (_, _) => adapter);
    services.AddKeyedScoped(otherForge, (_, _) => A.Fake<IForgeUserGateway>());
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    new ForgeGatewayProvider(scope.ServiceProvider).GetUserGateway(forge).ShouldBeSameAs(adapter);
  }

  [Fact]
  public void GetUserGateway_UnregisteredForge_ShouldThrowNotSupported()
  {
    var services = new ServiceCollection();
    services.AddKeyedScoped(Forge.GitHub, (_, _) => A.Fake<IForgeUserGateway>());
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    Should.Throw<NotSupportedException>(() => new ForgeGatewayProvider(scope.ServiceProvider).GetUserGateway(Forge.Bitbucket));
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeGatewayProviderTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Contracts.Forges;
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

  [Theory]
  [InlineData(Forge.GitHub)]
  [InlineData(Forge.GitLab)]
  public void GetUserTokenProvider_RegisteredForge_ShouldReturnItsAdapter(Forge forge)
  {
    var adapter = A.Fake<IForgeUserTokenProvider>();
    var otherForge = forge is Forge.GitHub ? Forge.GitLab : Forge.GitHub;
    var services = new ServiceCollection();
    services.AddKeyedSingleton(forge, (_, _) => adapter);
    services.AddKeyedSingleton(otherForge, (_, _) => A.Fake<IForgeUserTokenProvider>());
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    new ForgeGatewayProvider(scope.ServiceProvider).GetUserTokenProvider(forge).ShouldBeSameAs(adapter);
  }

  [Fact]
  public void GetUserTokenProvider_UnregisteredForge_ShouldThrowNotSupported()
  {
    var services = new ServiceCollection();
    services.AddKeyedSingleton(Forge.GitHub, (_, _) => A.Fake<IForgeUserTokenProvider>());
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    Should.Throw<NotSupportedException>(() => new ForgeGatewayProvider(scope.ServiceProvider).GetUserTokenProvider(Forge.Bitbucket));
  }

  [Fact]
  public void GetLinks_RegisteredForge_ShouldReturnItsLinks()
  {
    var links = A.Fake<IForgeLinks>();
    var services = new ServiceCollection();
    services.AddKeyedSingleton(Forge.GitHub, links);
    services.AddKeyedSingleton(Forge.GitLab, A.Fake<IForgeLinks>());
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    new ForgeGatewayProvider(scope.ServiceProvider).GetLinks(Forge.GitHub).ShouldBeSameAs(links);
  }

  [Fact]
  public void GetLinks_UnregisteredForge_ShouldThrowNotSupported()
  {
    var services = new ServiceCollection();
    services.AddKeyedSingleton(Forge.GitHub, A.Fake<IForgeLinks>());
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    Should.Throw<NotSupportedException>(() => new ForgeGatewayProvider(scope.ServiceProvider).GetLinks(Forge.AzureDevOps));
  }

  [Fact]
  public void GetCapabilities_RegisteredForge_ShouldReturnItsCapabilities()
  {
    var capabilities = new ForgeCapabilities(1000);
    var services = new ServiceCollection();
    services.AddKeyedSingleton(Forge.GitHub, capabilities);
    services.AddKeyedSingleton(Forge.GitLab, new ForgeCapabilities(2000));
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    new ForgeGatewayProvider(scope.ServiceProvider).GetCapabilities(Forge.GitHub).ShouldBeSameAs(capabilities);
  }

  [Fact]
  public void GetCapabilities_UnregisteredForge_ShouldThrowNotSupported()
  {
    var services = new ServiceCollection();
    services.AddKeyedSingleton(Forge.GitHub, new ForgeCapabilities(1000));
    using var provider = services.BuildServiceProvider(validateScopes: true);
    using var scope = provider.CreateScope();

    Should.Throw<NotSupportedException>(() => new ForgeGatewayProvider(scope.ServiceProvider).GetCapabilities(Forge.Bitbucket));
  }

  #endregion
}

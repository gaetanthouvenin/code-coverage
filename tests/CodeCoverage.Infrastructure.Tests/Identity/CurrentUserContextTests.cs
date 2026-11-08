// ------------------------------------------------------------------------------------------------
// <copyright file="CurrentUserContextTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure.Tests.Identity;

public sealed class CurrentUserContextTests
{
  #region Methods

  [Fact]
  public void Ctor_ShouldStartAnonymous()
  {
    var context = new CurrentUserContext();

    context.UserId.ShouldBeNull();
    context.Login.ShouldBeNull();
    context.IsAuthenticated.ShouldBeFalse();
    context.IsAdmin.ShouldBeFalse();
    context.IsSystem.ShouldBeFalse();
  }

  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public void SetUser_WithValidUser_ShouldAuthenticateWithoutSystem(bool isAdmin)
  {
    var context = new CurrentUserContext();

    context.SetUser(42, "octocat", isAdmin);

    context.UserId.ShouldBe(42);
    context.Login.ShouldBe("octocat");
    context.IsAuthenticated.ShouldBeTrue();
    context.IsAdmin.ShouldBe(isAdmin);
    context.IsSystem.ShouldBeFalse();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void SetUser_WithNonPositiveId_ShouldThrowArgumentOutOfRangeException(long userId)
  {
    var context = new CurrentUserContext();

    Should.Throw<ArgumentOutOfRangeException>(() => context.SetUser(userId, "octocat", isAdmin: false));
    context.IsAuthenticated.ShouldBeFalse();
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void SetUser_WithBlankLogin_ShouldThrowArgumentException(string login)
  {
    var context = new CurrentUserContext();

    Should.Throw<ArgumentException>(() => context.SetUser(42, login, isAdmin: false));
    context.IsAuthenticated.ShouldBeFalse();
  }

  [Fact]
  public void SetSystem_OnAnonymousScope_ShouldRunForTheSystemWithoutUser()
  {
    var context = new CurrentUserContext();

    context.SetSystem();

    context.IsSystem.ShouldBeTrue();
    context.IsAuthenticated.ShouldBeFalse();
    context.IsAdmin.ShouldBeFalse();
    context.UserId.ShouldBeNull();
    context.Login.ShouldBeNull();
  }

  [Fact]
  public void SetSystem_OnUserScope_ShouldThrowInvalidOperationException()
  {
    var context = new CurrentUserContext();
    context.SetUser(42, "octocat", isAdmin: true);

    Should.Throw<InvalidOperationException>(context.SetSystem);
    context.IsSystem.ShouldBeFalse();
  }

  [Fact]
  public void SetUser_OnSystemScope_ShouldThrowInvalidOperationException()
  {
    var context = new CurrentUserContext();
    context.SetSystem();

    Should.Throw<InvalidOperationException>(() => context.SetUser(42, "octocat", isAdmin: false));
    context.IsAuthenticated.ShouldBeFalse();
  }

  [Fact]
  public void SetSystem_ShouldNeverBeVisibleFromTheHosts()
  {
    var setSystem = typeof(CurrentUserContext).GetMethod(
                      nameof(CurrentUserContext.SetSystem),
                      BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    var friends = typeof(CurrentUserContext).Assembly
                                            .GetCustomAttributes<InternalsVisibleToAttribute>()
                                            .Select(a => a.AssemblyName)
                                            .ToList();

    setSystem.ShouldNotBeNull();
    setSystem.IsAssembly.ShouldBeTrue("SetSystem stays internal: the compiler keeps the Api and the Web from calling it");
    friends.ShouldNotContain(f => IsHost(f, "Api"));
    friends.ShouldNotContain(f => IsHost(f, "Web"));
  }

  [Fact]
  public async Task AddInfrastructure_ShouldShareOneContextPerScope()
  {
    var options = new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true };
    // The hosts always register their configuration: the settings store reads it.
    await using var provider = new ServiceCollection()
                               .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
                               .AddSingleton(A.Fake<ISecretProtector>())
                               .AddLogging()
                               .AddInfrastructure()
                               .BuildServiceProvider(options);
    await using var first = provider.CreateAsyncScope();
    await using var second = provider.CreateAsyncScope();

    var context = first.ServiceProvider.GetRequiredService<CurrentUserContext>();
    context.SetUser(42, "octocat", isAdmin: false);

    first.ServiceProvider.GetRequiredService<ICurrentUser>().ShouldBeSameAs(context);
    second.ServiceProvider.GetRequiredService<ICurrentUser>().IsAuthenticated.ShouldBeFalse();
  }

  private static bool IsHost(string friend, string host)
  {
    var name = friend.Split(',')[0].Trim();

    return name == $"CodeCoverage.{host}";
  }

  #endregion
}

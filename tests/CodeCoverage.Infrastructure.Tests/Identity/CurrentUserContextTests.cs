// ------------------------------------------------------------------------------------------------
// <copyright file="CurrentUserContextTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using CodeCoverage.Application.Configuration;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Operations;
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

  [Fact]
  public void SetUser_WithValidUser_ShouldAuthenticateWithoutSystem()
  {
    var context = new CurrentUserContext();

    context.SetUser(42, "octocat", Forge.GitHub, "583231");

    context.UserId.ShouldBe(42);
    context.Login.ShouldBe("octocat");
    context.Forge.ShouldBe(Forge.GitHub);
    context.ExternalId.ShouldBe("583231");
    context.IsAuthenticated.ShouldBeTrue();
    context.IsAdmin.ShouldBeFalse("a context built without the settings names no administrator");
    context.IsSystem.ShouldBeFalse();
  }

  [Fact]
  public void IsAdmin_AfterSettingChange_ShouldFollowOnNextRead()
  {
    var access = new AccessOptions();
    var context = new CurrentUserContext(Monitor(access), Monitor(new BootstrapOptions()));
    context.SetUser(42, "octocat", Forge.GitHub, "583231");

    context.IsAdmin.ShouldBeFalse();

    access.AdminUserIds = "1234, 583231";
    context.IsAdmin.ShouldBeTrue();

    access.AdminUserIds = "1234";
    context.IsAdmin.ShouldBeFalse();
  }

  [Fact]
  public void IsAdmin_NamedByTheBootstrapList_ShouldBeTrue()
  {
    var context = new CurrentUserContext(Monitor(new AccessOptions()), Monitor(new BootstrapOptions { AdminUserIds = "583231" }));
    context.SetUser(42, "octocat", Forge.GitHub, "583231");

    context.IsAdmin.ShouldBeTrue();
  }

  [Fact]
  public void IsAdmin_AnonymousOrSystem_ShouldBeFalse()
  {
    var lists = new AccessOptions { AdminUserIds = "583231" };
    var anonymous = new CurrentUserContext(Monitor(lists), Monitor(new BootstrapOptions()));
    var system = new CurrentUserContext(Monitor(lists), Monitor(new BootstrapOptions()));
    system.SetSystem();

    anonymous.IsAdmin.ShouldBeFalse();
    system.IsAdmin.ShouldBeFalse();
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void SetUser_WithNonPositiveId_ShouldThrowArgumentOutOfRangeException(long userId)
  {
    var context = new CurrentUserContext();

    Should.Throw<ArgumentOutOfRangeException>(() => context.SetUser(userId, "octocat", Forge.GitHub, "583231"));
    context.IsAuthenticated.ShouldBeFalse();
  }

  [Theory]
  [InlineData("")]
  [InlineData("  ")]
  public void SetUser_WithBlankLogin_ShouldThrowArgumentException(string login)
  {
    var context = new CurrentUserContext();

    Should.Throw<ArgumentException>(() => context.SetUser(42, login, Forge.GitHub, "583231"));
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
  public void SetCommandLine_OnAnonymousScope_ShouldRunForTheSystemUnderItsOwnActor()
  {
    var context = new CurrentUserContext();

    context.SetCommandLine();

    context.IsSystem.ShouldBeTrue();
    context.IsAuthenticated.ShouldBeFalse();
    context.IsAdmin.ShouldBeFalse();
    context.UserId.ShouldBeNull();
    context.Login.ShouldBe(AuditEntry.CommandLineActor);
    context.GetActorName().ShouldBe(AuditEntry.CommandLineActor);
  }

  [Fact]
  public void SetCommandLine_OnUserScope_ShouldThrowInvalidOperationException()
  {
    var context = new CurrentUserContext();
    context.SetUser(42, "octocat", Forge.GitHub, "583231");

    Should.Throw<InvalidOperationException>(context.SetCommandLine);
    context.IsSystem.ShouldBeFalse();
    context.Login.ShouldBe("octocat");
  }

  [Fact]
  public void SetCommandLine_ShouldNeverBeVisibleFromTheHosts()
  {
    var setCommandLine = typeof(CurrentUserContext).GetMethod(
                           nameof(CurrentUserContext.SetCommandLine),
                           BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    setCommandLine.ShouldNotBeNull();
    setCommandLine.IsAssembly.ShouldBeTrue("SetCommandLine stays internal: the hosts reach it through SystemCommandRunner only");
  }

  [Theory]
  [InlineData(false, null, AuditEntry.AnonymousActor)]
  [InlineData(false, "", AuditEntry.AnonymousActor)]
  [InlineData(false, "octocat", "octocat")]
  [InlineData(true, null, AuditEntry.SystemActor)]
  [InlineData(true, "", AuditEntry.SystemActor)]
  [InlineData(true, AuditEntry.CommandLineActor, AuditEntry.CommandLineActor)]
  public void GetActorName_ShouldNameThePrincipal(bool isSystem, string? login, string expected)
  {
    var user = A.Fake<ICurrentUser>();
    A.CallTo(() => user.IsSystem).Returns(isSystem);
    A.CallTo(() => user.Login).Returns(login);

    user.GetActorName().ShouldBe(expected);
  }

  [Fact]
  public void SetSystem_OnUserScope_ShouldThrowInvalidOperationException()
  {
    var context = new CurrentUserContext();
    context.SetUser(42, "octocat", Forge.GitHub, "583231");

    Should.Throw<InvalidOperationException>(context.SetSystem);
    context.IsSystem.ShouldBeFalse();
  }

  [Fact]
  public void SetUser_OnSystemScope_ShouldThrowInvalidOperationException()
  {
    var context = new CurrentUserContext();
    context.SetSystem();

    Should.Throw<InvalidOperationException>(() => context.SetUser(42, "octocat", Forge.GitHub, "583231"));
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
    context.SetUser(42, "octocat", Forge.GitHub, "583231");

    first.ServiceProvider.GetRequiredService<ICurrentUser>().ShouldBeSameAs(context);
    second.ServiceProvider.GetRequiredService<ICurrentUser>().IsAuthenticated.ShouldBeFalse();
  }

  private static IOptionsMonitor<T> Monitor<T>(T value)
  {
    var monitor = A.Fake<IOptionsMonitor<T>>();
    A.CallTo(() => monitor.CurrentValue).Returns(value);

    return monitor;
  }

  private static bool IsHost(string friend, string host)
  {
    var name = friend.Split(',')[0].Trim();

    return name == $"CodeCoverage.{host}";
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="SystemCommandRunnerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using Microsoft.Extensions.DependencyInjection;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Diagnostics.Commands.RunDiagnosticJob;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Features.Organizations.Commands.SuspendOrganization;
using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Administration;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure.Tests.Administration;

public sealed class SystemCommandRunnerTests
{
  #region Fields

  private const long OrganizationId = 7;

  private const string Login = "octo-org";

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly ISender _sender = A.Fake<ISender>();

  private (long? Organization, bool IsSystem) _observed;

  private string? _observedActor;

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Run_CommandOutsideList_ShouldThrow()
  {
    await using var services = Services();

    await Should.ThrowAsync<InvalidOperationException>(
      () => new SystemCommandRunner(ScopeFactory(services)).RunAsync(new RunDiagnosticJobCommand(1), Forge.GitHub, null, Ct)
    );

    A.CallTo(_sender).MustNotHaveHappened();
  }

  [Fact]
  public async Task Run_WithOrganization_ShouldEnterIt()
  {
    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitHub, Login, A<CancellationToken>._)).Returns(Organization(active: true));
    var observed = await RunObservingAsync(Login);

    observed.Organization.ShouldBe(OrganizationId);
    observed.IsSystem.ShouldBeTrue();
  }

  [Fact]
  public async Task Run_ShouldBeAuditedUnderTheCommandLineActor()
  {
    await RunObservingAsync(null);

    _observedActor.ShouldBe(AuditEntry.CommandLineActor);
    _observedActor.ShouldNotBe(AuditEntry.SystemActor, "the jobs and the command line are told apart in the audit trail");
  }

  [Fact]
  public async Task Run_WithoutOrganization_ShouldEnterNone()
  {
    var observed = await RunObservingAsync(null);

    observed.Organization.ShouldBeNull();
    observed.IsSystem.ShouldBeTrue();
    A.CallTo(_directory).MustNotHaveHappened();
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Run_OrganizationNotActive_ShouldThrowNotFound(bool known)
  {
    A.CallTo(() => _directory.FindByLoginAsync(Forge.GitHub, Login, A<CancellationToken>._)).Returns(known ? Organization(active: false) : null);
    await using var services = Services();

    var exception = await Should.ThrowAsync<NotFoundException>(
                      () => new SystemCommandRunner(ScopeFactory(services)).RunAsync(new SuspendOrganizationCommand(Forge.GitHub, Login), Forge.GitHub, Login, Ct)
                    );

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotFound);
    A.CallTo(_sender).MustNotHaveHappened();
  }

  private static IServiceScopeFactory ScopeFactory(ServiceProvider services) => services.GetRequiredService<IServiceScopeFactory>();

  private static Organization Organization(bool active)
  {
    var organization = Domain.Organizations.Organization.CreatePending(Forge.GitHub, "1001", Login, OrganizationAccountType.Organization, Now);

    typeof(Organization).GetProperty(nameof(organization.Id))!.SetValue(organization, OrganizationId);

    if (active)
    {
      organization.Admit("someone", Now);
    }

    return organization;
  }

  private ServiceProvider Services()
  {
    var services = new ServiceCollection();

    services.AddScoped<CurrentUserContext>();
    services.AddScoped<ICurrentUser>(static sp => sp.GetRequiredService<CurrentUserContext>());
    services.AddScoped<OrganizationContext>();
    services.AddScoped<ICurrentOrganization>(static sp => sp.GetRequiredService<OrganizationContext>());
    services.AddSingleton(_directory);

    // The sender of the scope, so that a test reads what the handler would see.
    services.AddScoped(sp =>
      {
        var user = sp.GetRequiredService<ICurrentUser>();
        var organization = sp.GetRequiredService<ICurrentOrganization>();

        A.CallTo(() => _sender.Send(A<ICommand<Unit>>._, A<CancellationToken>._))
         .Invokes(() =>
           {
             _observed = (organization.IsKnown ? organization.Id : null, user.IsSystem);
             _observedActor = user.GetActorName();
           })
         .Returns(ValueTask.FromResult(Unit.Value));

        return _sender;
      });

    return services.BuildServiceProvider();
  }

  private async Task<(long? Organization, bool IsSystem)> RunObservingAsync(string? login)
  {
    await using var services = Services();

    await new SystemCommandRunner(ScopeFactory(services)).RunAsync(new SuspendOrganizationCommand(Forge.GitHub, Login), Forge.GitHub, login, Ct);

    return _observed;
  }

  #endregion
}

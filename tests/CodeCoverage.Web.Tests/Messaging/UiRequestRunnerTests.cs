// ------------------------------------------------------------------------------------------------
// <copyright file="UiRequestRunnerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using FluentValidation.Results;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;
using CodeCoverage.Application.Security;
using CodeCoverage.Infrastructure;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Extensions;
using CodeCoverage.Web.Messaging;
using CodeCoverage.Web.Resources;
using CodeCoverage.Web.Tests.Support;

namespace CodeCoverage.Web.Tests.Messaging;

// The runner over the real composition of the Web host (mediator, principal, toasts, messaging).
public sealed class UiRequestRunnerTests : IAsyncDisposable
{
  #region Fields

  private readonly ServiceProvider _services;

  private readonly AsyncServiceScope _circuit;

  #endregion

  #region Ctors

  public UiRequestRunnerTests()
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddApplication();
    // The principal reads the lists of administrators from the configuration, which the hosts always register.
    services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
    services.AddInfrastructure();
    services.AddCoverageUi();
    services.AddUiMessaging();
    services.AddScoped<NavigationManager, RecordingNavigationManager>();

    _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    _circuit = _services.CreateAsyncScope();
  }

  #endregion

  #region Properties

  private UiRequestRunner Runner => _circuit.ServiceProvider.GetRequiredService<UiRequestRunner>();

  private IToastService Toasts => _circuit.ServiceProvider.GetRequiredService<IToastService>();

  private IStringLocalizer<Common> L => _circuit.ServiceProvider.GetRequiredService<IStringLocalizer<Common>>();

  private static CancellationToken Token => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task RunAsync_WithSuccess_ShouldReturnTheValueThroughTheMediator()
  {
    var expected = typeof(PingQuery).Assembly
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                                    .InformationalVersion;

    var result = await Runner.RunAsync((sender, ct) => sender.Send(new PingQuery(), ct).AsTask(), cancellationToken: Token);

    result.Succeeded.ShouldBeTrue();
    result.NotFound.ShouldBeFalse();
    result.Value.ShouldBe(expected);
    Toasts.Toasts.ShouldBeEmpty();
  }

  [Fact]
  public async Task RunAsync_WithApplicationException_ShouldShowLocalizedToast()
  {
    var result = await Runner.RunAsync<int>(
                   (_, _) => throw new ForbiddenException(SecurityErrors.Forbidden, "Forbidden for the developers."),
                   cancellationToken: Token);

    result.Succeeded.ShouldBeFalse();
    result.NotFound.ShouldBeFalse();
    result.Value.ShouldBe(0);
    var toast = Toasts.Toasts.ShouldHaveSingleItem();
    toast.Tone.ShouldBe(CcTone.Danger);
    toast.Title.ShouldBe(L["Toast_RequestFailed"].Value);
    toast.Message.ShouldBe(L[SecurityErrors.Forbidden].Value);
    toast.Message.ShouldNotBe(SecurityErrors.Forbidden, "the user never sees a raw error code");
    toast.Message!.ShouldNotContain("developers", Case.Insensitive, "the message of the exception is for the developers");
  }

  [Fact]
  public async Task RunAsync_WithUntranslatedErrorCode_ShouldFallBackToUnexpected()
  {
    await Runner.RunAsync<int>((_, _) => throw new ConflictException("Sample.NoTranslation", "Conflict."), cancellationToken: Token);

    Toasts.Toasts.ShouldHaveSingleItem().Message.ShouldBe(L["General.Unexpected"].Value);
  }

  [Fact]
  public async Task RunAsync_WithValidationException_ShouldPopulateEditContext()
  {
    var form = new EditContext(new SampleForm());

    var result = await Runner.RunAsync<int>(
                   (_, _) => throw new ValidationException(
                   [
                     new ValidationFailure(nameof(SampleForm.Name), "Name is required."),
                     new ValidationFailure(nameof(SampleForm.Name), "Name is too short."),
                     new ValidationFailure(string.Empty, "The token cannot be created."),
                   ]),
                   form,
                   Token);

    result.Succeeded.ShouldBeFalse();
    result.NotFound.ShouldBeFalse();
    form.GetValidationMessages(form.Field(nameof(SampleForm.Name))).ShouldBe(["Name is required.", "Name is too short."]);
    form.GetValidationMessages().ShouldContain("The token cannot be created.");
    Toasts.Toasts.ShouldBeEmpty("the messages go to the form, not to a toast");
  }

  [Fact]
  public async Task RunAsync_AfterValidationFailure_ShouldClearThePreviousMessages()
  {
    var form = new EditContext(new SampleForm());
    await Runner.RunAsync<int>(
      (_, _) => throw new ValidationException([new ValidationFailure(nameof(SampleForm.Name), "Name is required.")]),
      form,
      Token);

    var result = await Runner.RunAsync((_, _) => Task.FromResult(1), form, Token);

    result.Succeeded.ShouldBeTrue();
    form.GetValidationMessages().ShouldBeEmpty();
  }

  [Fact]
  public async Task RunAsync_WithValidationExceptionOutsideAForm_ShouldShowLocalizedToast()
  {
    await Runner.RunAsync<int>(
      (_, _) => throw new ValidationException([new ValidationFailure("Name", "Name is required.")]),
      cancellationToken: Token);

    Toasts.Toasts.ShouldHaveSingleItem().Message.ShouldBe(L["General.ValidationFailed"].Value);
  }

  [Fact]
  public async Task RunAsync_WithNotFound_ShouldReturnNotFoundResult()
  {
    var result = await Runner.RunAsync<int>((_, _) => throw NotFoundException.For("Repository", "42"), cancellationToken: Token);

    result.NotFound.ShouldBeTrue();
    result.Succeeded.ShouldBeFalse();
    Toasts.Toasts.ShouldBeEmpty("the page shows its « not found » empty state instead");
  }

  // The session ended: the page leaves for the sign-in page, with a full load that checks the cookie again.
  [Fact]
  public async Task RunAsync_WithUnauthorized_ShouldLeaveForTheSignInPage()
  {
    var result = await Runner.RunAsync<int>((_, _) => throw new UnauthorizedException(SecurityErrors.Unauthenticated, "No user."), cancellationToken: Token);

    result.Succeeded.ShouldBeFalse();
    result.NotFound.ShouldBeFalse();
    Toasts.Toasts.ShouldBeEmpty();
    var navigation = (RecordingNavigationManager)_circuit.ServiceProvider.GetRequiredService<NavigationManager>();
    navigation.Uri.ShouldBe("http://localhost/signin?expired=true");
    navigation.ForcedLoad.ShouldBeTrue();
  }

  [Fact]
  public async Task RunAsync_WithBug_ShouldRethrow()
  {
    var exception = await Should.ThrowAsync<InvalidOperationException>(
                      () => Runner.RunAsync<int>((_, _) => throw new InvalidOperationException("Bug."), cancellationToken: Token));

    exception.Message.ShouldBe("Bug.");
    Toasts.Toasts.ShouldBeEmpty();
  }

  [Fact]
  public async Task RunAsync_WithCancellation_ShouldRethrow()
  {
    await Should.ThrowAsync<OperationCanceledException>(
      () => Runner.RunAsync<int>((_, _) => Task.FromCanceled<int>(new CancellationToken(canceled: true)), cancellationToken: Token));

    Toasts.Toasts.ShouldBeEmpty();
  }

  #endregion

  #region Implementation of IAsyncDisposable

  public async ValueTask DisposeAsync()
  {
    await _circuit.DisposeAsync();
    await _services.DisposeAsync();
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UiRequestRunnerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;
using System.Reflection;

using FluentValidation.Results;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application;
using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Exceptions.ErrorCodes;
using CodeCoverage.Application.Features.Diagnostics.Queries.Ping;
using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Infrastructure;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Configuration;
using CodeCoverage.Web.Configuration.Authentication;
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

  private RecordingNavigationManager Navigation => (RecordingNavigationManager)_circuit.ServiceProvider.GetRequiredService<NavigationManager>();

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
                   (_, _) => throw new ConflictException(GeneralErrors.ConcurrentChange, "Conflict for the developers."),
                   cancellationToken: Token);

    result.Succeeded.ShouldBeFalse();
    result.NotFound.ShouldBeFalse();
    result.Forbidden.ShouldBeFalse();
    result.Value.ShouldBe(0);
    var toast = Toasts.Toasts.ShouldHaveSingleItem();
    toast.Tone.ShouldBe(CcTone.Danger);
    toast.Title.ShouldBe(L["Toast_RequestFailed"].Value);
    toast.Message.ShouldBe(L[GeneralErrors.ConcurrentChange].Value);
    toast.Message.ShouldNotBe(GeneralErrors.ConcurrentChange, "the user never sees a raw error code");
    toast.Message!.ShouldNotContain("developers", Case.Insensitive, "the message of the exception is for the developers");
  }

  // M3.37: a refusal of access is a state of the page, not a toast.
  [Fact]
  public async Task RunAsync_WithForbidden_ShouldReturnADeniedResultWithoutToast()
  {
    var result = await Runner.RunAsync<int>((_, _) => throw new ForbiddenException(SecurityErrors.Forbidden, "Forbidden."), cancellationToken: Token);

    result.Succeeded.ShouldBeFalse();
    result.Forbidden.ShouldBeTrue();
    result.NotFound.ShouldBeFalse();
    Toasts.Toasts.ShouldBeEmpty();
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

  // M4.02m: the message of a field is the translation of its error code, in the culture of the circuit.
  [Fact]
  public async Task RunAsync_WithValidationErrorCodeInFrench_ShouldShowTheFrenchMessageOnTheField()
  {
    var form = new EditContext(new SampleForm());
    var previous = CultureInfo.CurrentUICulture;
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

    try
    {
      await Runner.RunAsync<int>(
        (_, _) => throw new ValidationException(
        [
          new ValidationFailure(nameof(SampleForm.Name), "Give at least one numeric GitHub identifier.") { ErrorCode = "Setting.InvalidAdministrators" },
          new ValidationFailure(nameof(SampleForm.Name), "The same rule twice.") { ErrorCode = "Setting.InvalidAdministrators" },
        ]),
        form,
        Token);

      form.GetValidationMessages(form.Field(nameof(SampleForm.Name)))
          .ShouldHaveSingleItem()
          .ShouldBe("Indiquez au moins un administrateur, par ses identifiants GitHub numériques séparés par des virgules.");
    }
    finally
    {
      CultureInfo.CurrentUICulture = previous;
    }
  }

  // A code without translation, such as the default code of a FluentValidation rule, keeps the message of the rule.
  [Fact]
  public async Task RunAsync_WithUntranslatedValidationErrorCode_ShouldKeepTheMessage()
  {
    var form = new EditContext(new SampleForm());

    await Runner.RunAsync<int>(
      (_, _) => throw new ValidationException([new ValidationFailure(nameof(SampleForm.Name), "Name is required.") { ErrorCode = "NotEmptyValidator" }]),
      form,
      Token);

    form.GetValidationMessages(form.Field(nameof(SampleForm.Name))).ShouldBe(["Name is required."]);
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
    SignIn();

    var result = await Runner.RunAsync<int>((_, _) => throw NotFoundException.For("Repository", "42"), cancellationToken: Token);

    result.NotFound.ShouldBeTrue();
    result.Succeeded.ShouldBeFalse();
    Toasts.Toasts.ShouldBeEmpty("the page shows its « not found » empty state instead");
  }

  // M4.02j: a deep link followed by a visitor leads to the sign-in, which brings it back; no session ever expired.
  [Fact]
  public async Task RunAsync_WithNotFoundWhileAnonymous_ShouldLeaveForTheSignInWithoutExpired()
  {
    Navigation.NavigateTo("github/octo-org/app/commits?branch=main");

    var result = await Runner.RunAsync<int>((_, _) => throw NotFoundException.For("Repository", "42"), cancellationToken: Token);

    result.NotFound.ShouldBeFalse("a visitor is not told the address is missing before signing in");
    result.Succeeded.ShouldBeFalse();
    Navigation.Uri.ShouldBe("http://localhost/signin?returnUrl=%2Fgithub%2Focto-org%2Fapp%2Fcommits%3Fbranch%3Dmain");
    Navigation.ForcedLoad.ShouldBeTrue();
  }

  [Fact]
  public async Task RunAsync_WithUnauthorizedWhileAnonymous_ShouldNotSayTheSessionExpired()
  {
    await Runner.RunAsync<int>((_, _) => throw new UnauthorizedException(SecurityErrors.Unauthenticated, "No user."), cancellationToken: Token);

    Navigation.Uri.ShouldBe("http://localhost/signin?returnUrl=%2F");
  }

  // The cookie of the request was rejected: the session existed, and ended.
  [Fact]
  public async Task RunAsync_WithNotFoundAfterARejectedCookie_ShouldSayTheSessionExpired()
  {
    Navigation.NavigateTo("github/octo-org/app");
    _circuit.ServiceProvider.GetRequiredService<SessionState>().Reject();

    await Runner.RunAsync<int>((_, _) => throw NotFoundException.For("Repository", "42"), cancellationToken: Token);

    Navigation.Uri.ShouldBe("http://localhost/signin?returnUrl=%2Fgithub%2Focto-org%2Fapp&expired=true");
  }

  // The session of the circuit ended: the page leaves for the sign-in page, with a full load that checks the cookie again.
  [Fact]
  public async Task RunAsync_WithUnauthorizedWhileSignedIn_ShouldLeaveForTheSignInPageAndSayTheSessionExpired()
  {
    SignIn();
    Navigation.NavigateTo("account/tokens");

    var result = await Runner.RunAsync<int>((_, _) => throw new UnauthorizedException(SecurityErrors.Unauthenticated, "No user."), cancellationToken: Token);

    result.Succeeded.ShouldBeFalse();
    result.NotFound.ShouldBeFalse();
    Toasts.Toasts.ShouldBeEmpty();
    Navigation.Uri.ShouldBe("http://localhost/signin?returnUrl=%2Faccount%2Ftokens&expired=true");
    Navigation.ForcedLoad.ShouldBeTrue();
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

  private void SignIn() =>
    _circuit.ServiceProvider.GetRequiredService<CurrentUserContext>().SetUser(42, "octocat", Forge.GitHub, "583231");

  #endregion

  #region Implementation of IAsyncDisposable

  public async ValueTask DisposeAsync()
  {
    await _circuit.DisposeAsync();
    await _services.DisposeAsync();
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UiRequestRunner.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

using Mediator;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;
using CodeCoverage.Web.Localization;
using CodeCoverage.Web.Resources;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Web.Messaging;

/// <summary>
///   Sends the requests of the pages and turns the refusals of the Application into feedback for the
///   user: the central error handling of the Blazor side (CONVENTIONS §11.4).
/// </summary>
/// <remarks>
///   <para>Each request goes through the <see cref="ScopedSender" /> of the circuit. Then:</para>
///   <list type="bullet">
///     <item>
///       <description>
///         a <see cref="ValidationException" />, when a form is given, puts its messages on the
///         fields of the form (by property name; a message without property goes to the summary);
///       </description>
///     </item>
///     <item>
///       <description>
///         a <see cref="NotFoundException" /> gives a <see cref="UiResult{T}.NotFound" /> result,
///         for the page to show its « not found or not accessible » empty state;
///       </description>
///     </item>
///     <item>
///       <description>
///         any other <see cref="ApplicationException" /> shows a danger toast, whose message is the
///         translation of its <see cref="ApplicationException.ErrorCode" />;
///       </description>
///     </item>
///     <item>
///       <description>
///         anything else (a bug, a cancellation) propagates, up to the <c>CcErrorBoundary</c> of the
///         layout.
///       </description>
///     </item>
///   </list>
///   <para>
///     The runner awaits on the dispatcher of the circuit (<c>ConfigureAwait(true)</c>, like a
///     <c>.razor.cs</c>): the <see cref="EditContext" /> of a form may only be changed there.
///   </para>
/// </remarks>
/// <param name="sender">The sender of the circuit, one scope per request.</param>
/// <param name="toasts">The toasts of the circuit.</param>
/// <param name="errors">Translates the error codes.</param>
/// <param name="localizer">The strings of the application.</param>
public sealed class UiRequestRunner(
  ScopedSender sender,
  IToastService toasts,
  ErrorCodeLocalizer errors,
  IStringLocalizer<Common> localizer)
{
  #region Fields

  // The messages the runner put on each form, cleared by the next request on the same form.
  private readonly ConditionalWeakTable<EditContext, ValidationMessageStore> _stores = [];

  #endregion

  #region Methods

  /// <summary>Sends a request and reports its refusal to the user.</summary>
  /// <typeparam name="T">The type of the value the request returns.</typeparam>
  /// <param name="request">
  ///   Sends the request with the given sender, usually through a mapper extension of the
  ///   <c>Mapping</c> namespace that converts the Domain object into a view model.
  /// </param>
  /// <param name="form">
  ///   The form the request comes from, which receives the messages of a
  ///   <see cref="ValidationException" />; <see langword="null" /> outside a form.
  /// </param>
  /// <param name="cancellationToken">A token to cancel the request.</param>
  /// <returns>
  ///   The value returned, a <see cref="UiResult{T}.NotFound" /> result, or a refused result already
  ///   reported to the user.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  /// <exception cref="OperationCanceledException"><paramref name="cancellationToken" /> is canceled.</exception>
  public async Task<UiResult<T>> RunAsync<T>(
    Func<ISender, CancellationToken, Task<T>> request,
    EditContext? form = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var store = form is null ? null : ClearMessages(form);

    try
    {
      var value = await request(sender, cancellationToken).ConfigureAwait(true);
      return UiResult<T>.Success(value);
    }
    catch (ValidationException exception) when (form is not null && store is not null)
    {
      ShowMessages(form, store, exception);
      return UiResult<T>.Refused();
    }
    catch (NotFoundException)
    {
      return UiResult<T>.Missing();
    }
    catch (ApplicationException exception)
    {
      toasts.Show(CcTone.Danger, localizer["Toast_RequestFailed"], errors.Localize(exception.ErrorCode));
      return UiResult<T>.Refused();
    }
  }

  /// <summary>Removes the messages the runner put on a form by a previous request.</summary>
  /// <param name="form">The form.</param>
  /// <returns>The message store of the runner on this form.</returns>
  private ValidationMessageStore ClearMessages(EditContext form)
  {
    if (!_stores.TryGetValue(form, out var store))
    {
      store = new ValidationMessageStore(form);
      _stores.Add(form, store);
      return store;
    }

    store.Clear();
    form.NotifyValidationStateChanged();
    return store;
  }

  /// <summary>Puts the messages of a validation failure on the fields of a form.</summary>
  /// <param name="form">The form.</param>
  /// <param name="store">The message store of the runner on this form.</param>
  /// <param name="exception">The validation failure.</param>
  private static void ShowMessages(EditContext form, ValidationMessageStore store, ValidationException exception)
  {
    foreach (var (property, messages) in exception.Errors)
    {
      store.Add(new FieldIdentifier(form.Model, property), messages);
    }

    form.NotifyValidationStateChanged();
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="ScopedSender.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;

using Mediator;

namespace CodeCoverage.Web.Messaging;

/// <summary>
///   Sends each request of a Blazor circuit in a dependency-injection scope of its own, as the user
///   of the circuit.
/// </summary>
/// <remarks>
///   <para>
///     The scope of a circuit lives as long as the browser tab: a <c>DbContext</c> resolved there
///     would serve one operation at a time (several parts of a page load at once) and would keep
///     serving what it read first. A scope per request gives every request a fresh context.
///   </para>
///   <para>
///     Each new scope is prepared by the <see cref="ISendScopeInitializer" />, which copies the user
///     of the circuit into the principal of the scope, then disposed once the request is handled
///     (for a stream, once it is enumerated). The pages never use it directly: they go through
///     <see cref="UiRequestRunner" />, which hands it to them as an <see cref="ISender" />.
///   </para>
/// </remarks>
/// <param name="scopes">Opens the scope of each request.</param>
/// <param name="initializer">Prepares each scope before the request is sent in it.</param>
public sealed class ScopedSender(IServiceScopeFactory scopes, ISendScopeInitializer initializer) : ISender
{
  #region Methods

  /// <summary>Opens and prepares the scope of one request.</summary>
  /// <returns>The scope, to dispose once the request is handled.</returns>
  private AsyncServiceScope OpenScope()
  {
    var scope = scopes.CreateAsyncScope();

    try
    {
      initializer.Initialize(scope.ServiceProvider);
      return scope;
    }
    catch
    {
      scope.Dispose();
      throw;
    }
  }

  /// <summary>Sends a request in a scope of its own and disposes the scope.</summary>
  /// <typeparam name="TResponse">The type of the response.</typeparam>
  /// <param name="send">Sends the request with the sender of the scope.</param>
  /// <returns>The response.</returns>
  private async ValueTask<TResponse> SendInScopeAsync<TResponse>(Func<ISender, ValueTask<TResponse>> send)
  {
    var scope = OpenScope();

    await using (scope.ConfigureAwait(false))
    {
      return await send(scope.ServiceProvider.GetRequiredService<ISender>()).ConfigureAwait(false);
    }
  }

  /// <summary>Enumerates a stream in a scope of its own, disposed once the stream ends.</summary>
  /// <typeparam name="TResponse">The type of the items.</typeparam>
  /// <param name="create">Creates the stream with the sender of the scope.</param>
  /// <param name="cancellationToken">A token to cancel the enumeration.</param>
  /// <returns>The items of the stream.</returns>
  private async IAsyncEnumerable<TResponse> StreamInScopeAsync<TResponse>(
    Func<ISender, IAsyncEnumerable<TResponse>> create,
    [EnumeratorCancellation] CancellationToken cancellationToken)
  {
    var scope = OpenScope();

    await using (scope.ConfigureAwait(false))
    {
      var stream = create(scope.ServiceProvider.GetRequiredService<ISender>());

      await foreach (var item in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
      {
        yield return item;
      }
    }
  }

  #endregion

  #region Implementation of ISender

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    return SendInScopeAsync(sender => sender.Send(request, cancellationToken));
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="command" /> is <see langword="null" />.</exception>
  public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    return SendInScopeAsync(sender => sender.Send(command, cancellationToken));
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="query" /> is <see langword="null" />.</exception>
  public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);
    return SendInScopeAsync(sender => sender.Send(query, cancellationToken));
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
  public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);
    return SendInScopeAsync(sender => sender.Send(message, cancellationToken));
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="query" /> is <see langword="null" />.</exception>
  public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
    IStreamQuery<TResponse> query,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(query);
    return StreamInScopeAsync(sender => sender.CreateStream(query, cancellationToken), cancellationToken);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="request" /> is <see langword="null" />.</exception>
  public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
    IStreamRequest<TResponse> request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    return StreamInScopeAsync(sender => sender.CreateStream(request, cancellationToken), cancellationToken);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="command" /> is <see langword="null" />.</exception>
  public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
    IStreamCommand<TResponse> command,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(command);
    return StreamInScopeAsync(sender => sender.CreateStream(command, cancellationToken), cancellationToken);
  }

  /// <inheritdoc />
  /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
  public IAsyncEnumerable<object?> CreateStream(object message, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(message);
    return StreamInScopeAsync(sender => sender.CreateStream(message, cancellationToken), cancellationToken);
  }

  #endregion
}

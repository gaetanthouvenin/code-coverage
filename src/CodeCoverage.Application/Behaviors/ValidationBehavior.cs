// ------------------------------------------------------------------------------------------------
// <copyright file="ValidationBehavior.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using FluentValidation;
using FluentValidation.Results;

using Mediator;

using ValidationException = CodeCoverage.Application.Exceptions.ValidationException;

namespace CodeCoverage.Application.Behaviors;

/// <summary>
///   Pipeline behavior that runs every validator of the message and refuses it, before its
///   handler, when one rule fails.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <param name="validators">The validators of the message.</param>
internal sealed class ValidationBehavior<TMessage, TResponse>(
  IEnumerable<IValidator<TMessage>> validators) : IPipelineBehavior<TMessage, TResponse>
  where TMessage : IMessage
{
  #region Implementation of IPipelineBehavior<TMessage,TResponse>

  /// <inheritdoc />
  /// <exception cref="ValidationException">One validation rule at least fails.</exception>
  public async ValueTask<TResponse> Handle(
    TMessage message,
    MessageHandlerDelegate<TMessage, TResponse> next,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(next);

    var rules = validators as IValidator<TMessage>[] ?? [.. validators];

    if (rules.Length == 0)
    {
      return await next(message, cancellationToken).ConfigureAwait(false);
    }

    // The validators run one after another, never concurrently: they are scoped, like the
    // DbContext an async rule (uniqueness, existence) may query, and EF Core refuses a second
    // operation started on a context before the first one completes.
    // One context per validator: a FluentValidation context accumulates its failures, so a
    // shared one would report the failures of a validator in the result of every other.
    var failures = new List<ValidationFailure>();

    foreach (var rule in rules)
    {
      var result = await rule.ValidateAsync(new ValidationContext<TMessage>(message), cancellationToken)
                             .ConfigureAwait(false);

      failures.AddRange(result.Errors.Where(f => f is not null));
    }

    if (failures.Count != 0)
    {
      throw new ValidationException(failures);
    }

    return await next(message, cancellationToken).ConfigureAwait(false);
  }

  #endregion
}

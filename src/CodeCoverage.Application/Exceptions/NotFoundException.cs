// ------------------------------------------------------------------------------------------------
// <copyright file="NotFoundException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown when a requested resource cannot be found, or is hidden from the current
///   principal.
///   Maps to HTTP 404 Not Found.
/// </summary>
public sealed class NotFoundException : ApplicationException
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="NotFoundException" /> class.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  public NotFoundException(string errorCode, string message)
    : base(errorCode, message)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="NotFoundException" /> class with its cause.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="innerException">The exception that caused the current exception.</param>
  public NotFoundException(string errorCode, string message, Exception innerException)
    : base(errorCode, message, innerException)
  {
  }

  #endregion

  #region Methods

  /// <summary>Creates the exception for an entity that does not exist.</summary>
  /// <param name="entity">The name of the entity, which is also its feature, e.g. <c>Repository</c>.</param>
  /// <param name="id">Its identifier.</param>
  /// <returns>The exception, with the code <c>{entity}.NotFound</c>.</returns>
  /// <exception cref="ArgumentException"><paramref name="entity" /> or <paramref name="id" /> is <see langword="null" />, empty or white space.</exception>
  public static NotFoundException For(string entity, string id)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(entity);
    ArgumentException.ThrowIfNullOrWhiteSpace(id);

    return new NotFoundException($"{entity}.NotFound", $"{entity} {id} does not exist.");
  }

  #endregion
}

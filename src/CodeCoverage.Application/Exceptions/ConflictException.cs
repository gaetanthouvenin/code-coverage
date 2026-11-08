// ------------------------------------------------------------------------------------------------
// <copyright file="ConflictException.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions;

/// <summary>
///   Exception thrown when the request conflicts with the current state, such as a duplicate or a concurrent change.
///   Maps to HTTP 409 Conflict.
/// </summary>
public sealed class ConflictException : ApplicationException
{
  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="ConflictException" /> class.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  public ConflictException(string errorCode, string message)
    : base(errorCode, message)
  {
  }

  /// <summary>Initializes a new instance of the <see cref="ConflictException" /> class with its cause.</summary>
  /// <param name="errorCode">The machine-readable error code, in the <c>Feature.Reason</c> format.</param>
  /// <param name="message">The message that describes the error.</param>
  /// <param name="innerException">The exception that caused the current exception.</param>
  public ConflictException(string errorCode, string message, Exception innerException)
    : base(errorCode, message, innerException)
  {
  }

  #endregion
}

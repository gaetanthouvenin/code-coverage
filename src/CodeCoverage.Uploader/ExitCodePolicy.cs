// ------------------------------------------------------------------------------------------------
// <copyright file="ExitCodePolicy.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Uploader;

/// <summary>Decides the exit code of the uploader: coverage tooling never breaks a build.</summary>
internal static class ExitCodePolicy
{
  #region Fields

  /// <summary>The exit code of a success, or of a failure without --fail-on-error.</summary>
  public const int Success = 0;

  /// <summary>The exit code of a run that failed with --fail-on-error.</summary>
  public const int Failure = 1;

  #endregion

  #region Methods

  /// <summary>Gets the exit code of a run.</summary>
  /// <param name="succeeded">A value indicating whether the run succeeded.</param>
  /// <param name="failOnError">A value indicating whether a failure must fail the build.</param>
  /// <returns>
  ///   <see cref="Success" />, unless the run failed and <paramref name="failOnError" /> is set.
  /// </returns>
  public static int Resolve(bool succeeded, bool failOnError)
  {
    return succeeded || !failOnError ? Success : Failure;
  }

  #endregion
}

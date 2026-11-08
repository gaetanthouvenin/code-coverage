// ------------------------------------------------------------------------------------------------
// <copyright file="ApplicationExceptionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;

namespace CodeCoverage.Application.Tests.Exceptions;

public sealed class ApplicationExceptionTests
{
  #region Methods

  [Fact]
  public void Ctor_WithEmptyErrorCode_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => new ConflictException(" ", "A conflict."));
  }

  #endregion
}

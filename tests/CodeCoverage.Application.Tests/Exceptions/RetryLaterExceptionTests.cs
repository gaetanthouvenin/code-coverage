// ------------------------------------------------------------------------------------------------
// <copyright file="RetryLaterExceptionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;

namespace CodeCoverage.Application.Tests.Exceptions;

public sealed class RetryLaterExceptionTests
{
  #region Methods

  [Fact]
  public void Ctor_ShouldKeepDelayAndStayOutsideTheRefusals()
  {
    var exception = new RetryLaterException(TimeSpan.FromSeconds(30), "The ancestor is pending.");

    exception.Delay.ShouldBe(TimeSpan.FromSeconds(30));
    exception.ShouldNotBeAssignableTo<ApplicationException>();
  }

  [Fact]
  public void Ctor_WithNegativeDelay_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => new RetryLaterException(TimeSpan.FromSeconds(-1), "Negative."));
  }

  #endregion
}

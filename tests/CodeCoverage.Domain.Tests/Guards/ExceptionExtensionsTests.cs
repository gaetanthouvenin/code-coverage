// ------------------------------------------------------------------------------------------------
// <copyright file="ExceptionExtensionsTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Domain.Tests.Guards;

public sealed class ExceptionExtensionsTests
{
  #region Methods

  [Fact]
  public void ArgumentExceptionThrowIf_WhenFalse_ShouldNotThrow()
  {
    Should.NotThrow(() => ArgumentException.ThrowIf(false, "message", "name"));
  }

  [Fact]
  public void ArgumentExceptionThrowIf_WhenTrue_ShouldThrowWithMessageAndParameter()
  {
    var exception = Should.Throw<ArgumentException>(() => ArgumentException.ThrowIf(true, "message", "name"));

    exception.ParamName.ShouldBe("name");
    exception.Message.ShouldStartWith("message");
  }

  [Fact]
  public void ArgumentExceptionThrowIf_WithNullMessage_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => ArgumentException.ThrowIf(false, null!, "name"))
          .ParamName.ShouldBe("message");
  }

  [Fact]
  public void ThrowIfUndefined_WithMember_ShouldNotThrow()
  {
    Should.NotThrow(() => ArgumentOutOfRangeException.ThrowIfUndefined(SettingType.Text));
  }

  [Fact]
  public void ThrowIfUndefined_WithOtherValue_ShouldNameTheArgumentAndKeepTheValue()
  {
    var type = (SettingType)42;

    var exception = Should.Throw<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfUndefined(type));

    exception.ParamName.ShouldBe(nameof(type));
    exception.ActualValue.ShouldBe(type);
    exception.Message.ShouldContain(nameof(SettingType));
  }

  [Fact]
  public void InvalidOperationExceptionThrowIf_WhenFalse_ShouldNotThrow()
  {
    Should.NotThrow(() => InvalidOperationException.ThrowIf(false, "message"));
  }

  [Fact]
  public void InvalidOperationExceptionThrowIf_WhenTrue_ShouldThrowWithMessage()
  {
    Should.Throw<InvalidOperationException>(() => InvalidOperationException.ThrowIf(true, "message"))
          .Message.ShouldBe("message");
  }

  [Fact]
  public void InvalidOperationExceptionThrowIf_WithNullMessage_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => InvalidOperationException.ThrowIf(false, null!))
          .ParamName.ShouldBe("message");
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="TokenGeneratorTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using CodeCoverage.Infrastructure.Security;

namespace CodeCoverage.Infrastructure.Tests.Security;

public sealed class TokenGeneratorTests
{
  #region Methods

  [Fact]
  public void NewUploadToken_ShouldHaveThePrefixAnd43Base64UrlCharacters()
  {
    Regex.IsMatch(new TokenGenerator().NewUploadToken(), "^cvc_[A-Za-z0-9_-]{43}$").ShouldBeTrue();
  }

  [Fact]
  public void NewUploadToken_ShouldDifferEachTime()
  {
    var generator = new TokenGenerator();

    generator.NewUploadToken().ShouldNotBe(generator.NewUploadToken());
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Domain.Tests.Tokens;

public sealed class UploadTokenTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  private static readonly byte[] Hash = new byte[UploadToken.HashLength];

  #endregion

  #region Methods

  [Fact]
  public void Create_OrganizationScope_ShouldKeepTheValues()
  {
    var token = UploadToken.Create(UploadTokenScope.Organization, null, "ci", "cvc_ab12", Hash, false, "octocat", Now, Now.AddDays(1));

    token.Scope.ToString().ShouldBe(nameof(UploadTokenScope.Organization));
    token.RepositoryId.ShouldBeNull();
    token.Label.ShouldBe("ci");
    token.Hint.ShouldBe("cvc_ab12");
    token.Hash.ShouldBe(Hash);
    token.Imported.ShouldBeFalse();
    token.CreatedBy.ShouldBe("octocat");
    token.CreatedAt.ShouldBe(Now);
    token.ExpiresAt.ShouldBe(Now.AddDays(1));
    token.RevokedAt.ShouldBeNull();
  }

  [Theory]
  [InlineData(UploadTokenScope.Organization, 5L)]
  [InlineData(UploadTokenScope.Repository, null)]
  [InlineData(UploadTokenScope.Repository, 0L)]
  public void Create_RepositoryThatDoesNotMatchTheScope_ShouldThrow(UploadTokenScope scope, long? repositoryId)
  {
    Should.Throw<ArgumentException>(() => UploadToken.Create(scope, repositoryId, "ci", "hint", Hash, false, "octocat", Now, null))
          .ParamName.ShouldBe("repositoryId");
  }

  [Fact]
  public void Create_HashOfTheWrongLength_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", new byte[8], false, "octocat", Now, null))
          .ParamName.ShouldBe("hash");
  }

  [Fact]
  public void Create_LabelTooLong_ShouldThrow()
  {
    var label = new string('l', UploadToken.LabelMaxLength + 1);

    Should.Throw<ArgumentException>(() => UploadToken.Create(UploadTokenScope.Organization, null, label, "hint", Hash, false, "octocat", Now, null))
          .ParamName.ShouldBe("label");
  }

  [Fact]
  public void Revoke_Twice_ShouldKeepTheFirstDate()
  {
    var token = UploadToken.Create(UploadTokenScope.Organization, null, "ci", "hint", Hash, false, "octocat", Now, null);

    token.Revoke(Now);
    token.Revoke(Now.AddDays(1));

    token.RevokedAt.ShouldBe(Now);
  }

  [Fact]
  public void IssuedToken_ToString_ShouldNotShowTheValue()
  {
    var token = UploadToken.Create(UploadTokenScope.Organization, null, "ci", "cvc_ab12", Hash, false, "octocat", Now, null);

    new IssuedToken(token, "cvc_ab12cd34secret").ToString().ShouldBe("cvc_ab12…(redacted)");
  }

  #endregion
}

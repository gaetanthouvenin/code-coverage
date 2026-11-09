// ------------------------------------------------------------------------------------------------
// <copyright file="UploadTokenFixture.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Services;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Tests.Features.UploadTokens;

// The fakes behind UploadTokenService, shared by the handlers of the six messages.
public sealed class UploadTokenFixture
{
  #region Fields

  public const string Generated = "cvc_ab12cd34ef56gh78ij90kl12mn34op56qr78st90uvw";

  public const string Imported = "5b8c4f8a-1111-4222-8333-444455556666";

  public static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  public static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Ctors

  public UploadTokenFixture()
  {
    A.CallTo(() => Clock.UtcNow).Returns(Now);
    A.CallTo(() => Generator.NewUploadToken()).Returns(Generated);
    A.CallTo(() => Hasher.Hash(A<string>._)).Returns(Hash);
    A.CallTo(() => User.Login).Returns("octocat");
    A.CallTo(() => Tokens.TryAddAsync(A<UploadToken>._, A<CancellationToken>._)).Returns(true);
  }

  #endregion

  #region Properties

  public IUploadTokenRepository Tokens { get; } = A.Fake<IUploadTokenRepository>();

  public IRepositoryRepository Repositories { get; } = A.Fake<IRepositoryRepository>();

  public ITokenGenerator Generator { get; } = A.Fake<ITokenGenerator>();

  public ITokenHasher Hasher { get; } = A.Fake<ITokenHasher>();

  public ICurrentUser User { get; } = A.Fake<ICurrentUser>();

  public IClock Clock { get; } = A.Fake<IClock>();

  public IAuditLog Audit { get; } = A.Fake<IAuditLog>();

  #endregion

  #region Methods

  internal UploadTokenService Service() => new(Tokens, Repositories, Generator, Hasher, User, Clock, Audit);

  #endregion
}

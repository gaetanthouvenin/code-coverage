// ------------------------------------------------------------------------------------------------
// <copyright file="MemoryUploadCredentialCacheTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Caching.Memory;

using CodeCoverage.Domain.Contracts.Uploads;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Uploads;

namespace CodeCoverage.Infrastructure.Tests.Uploads;

public sealed class MemoryUploadCredentialCacheTests : IDisposable
{
  #region Fields

  private static readonly byte[] Hash = Enumerable.Repeat((byte)7, UploadToken.HashLength).ToArray();

  private static readonly UploadTokenIdentity Identity = new(3, UploadTokenScope.Organization, null, null);

  private readonly MemoryCache _memory = new(new MemoryCacheOptions());

  #endregion

  #region Methods

  [Fact]
  public void Find_AfterStore_ShouldReturnTheIdentity()
  {
    var cache = Cache(1);

    cache.Store(Hash, Identity);

    cache.Find(Hash).ShouldBe(Identity);
  }

  [Fact]
  public void Find_SameHashInOtherOrganization_ShouldReturnNull()
  {
    Cache(1).Store(Hash, Identity);

    Cache(2).Find(Hash).ShouldBeNull();
  }

  [Fact]
  public void Find_ScopeWithoutOrganization_ShouldThrow()
  {
    var unset = A.Fake<ICurrentOrganization>();
    A.CallTo(() => unset.Id).Throws<InvalidOperationException>();

    Should.Throw<InvalidOperationException>(() => new MemoryUploadCredentialCache(_memory, unset).Find(Hash));
  }

  public void Dispose() => _memory.Dispose();

  private MemoryUploadCredentialCache Cache(long organizationId)
  {
    var organization = A.Fake<ICurrentOrganization>();
    A.CallTo(() => organization.Id).Returns(organizationId);

    return new MemoryUploadCredentialCache(_memory, organization);
  }

  #endregion
}

// ------------------------------------------------------------------------------------------------
// <copyright file="Sha256TokenHasherTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Security;

namespace CodeCoverage.Infrastructure.Tests.Security;

public sealed class Sha256TokenHasherTests
{
  #region Fields

  private readonly Sha256TokenHasher _hasher = new();

  #endregion

  #region Methods

  // FIPS 180-2 test vector: SHA-256("abc").
  [Fact]
  public void Hash_KnownToken_ShouldReturnItsSha256()
  {
    Convert.ToHexStringLower(_hasher.Hash("abc"))
           .ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
  }

  [Fact]
  public void Hash_ShouldBeDeterministicAnd32BytesLong()
  {
    var first = _hasher.Hash("cvc_token");
    var second = _hasher.Hash("cvc_token");

    first.Length.ShouldBe(32);
    second.ShouldBe(first);
  }

  [Fact]
  public void Hash_DifferentTokens_ShouldDiffer()
  {
    _hasher.Hash("cvc_a").ShouldNotBe(_hasher.Hash("cvc_b"));
  }

  [Fact]
  public void Hash_NullToken_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => _hasher.Hash(null!));
  }

  [Fact]
  public void FixedTimeEquals_SameHash_ShouldReturnTrue()
  {
    _hasher.FixedTimeEquals(_hasher.Hash("cvc_token"), _hasher.Hash("cvc_token")).ShouldBeTrue();
  }

  [Fact]
  public void FixedTimeEquals_DifferentHash_ShouldReturnFalse()
  {
    _hasher.FixedTimeEquals(_hasher.Hash("cvc_a"), _hasher.Hash("cvc_b")).ShouldBeFalse();
  }

  [Fact]
  public void FixedTimeEquals_DifferentLengths_ShouldReturnFalse()
  {
    _hasher.FixedTimeEquals([1, 2, 3], [1, 2]).ShouldBeFalse();
  }

  [Fact]
  public void FixedTimeEquals_NullHash_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => _hasher.FixedTimeEquals(null!, [1]));
    Should.Throw<ArgumentNullException>(() => _hasher.FixedTimeEquals([1], null!));
  }

  #endregion
}

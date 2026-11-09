// ------------------------------------------------------------------------------------------------
// <copyright file="SlowToMatchPath.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Numerics;

namespace CodeCoverage.Domain.Tests.Paths;

// A value that matches a pattern only at its last character, after hundreds of milliseconds. The
// Thue-Morse sequence never settles into a cycle, so the automaton keeps building new states, which
// is where the regular expression engine reads the clock. The length is what makes the match long
// on any machine: it replaces a load that a test cannot create on demand.
internal static class SlowToMatchPath
{
  #region Fields

  public const string Pattern = ".*a.{1000}c";

  private const int Length = 60_000;

  #endregion

  #region Methods

  public static string Build()
  {
    var noise = string.Create(
      Length,
      0,
      static (span, _) =>
      {
        for (var index = 0; index < span.Length; index++)
        {
          span[index] = BitOperations.PopCount((uint)index) % 2 == 0 ? 'a' : 'b';
        }
      });

    return string.Concat(noise, "a", new string('b', 1000), "c");
  }

  #endregion
}

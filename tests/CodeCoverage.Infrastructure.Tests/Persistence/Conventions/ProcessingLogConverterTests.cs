// ------------------------------------------------------------------------------------------------
// <copyright file="ProcessingLogConverterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Persistence.Conventions;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Conventions;

public sealed class ProcessingLogConverterTests
{
  #region Methods

  [Fact]
  public void Convert_ShouldKeepTheItemsAndTheTotals()
  {
    var converter = new ProcessingLogConverter();
    var log = ProcessingLog.Restore(["message"], 3, ["src/a.cs"], 5);

    var read = (ProcessingLog)converter.ConvertFromProvider(converter.ConvertToProvider(log)!)!;

    read.Messages.ShouldBe(["message"]);
    read.MessageTotal.ShouldBe(3);
    read.Paths.ShouldBe(["src/a.cs"]);
    read.PathTotal.ShouldBe(5);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("null")]
  public void ConvertFromProvider_NoLog_ShouldReadTheEmptyLog(string json)
  {
    var converter = new ProcessingLogConverter();

    converter.ConvertFromProvider(json).ShouldBeSameAs(ProcessingLog.Empty);
  }

  [Fact]
  public void ConvertFromProvider_TotalBelowTheItems_ShouldThrow()
  {
    var converter = new ProcessingLogConverter();

    Should.Throw<ArgumentException>(() => converter.ConvertFromProvider("""{"messages":["a"],"messageTotal":0,"paths":[],"pathTotal":0}"""));
  }

  // The cap is on the column: é, quotes and backslashes are stored as escape sequences, and they must fit too.
  [Theory]
  [InlineData("é")]
  [InlineData("\"")]
  [InlineData("\\")]
  [InlineData("😀")]
  [InlineData("a")]
  public void ConvertToProvider_FullLog_ShouldStayWithinTheCap(string character)
  {
    var converter = new ProcessingLogConverter();
    var text = string.Concat(Enumerable.Repeat(character, 300));
    var log = ProcessingLog.Empty;

    for (var i = 0; i < 2000; i++)
    {
      log = log.Add(text);
    }

    for (var i = 0; i < ProcessingLog.MaxPaths; i++)
    {
      log = log.AddPath(text);
    }

    log.IsTruncated.ShouldBeTrue();
    Encoding.UTF8.GetByteCount((string)converter.ConvertToProvider(log)!).ShouldBeLessThanOrEqualTo(ProcessingLog.MaxBytes);
  }

  #endregion
}

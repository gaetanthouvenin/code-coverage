// ------------------------------------------------------------------------------------------------
// <copyright file="ToastServiceTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Web.Components.Ui.Models;
using CodeCoverage.Web.Components.Ui.Services;

namespace CodeCoverage.Web.Tests.Components.Ui.Services;

public sealed class ToastServiceTests
{
  #region Methods

  [Fact]
  public void Show_ShouldAddTheToastAndRaiseChanged()
  {
    var service = new ToastService();
    var changes = 0;
    service.Changed += (_, _) => changes++;

    var toast = service.Show(CcTone.Success, "Token revoked", "  ");

    service.Toasts.ShouldBe([toast]);
    toast.Message.ShouldBeNull("a blank message is no message");
    changes.ShouldBe(1);
  }

  [Fact]
  public void Show_WithSameText_ShouldKeepTwoDistinctToasts()
  {
    var service = new ToastService();

    var first = service.Show(CcTone.Danger, "Failed");
    var second = service.Show(CcTone.Danger, "Failed");
    service.Dismiss(first);

    service.Toasts.ShouldBe([second]);
  }

  [Fact]
  public void Show_BeyondTheMaximum_ShouldDropTheOldest()
  {
    var service = new ToastService();
    var toasts = Enumerable.Range(0, ToastService.MaxToasts + 1)
                           .Select(i => service.Show(CcTone.Info, $"Toast {i}"))
                           .ToList();

    service.Toasts.ShouldBe(toasts.Skip(1));
  }

  [Fact]
  public void Dismiss_WithToastNoLongerShown_ShouldNotRaiseChanged()
  {
    var service = new ToastService();
    var toast = service.Show(CcTone.Info, "Saved");
    service.Dismiss(toast);
    var changes = 0;
    service.Changed += (_, _) => changes++;

    service.Dismiss(toast);

    changes.ShouldBe(0);
    service.Toasts.ShouldBeEmpty();
  }

  [Fact]
  public void Show_WithBlankTitle_ShouldThrow()
  {
    Should.Throw<ArgumentException>(() => new ToastService().Show(CcTone.Info, " "));
  }

  #endregion
}

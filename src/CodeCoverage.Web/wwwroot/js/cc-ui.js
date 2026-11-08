// ------------------------------------------------------------------------------------------------
// Coverage UI helpers (Web/Components/Ui). Loaded synchronously in <head>: the remembered theme is
// applied before the first paint, so a reload never flashes the other theme.
// ------------------------------------------------------------------------------------------------
(function () {
  "use strict";

  var storageKey = "cc-theme";
  var root = document.documentElement;

  function read() {
    try {
      return window.localStorage.getItem(storageKey);
    } catch (e) {
      return null;
    }
  }

  function write(theme) {
    try {
      window.localStorage.setItem(storageKey, theme);
    } catch (e) {
      // Private window or blocked storage: the choice lasts until the next reload.
    }
  }

  function isDark() {
    var forced = root.getAttribute("data-theme");

    if (forced) {
      return forced === "dark";
    }

    return window.matchMedia("(prefers-color-scheme: dark)").matches;
  }

  var stored = read();

  if (stored === "light" || stored === "dark") {
    root.setAttribute("data-theme", stored);
  }

  window.ccUi = {
    // Whether the page currently renders the dark theme (forced or from the operating system).
    isDark: isDark,

    // Switches between the light and the dark theme, remembers the choice, and returns whether the
    // page is now dark.
    toggleTheme: function () {
      var theme = isDark() ? "light" : "dark";

      root.setAttribute("data-theme", theme);
      write(theme);

      return theme === "dark";
    }
  };
})();

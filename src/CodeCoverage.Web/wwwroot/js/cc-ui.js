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

  // The open dialogs, the last one on top, each with the element that had the focus when it opened.
  var dialogs = [];

  var focusable = "a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), " +
    "textarea:not([disabled]), [tabindex]:not([tabindex='-1'])";

  // Keeps Tab and Shift+Tab within the dialog.
  function trap(panel, e) {
    if (e.key !== "Tab") {
      return;
    }

    var items = panel.querySelectorAll(focusable);

    if (items.length === 0) {
      e.preventDefault();
      return;
    }

    var first = items[0];
    var last = items[items.length - 1];

    if (e.shiftKey && (document.activeElement === first || document.activeElement === panel)) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  }

  // Drops the dialogs whose panel left the page: closed, or lost with a circuit that never called closeDialog. Returns
  // the entries dropped, the last opened last.
  function prune() {
    var dropped = [];

    dialogs = dialogs.filter(function (d) {
      if (d.panel.isConnected) {
        return true;
      }

      dropped.push(d);

      return false;
    });

    return dropped;
  }

  // Moves the focus into a dialog: its first enabled field, else the dialog itself.
  function focusIn(panel) {
    var field = panel.querySelector("input:not([disabled]), select:not([disabled]), textarea:not([disabled])");

    (field || panel).focus();
  }

  // The focus may leave the top dialog while it is open (a focused button that becomes disabled sends it to the
  // body): Tab and Escape then bring it back, and Escape still closes the dialog. Only the top dialog is handled.
  document.addEventListener("keydown", function (e) {
    prune();

    var top = dialogs[dialogs.length - 1];

    if (!top || top.panel.contains(document.activeElement)) {
      return;
    }

    if (e.key === "Tab") {
      e.preventDefault();
      focusIn(top.panel);
    } else if (e.key === "Escape") {
      e.preventDefault();
      top.panel.focus();
      top.panel.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    }
  });

  var stored = read();

  if (stored === "light" || stored === "dark") {
    root.setAttribute("data-theme", stored);
  }

  window.ccUi = {
    // Copies a text (a token, a badge snippet) to the clipboard. It throws when the browser refuses it, so
    // that the page says so instead of claiming a copy that never happened.
    copy: function (text) {
      if (!navigator.clipboard || !navigator.clipboard.writeText) {
        throw new Error("The clipboard is not available.");
      }

      return navigator.clipboard.writeText(text);
    },

    // Whether the page currently renders the dark theme (forced or from the operating system).
    isDark: isDark,

    // Switches between the light and the dark theme, remembers the choice, and returns whether the
    // page is now dark.
    toggleTheme: function () {
      var theme = isDark() ? "light" : "dark";

      root.setAttribute("data-theme", theme);
      write(theme);

      return theme === "dark";
    },

    // Moves the focus into a dialog that opened (its first field, else the dialog) and keeps it inside.
    openDialog: function (panel) {
      if (!panel) {
        return;
      }

      prune();
      dialogs.push({ panel: panel, opener: document.activeElement });
      panel.addEventListener("keydown", function (e) { trap(panel, e); });
      focusIn(panel);
    },

    // Moves the focus back into a dialog that is open, e.g. after a refusal disabled its focused button.
    focusDialog: function (panel) {
      if (panel) {
        focusIn(panel);
      }
    },

    // Gives the focus back to the element that opened the dialog just closed, if it is still on the page. The closed
    // dialog has left the page by now: its own entry is the one dropped, never the top of the stack blindly.
    closeDialog: function () {
      var dropped = prune();
      var closed = dropped[dropped.length - 1];
      var opener = closed && closed.opener;

      if (opener && document.contains(opener) && typeof opener.focus === "function") {
        opener.focus();
      }
    }
  };
})();

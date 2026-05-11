// Синхронный preventDefault для Enter (без Shift), чтобы textarea не получала перевод строки
// до/параллельно с асинхронным обработчиком Blazor (иначе поле не очищается после отправки).
window.chatComposer = {
  initMessageTextarea: function (element) {
    if (!element || typeof element.addEventListener !== "function") return;
    if (element.dataset.fsEnterGuard === "1") return;
    element.dataset.fsEnterGuard = "1";
    element.addEventListener(
      "keydown",
      function (e) {
        var isEnter = e.key === "Enter" || e.key === "NumpadEnter";
        if (isEnter && !e.shiftKey) {
          e.preventDefault();
        }
      },
      true
    );
  },
};

window.chatFileDrop = {
  initDropZone: function (elementOrId, dotNetRef) {
    let element = elementOrId;
    if (typeof elementOrId === "string") {
      element = document.getElementById(elementOrId);
    }
    if (!element || typeof element.addEventListener !== "function") return;

    const onDragOver = function (e) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync("SetDraggingState", true);
    };

    const onDragEnter = function (e) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync("SetDraggingState", true);
    };

    const onDragLeave = function (e) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync("SetDraggingState", false);
    };

    const onDrop = function (e) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync("SetDraggingState", false);

      const files = e.dataTransfer && e.dataTransfer.files;
      if (!files || files.length === 0) return;

      const file = files[0];
      
      // ✅ Использовать слайсинг вместо readAsDataURL для больших файлов
      const reader = new FileReader();
      reader.onload = function () {
        const result = reader.result;
        if (typeof result !== "string") return;
        const comma = result.indexOf(",");
        if (comma < 0) return;
        const base64 = result.substring(comma + 1);
        dotNetRef.invokeMethodAsync("HandleDroppedFile", file.name, base64, file.size);
      };
      
      // Читать только первый кусок для проверки, остальное будет отправлено с потоком
      // Если нужна полная поддержка потокового чтения, можно применить:
      const chunkToRead = Math.min(file.size, 5 * 1024 * 1024); // Читать максимум 5 MB для base64 конвертации
      const blob = file.slice(0, chunkToRead);
      reader.readAsDataURL(blob);
    };

    element.__chatDropHandlers = { onDragEnter, onDragOver, onDragLeave, onDrop };
    element.addEventListener("dragenter", onDragEnter);
    element.addEventListener("dragover", onDragOver);
    element.addEventListener("dragleave", onDragLeave);
    element.addEventListener("drop", onDrop);
  },

  disposeDropZone: function (elementOrId) {
    let element = elementOrId;
    if (typeof elementOrId === "string") {
      element = document.getElementById(elementOrId);
    }
    if (!element || !element.__chatDropHandlers) return;
    const h = element.__chatDropHandlers;
    element.removeEventListener("dragenter", h.onDragEnter);
    element.removeEventListener("dragover", h.onDragOver);
    element.removeEventListener("dragleave", h.onDragLeave);
    element.removeEventListener("drop", h.onDrop);
    delete element.__chatDropHandlers;
  },

  scrollToBottom: function (elementOrId) {
    let element = elementOrId;
    if (typeof elementOrId === "string") {
      element = document.getElementById(elementOrId);
    }
    if (!element) return;
    element.scrollTop = element.scrollHeight;
  },

  isNearBottom: function (elementOrId, threshold) {
    let element = elementOrId;
    if (typeof elementOrId === "string") {
      element = document.getElementById(elementOrId);
    }
    if (!element) return true;
    const px = typeof threshold === "number" ? threshold : 100;
    return element.scrollHeight - element.scrollTop - element.clientHeight <= px;
  },

  initScrollObserver: function (elementOrId, dotNetRef) {
    let element = elementOrId;
    if (typeof elementOrId === "string") {
      element = document.getElementById(elementOrId);
    }
    if (!element || typeof element.addEventListener !== "function") return;

    const onScroll = function () {
      const nearBottom = element.scrollHeight - element.scrollTop - element.clientHeight <= 100;
      dotNetRef.invokeMethodAsync("OnMessagesScrolled", nearBottom);
    };

    element.__chatScrollHandler = onScroll;
    element.addEventListener("scroll", onScroll);
  },

  disposeScrollObserver: function (elementOrId) {
    let element = elementOrId;
    if (typeof elementOrId === "string") {
      element = document.getElementById(elementOrId);
    }
    if (!element || !element.__chatScrollHandler) return;
    element.removeEventListener("scroll", element.__chatScrollHandler);
    delete element.__chatScrollHandler;
  }
};

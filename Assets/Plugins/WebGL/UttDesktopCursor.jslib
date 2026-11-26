mergeInto(LibraryManager.library, {
  UttCursorSetCaptureAvailable: function (available) {
    var canvas = Module.canvas;
    if (!canvas) return;
    if (!canvas.uttCursor) {
      canvas.uttCursor = { available: false, denied: false };
      canvas.addEventListener('pointerdown', function (event) {
        if (!event.isTrusted || event.button !== 0 || !canvas.uttCursor.available ||
            document.pointerLockElement || !canvas.requestPointerLock) return;
        try {
          const request = canvas.requestPointerLock();
          if (request && request.catch) request.catch(function () { canvas.uttCursor.denied = canvas.uttCursor.available; });
        } catch (_) {
          canvas.uttCursor.denied = canvas.uttCursor.available;
        }
      }, true);
      document.addEventListener('pointerlockerror', function () {
        canvas.uttCursor.denied = canvas.uttCursor.available;
      });
      function release() {
        if (document.pointerLockElement === canvas) document.exitPointerLock();
      }
      window.addEventListener('blur', release);
      document.addEventListener('visibilitychange', function () {
        if (document.hidden) release();
      });
    }
    canvas.uttCursor.available = !!available;
    if (!available) canvas.uttCursor.denied = false;
    if (!available && document.pointerLockElement === canvas) document.exitPointerLock();
  },

  UttCursorCaptured: function () {
    const canvas = Module.canvas;
    return document.pointerLockElement === canvas ? 1 : canvas && canvas.uttCursor && canvas.uttCursor.denied ? 2 : 0;
  }
});

mergeInto(LibraryManager.library, {
  RequestWebGLFullscreen: function () {
    var canvas = Module.canvas;
    var target = canvas || document.documentElement;

    function requestFullscreen(element) {
      var request =
        element.requestFullscreen ||
        element.webkitRequestFullscreen ||
        element.mozRequestFullScreen ||
        element.msRequestFullscreen;

      if (!request) {
        return null;
      }

      return request.call(element);
    }

    function lockLandscape() {
      if (!screen.orientation || !screen.orientation.lock) {
        return;
      }

      screen.orientation.lock("landscape").catch(function () {
        // Some mobile browsers only allow orientation lock in fullscreen,
        // and iOS Safari may not support it at all.
      });
    }

    try {
      var fullscreenResult = requestFullscreen(target);

      if (fullscreenResult && fullscreenResult.then) {
        fullscreenResult.then(lockLandscape).catch(lockLandscape);
      } else {
        lockLandscape();
      }
    } catch (error) {
      lockLandscape();
    }
  }
});

mergeInto(LibraryManager.library, {
  UttBimExplorerFillBrowser: function (enabled) {
    var canvas = Module.canvas;
    if (!canvas || !canvas.parentElement) return;
    var container = canvas.parentElement;
    var footer = document.getElementById('unity-footer');
    var nodes = footer ? [container, canvas, footer] : [container, canvas];
    if (enabled) {
      if (canvas.uttBimOriginalStyles) return;
      canvas.uttBimOriginalStyles = nodes.map(function (node) { return node.getAttribute('style'); });
      canvas.uttBimContextMenu = function (event) { event.preventDefault(); };
      canvas.addEventListener('contextmenu', canvas.uttBimContextMenu);
      Object.assign(container.style, { position: 'fixed', inset: '0', transform: 'none', overflow: 'hidden' });
      Object.assign(canvas.style, { width: '100%', height: '100%', display: 'block' });
      if (footer) footer.style.display = 'none';
    } else if (canvas.uttBimOriginalStyles) {
      nodes.forEach(function (node, index) {
        var style = canvas.uttBimOriginalStyles[index];
        if (style === null) node.removeAttribute('style');
        else node.setAttribute('style', style);
      });
      canvas.removeEventListener('contextmenu', canvas.uttBimContextMenu);
      delete canvas.uttBimContextMenu;
      delete canvas.uttBimOriginalStyles;
    }
  }
});

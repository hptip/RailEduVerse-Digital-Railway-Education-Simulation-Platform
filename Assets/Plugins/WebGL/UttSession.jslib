// Lưu phiên đăng nhập của bản Web UTTVR vào sessionStorage.
// sessionStorage tách riêng cho từng tab: mở 2 tab là 2 tài khoản khác nhau, bấm F5 vẫn giữ phiên.
mergeInto(LibraryManager.library, {
  UttSessionGet: function (key) {
    var value = '';
    try { value = window.sessionStorage.getItem(UTF8ToString(key)) || ''; } catch (e) {}
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },
  UttSessionSet: function (key, value) {
    try { window.sessionStorage.setItem(UTF8ToString(key), UTF8ToString(value)); } catch (e) {}
  },
  UttSessionRemove: function (key) {
    try { window.sessionStorage.removeItem(UTF8ToString(key)); } catch (e) {}
  }
});

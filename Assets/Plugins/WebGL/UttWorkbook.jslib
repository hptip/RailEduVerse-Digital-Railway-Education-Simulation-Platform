mergeInto(LibraryManager.library, {
  UttPickWorkbook: function (receiverPtr) {
    const receiver = UTF8ToString(receiverPtr);
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
    input.style.display = 'none';
    const complete = (value) => {
      input.remove();
      SendMessage(receiver, 'ReceiveWorkbook', value);
    };
    input.addEventListener('cancel', () => complete(''), { once: true });
    input.addEventListener('change', () => {
      const file = input.files[0];
      if (!file) { complete(''); return; }
      if (!/\.xlsx$/i.test(file.name) || file.size > 16 * 1024 * 1024) {
        complete('!Chọn tệp XLSX tối đa 16 MB.');
        return;
      }
      const reader = new FileReader();
      reader.onload = () => complete(String(reader.result).split(',')[1]);
      reader.onerror = () => complete('!Không đọc được tệp.');
      reader.readAsDataURL(file);
    }, { once: true });
    document.body.appendChild(input);
    input.click();
  },
  UttPersistInfrastructure__deps: ['$IDBFS', '$FS'],
  UttPersistInfrastructure: function (pathPtr, receiverPtr, messagePtr) {
    const path = UTF8ToString(pathPtr);
    const receiver = UTF8ToString(receiverPtr);
    const message = UTF8ToString(messagePtr);
    const failed = () => SendMessage(receiver, 'ReceiveInfrastructurePersistence',
      'Chưa lưu được vào trình duyệt. Dữ liệu vẫn mở trong phiên này. Hãy xuất Excel trước khi rời trang.');
    const snapshot = new Map();
    try {
      // Copy the committed SQLite pages before the next edit can change them.
      const directory = FS.stat(path);
      snapshot.set(path, { timestamp: directory.mtime, mode: directory.mode });
      for (const name of FS.readdir(path)) {
        if (name === '.' || name === '..') continue;
        const key = path + '/' + name;
        const stat = FS.stat(key);
        snapshot.set(key, { timestamp: stat.mtime, mode: stat.mode, contents: FS.readFile(key) });
      }
    } catch (error) {
      console.error('Infrastructure database snapshot failed', error);
      failed();
      return;
    }

    IDBFS.getDB('/idbfs', (error, db) => {
      if (error) { failed(); return; }
      try {
        const transaction = db.transaction([IDBFS.DB_STORE_NAME], 'readwrite');
        const store = transaction.objectStore(IDBFS.DB_STORE_NAME);
        const keys = store.getAllKeys(IDBKeyRange.bound(path, path + '/\uffff'));
        keys.onsuccess = () => {
          for (const key of keys.result) {
            if (!snapshot.has(key)) store.delete(key);
          }
          for (const [key, entry] of snapshot) store.put(entry, key);
        };
        transaction.oncomplete = () => SendMessage(receiver, 'ReceiveInfrastructurePersistence', message);
        transaction.onabort = failed;
      } catch (failure) {
        console.error('Infrastructure database persistence failed', failure);
        failed();
      }
    });
  },
  UttDownloadWorkbook: function (namePtr, data, size) {
    const name = UTF8ToString(namePtr);
    const blob = new Blob([HEAPU8.slice(data, data + size)], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = name;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
});

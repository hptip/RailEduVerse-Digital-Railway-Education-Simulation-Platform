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

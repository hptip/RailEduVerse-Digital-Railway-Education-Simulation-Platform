# Đưa bản Web UTTVR lên tên miền

## 1. Build
1. **File → Build Profiles → UTTVR Web**. Tắt **Development Build**.
2. Giữ **Compression Format = Gzip** và **Decompression Fallback = tắt** (như hiện tại).
3. Build vào một thư mục, ví dụ `Builds/UTTVR_Web_Release`.
4. Chạy thử ở máy trước khi đưa lên:
   `node .\Documentation\PlatformCompatibility\WebTests\serve.mjs .\Builds\UTTVR_Web_Release 8778`

## 2. Máy chủ (Ubuntu + Nginx)
1. Chép **toàn bộ** thư mục build (gồm `index.html`, `Build/`, `StreamingAssets/`, `TemplateData/`) lên máy chủ, ví dụ `/var/www/uttvr`.
2. Chép `nginx-uttvr.conf` vào `/etc/nginx/sites-available/uttvr`, sửa `TEN_MIEN` và đường dẫn, rồi:
   `sudo ln -s /etc/nginx/sites-available/uttvr /etc/nginx/sites-enabled/ && sudo nginx -t && sudo systemctl reload nginx`
3. Bật HTTPS: `sudo certbot --nginx -d TEN_MIEN`. Bản Web và kết nối Photon nên chạy qua HTTPS.
4. Backend (**bắt buộc**: bản Web mặc định đăng nhập qua backend; chỉ chạy offline khi địa chỉ trang có `?offline=1`): chạy `Backend/server.py` bằng gunicorn ở `127.0.0.1:5000` dưới dạng dịch vụ systemd. Đổi mật khẩu mặc định trong `server.py` trước khi mở ra ngoài.

## 3. Cập nhật bản mới
Chép đè thư mục build. `index.html` và các file nén được đặt `no-cache`, trình duyệt hỏi lại bằng ETag và chỉ tải lại phần đã đổi.

## Lưu ý
- Gói `.data.gz` khoảng 900 MB: lần đầu người dùng tải lâu; những lần sau dùng cache của trình duyệt (Data Caching đang bật).
- Nếu đặt sau Cloudflare: gói tải lớn hơn giới hạn cache của gói miễn phí sẽ không được Cloudflare lưu đệm, vẫn tải được nhưng đi thẳng từ máy chủ.
- Máy yếu: hướng dẫn người dùng mở **menu E → Cài đặt → Chất lượng đồ họa = Thấp**.

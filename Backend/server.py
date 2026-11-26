import os
import sqlite3
import datetime
import csv
import io
import secrets
from functools import wraps
from flask import Flask, request, jsonify, render_template_string, g
from flask_cors import CORS
from werkzeug.security import generate_password_hash, check_password_hash

app = Flask(__name__)
CORS(app)

DB_NAME = os.path.join(os.path.dirname(os.path.abspath(__file__)), "classroom.db")

# Phiên đăng nhập hết hạn sau SESSION_HOURS giờ không hoạt động.
SESSION_HOURS = 8
HASH_PREFIXES = ("scrypt:", "pbkdf2:")

def now_str():
    return datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

def is_hashed(stored):
    return isinstance(stored, str) and stored.startswith(HASH_PREFIXES)

def hash_password(raw):
    return generate_password_hash(raw)

def verify_password(stored, raw):
    """Hỗ trợ cả mật khẩu cũ lưu dạng chữ thường (tự nâng cấp lên dạng băm khi đăng nhập)."""
    if is_hashed(stored):
        return check_password_hash(stored, raw)
    return secrets.compare_digest(str(stored), str(raw))

def get_db_connection():
    conn = sqlite3.connect(DB_NAME, timeout=30.0)
    conn.row_factory = sqlite3.Row
    conn.execute("PRAGMA foreign_keys = ON;")
    conn.execute("PRAGMA journal_mode = WAL;")
    conn.execute("PRAGMA busy_timeout = 30000;")
    return conn

def init_db():
    with get_db_connection() as conn:
        c = conn.cursor()
        
        # 1. Bảng Users (Tài khoản)
        c.execute('''CREATE TABLE IF NOT EXISTS users (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        username TEXT UNIQUE NOT NULL,
                        password TEXT NOT NULL,
                        full_name TEXT NOT NULL,
                        role TEXT NOT NULL DEFAULT 'student'
                    )''')

        # 2. Bảng kết quả làm bài trắc nghiệm
        c.execute('''CREATE TABLE IF NOT EXISTS quiz_results (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        user_id INTEGER NOT NULL,
                        quiz_title TEXT NOT NULL,
                        score INTEGER NOT NULL,
                        total_questions INTEGER NOT NULL,
                        submitted_at TEXT NOT NULL,
                        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                    )''')

        # 3. Bảng tiến độ trình chiếu Slide PPT
        c.execute('''CREATE TABLE IF NOT EXISTS slide_progress (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        presentation_name TEXT UNIQUE NOT NULL,
                        current_slide INTEGER NOT NULL,
                        updated_at TEXT NOT NULL
                    )''')

        # 4. Bảng phiên đăng nhập: mỗi tab trình duyệt / mỗi lần đăng nhập có một token riêng
        c.execute('''CREATE TABLE IF NOT EXISTS sessions (
                        token TEXT PRIMARY KEY,
                        user_id INTEGER NOT NULL,
                        created_at TEXT NOT NULL,
                        last_seen TEXT NOT NULL,
                        expires_at TEXT NOT NULL,
                        FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
                    )''')
        c.execute("CREATE INDEX IF NOT EXISTS idx_sessions_user ON sessions(user_id)")

        # Cấp sẵn 3 tài khoản ban đầu nếu chưa có
        default_users = [
            ("admin", "admin123", "Quản Trị Viên Hệ Thống", "admin"),
            ("gv01", "123456", "Thầy Cương", "teacher"),
            ("sv01", "123456", "Nguyễn Văn A (K74)", "student")
        ]
        for u, p, fn, r in default_users:
            c.execute("INSERT OR IGNORE INTO users (username, password, full_name, role) VALUES (?, ?, ?, ?)", (u, hash_password(p), fn, r))

        # Xoá phiên đã hết hạn
        c.execute("DELETE FROM sessions WHERE expires_at < ?", (now_str(),))
        conn.commit()

init_db()

# ==============================================================================
# GIAO DIỆN QUẢN TRỊ (ADMIN PORTAL HTML)
# ==============================================================================
ADMIN_HTML = """
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Cổng Quản Trị - Lớp Học Số</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; margin: 0; background-color: #f0f2f5; color: #1c1e21; }
        .header { background: #1a73e8; color: white; padding: 18px 40px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .header h1 { margin: 0; font-size: 22px; }
        .container { max-width: 1200px; margin: 25px auto; padding: 0 20px; }
        .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-bottom: 25px; }
        .card { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,0.12); }
        .card h3 { margin-top: 0; border-bottom: 2px solid #e8f0fe; padding-bottom: 10px; color: #1a73e8; font-size: 17px; }
        input, select, textarea, button { width: 100%; padding: 10px; margin: 8px 0; border: 1px solid #dadce0; border-radius: 6px; box-sizing: border-box; font-size: 14px; }
        button { background-color: #1a73e8; color: white; border: none; font-weight: 600; cursor: pointer; transition: background 0.2s; }
        button:hover { background-color: #1557b0; }
        .btn-green { background-color: #34a853; }
        .btn-green:hover { background-color: #2d8e47; }
        table { width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 14px; background: white; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        th, td { padding: 12px 16px; text-align: left; border-bottom: 1px solid #e8eaed; }
        th { background-color: #f8f9fa; font-weight: 600; color: #5f6368; }
        .badge { padding: 4px 10px; border-radius: 12px; font-size: 12px; font-weight: bold; text-transform: uppercase; }
        .badge-teacher { background: #feefe3; color: #c5221f; }
        .badge-student { background: #e6f4ea; color: #137333; }
        .badge-admin { background: #e8f0fe; color: #1a73e8; }
        .del-btn { color: #d93025; text-decoration: none; font-weight: 600; }
        .del-btn:hover { text-decoration: underline; }
    </style>
</head>
<body>
    <div class="header">
        <h1>Bảng Điều Khiển Quản Trị Hệ Thống Lớp Học Số</h1>
    </div>

    <div class="container">
        <div class="grid">
            <div class="card">
                <h3>Thêm Tài Khoản Đơn Lẻ</h3>
                <form action="/admin/add-user" method="POST">
                    <input type="text" name="username" placeholder="Mã SV / Tên đăng nhập (VD: 74DCTT21001)" required>
                    <input type="password" name="password" placeholder="Mật khẩu" required>
                    <input type="text" name="full_name" placeholder="Họ và tên (VD: Nguyễn Văn Nam)" required>
                    <select name="role">
                        <option value="student">Sinh viên (Student)</option>
                        <option value="teacher">Giảng viên (Teacher)</option>
                    </select>
                    <button type="submit">Cấp Tài Khoản</button>
                </form>
            </div>

            <div class="card">
                <h3>Nhập Danh Sách Cả Lớp (CSV / Text)</h3>
                <p style="font-size: 12px; color: #666; margin: 0 0 6px 0;">Cấu trúc mỗi dòng: <code>MãSV, Họ tên, Mật khẩu</code></p>
                <form action="/admin/import-csv" method="POST">
                    <textarea name="csv_data" rows="5" placeholder="74DCTT21001, Nguyễn Văn A, 123456&#10;74DCTT21002, Trần Thị B, 123456"></textarea>
                    <button type="submit" class="btn-green">Nhập Dữ Liệu Vào CSDL</button>
                </form>
            </div>
        </div>

        <div class="card" style="margin-bottom: 25px;">
            <h3>Danh Sách Người Dùng ({{ users|length }})</h3>
            <table>
                <thead>
                    <tr>
                        <th>ID</th>
                        <th>Tên Đăng Nhập</th>
                        <th>Họ Và Tên</th>
                        <th>Vai Trò</th>
                        <th>Thao Tác</th>
                    </tr>
                </thead>
                <tbody>
                    {% for u in users %}
                    <tr>
                        <td>{{ u.id }}</td>
                        <td><b>{{ u.username }}</b></td>
                        <td>{{ u.full_name }}</td>
                        <td><span class="badge badge-{{ u.role }}">{{ u.role }}</span></td>
                        <td>
                            {% if u.role != 'admin' %}
                            <a class="del-btn" href="/admin/delete-user/{{ u.id }}" onclick="return confirm('Xoá tài khoản {{ u.full_name }}?')">Xoá</a>
                            {% else %}
                            <span style="color:#999;">Mặc định</span>
                            {% endif %}
                        </td>
                    </tr>
                    {% endfor %}
                </tbody>
            </table>
        </div>

        <div class="card">
            <h3>Kết Quả Bài Kiểm Tra Trực Tuyến ({{ results|length }})</h3>
            <table>
                <thead>
                    <tr>
                        <th>Mã SV</th>
                        <th>Họ Và Tên</th>
                        <th>Đề Thi</th>
                        <th>Điểm Số</th>
                        <th>Thời Gian Nộp</th>
                    </tr>
                </thead>
                <tbody>
                    {% for r in results %}
                    <tr>
                        <td><b>{{ r.username }}</b></td>
                        <td>{{ r.full_name }}</td>
                        <td>{{ r.quiz_title }}</td>
                        <td><b style="color: #1a73e8;">{{ r.score }} / {{ r.total_questions }}</b></td>
                        <td>{{ r.submitted_at }}</td>
                    </tr>
                    {% endfor %}
                </tbody>
            </table>
        </div>
    </div>
</body>
</html>
"""

# ==============================================================================
# ADMIN WEB ROUTES
# ==============================================================================
@app.route('/admin', methods=['GET'])
def admin_portal():
    with get_db_connection() as conn:
        users = conn.execute("SELECT id, username, full_name, role FROM users ORDER BY id DESC").fetchall()
        results = conn.execute("""
            SELECT qr.score, qr.total_questions, qr.quiz_title, qr.submitted_at, u.username, u.full_name
            FROM quiz_results qr
            INNER JOIN users u ON qr.user_id = u.id
            ORDER BY qr.id DESC
        """).fetchall()
    return render_template_string(ADMIN_HTML, users=users, results=results)

@app.route('/admin/add-user', methods=['POST'])
def admin_add_user():
    u = request.form.get('username', '').strip()
    p = request.form.get('password', '').strip()
    fn = request.form.get('full_name', '').strip()
    r = request.form.get('role', 'student')

    if u and p and fn:
        with get_db_connection() as conn:
            try:
                conn.execute("INSERT INTO users (username, password, full_name, role) VALUES (?, ?, ?, ?)", (u, hash_password(p), fn, r))
                conn.commit()
            except sqlite3.IntegrityError:
                pass
    return render_template_string("<script>window.location.href='/admin';</script>")

@app.route('/admin/import-csv', methods=['POST'])
def admin_import_csv():
    raw_data = request.form.get('csv_data', '').strip()
    if raw_data:
        reader = csv.reader(io.StringIO(raw_data))
        with get_db_connection() as conn:
            for row in reader:
                if len(row) >= 3:
                    u = row[0].strip()
                    fn = row[1].strip()
                    p = row[2].strip()
                    try:
                        conn.execute("INSERT INTO users (username, password, full_name, role) VALUES (?, ?, ?, 'student')", (u, hash_password(p), fn))
                    except sqlite3.IntegrityError:
                        continue
            conn.commit()
    return render_template_string("<script>window.location.href='/admin';</script>")

@app.route('/admin/delete-user/<int:user_id>', methods=['GET'])
def admin_delete_user(user_id):
    with get_db_connection() as conn:
        conn.execute("DELETE FROM users WHERE id = ? AND role != 'admin'", (user_id,))
        conn.commit()
    return render_template_string("<script>window.location.href='/admin';</script>")

# ==============================================================================
# XÁC THỰC BẰNG TOKEN (mỗi tab một phiên, nhiều tài khoản đăng nhập cùng lúc)
# ==============================================================================
def _token_from_request():
    auth = request.headers.get('Authorization', '')
    if auth.lower().startswith('bearer '):
        return auth[7:].strip()
    return ''

def _session_user(conn, token):
    if not token:
        return None
    row = conn.execute("""
        SELECT u.id, u.username, u.full_name, u.role, s.expires_at
        FROM sessions s INNER JOIN users u ON u.id = s.user_id
        WHERE s.token = ?""", (token,)).fetchone()
    if row is None:
        return None
    now = datetime.datetime.now()
    if row["expires_at"] < now.strftime("%Y-%m-%d %H:%M:%S"):
        conn.execute("DELETE FROM sessions WHERE token = ?", (token,))
        conn.commit()
        return None
    # Gia hạn phiên khi còn hoạt động
    expires = (now + datetime.timedelta(hours=SESSION_HOURS)).strftime("%Y-%m-%d %H:%M:%S")
    conn.execute("UPDATE sessions SET last_seen = ?, expires_at = ? WHERE token = ?",
                 (now.strftime("%Y-%m-%d %H:%M:%S"), expires, token))
    conn.commit()
    return row

def require_auth(*roles):
    """Bắt buộc có token hợp lệ. Truyền roles để giới hạn vai trò, ví dụ require_auth('teacher', 'admin')."""
    def deco(fn):
        @wraps(fn)
        def wrapper(*args, **kwargs):
            with get_db_connection() as conn:
                user = _session_user(conn, _token_from_request())
            if user is None:
                return jsonify({"status": "error", "message": "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại."}), 401
            if roles and user["role"] not in roles:
                return jsonify({"status": "error", "message": "Tài khoản không có quyền thực hiện thao tác này."}), 403
            g.user = user
            return fn(*args, **kwargs)
        return wrapper
    return deco

def _user_json(user):
    return {
        "user_id": user["id"],
        "username": user["username"],
        "full_name": user["full_name"],
        "role": user["role"]
    }

# ==============================================================================
# REST API (DÀNH CHO UNITY WEBGL GỌI LÊN)
# ==============================================================================
@app.route('/api/auth/login', methods=['POST'])
def login():
    data = request.get_json(silent=True) or {}
    username = str(data.get('username', '')).strip()
    password = str(data.get('password', '')).strip()

    with get_db_connection() as conn:
        user = conn.execute("SELECT id, username, password, full_name, role FROM users WHERE username = ?",
                            (username,)).fetchone()
        if user is None or not verify_password(user["password"], password):
            return jsonify({"status": "error", "message": "Sai tài khoản hoặc mật khẩu!"}), 401

        # Mật khẩu cũ dạng chữ thường: nâng cấp lên dạng băm ngay lần đăng nhập này
        if not is_hashed(user["password"]):
            conn.execute("UPDATE users SET password = ? WHERE id = ?", (hash_password(password), user["id"]))

        token = secrets.token_urlsafe(32)
        now = datetime.datetime.now()
        expires = (now + datetime.timedelta(hours=SESSION_HOURS)).strftime("%Y-%m-%d %H:%M:%S")
        conn.execute("INSERT INTO sessions (token, user_id, created_at, last_seen, expires_at) VALUES (?, ?, ?, ?, ?)",
                     (token, user["id"], now_str(), now_str(), expires))
        conn.execute("DELETE FROM sessions WHERE expires_at < ?", (now_str(),))
        conn.commit()

    payload = _user_json(user)
    payload["token"] = token
    payload["expires_at"] = expires
    return jsonify({"status": "success", "data": payload}), 200

@app.route('/api/auth/logout', methods=['POST'])
def logout():
    token = _token_from_request()
    if token:
        with get_db_connection() as conn:
            conn.execute("DELETE FROM sessions WHERE token = ?", (token,))
            conn.commit()
    return jsonify({"status": "success"}), 200

@app.route('/api/me', methods=['GET'])
@require_auth()
def me():
    return jsonify({"status": "success", "data": _user_json(g.user)}), 200

@app.route('/api/quiz/submit', methods=['POST'])
@require_auth()
def submit_quiz():
    data = request.get_json(silent=True) or {}
    # Lấy người nộp từ token, không tin user_id do máy gửi lên
    user_id = g.user["id"]
    quiz_title = data.get('quiz_title', 'Bài kiểm tra trắc nghiệm')
    score = data.get('score', 0)
    total_questions = data.get('total_questions', 0)
    submitted_at = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    with get_db_connection() as conn:
        try:
            conn.execute("""
                INSERT INTO quiz_results (user_id, quiz_title, score, total_questions, submitted_at) 
                VALUES (?, ?, ?, ?, ?)
            """, (user_id, quiz_title, score, total_questions, submitted_at))
            conn.commit()
            return jsonify({"status": "success", "message": "Đã lưu điểm thành công"}), 200
        except sqlite3.IntegrityError as e:
            return jsonify({"status": "error", "message": f"Lỗi ràng buộc dữ liệu: {str(e)}"}), 400

@app.route('/api/quiz/results', methods=['GET'])
@require_auth('teacher', 'admin')
def get_quiz_results():
    with get_db_connection() as conn:
        rows = conn.execute("""
            SELECT qr.id, u.username AS student_id, u.full_name, qr.quiz_title, 
                   qr.score, qr.total_questions, qr.submitted_at
            FROM quiz_results qr
            INNER JOIN users u ON qr.user_id = u.id
            ORDER BY qr.id DESC
        """).fetchall()
    return jsonify({"status": "success", "data": [dict(r) for r in rows]}), 200

# API LẤY DANH SÁCH SINH VIÊN VÀ ĐIỂM SỐ CHO UNITY LECTURE DASHBOARD
@app.route('/api/students/scores', methods=['GET'])
@app.route('/api/students/scores/', methods=['GET'])
@require_auth('teacher', 'admin')
def get_students_scores():
    with get_db_connection() as conn:
        # Lấy tất cả sinh viên (role = student) và lấy điểm bài thi gần nhất
        query = """
            SELECT 
                u.full_name,
                u.username AS student_id,
                COALESCE(
                    (SELECT qr.score || '/' || qr.total_questions 
                     FROM quiz_results qr 
                     WHERE qr.user_id = u.id 
                     ORDER BY qr.id DESC LIMIT 1),
                    'Chưa làm bài'
                ) AS score
            FROM users u
            WHERE u.role = 'student'
            ORDER BY u.full_name ASC
        """
        rows = conn.execute(query).fetchall()

    student_list = []
    for r in rows:
        student_list.append({
            "full_name": r["full_name"],
            "student_id": str(r["student_id"]),
            "score": str(r["score"])
        })

    return jsonify({
        "status": "success",
        "data": student_list
    }), 200

@app.route('/api/slide/update', methods=['POST'])
@require_auth('teacher', 'admin')
def update_slide():
    data = request.get_json(silent=True) or {}
    pres_name = data.get('presentation_name', 'DefaultPresentation')
    slide_idx = data.get('current_slide', 0)
    updated_at = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    with get_db_connection() as conn:
        conn.execute('''
            INSERT INTO slide_progress (presentation_name, current_slide, updated_at) 
            VALUES (?, ?, ?) 
            ON CONFLICT(presentation_name) 
            DO UPDATE SET current_slide=excluded.current_slide, updated_at=excluded.updated_at
        ''', (pres_name, slide_idx, updated_at))
        conn.commit()
    return jsonify({"status": "success"}), 200

@app.route('/api/slide/current', methods=['GET'])
def get_current_slide():
    pres_name = request.args.get('name', 'DefaultPresentation')
    with get_db_connection() as conn:
        row = conn.execute("SELECT current_slide FROM slide_progress WHERE presentation_name = ?", (pres_name,)).fetchone()
    current_slide = row["current_slide"] if row else 0
    return jsonify({"status": "success", "current_slide": current_slide}), 200

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=False, threaded=True)
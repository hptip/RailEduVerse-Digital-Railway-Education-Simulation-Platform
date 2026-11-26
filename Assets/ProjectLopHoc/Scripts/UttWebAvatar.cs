using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Model người có animation trên bản Web.
///  - Chính mình: chỉ hiện ở góc nhìn thứ ba.
///  - Người chơi khác: luôn dùng model này thay cho khối trắng VisualModel, chất lượng giảm dần theo khoảng cách (UttAvatarLod).
/// Được FirstPersonController tạo khi cần. Animation tính từ chuyển động của nhân vật nên không cần gửi thêm dữ liệu mạng.
///  - Tự co giãn model về chiều cao ~1,75 m và đặt chân chạm đất.
///  - Animator "Speed" (0 = đứng, 0,5 = đi, 1 = chạy) lấy theo tốc độ di chuyển thực tế.
///  - Quay mặt theo hướng di chuyển.
/// </summary>
[DefaultExecutionOrder(100)] // chạy sau FirstPersonController (đặt vị trí người chơi khác trên tàu) rồi mới tính tốc độ
public sealed class UttWebAvatar : MonoBehaviour
{
    const float TargetHeight = 1.75f;
    const int FramesBeforeFit = 3;
    static readonly int SpeedId = Animator.StringToHash("Speed");

    Transform _owner;
    Transform _model;
    Animator _animator;
    Renderer[] _renderers;
    float _runSpeed;
    bool _visible = true;
    bool _fitted;
    int _framesShown;
    Vector3 _lastLocalPosition;
    Vector3 _baseScale, _basePosition;
    float _heightRatio = 1f;
    int _sceneHandle = int.MinValue;
    float _speedParam;
    bool _remote;
    UttAvatarLod.Tier _tier = UttAvatarLod.Tier.Full;
    float _stepTime;
    int _stepFrame;

    /// <summary>Đang ngồi ghế lái: đứng yên, quay theo hướng ghế (chưa có animation ngồi).</summary>
    public bool Seated { get; set; }

    /// <summary>Đã co giãn xong và đang được vẽ.</summary>
    public bool IsShowing => _visible && _fitted && gameObject.activeInHierarchy;

    public static UttWebAvatar Create(Transform owner, GameObject prefab, RuntimeAnimatorController controller, float runSpeed, bool remote = false)
    {
        if (owner == null || prefab == null) return null;

        var root = new GameObject("WebAvatar");
        root.transform.SetParent(owner, false);

        var model = Instantiate(prefab, root.transform, false);
        model.name = prefab.name;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);

        var avatar = root.AddComponent<UttWebAvatar>();
        avatar._owner = owner;
        avatar._model = model.transform;
        avatar._baseScale = model.transform.localScale;
        avatar._basePosition = model.transform.localPosition;
        avatar._runSpeed = Mathf.Max(0.1f, runSpeed);
        avatar._lastLocalPosition = owner.localPosition;
        avatar._remote = remote;
        avatar._stepFrame = Random.Range(0, 3); // người ở xa không cùng cập nhật một khung hình

        avatar._animator = model.GetComponent<Animator>();
        if (avatar._animator == null) avatar._animator = model.GetComponentInChildren<Animator>();
        if (avatar._animator == null) avatar._animator = model.AddComponent<Animator>();
        if (controller != null) avatar._animator.runtimeAnimatorController = controller;
        avatar._animator.applyRootMotion = false;
        avatar._animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        avatar._animator.Rebind();
        avatar.ReportAnimatorProblems(prefab.name);

        avatar._renderers = model.GetComponentsInChildren<Renderer>(true);
        foreach (var r in avatar._renderers)
        {
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            r.shadowCastingMode = ShadowCastingMode.On;
            // Ẩn cho tới khi đã co giãn xong, tránh model to/nhỏ bất thường trong vài khung hình đầu.
            r.forceRenderingOff = true;
        }
        return avatar;
    }

    /// <summary>Ghi cảnh báo ra Console (F12 trên trình duyệt) nếu model không thể chạy animation Humanoid.</summary>
    void ReportAnimatorProblems(string modelName)
    {
        var a = _animator;
        if (a.runtimeAnimatorController == null)
            Debug.LogWarning($"[UttWebAvatar] '{modelName}': chưa gán Animator Controller nên model sẽ đứng chữ T.");
        if (a.avatar == null)
            Debug.LogWarning($"[UttWebAvatar] '{modelName}': Animator không có Avatar. Đặt Rig = Humanoid cho model.");
        else if (!a.avatar.isValid || !a.avatar.isHuman)
            Debug.LogWarning($"[UttWebAvatar] '{modelName}': Avatar không hợp lệ hoặc không phải Humanoid (isValid={a.avatar.isValid}, isHuman={a.avatar.isHuman}).");
    }

    /// <summary>
    /// Mức chi tiết cho người chơi khác:
    ///  - Full: animation mỗi khung hình, có bóng đổ.
    ///  - Mid: animation 3 khung hình mới tính một lần, mỗi đỉnh chỉ theo 1 xương, không bóng đổ, không tính khi nằm ngoài khung hình.
    /// </summary>
    public void SetTier(UttAvatarLod.Tier tier)
    {
        if (tier == _tier || _renderers == null) return;
        _tier = tier;
        bool full = tier == UttAvatarLod.Tier.Full;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.shadowCastingMode = full ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer skinned) skinned.quality = full ? SkinQuality.Auto : SkinQuality.Bone1;
        }
        if (_animator != null) _animator.enabled = full;
        _stepTime = 0f;
    }

    bool AnyRendererVisible()
    {
        foreach (var r in _renderers)
            if (r != null && r.isVisible) return true;
        return false;
    }

    /// <summary>
    /// Ẩn = tắt hẳn GameObject: không chạy Animator, không skinning, không vẽ bóng (skinning trên WebGL chạy bằng CPU).
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_visible == visible) return;
        _visible = visible;
        if (!_fitted) _framesShown = 0;
        gameObject.SetActive(visible);
    }

    void OnEnable()
    {
        // Bỏ qua quãng đường đi được lúc đang ẩn, tránh nhân vật "chạy" một khung hình khi hiện lại.
        if (_owner != null) _lastLocalPosition = _owner.localPosition;
    }

    void ApplyRendering()
    {
        if (_renderers == null) return;
        bool draw = _visible && _fitted;
        foreach (var r in _renderers)
            if (r != null) r.forceRenderingOff = !draw;
    }

    void LateUpdate()
    {
        if (_owner == null) { Destroy(gameObject); return; }

        // Tốc độ tính trong hệ tọa độ của cha (ví dụ khi đứng trong toa tàu đang chạy,
        // chuyển động của tàu không làm nhân vật "chạy").
        Vector3 local = _owner.localPosition;
        Vector3 delta = local - _lastLocalPosition;
        _lastLocalPosition = local;
        if (_owner.parent != null) delta = _owner.parent.TransformVector(delta);
        Vector3 flat = Vector3.ProjectOnPlane(delta, _owner.up);

        float dt = Time.deltaTime;
        float speed = dt > 0f ? flat.magnitude / dt : 0f;
        if (speed > 20f) speed = 0f; // dịch chuyển tức thời (đổi scene, ngồi ghế…)
        if (Seated) speed = 0f;

        if (!_visible) return;

        // Scene thu nhỏ (Yên Tử – Hạ Long dựng khoảng 53% thực tế): co model theo cùng tỉ lệ tầm mắt.
        int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        if (scene != _sceneHandle)
        {
            _sceneHandle = scene;
            float ratio = HeightRatio();
            if (!Mathf.Approximately(ratio, _heightRatio))
            {
                _heightRatio = ratio;
                Refit();
            }
        }

        if (!_fitted && ++_framesShown >= FramesBeforeFit) Fit();

        if (_animator != null && _animator.runtimeAnimatorController != null)
        {
            float target = Mathf.Clamp01(speed / _runSpeed);
            _speedParam = Mathf.MoveTowards(_speedParam, target, dt * 4f);
            _animator.SetFloat(SpeedId, _speedParam);

            // Mức Mid: Animator tắt, tự tính tay mỗi 3 khung hình và chỉ khi đang nhìn thấy.
            if (!_animator.enabled)
            {
                _stepTime += dt;
                if (++_stepFrame >= 3)
                {
                    _stepFrame = 0;
                    if (AnyRendererVisible()) _animator.Update(_stepTime);
                    _stepTime = 0f;
                }
            }
        }

        if (Seated)
        {
            transform.localRotation = Quaternion.identity; // nhìn theo hướng ghế
        }
        else if (speed > 0.2f && flat.sqrMagnitude > 1e-8f)
        {
            Quaternion want = Quaternion.LookRotation(flat.normalized, _owner.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-12f * dt));
        }
    }

    static float HeightRatio()
    {
        var shrink = FindFirstObjectByType<YenTuHaLongCameraHeight>();
        return shrink != null && shrink.isActiveAndEnabled ? shrink.StandingHeightMultiplier : 1f;
    }

    /// <summary>Đưa model về kích thước gốc rồi đo lại (ẩn trong lúc đo).</summary>
    void Refit()
    {
        _model.localScale = _baseScale;
        _model.localPosition = _basePosition;
        _fitted = false;
        _framesShown = 0;
        foreach (var r in _renderers)
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        ApplyRendering();
    }

    void Fit()
    {
        bool has = false;
        Bounds bounds = default;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!has || bounds.size.y < 0.01f)
        {
            // Không đo được kích thước: sau ~1 giây vẫn hiện model với tỉ lệ gốc.
            if (_framesShown > 60)
            {
                _fitted = true;
                foreach (var r in _renderers)
                    if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = false;
                ApplyRendering();
            }
            return;
        }

        float height = bounds.size.y;
        float desired = TargetHeight * _heightRatio;
        float factor = Mathf.Abs(height - desired) > desired * 0.25f ? desired / height : 1f;
        _model.localScale *= factor;

        // Đặt chân model chạm vị trí chân của người chơi.
        float rootY = _model.position.y;
        float newMinY = rootY + (bounds.min.y - rootY) * factor;
        _model.position += Vector3.up * (_owner.position.y - newMinY);

        _fitted = true;
        // Đã đo xong kích thước: thôi tính khung bao mỗi khung hình (đỡ CPU trên WebGL).
        foreach (var r in _renderers)
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = false;
        // Người chơi khác: không tính animation khi nằm ngoài khung hình.
        if (_remote && _animator != null) _animator.cullingMode = AnimatorCullingMode.CullCompletely;
        ApplyRendering();
    }
}

/// <summary>
/// Chia mức chi tiết cho model người chơi khác trên bản Web, theo khoảng cách tới camera:
///  - Full: gần, và nằm trong MaxFull người gần nhất.
///  - Mid: tầm vừa (hoặc gần nhưng đã đủ MaxFull người Full).
///  - Far: xa, dùng khối VisualModel tĩnh (không animation).
///  - Hidden: rất xa, ẩn cả model lẫn bảng tên.
/// Tính lại 4 lần mỗi giây, có độ trễ 2 m để model không nhảy mức liên tục ở sát ranh giới.
/// </summary>
public static class UttAvatarLod
{
    public enum Tier { Full = 0, Mid = 1, Far = 2, Hidden = 3 }

    public sealed class Entry
    {
        public Transform owner;
        public Tier tier = Tier.Mid;
        internal float distance;
    }

    /// <summary>Số người tối đa được animation đầy đủ cùng lúc.</summary>
    public const int MaxFull = 10;
    const float Slack = 2f;

    static readonly List<Entry> Entries = new List<Entry>();
    static float _nextRefresh;
    static int _sceneHandle = int.MinValue;
    static float _near = 15f, _mid = 40f, _far = 80f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Entries.Clear();
        _nextRefresh = 0f;
        _sceneHandle = int.MinValue;
    }

    public static Entry Register(Transform owner)
    {
        var e = new Entry { owner = owner };
        Entries.Add(e);
        _nextRefresh = 0f;
        return e;
    }

    public static void Unregister(Entry e)
    {
        if (e != null) Entries.Remove(e);
    }

    /// <summary>Mốc khoảng cách (m) theo scene: phòng nhỏ thì ngắn, khuôn viên rộng thì dài.</summary>
    static void ConfigureForScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.handle == _sceneHandle) return;
        _sceneHandle = scene.handle;
        switch (scene.name)
        {
            case "Classroom": _near = 12f; _mid = 25f; _far = 50f; break;
            case "UTTCampus": _near = 20f; _mid = 50f; _far = 110f; break;
            default: _near = 15f; _mid = 40f; _far = 80f; break;
        }
    }

    /// <summary>Gọi mỗi khung hình từ bất kỳ người chơi nào; thực sự tính lại tối đa 4 lần/giây.</summary>
    public static void Refresh()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.25f;
        ConfigureForScene();

        var cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;

        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            var e = Entries[i];
            if (e.owner == null) { Entries.RemoveAt(i); continue; }
            e.distance = Vector3.Distance(eye, e.owner.position);
        }
        Entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        int full = 0;
        foreach (var e in Entries)
        {
            float d = e.distance;
            Tier t;
            if ((e.tier == Tier.Full ? d - Slack : d) < _near && full < MaxFull) { t = Tier.Full; full++; }
            else if ((e.tier <= Tier.Mid ? d - Slack : d) < _mid) t = Tier.Mid;
            else if ((e.tier <= Tier.Far ? d - Slack : d) < _far) t = Tier.Far;
            else t = Tier.Hidden;
            e.tier = t;
        }
    }
}

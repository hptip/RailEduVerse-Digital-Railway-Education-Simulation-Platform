using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Model người của chính mình, chỉ hiện ở góc nhìn thứ ba trên bản Web.
/// Được FirstPersonController tạo khi cần; không đồng bộ mạng (người khác vẫn thấy model cũ).
///  - Tự co giãn model về chiều cao ~1,75 m và đặt chân chạm đất.
///  - Animator "Speed" (0 = đứng, 0,5 = đi, 1 = chạy) lấy theo tốc độ di chuyển thực tế.
///  - Quay mặt theo hướng di chuyển.
/// </summary>
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

    public static UttWebAvatar Create(Transform owner, GameObject prefab, RuntimeAnimatorController controller, float runSpeed)
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
        }

        if (speed > 0.2f && flat.sqrMagnitude > 1e-8f)
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
        ApplyRendering();
    }
}

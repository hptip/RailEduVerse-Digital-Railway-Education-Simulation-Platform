using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using ProjectLopHoc.Core.Interfaces;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using InputXRController = UnityEngine.InputSystem.XR.XRController;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
#endif

public class FirstPersonController : NetworkBehaviour, ITunnelingVignetteProvider
{
    private NetworkCharacterController _ncc;
    private CharacterController _cc;
    private Camera _mainCamera;

    [Header("Cấu hình di chuyển và Chuột")]
    public float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float xrMoveSpeed = 0.8f;
    [SerializeField, Range(1f, 180f)] private float snapTurnAngle = 30f;
    [SerializeField, Min(0f)] private float snapTurnDebounceTime = 0.35f;
    public float mouseSensitivity = 1.5f;
    public float eyeHeight = 1.65f;

    [Header("Web – góc nhìn thứ ba")]
    [Tooltip("Model người hiện khi ở góc nhìn thứ ba (bản Web).")]
    [SerializeField] private GameObject webAvatarPrefab;
    [SerializeField] private RuntimeAnimatorController webAvatarController;
    [SerializeField, Min(0.5f)] private float thirdPersonDistance = 3f;
    private UttWebAvatar _webAvatar;
    // Bản Web: khối VisualModel có sẵn của prefab (tĩnh, không animation)
    private Renderer[] _defaultBody;
    private bool _defaultBodyVisible = true;
    private UttAvatarLod.Entry _lod;
    // Bảng tên: chỉ dựng lại chữ khi tên/vai trò đổi
    private bool _nameplateSet, _shownDriving;
    private NetworkString<_32> _shownName, _shownRole;
    private float _thirdPersonCamOffset;
    private static readonly RaycastHit[] CameraHits = new RaycastHit[16];

    private float _verticalRotation = 0f;
    private float _horizontalRotation = 0f;
    private bool _xrMode;
    private Transform _xrOrigin;
    private UnityEngine.XR.InputDevice _leftController;
    private UnityEngine.XR.InputDevice _rightController;
#if ENABLE_INPUT_SYSTEM
    private XRSimulatedController _simulatedLeftController;
    private XRSimulatedController _simulatedRightController;
    private InputXRController _inputSystemLeftController;
    private InputXRController _inputSystemRightController;
    private Transform _rightRayOriginTransform;
    private bool _simulatedXRMode;
#endif
    private Vector2 _xrMove;
    private Vector2 _xrTurn;
    private bool _xrJump;
    private bool _xrJumpDown;
    private bool _xrInteractDown;
    private bool _xrInteractPressed;
    private bool _snapTurnArmed = true;
    private float _nextSnapTurnTime;
    private float _snapTurnVignetteUntil;
    private TunnelingVignetteController _tunnelingVignette;
    private bool _comfortVignetteActive;
    private Transform _driverViewAnchor;
    private Transform _seatedViewOverride;
    public bool IsDrivingTrain(TrainSplineMover mover) => isDriving && _currentSeat != null && _currentSeat.trainMover == mover;
    public void SetSeatedViewOverride(Transform anchor)
    {
        if (anchor != null && (!isDriving || Object == null || !Object.IsValid || !HasStateAuthority)) return;
        _seatedViewOverride = anchor;
        if (_xrMode && isDriving) FollowDriverViewAnchor();
    }
    private Vector3 _seatedCameraOffsetInOrigin;
    private Quaternion _seatedRigRotationOffset;
    private UnityEngine.InputSystem.XR.TrackedPoseDriver _headTrackedPoseDriver;
    private XRNodePoseDriver _headNodePoseDriver;
    private UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType _previousHeadTrackingType;
    private bool _previousHeadPositionTracking;
    private bool _hasSavedSeatedTracking;
    private readonly VignetteParameters _comfortVignetteParameters = new VignetteParameters
    {
        apertureSize = 0.7f,
        featheringEffect = 0.25f,
        easeInTime = 0.15f,
        easeOutTime = 0.2f,
        easeInTimeLock = true,
        easeOutDelayTime = 0f
    };

    public VignetteParameters vignetteParameters => _comfortVignetteParameters;

    [Header("Cấu hình Tương tác (Raycast)")]
    public float interactionRange = 4f;
    public LayerMask interactableLayer = ~0;

    [Header("Trạng thái Lái Tàu")]
    public bool isDriving = false;
    private TrainSeatInteractable _currentSeat;
    private Transform _trainRoot;

    [Header("Bảng tên trên đầu (Nameplate)")]
    public GameObject nameplateObject;
    public TMP_Text nameplateText;

    [Networked] public NetworkString<_32> NetworkPlayerName { get; set; }
    [Networked] public NetworkString<_32> NetworkPlayerRole { get; set; }

    // Đứng trong tàu Yên Tử: gửi vị trí TÍNH THEO CON TÀU, để khi tàu chạy nhanh người khác không thấy mình bị trôi lệch.
    [Networked] public NetworkBool NetInTrain { get; set; }
    [Networked] public Vector3 NetTrainLocalPos { get; set; }
    [Networked] public float NetTrainLocalYaw { get; set; }
    /// <summary>Ghế khách đang ngồi: 0 = không ngồi, n = ghế số n-1 (UttTrainSeats).</summary>
    [Networked] public int NetSeat { get; set; }
    private int _passengerSeat = -1;
    private Transform _preSeatCar;      // chỗ đứng ngay trước khi ngồi, tính theo toa (để đứng dậy vẫn ở trong toa)
    private Vector3 _preSeatLocal;
    private bool _standLocked;          // tàu đang chạy, hết ghế: đứng cố định trong toa
    private Transform _lockCar;
    private Vector3 _lockLocal;
    private float _nextSeatCheck;
    private Transform _remoteTrainRoot;
    private Vector3 _remoteLocalPos;
    private float _remoteLocalYaw;
    private bool _remoteHasPose;

    /// <summary>Gửi tin nhắn chat (bản Web) tới mọi người trong phòng, kể cả chính mình.</summary>
    public void SendChat(string message)
    {
        if (Object == null || !Object.IsValid || !HasStateAuthority) return;
        message = UttChat.Clean(message);
        if (message.Length == 0) return;
        RpcChat(message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RpcChat(string message)
    {
        UttChat.Receive(this, message);
    }

    private bool _jumpRequested = false;
    private InteractableObject _currentHoveredObject;
    private IInteractable _currentXRHoveredObject;

    private void Awake()
    {
        _ncc = GetComponent<NetworkCharacterController>();
        _cc = GetComponent<CharacterController>();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_lod != null) { UttAvatarLod.Unregister(_lod); _lod = null; }
        UpdateTunnelingVignette(false);
        if (HasStateAuthority && _currentSeat != null && _currentSeat.trainMover != null)
        {
            _currentSeat.trainMover.SetVRControlInput(Vector2.zero);
            _currentSeat.trainMover.isDriverSeated = false;
        }
        if (HasStateAuthority)
            FindFirstObjectByType<YenTuHaLongCameraHeight>()?.SetSeated(false);
        RestoreSeatedXRTracking();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_xrMode) ConfigureXRCameras();
    }

    public override void Spawned()
    {
        GameObject originObject = GameObject.Find("XR Origin (VR)");
        _xrOrigin = originObject != null ? originObject.transform : null;
#if ENABLE_INPUT_SYSTEM
        ConfigureXRInputSource();
#endif
        _xrMode = XRSettings.isDeviceActive
#if ENABLE_INPUT_SYSTEM
                  || _simulatedXRMode
#endif
                  ;
        if (!_xrMode && _xrOrigin != null)
        {
            _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            _xrMode = _leftController.isValid || _rightController.isValid;
        }
        if (HasStateAuthority)
        {
            FirstPersonController[] existingControllers = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
            foreach (var controller in existingControllers)
            {
                if (controller != this && controller.Object != null && controller.Object.IsValid &&
                    controller.Runner == Runner && controller.HasStateAuthority && controller.isActiveAndEnabled)
                {
                    Debug.LogWarning("[FirstPersonController] Discarding duplicate local player in the same runner; keeping " + controller.name, this);
                    Runner.Despawn(Object);
                    return;
                }
            }

            _mainCamera = ResolvePlayerCamera();
            if (_xrMode)
            {
                ConfigureXRCameras();
                if (_xrOrigin != null)
                {
                    _xrOrigin.SetPositionAndRotation(transform.position, transform.rotation);
#if ENABLE_INPUT_SYSTEM
                    if (_simulatedXRMode)
                    {
                        RefreshSimulatedControllers();
                        CacheRightRayOriginTransform();
                    }
#endif
                    _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                    _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                }
            }
            else
            {
                if (!UttWebPresentation.Active)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            GameObject trainObj = GameObject.Find("Train_THSR_700T");
            if (trainObj != null)
            {
                _trainRoot = trainObj.transform;
                transform.SetParent(_trainRoot, true);
                _horizontalRotation = transform.localEulerAngles.y;
            }
            else
            {
                _horizontalRotation = transform.eulerAngles.y;
            }

            string myName = UttSession.DisplayName;
            bool isLecturer = UttSession.IsLecturer;
            NetworkPlayerName = myName;
            NetworkPlayerRole = isLecturer ? "Giảng viên" : "Lớp: 74DCTT21";
        }
    }

    private void Update()
    {
        if (!HasStateAuthority) return;

        RefreshXRMode();

        if (_xrMode)
        {
#if ENABLE_INPUT_SYSTEM
            RefreshInputSystemControllers();
            if (IsLegacyControllerTracked(XRNode.LeftHand) || IsLegacyControllerTracked(XRNode.RightHand) ||
                IsPhysicalInputController(_inputSystemLeftController) ||
                IsPhysicalInputController(_inputSystemRightController))
                _simulatedXRMode = false;

            if (_simulatedXRMode)
            {
                RefreshSimulatedControllers();
                _xrMove = _simulatedLeftController != null
                    ? _simulatedLeftController.primary2DAxis.ReadValue()
                    : Vector2.zero;
                _xrTurn = _simulatedRightController != null
                    ? _simulatedRightController.primary2DAxis.ReadValue()
                    : Vector2.zero;
                _xrJump = _simulatedLeftController != null && _simulatedLeftController.primaryButton.isPressed;
                _xrInteractDown = _simulatedRightController != null &&
                                  (_simulatedRightController.triggerButton.isPressed ||
                                   _simulatedRightController.trigger.ReadValue() >= 0.55f);
                if (_xrMove == Vector2.zero)
                    TryReadInputSystemAxis(_inputSystemLeftController, out _xrMove);
                if (_xrTurn == Vector2.zero)
                    TryReadInputSystemAxis(_inputSystemRightController, out _xrTurn);
            }
            else
#endif
            {
            if (!_leftController.isValid) _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!_rightController.isValid) _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            _leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out _xrMove);
            _rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out _xrTurn);
            if (_xrMove == Vector2.zero) TryReadInputSystemAxis(_inputSystemLeftController, out _xrMove);
            if (_xrTurn == Vector2.zero) TryReadInputSystemAxis(_inputSystemRightController, out _xrTurn);
            _xrJump = _leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool jumpPressed) && jumpPressed;
            _xrInteractDown = _rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerPressed) && triggerPressed;
            }

            _xrMove = ApplyStickDeadzone(_xrMove);
            _xrTurn = ApplyStickDeadzone(_xrTurn);

            if (isDriving)
            {
                if (_currentSeat != null && _currentSeat.trainMover != null)
                {
                    _currentSeat.trainMover.isDriverSeated = true;
                    _currentSeat.trainMover.SetVRControlInput(_xrMove);
                }
                if (_xrJump && !_xrJumpDown) YeuCauRoiGhe();
                _xrJumpDown = _xrJump;
                UpdateTunnelingVignette(false);
                ClearCurrentHover();
                HandleXRRaycastInteraction(_xrInteractDown && !_xrInteractPressed);
                _xrInteractPressed = _xrInteractDown;
                return;
            }

            ProcessSnapTurn(_xrTurn.x);
            UpdateTunnelingVignette(_xrMove.sqrMagnitude > 0.0225f || Time.unscaledTime < _snapTurnVignetteUntil);
            if (_xrJump && !_xrJumpDown) _jumpRequested = true;
            _xrJumpDown = _xrJump;
            ClearCurrentHover();
            HandleXRRaycastInteraction(_xrInteractDown && !_xrInteractPressed);
            _xrInteractPressed = _xrInteractDown;
            return;
        }

        ClearXRCurrentHover();
        UpdateTunnelingVignette(false);

        // Xoay góc nhìn chuột liên tục cho cả khi đi bộ lẫn khi ngồi ghế lái
        if (DesktopLookActive())
        {
            Vector2 mouseDelta = ReadMouseDelta();
            float mouseX = mouseDelta.x * mouseSensitivity;
            float mouseY = mouseDelta.y * mouseSensitivity;

            _horizontalRotation += mouseX;
            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -85f, 85f);
        }

        // Đang ngồi lái tàu
        if (isDriving)
        {
            if (WasKeyPressedThisFrame(KeyCode.F))
            {
                YeuCauRoiGhe();
            }
            return;
        }

        // Ngồi ghế khách: F để đứng dậy (chỉ khi tàu dừng)
        if (_passengerSeat >= 0)
        {
            if (WasKeyPressedThisFrame(KeyCode.F)) StandFromPassengerSeat();
            return;
        }
        if (_standLocked) return; // tàu đang chạy, đứng cố định trong toa

        // Đang đi bộ tự do
        if (WasKeyPressedThisFrame(KeyCode.Space))
        {
            _jumpRequested = true;
        }

        HandleRaycastInteraction();

        if (WasKeyPressedThisFrame(KeyCode.F))
        {
            KiemTraNgoiVaoGheBangPhimF();
        }
    }

    private void RefreshXRMode()
    {
        if (UttWebPresentation.Active) return;

        if (_xrOrigin == null)
        {
            GameObject originObject = GameObject.Find("XR Origin (VR)");
            _xrOrigin = originObject != null ? originObject.transform : null;
        }
#if ENABLE_INPUT_SYSTEM
        ConfigureXRInputSource();
#endif
        if (_xrMode || _xrOrigin == null) return;

        _leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!XRSettings.isDeviceActive && !_leftController.isValid && !_rightController.isValid
#if ENABLE_INPUT_SYSTEM
            && !_simulatedXRMode
#endif
            ) return;

        _xrMode = true;
        _mainCamera = ResolvePlayerCamera();
        ConfigureXRCameras();
        _xrOrigin.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, _horizontalRotation, 0f));
#if ENABLE_INPUT_SYSTEM
        if (_simulatedXRMode)
        {
            RefreshSimulatedControllers();
            CacheRightRayOriginTransform();
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void ConfigureXRInputSource()
    {
        if (!Application.isEditor || _xrOrigin == null) return;

        XRDeviceSimulator simulator = UnityEngine.Object.FindAnyObjectByType<XRDeviceSimulator>();
        if (simulator == null)
        {
            _simulatedXRMode = false;
            return;
        }

        bool hasPhysicalController =
            IsLegacyControllerTracked(XRNode.LeftHand) ||
            IsLegacyControllerTracked(XRNode.RightHand);

        foreach (var device in InputSystem.devices)
        {
            if (device is InputXRController controller && IsPhysicalInputController(controller))
            {
                hasPhysicalController = true;
                break;
            }
        }

        simulator.enabled = !hasPhysicalController;
        _simulatedXRMode = simulator.isActiveAndEnabled && !hasPhysicalController;
    }
#endif

    private Camera ResolvePlayerCamera()
    {
        if (_xrMode && _xrOrigin != null)
        {
            Camera[] cameras = _xrOrigin.GetComponentsInChildren<Camera>(true);
            foreach (Camera camera in cameras)
                if (camera != null && camera.enabled) return camera;
            if (cameras.Length > 0) return cameras[0];
        }
        return Camera.main;
    }

    private void ConfigureXRCameras()
    {
        if (_xrOrigin == null) return;
        _mainCamera = ResolvePlayerCamera();
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || camera == _mainCamera || camera.gameObject.name != "Main Camera" ||
                camera.transform.IsChildOf(_xrOrigin)) continue;
            camera.enabled = false;
            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
    }

    private static Vector2 ApplyStickDeadzone(Vector2 value)
    {
        if (value.sqrMagnitude < 0.0225f) return Vector2.zero;
        return Vector2.ClampMagnitude(value, 1f);
    }

    private void ProcessSnapTurn(float horizontalAxis)
    {
        if (Mathf.Abs(horizontalAxis) < 0.35f)
        {
            _snapTurnArmed = true;
            return;
        }

        if (!_snapTurnArmed || Time.unscaledTime < _nextSnapTurnTime) return;

        _horizontalRotation += Mathf.Sign(horizontalAxis) * snapTurnAngle;
        _snapTurnArmed = false;
        _nextSnapTurnTime = Time.unscaledTime + snapTurnDebounceTime;
        _snapTurnVignetteUntil = Time.unscaledTime + 0.35f;
    }

    private void UpdateTunnelingVignette(bool shouldBeActive)
    {
        if (_tunnelingVignette == null && _xrOrigin != null)
            _tunnelingVignette = _xrOrigin.GetComponentInChildren<TunnelingVignetteController>(true);
        if (_tunnelingVignette == null || shouldBeActive == _comfortVignetteActive) return;

        if (shouldBeActive)
            _tunnelingVignette.BeginTunnelingVignette(this);
        else
            _tunnelingVignette.EndTunnelingVignette(this);

        _comfortVignetteActive = shouldBeActive;
    }

    private void KiemTraNgoiVaoGheBangPhimF()
    {
        if (UttWebPresentation.Active)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (SceneQuickMenu.IsOpen || selected != null && selected.TryGetComponent<TMP_InputField>(out var field) && field.isFocused)
                return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = ResolvePlayerCamera();
            if (_mainCamera == null) return;
        }

        Ray ray = BuildViewRay();
        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayer))
        {
            TrainSeatInteractable seat = hit.collider.GetComponentInParent<TrainSeatInteractable>();
            if (seat != null)
            {
                seat.RequestOccupy(this);
                return;
            }
        }

        if (UttWebPresentation.Active && SceneManager.GetActiveScene().name == "YenTuHaLongXanh")
        {
            TrainSeatInteractable driverSeat = FindReachableDriverSeat(ray.origin);
            if (driverSeat != null) { driverSeat.RequestOccupy(this); return; }
        }

        // Không có ghế lái trong tầm: ngồi ghế khách gần nhất (trong 2,5 m)
        int passengerSeat = UttTrainSeats.FindNearestFree(transform.position, 2.5f, this);
        if (passengerSeat >= 0) SitInPassengerSeat(passengerSeat);
    }

    private TrainSeatInteractable FindReachableDriverSeat(Vector3 origin)
    {
        TrainSeatInteractable nearest = null;
        float nearestDistance = interactionRange;
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (var seat in FindObjectsByType<TrainSeatInteractable>(FindObjectsSortMode.None))
        {
            if (seat.gameObject.scene != activeScene || seat.IsOccupied ||
                !seat.TryGetComponent<Collider>(out var collider) || !collider.enabled) continue;

            Vector3 offset = collider.bounds.center - origin;
            float distance = offset.magnitude;
            if (distance <= 0f || distance > nearestDistance) continue;

            if (!Physics.Raycast(origin, offset / distance, out var hit, distance, interactableLayer, QueryTriggerInteraction.Ignore) ||
                hit.collider != collider) continue;

            nearest = seat;
            nearestDistance = distance;
        }
        return nearest;
    }

    public void ThietLapVaoGhe(TrainSeatInteractable seat, Transform mountPoint)
    {
        isDriving = true;
        _currentSeat = seat;
        if (HasStateAuthority && _currentSeat != null && _currentSeat.trainMover != null)
            _currentSeat.trainMover.isDriverSeated = true;

        if (_ncc != null) _ncc.enabled = false;
        if (_cc != null) _cc.enabled = false;

        Transform target = mountPoint != null ? mountPoint : seat.transform;
        transform.position = target.position;
        transform.rotation = target.rotation;

        _horizontalRotation = target.eulerAngles.y;
        _verticalRotation = 0f;

        if (UttWebPresentation.Active && !_xrMode)
        {
            _driverViewAnchor = target.Find("DriverViewAnchor");
            _horizontalRotation = 0f;
        }

        if (_xrMode && HasStateAuthority)
        {
            FindFirstObjectByType<YenTuHaLongCameraHeight>()?.SetSeated(true);
            EnterSeatedXRMode(seat, target);
        }

        if (UttWebPresentation.Active && HasStateAuthority)
            Debug.Log("[TrainSeat] Đã vào ghế lái.", this);
    }

    private void EnterSeatedXRMode(TrainSeatInteractable seat, Transform mountPoint)
    {
        if (_xrOrigin == null)
        {
            GameObject originObject = GameObject.Find("XR Origin (VR)");
            _xrOrigin = originObject != null ? originObject.transform : null;
        }

        _mainCamera = ResolvePlayerCamera();
        if (_xrOrigin == null || _mainCamera == null) return;

        _driverViewAnchor = seat != null ? seat.transform.Find("Driver_SeatPoint/DriverViewAnchor") : null;
        if (_driverViewAnchor == null && mountPoint != null)
            _driverViewAnchor = mountPoint.Find("DriverViewAnchor");
        if (_driverViewAnchor == null)
            _driverViewAnchor = mountPoint != null ? mountPoint : seat != null ? seat.transform : null;

        _headTrackedPoseDriver = _mainCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
        _headNodePoseDriver = _mainCamera.GetComponent<XRNodePoseDriver>();
        if (!_hasSavedSeatedTracking)
        {
            if (_headTrackedPoseDriver != null)
                _previousHeadTrackingType = _headTrackedPoseDriver.trackingType;
            if (_headNodePoseDriver != null)
                _previousHeadPositionTracking = _headNodePoseDriver.trackPosition;
            _hasSavedSeatedTracking = true;
        }

        if (_headTrackedPoseDriver != null)
            _headTrackedPoseDriver.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationOnly;
        if (_headNodePoseDriver != null)
            _headNodePoseDriver.trackPosition = false;

        Vector3 currentForward = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized;
        Vector3 anchorForward = Vector3.ProjectOnPlane(_driverViewAnchor.forward, Vector3.up).normalized;
        if (currentForward.sqrMagnitude > 0.001f && anchorForward.sqrMagnitude > 0.001f)
        {
            float yawCorrection = Vector3.SignedAngle(currentForward, anchorForward, Vector3.up);
            _xrOrigin.rotation = Quaternion.AngleAxis(yawCorrection, Vector3.up) * _xrOrigin.rotation;
        }

        _seatedCameraOffsetInOrigin = _xrOrigin.InverseTransformPoint(_mainCamera.transform.position);
        _seatedRigRotationOffset = Quaternion.Inverse(_driverViewAnchor.rotation) * _xrOrigin.rotation;
        FollowDriverViewAnchor();
        UpdateTunnelingVignette(false);
    }

    private void FollowDriverViewAnchor()
    {
        if (_driverViewAnchor == null || _xrOrigin == null || _mainCamera == null) return;

        Transform view = _seatedViewOverride != null ? _seatedViewOverride : _driverViewAnchor;
        _xrOrigin.rotation = view.rotation * _seatedRigRotationOffset;
        _xrOrigin.position = view.position - _xrOrigin.rotation * _seatedCameraOffsetInOrigin;
    }

    private void RestoreSeatedXRTracking()
    {
        _seatedViewOverride = null;
        if (!_hasSavedSeatedTracking) return;

        if (_headTrackedPoseDriver != null)
            _headTrackedPoseDriver.trackingType = _previousHeadTrackingType;
        if (_headNodePoseDriver != null)
            _headNodePoseDriver.trackPosition = _previousHeadPositionTracking;

        _headTrackedPoseDriver = null;
        _headNodePoseDriver = null;
        _driverViewAnchor = null;
        _hasSavedSeatedTracking = false;
    }

    public void YeuCauRoiGhe()
    {
        if (!isDriving) return;

        TrainSeatInteractable seat = _currentSeat;
        if (seat != null && seat.trainMover != null && !seat.trainMover.IsStopped) return;

        ThietLapRoiGhe();

        if (seat != null)
        {
            seat.RequestVacate(this);
        }
    }

    public bool AdjustTrainSpeedLevel(int delta)
    {
        if (!HasStateAuthority || !isDriving || _currentSeat == null || _currentSeat.trainMover == null)
            return false;

        _currentSeat.trainMover.isDriverSeated = true;
        _currentSeat.trainMover.AdjustSpeedLevel(delta);
        return true;
    }

    public void ThietLapRoiGhe()
    {
        if (!isDriving) return;

        TrainSeatInteractable seat = _currentSeat;
        if (seat != null && seat.trainMover != null && !seat.trainMover.IsStopped) return;
        if (seat != null && HasStateAuthority)
            seat.trainMover?.SetVRControlInput(Vector2.zero);

        isDriving = false;
        _currentSeat = null;
        if (UttWebPresentation.Active && !_xrMode)
        {
            _horizontalRotation = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : transform.eulerAngles.y;
            _driverViewAnchor = null;
        }

        // Clear the driver's control gate immediately. RequestVacate also arrives as an RPC,
        // but by then this local controller is already out of the seat and the RPC callback
        // can legitimately return early from this method.
        if (HasStateAuthority && seat != null && seat.trainMover != null)
        {
            seat.trainMover.SetVRControlInput(Vector2.zero);
            seat.trainMover.isDriverSeated = false;
        }

        // Lùi lại phía sau ghế lái 0.8m trên sàn buồng lái
        transform.position -= transform.forward * 0.8f;

        if (HasStateAuthority && _xrMode && _xrOrigin != null)
        {
            _xrOrigin.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, _horizontalRotation, 0f));
            FindFirstObjectByType<YenTuHaLongCameraHeight>()?.SetSeated(false);
            RestoreSeatedXRTracking();
        }

        if (_ncc != null) _ncc.enabled = true;
        if (_cc != null) _cc.enabled = true;
    }

    private bool DesktopLookActive()
    {
#if ENABLE_INPUT_SYSTEM
        if (UttWebPresentation.Active)
        {
            if (UttOverlay.BlocksGameplay) return false; // menu E, bản đồ M, đang gõ chat
            // Góc nhìn thứ nhất / thứ ba (khóa chuột): di chuyển chuột là xoay góc nhìn.
            if (UttWebView.MouseLookActive)
                return !SceneQuickMenu.IsOpen && Cursor.lockState == CursorLockMode.Locked;
            // Chuột tự do: giữ chuột phải để xoay.
            return !SceneQuickMenu.IsOpen && !UttWebPresentation.PointerOverUi && Mouse.current?.rightButton.isPressed == true;
        }
#endif
        return Cursor.lockState == CursorLockMode.Locked;
    }

    private void HandleRaycastInteraction()
    {
        bool webFirstPerson = UttWebPresentation.Active && UttWebView.MouseLookActive;
        if (webFirstPerson && (SceneQuickMenu.IsOpen || Cursor.lockState != CursorLockMode.Locked))
        {
            ClearCurrentHover();
            return;
        }
        if (UttWebPresentation.Active && !webFirstPerson && (SceneQuickMenu.IsOpen || UttWebPresentation.PointerOverUi))
        {
            ClearCurrentHover();
            return;
        }
        if (_mainCamera == null)
        {
            _mainCamera = ResolvePlayerCamera();
            if (_mainCamera == null) return;
        }

        Ray ray = BuildViewRay();

        if (Physics.Raycast(ray, out RaycastHit hit, interactionRange, interactableLayer))
        {
            InteractableObject target = hit.collider.GetComponentInParent<InteractableObject>();

            if (target != null)
            {
                if (_currentHoveredObject != target)
                {
                    ClearCurrentHover();
                    _currentHoveredObject = target;
                    _currentHoveredObject.OnHoverEnter();
                }

                if ((WasMousePressedThisFrame() && UttWebView.LockClickFrame != Time.frameCount) || (!UttWebPresentation.Active && WasKeyPressedThisFrame(KeyCode.E)))
                {
                    target.OnInteract();
                }

                return;
            }

            if (UttWebPresentation.Active && WasMousePressedThisFrame() && UttWebView.LockClickFrame != Time.frameCount)
            {
                var seat = hit.collider.GetComponentInParent<TrainSeatInteractable>();
                if (seat != null) seat.RequestOccupy(this);
            }
        }

        ClearCurrentHover();
    }

    private void HandleXRRaycastInteraction(bool interactPressed)
    {
        if (_xrOrigin == null || !TryGetRightControllerPose(out Vector3 position, out Quaternion rotation) ||
            !Physics.Raycast(new Ray(_xrOrigin.TransformPoint(position), _xrOrigin.rotation * rotation * Vector3.forward), out RaycastHit hit, interactionRange, interactableLayer))
        {
            ClearXRCurrentHover();
            return;
        }

        IInteractable target = FindXRInteractable(hit.collider);
        if (_currentXRHoveredObject != target)
        {
            ClearXRCurrentHover();
            _currentXRHoveredObject = target;
            _currentXRHoveredObject?.OnHoverEnter();
        }

        if (!interactPressed) return;
        if (target != null) target.OnInteract(gameObject);
        TrainSeatInteractable seat = hit.collider.GetComponentInParent<TrainSeatInteractable>();
        if (seat != null) seat.RequestOccupy(this);
    }

    private static IInteractable FindXRInteractable(Collider hitCollider)
    {
        if (hitCollider == null) return null;

        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IInteractable interactable)
                return interactable;
        }

        InteractableObject legacyInteractable = hitCollider.GetComponentInParent<InteractableObject>();
        return legacyInteractable;
    }

    private bool TryGetRightControllerPose(out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = Quaternion.identity;
#if ENABLE_INPUT_SYSTEM
        CacheRightRayOriginTransform();
        if (_rightRayOriginTransform != null)
        {
            position = _xrOrigin.InverseTransformPoint(_rightRayOriginTransform.position);
            rotation = Quaternion.Inverse(_xrOrigin.rotation) * _rightRayOriginTransform.rotation;
            return true;
        }

        if (_simulatedXRMode)
        {
            RefreshSimulatedControllers();
            if (_simulatedRightController != null)
            {
                var positionControl = _simulatedRightController.TryGetChildControl<Vector3Control>("devicePosition");
                var rotationControl = _simulatedRightController.TryGetChildControl<QuaternionControl>("deviceRotation");
                if (positionControl != null && rotationControl != null)
                {
                    position = positionControl.ReadValue();
                    rotation = rotationControl.ReadValue();
                    return true;
                }
            }
            position = default;
            rotation = Quaternion.identity;
            return false;
        }
#endif
        if (!_rightController.isValid) _rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!_rightController.isValid ||
            !_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out position) ||
            !_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rotation))
            return false;
        return true;
    }

#if ENABLE_INPUT_SYSTEM
    private void RefreshInputSystemControllers()
    {
        foreach (var device in InputSystem.devices)
        {
            if (!(device is InputXRController controller)) continue;
            for (int i = 0; i < controller.usages.Count; i++)
            {
                string usage = controller.usages[i].ToString();
                if (usage == "LeftHand") _inputSystemLeftController = controller;
                else if (usage == "RightHand") _inputSystemRightController = controller;
            }
        }
    }

    private static bool IsPhysicalInputController(InputXRController controller)
    {
        if (controller == null || controller is XRSimulatedController || !controller.added)
            return false;

        var trackingControl = controller.TryGetChildControl<IntegerControl>("trackingState");
        if (trackingControl != null)
            return (trackingControl.ReadValue() &
                    (int)(UnityEngine.XR.InputTrackingState.Position | UnityEngine.XR.InputTrackingState.Rotation)) != 0;

        var trackedControl = controller.TryGetChildControl<ButtonControl>("isTracked");
        if (trackedControl != null)
            return trackedControl.isPressed;

        var position = controller.TryGetChildControl<Vector3Control>("devicePosition");
        var rotation = controller.TryGetChildControl<QuaternionControl>("deviceRotation");
        return (position != null && position.ReadValue().sqrMagnitude > 0.000001f) ||
               (rotation != null && Quaternion.Angle(rotation.ReadValue(), Quaternion.identity) > 0.001f);
    }

    private static bool IsLegacyControllerTracked(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;

        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,
                out UnityEngine.XR.InputTrackingState trackingState))
            return (trackingState &
                    (UnityEngine.XR.InputTrackingState.Position | UnityEngine.XR.InputTrackingState.Rotation)) != 0;

        bool hasPosition = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position) &&
                           position.sqrMagnitude > 0.000001f;
        bool hasRotation = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation) &&
                           Quaternion.Angle(rotation, Quaternion.identity) > 0.001f;
        return hasPosition || hasRotation;
    }

    private static bool TryReadInputSystemAxis(InputXRController controller, out Vector2 axis)
    {
        axis = Vector2.zero;
        if (controller == null || !controller.added) return false;

        Vector2Control control = controller.TryGetChildControl<Vector2Control>("primary2DAxis");
        if (control == null) control = controller.TryGetChildControl<Vector2Control>("thumbstick");
        if (control == null) control = controller.TryGetChildControl<Vector2Control>("joystick");
        if (control == null) return false;

        axis = control.ReadValue();
        return true;
    }

    private void RefreshSimulatedControllers()
    {
        foreach (var device in InputSystem.devices)
        {
            if (!(device is XRSimulatedController controller)) continue;
            for (var i = 0; i < controller.usages.Count; i++)
            {
                var usage = controller.usages[i].ToString();
                if (usage == "LeftHand") _simulatedLeftController = controller;
                else if (usage == "RightHand") _simulatedRightController = controller;
            }
        }
    }

    private void CacheRightRayOriginTransform()
    {
        if (_rightRayOriginTransform != null || _xrOrigin == null) return;
        var rays = _xrOrigin.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true);
        foreach (var ray in rays)
        {
            if (ray != null && ray.name.Contains("Right"))
            {
                _rightRayOriginTransform = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
                return;
            }
        }
    }
#endif

    private void ClearCurrentHover()
    {
        if (_currentHoveredObject != null)
        {
            _currentHoveredObject.OnHoverExit();
            _currentHoveredObject = null;
        }
    }

    private void ClearXRCurrentHover()
    {
        _currentXRHoveredObject?.OnHoverExit();
        _currentXRHoveredObject = null;
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority) PublishTrainPose();
        if (!HasStateAuthority || isDriving || _passengerSeat >= 0 || _standLocked) return;

        if (_xrMode)
        {
            transform.rotation = Quaternion.Euler(0f, _horizontalRotation, 0f);
            Vector3 forward = _mainCamera != null ? Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 xrMoveDirection = forward * _xrMove.y + right * _xrMove.x;
            MoveCharacter(xrMoveDirection * xrMoveSpeed * Runner.DeltaTime);
            if (_jumpRequested)
            {
                _ncc.Jump();
                _jumpRequested = false;
            }
            return;
        }

        if (!DesktopLookActive() && !UttWebPresentation.Active) return;
        if (UttWebPresentation.Active && (SceneQuickMenu.IsOpen || UttOverlay.BlocksGameplay || UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>()?.isFocused == true)) return;

        if (_trainRoot != null)
        {
            transform.localRotation = Quaternion.Euler(0f, _horizontalRotation, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, _horizontalRotation, 0f);
        }

        Vector2 keyboardMove = ReadKeyboardMove();
        float moveHorizontal = keyboardMove.x;
        float moveVertical = keyboardMove.y;

        Vector3 moveDirection = (transform.forward * moveVertical + transform.right * moveHorizontal).normalized;
        MoveCharacter(moveDirection * moveSpeed * Runner.DeltaTime);

        if (_jumpRequested)
        {
            _ncc.Jump();
            _jumpRequested = false;
        }
    }

    /// <summary>
    /// NetworkCharacterController tính vận tốc = quãng dịch trong tick × TickRate. Khi CharacterController
    /// bị đẩy ra khỏi collider (spawn lún vào sàn, chạm mép bậc) chỉ vài cm cũng thành cú bật lên rất mạnh
    /// rồi rơi xuống. Không cho vận tốc đi lên vượt quá lực nhảy thật.
    /// </summary>
    private void MoveCharacter(Vector3 motion)
    {
        _ncc.Move(motion);
        Vector3 velocity = _ncc.Velocity;
        if (velocity.y > _ncc.jumpImpulse + 0.5f)
        {
            velocity.y = 0f;
            _ncc.Velocity = velocity;
        }
    }

    // ---------- Web: góc nhìn thứ ba ----------

    /// <summary>Tia ngắm: theo con trỏ (chuột tự do) hoặc tâm màn hình (góc nhìn 1/3).</summary>
    private Ray BuildViewRay()
    {
        Ray ray = new Ray(_mainCamera.transform.position, _mainCamera.transform.forward);
#if ENABLE_INPUT_SYSTEM
        if (UttWebPresentation.Active && !UttWebView.MouseLookActive && Mouse.current != null)
            return _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
#endif
        // Góc nhìn thứ ba: bắt đầu tia từ ngang đầu nhân vật để không trúng chính mình
        // và giữ nguyên tầm tương tác như góc nhìn thứ nhất.
        if (_thirdPersonCamOffset > 0f)
            ray.origin += ray.direction * _thirdPersonCamOffset;
        return ray;
    }

    private void PlaceThirdPersonCamera(Vector3 head, Quaternion lookRotation)
    {
        // Camera đặt sau vai phải, lùi lại thirdPersonDistance, tự kéo gần khi bị tường che.
        // Scene thu nhỏ (tầm mắt bị hạ, ví dụ Yên Tử – Hạ Long): camera lùi và lệch vai theo cùng tỉ lệ.
        float scale = Mathf.Clamp(eyeHeight / 1.65f, 0.4f, 1f);
        Vector3 shoulder = lookRotation * (new Vector3(0.4f, 0.15f, 0f) * scale);
        float shoulderLength = shoulder.magnitude;
        Vector3 shoulderDir = shoulder / shoulderLength;
        Vector3 pivot = head + shoulderDir * CameraClearDistance(head, shoulderDir, shoulderLength);

        Vector3 back = lookRotation * Vector3.back;
        float distance = CameraClearDistance(pivot, back, thirdPersonDistance * scale);
        _mainCamera.transform.SetPositionAndRotation(pivot + back * distance, lookRotation);
        _thirdPersonCamOffset = distance;
    }

    private float CameraClearDistance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        const float radius = 0.2f;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, CameraHits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
        float clear = maxDistance;
        for (int i = 0; i < count; i++)
        {
            var hit = CameraHits[i];
            if (hit.collider == null || hit.distance <= 0f) continue;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance < clear) clear = hit.distance;
        }
        return Mathf.Max(0f, clear);
    }

    private void UpdateWebAvatar()
    {
        bool show = UttWebPresentation.Active && !isDriving && UttWebView.ThirdPersonActive;
        if (show && _webAvatar == null && webAvatarPrefab != null)
            _webAvatar = UttWebAvatar.Create(transform, webAvatarPrefab, webAvatarController, moveSpeed);
        if (_webAvatar != null)
        {
            _webAvatar.SetVisible(show);
            _webAvatar.Seated = _passengerSeat >= 0;
        }
        // Góc nhìn thứ ba: model người đã hiện thì ẩn khối trắng để không bị chồng 2 model.
        if (UttWebPresentation.Active) SetDefaultBodyVisible(!(show && _webAvatar != null && _webAvatar.IsShowing));
    }

    /// <summary>
    /// Bản Web, người chơi khác: model người có animation thay cho khối trắng, giảm chi tiết theo khoảng cách
    /// (gần: đầy đủ; vừa: animation thưa, không bóng; xa: khối tĩnh; rất xa: ẩn cả bảng tên).
    /// </summary>
    private void UpdateRemoteAppearance()
    {
        if (_lod == null) _lod = UttAvatarLod.Register(transform);
        UttAvatarLod.Refresh();
        var tier = _lod.tier;
        bool wantAvatar = tier == UttAvatarLod.Tier.Full || tier == UttAvatarLod.Tier.Mid;

        if (wantAvatar && _webAvatar == null && webAvatarPrefab != null)
            _webAvatar = UttWebAvatar.Create(transform, webAvatarPrefab, webAvatarController, moveSpeed, true);
        if (_webAvatar != null)
        {
            _webAvatar.SetVisible(wantAvatar);
            _webAvatar.Seated = isDriving || NetSeat > 0;
            if (wantAvatar) _webAvatar.SetTier(tier);
        }

        bool avatarShown = wantAvatar && _webAvatar != null && _webAvatar.IsShowing;
        // Khối trắng: dùng cho mức Xa, và tạm thời trong vài khung hình model người đang được co giãn.
        SetDefaultBodyVisible(tier == UttAvatarLod.Tier.Far || (wantAvatar && !avatarShown));

        bool plate = tier != UttAvatarLod.Tier.Hidden;
        if (nameplateObject != null && nameplateObject.activeSelf != plate) nameplateObject.SetActive(plate);
    }

    // ---------- Tàu Yên Tử: đồng bộ người lái và người đứng trong toa ----------

    private void PublishTrainPose()
    {
        NetSeat = _passengerSeat + 1;
        bool inTrain = _trainRoot != null && transform.parent == _trainRoot && !isDriving && _passengerSeat < 0;
        NetInTrain = inTrain;
        if (!inTrain) return;
        NetTrainLocalPos = transform.localPosition;
        NetTrainLocalYaw = transform.localEulerAngles.y;
    }

    public bool IsSeatedIn(TrainSeatInteractable seat) => isDriving && _currentSeat == seat;

    // ---------- Ghế khách (tối đa 20, mỗi ghế 1 người) ----------

    private TrainSplineMover _seatTrain;
    private bool TrainMoving()
    {
        if (_seatTrain == null) _seatTrain = _trainRoot != null ? _trainRoot.GetComponentInChildren<TrainSplineMover>() : null;
        if (_seatTrain == null) _seatTrain = FindFirstObjectByType<TrainSplineMover>();
        return _seatTrain != null && _seatTrain.currentSpeedKmh > 0.5f;
    }

    private void SitInPassengerSeat(int seat)
    {
        if (seat < 0 || isDriving) return;
        ReleaseStandLock(false);
        // Nhớ chỗ đang đứng (trong toa) để lúc đứng dậy quay về đúng chỗ này
        _preSeatCar = UttTrainSeats.GetCar(seat);
        if (_preSeatCar != null) _preSeatLocal = _preSeatCar.InverseTransformPoint(transform.position);
        _passengerSeat = seat;
        _horizontalRotation = 0f;   // nhìn về đầu tàu; chuột vẫn xoay nhìn quanh được
        _verticalRotation = 0f;
        if (_ncc != null) _ncc.enabled = false;
        if (_cc != null) _cc.enabled = false;
        ApplyPassengerSeatPose();
    }

    private void StandFromPassengerSeat()
    {
        if (_passengerSeat < 0 || TrainMoving()) return; // tàu đang chạy: phải ngồi yên
        int seat = _passengerSeat;
        Quaternion look = transform.rotation;
        _passengerSeat = -1;
        _horizontalRotation = (Quaternion.Inverse(_trainRoot.rotation) * look).eulerAngles.y;
        PlaceInsideCar(StandUpPoint(seat));
    }

    /// <summary>
    /// Chỗ đứng dậy: ưu tiên đúng chỗ đã đứng trước khi ngồi (tính theo toa nên tàu chạy rồi vẫn đúng),
    /// không có thì ra lối đi cạnh ghế.
    /// </summary>
    private Vector3 StandUpPoint(int seat)
    {
        if (_preSeatCar != null) return _preSeatCar.TransformPoint(_preSeatLocal);
        return UttTrainSeats.TryGetAisle(seat, out Vector3 aisle) ? aisle : transform.position;
    }

    /// <summary>Đặt người lên đúng mặt sàn toa tại điểm này rồi bật lại di chuyển (không rơi xuyên sàn).</summary>
    private void PlaceInsideCar(Vector3 point)
    {
        Vector3 target = point;
        var hits = Physics.RaycastAll(point + Vector3.up * 1.2f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.distance < best) { best = hit.distance; target = hit.point + Vector3.up * 0.05f; }
        }
        transform.position = target;
        if (_cc != null) _cc.enabled = true;
        if (_ncc != null) { _ncc.enabled = true; _ncc.Teleport(target); }
    }

    private void ApplyPassengerSeatPose()
    {
        if (UttTrainSeats.TryGetPose(_passengerSeat, _horizontalRotation, out Vector3 pos, out Quaternion rot))
            transform.SetPositionAndRotation(pos, rot);
    }

    private void LockStanding(Transform car)
    {
        _standLocked = true;
        _lockCar = car;
        _lockLocal = car.InverseTransformPoint(transform.position);
        _horizontalRotation = (Quaternion.Inverse(car.rotation) * transform.rotation).eulerAngles.y;
        if (_ncc != null) _ncc.enabled = false;
        if (_cc != null) _cc.enabled = false;
    }

    private void ReleaseStandLock(bool restoreMovement)
    {
        if (!_standLocked) return;
        _standLocked = false;
        Quaternion look = transform.rotation;
        _lockCar = null;
        if (!restoreMovement) return;
        if (_cc != null) _cc.enabled = true;
        if (_ncc != null) { _ncc.enabled = true; _ncc.Teleport(transform.position); }
        _horizontalRotation = (Quaternion.Inverse(_trainRoot.rotation) * look).eulerAngles.y;
    }

    /// <summary>
    /// Mỗi khung hình (chỉ người chơi của mình, ở scene có tàu):
    ///  - Đang ngồi ghế khách: gắn chặt vào ghế (ghế thuộc toa nên vào cua, tăng tốc không văng).
    ///  - Tàu bắt đầu chạy mà đang đứng trong toa: tự ngồi ghế trống gần nhất; hết ghế thì đứng cố định trong toa.
    ///  - Tàu dừng hẳn: người đứng cố định được đi lại bình thường.
    /// Tìm ghế chỉ chạy tối đa 2 lần/giây.
    /// </summary>
    private void UpdatePassengerState()
    {
        if (_passengerSeat >= 0)
        {
            if (Time.unscaledTime >= _nextSeatCheck)
            {
                _nextSeatCheck = Time.unscaledTime + 0.5f;
                // Hai người cùng bấm một ghế: người vào phòng trước giữ ghế, mình sang ghế trống gần nhất.
                if (UttTrainSeats.MustYield(_passengerSeat, this))
                {
                    int other = UttTrainSeats.FindNearestFree(transform.position, 100f, this);
                    if (other >= 0) _passengerSeat = other;
                    else
                    {
                        int seat = _passengerSeat;
                        _passengerSeat = -1;
                        PlaceInsideCar(StandUpPoint(seat));
                        if (TrainMoving() && UttTrainSeats.TryGetCarAt(transform.position, out Transform car)) LockStanding(car);
                    }
                }
            }
            if (_passengerSeat >= 0) ApplyPassengerSeatPose();
            return;
        }

        if (_standLocked)
        {
            if (_lockCar == null || !TrainMoving()) { ReleaseStandLock(true); return; }
            transform.SetPositionAndRotation(_lockCar.TransformPoint(_lockLocal),
                _lockCar.rotation * Quaternion.Euler(0f, _horizontalRotation, 0f));
            return;
        }

        if (Time.unscaledTime < _nextSeatCheck) return;
        _nextSeatCheck = Time.unscaledTime + 0.5f;
        if (!TrainMoving() || !UttTrainSeats.TryGetCarAt(transform.position, out Transform inCar)) return;

        int free = UttTrainSeats.FindNearestFree(transform.position, 100f, this);
        if (free >= 0) SitInPassengerSeat(free);
        else LockStanding(inCar);
    }

    /// <summary>Máy người khác: đánh dấu nhân vật này đang/không còn ngồi ghế lái (TrainSeatInteractable gọi).</summary>
    public void SetRemoteSeated(TrainSeatInteractable seat, bool seated)
    {
        if (HasStateAuthority) return;
        if (seated)
        {
            isDriving = true;
            _currentSeat = seat;
            if (_ncc != null) _ncc.enabled = false;
            if (_cc != null) _cc.enabled = false;
        }
        else if (_currentSeat == seat)
        {
            isDriving = false;
            _currentSeat = null;
            if (_ncc != null) _ncc.enabled = true;
            if (_cc != null) _cc.enabled = true;
        }
    }

    /// <summary>
    /// Máy người khác: người lái luôn nằm đúng ghế của con tàu (đã đồng bộ) trên máy này;
    /// người đứng trong toa đặt theo vị trí tính theo con tàu.
    /// </summary>
    private void ApplyRemoteTrainPose()
    {
        if (isDriving && _currentSeat != null)
        {
            Transform mount = _currentSeat.driverSeatPoint != null ? _currentSeat.driverSeatPoint : _currentSeat.transform;
            transform.SetPositionAndRotation(mount.position, mount.rotation);
            _remoteHasPose = false;
            return;
        }

        if (NetSeat > 0)
        {
            if (UttTrainSeats.TryGetPose(NetSeat - 1, 0f, out Vector3 seatPos, out Quaternion seatRot))
                transform.SetPositionAndRotation(seatPos, seatRot);
            _remoteHasPose = false;
            return;
        }

        if (NetInTrain)
        {
            if (_remoteTrainRoot == null)
            {
                GameObject train = GameObject.Find("Train_THSR_700T");
                _remoteTrainRoot = train != null ? train.transform : null;
                if (_remoteTrainRoot == null) return;
            }
            if (transform.parent != _remoteTrainRoot)
            {
                transform.SetParent(_remoteTrainRoot, true);
                _remoteHasPose = false;
            }

            Vector3 target = NetTrainLocalPos;
            float yaw = NetTrainLocalYaw;
            if (!_remoteHasPose || (target - _remoteLocalPos).sqrMagnitude > 9f)
            {
                _remoteLocalPos = target;
                _remoteLocalYaw = yaw;
                _remoteHasPose = true;
            }
            else
            {
                float k = 1f - Mathf.Exp(-15f * Time.deltaTime);
                _remoteLocalPos = Vector3.Lerp(_remoteLocalPos, target, k);
                _remoteLocalYaw = Mathf.LerpAngle(_remoteLocalYaw, yaw, k);
            }
            transform.localPosition = _remoteLocalPos;
            transform.localRotation = Quaternion.Euler(0f, _remoteLocalYaw, 0f);
        }
        else if (_remoteTrainRoot != null && transform.parent == _remoteTrainRoot)
        {
            transform.SetParent(null, true);
            _remoteHasPose = false;
        }
    }

    private void SetDefaultBodyVisible(bool visible)
    {
        if (_defaultBody == null)
        {
            Transform body = FindChildRecursive(transform, "Model_Offset") ?? FindChildRecursive(transform, "VisualModel");
            _defaultBody = body != null ? body.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            _defaultBodyVisible = true;
        }
        if (_defaultBodyVisible == visible) return;
        _defaultBodyVisible = visible;
        foreach (var r in _defaultBody)
            if (r != null) r.enabled = visible;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static Vector2 ReadMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

    private static bool WasKeyPressedThisFrame(KeyCode key)
    {
        if (UttChat.IsTyping) return false; // đang gõ chat: F, Space, E không điều khiển nhân vật
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        return key switch
        {
            KeyCode.F => Keyboard.current.fKey.wasPressedThisFrame,
            KeyCode.E => Keyboard.current.eKey.wasPressedThisFrame,
            KeyCode.Space => Keyboard.current.spaceKey.wasPressedThisFrame,
            _ => false
        };
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        return false;
#endif
    }

    private static bool WasMousePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static Vector2 ReadKeyboardMove()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return Vector2.zero;
        float horizontal = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) -
                           (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
        float vertical = (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ? 1f : 0f) -
                         (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
        return new Vector2(horizontal, vertical);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    private void LateUpdate()
    {
        if (Object == null || !Object.IsValid) return;

        if (HasStateAuthority)
        {
            if (_trainRoot != null && !isDriving) UpdatePassengerState();

            if (_xrMode)
            {
                if (_xrOrigin == null)
                {
                    GameObject originObject = GameObject.Find("XR Origin (VR)");
                    _xrOrigin = originObject != null ? originObject.transform : null;
                }
                if (_xrOrigin != null)
                {
                    if (isDriving && _driverViewAnchor != null && HasStateAuthority)
                    {
                        Transform mount = _currentSeat != null && _currentSeat.driverSeatPoint != null
                            ? _currentSeat.driverSeatPoint
                            : _driverViewAnchor.parent;
                        if (mount != null)
                            transform.SetPositionAndRotation(mount.position, mount.rotation);
                        FollowDriverViewAnchor();
                    }
                    else
                    {
                        _xrOrigin.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, _horizontalRotation, 0f));
                    }
                    if (_mainCamera == null || !_mainCamera.enabled) _mainCamera = ResolvePlayerCamera();
                }
            }

            if (_mainCamera == null) _mainCamera = ResolvePlayerCamera();

            if (_mainCamera != null && !_xrMode)
            {
                if (isDriving && _currentSeat != null)
                {
                    Transform mount = _currentSeat.driverSeatPoint != null ? _currentSeat.driverSeatPoint : _currentSeat.transform;
                    transform.position = mount.position;

                    Transform view = UttWebPresentation.Active ? _driverViewAnchor : null;
                    _mainCamera.transform.position = view != null ? view.position : mount.position + new Vector3(0f, 1.15f, 0f);
                    Quaternion cabinBaseRot = view != null ? view.rotation : mount.rotation;
                    _mainCamera.transform.rotation = cabinBaseRot * Quaternion.Euler(_verticalRotation, _horizontalRotation, 0f);
                    _thirdPersonCamOffset = 0f;
                }
                else
                {
                    Vector3 head = transform.position + new Vector3(0f, eyeHeight, 0f);
                    Quaternion lookRotation = _trainRoot != null
                        ? transform.rotation * Quaternion.Euler(_verticalRotation, 0f, 0f)
                        : Quaternion.Euler(_verticalRotation, _horizontalRotation, 0f);

                    if (UttWebView.ThirdPersonActive)
                    {
                        PlaceThirdPersonCamera(head, lookRotation);
                    }
                    else
                    {
                        _mainCamera.transform.SetPositionAndRotation(head, lookRotation);
                        _thirdPersonCamOffset = 0f;
                    }
                }
            }

            if (!_xrMode) UpdateWebAvatar();
        }
        else
        {
            ApplyRemoteTrainPose();
            if (UttWebPresentation.Active) UpdateRemoteAppearance();
        }

        if (nameplateObject != null && nameplateObject.activeInHierarchy)
        {
            if (nameplateText != null)
            {
                var plateName = NetworkPlayerName;
                var plateRole = NetworkPlayerRole;
                if (!_nameplateSet || !plateName.Equals(_shownName) || !plateRole.Equals(_shownRole) || _shownDriving != isDriving)
                {
                    _nameplateSet = true;
                    _shownName = plateName;
                    _shownRole = plateRole;
                    _shownDriving = isDriving;
                    string driving = isDriving ? " · Đang lái tàu" : "";
                    nameplateText.text = $"{plateName}\n<size=70%><color=#FFD700>{plateRole}{driving}</color></size>";
                }
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                nameplateObject.transform.rotation = cam.transform.rotation;
            }
        }
    }
}

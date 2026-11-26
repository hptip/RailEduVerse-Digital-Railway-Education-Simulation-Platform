using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class TrainSeatInteractable : NetworkBehaviour
{
    [Header("Mốc vị trí ngồi và Điều khiển tàu")]
    public Transform driverSeatPoint;
    public TrainSplineMover trainMover;

    [Header("Đồng bộ mạng qua Fusion")]
    [Networked] public PlayerRef CurrentDriver { get; set; } = PlayerRef.None;

    // Trạng thái tàu do người giữ quyền tàu (người đang lái, hoặc chủ phòng khi không ai lái) gửi đi.
    // Cần tích "Allow State Authority Override" trên NetworkObject của Train_THSR_700T để người lái xin được quyền.
    [Networked] public float NetDistance { get; set; }
    [Networked] public float NetSpeedKmh { get; set; }
    [Networked] public float NetTargetKmh { get; set; }
    [Networked] public int NetSpeedLevel { get; set; }

    private readonly List<FirstPersonController> _proxies = new List<FirstPersonController>();
    private float _nextProxyScan, _nextAuthorityCheck;
    private bool _singlePlayerOccupied;
    private FirstPersonController _singlePlayerDriver;

    public bool IsOccupied => _singlePlayerOccupied ||
        (Object != null && Object.IsValid && CurrentDriver != PlayerRef.None);

    public override void Spawned()
    {
        if (trainMover != null)
        {
            trainMover.enabled = true;
            trainMover.isDriverSeated = false;
        }
    }

    /// <summary>Người giữ quyền tàu ghi trạng thái tàu mỗi nhịp mạng (Fusion chỉ gửi khi giá trị đổi).</summary>
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || trainMover == null) return;
        NetDistance = trainMover.currentDistanceMeters;
        NetSpeedKmh = trainMover.currentSpeedKmh;
        NetTargetKmh = trainMover.targetSpeedKmh;
        NetSpeedLevel = trainMover.SpeedLevel;

        // Người lái đã thoát khỏi phòng: giải phóng ghế để người khác ngồi được.
        if (CurrentDriver != PlayerRef.None && Runner.GameMode != GameMode.Single && !IsActivePlayer(CurrentDriver))
            CurrentDriver = PlayerRef.None;
    }

    public override void Render()
    {
        if (trainMover == null || Runner == null) return;
        bool networked = Runner.IsRunning && Runner.GameMode != GameMode.Single;
        bool follow = networked && !Object.HasStateAuthority;
        trainMover.followNetwork = follow;
        if (follow) trainMover.SetNetworkState(NetDistance, NetSpeedKmh, NetTargetKmh, NetSpeedLevel);
        if (!networked) return;

        // Không ai giữ quyền tàu (người giữ quyền vừa thoát): chủ phòng nhận lại để tàu không bị bỏ trôi.
        if (Time.unscaledTime >= _nextAuthorityCheck)
        {
            _nextAuthorityCheck = Time.unscaledTime + 1f;
            if (!Object.HasStateAuthority && Runner.IsSharedModeMasterClient &&
                (Object.StateAuthority == PlayerRef.None || !IsActivePlayer(Object.StateAuthority)))
                Object.RequestStateAuthority();
        }

        SyncDriverProxies();
    }

    private bool IsActivePlayer(PlayerRef player)
    {
        foreach (var p in Runner.ActivePlayers)
            if (p == player) return true;
        return false;
    }

    /// <summary>
    /// Trên máy người khác: đặt nhân vật của người lái vào ghế (kể cả người vào phòng sau, không nhận được RPC ngồi ghế),
    /// và trả lại khi người lái rời ghế.
    /// </summary>
    private void SyncDriverProxies()
    {
        if (Time.unscaledTime >= _nextProxyScan)
        {
            _nextProxyScan = Time.unscaledTime + 1f;
            _proxies.Clear();
            foreach (var p in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                if (p != null && p.Object != null && p.Object.IsValid && !p.HasStateAuthority) _proxies.Add(p);
        }

        PlayerRef driver = CurrentDriver;
        foreach (var p in _proxies)
        {
            if (p == null || p.Object == null || !p.Object.IsValid) continue;
            bool shouldSit = driver != PlayerRef.None && p.Object.StateAuthority == driver;
            if (shouldSit != p.IsSeatedIn(this)) p.SetRemoteSeated(this, shouldSit);
        }
    }

    public void RequestOccupy(FirstPersonController player)
    {
        if (player == null || IsOccupied) return;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj == null)
            return;

        // Offline single-player scenes are often entered directly. Handle the
        // local seat transition without relying on a replicated RPC round trip.
        if (player.Runner != null && player.Runner.IsRunning &&
            player.Runner.GameMode == GameMode.Single && player.HasStateAuthority)
        {
            _singlePlayerOccupied = true;
            _singlePlayerDriver = player;
            ApplyOccupy(netObj);
            return;
        }

        if (Object == null || !Object.IsValid || Runner == null || !Runner.IsRunning)
            return;

        PlayerRef playerRef = player.Object.InputAuthority.IsValid ? player.Object.InputAuthority : Runner.LocalPlayer;
        RPC_YeuCauNgoiGhe(playerRef, netObj);
    }

    public void RequestVacate(FirstPersonController player)
    {
        if (player == null) return;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj == null)
            return;

        if (_singlePlayerDriver == player && player.Runner != null &&
            player.Runner.GameMode == GameMode.Single && player.HasStateAuthority)
        {
            if (trainMover != null && !trainMover.IsStopped) return;
            _singlePlayerOccupied = false;
            _singlePlayerDriver = null;
            player.ThietLapRoiGhe();
            if (trainMover != null)
                trainMover.isDriverSeated = false;
            return;
        }

        if (Object == null || !Object.IsValid || Runner == null || !Runner.IsRunning)
            return;

        PlayerRef playerRef = player.Object.InputAuthority.IsValid ? player.Object.InputAuthority : Runner.LocalPlayer;
        RPC_YeuCauRoiGhe(playerRef, netObj);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_YeuCauNgoiGhe(PlayerRef requestingPlayer, NetworkObject playerNetObj)
    {
        if (IsOccupied && CurrentDriver != requestingPlayer) return;

        CurrentDriver = requestingPlayer;
        // Người lái nhận quyền tàu để tự chạy tàu và gửi trạng thái cho mọi người.
        if (requestingPlayer == Runner.LocalPlayer && !Object.HasStateAuthority)
            Object.RequestStateAuthority();
        ApplyOccupy(playerNetObj);
    }

    private void ApplyOccupy(NetworkObject playerNetObj)
    {
        // Chỉ máy của chính người lái mới nhận phím ga/phanh (trước đây mọi máy đều bật, người khác bấm W cũng chạy tàu).
        FirstPersonController occupant = playerNetObj != null ? playerNetObj.GetComponent<FirstPersonController>() : null;
        if (trainMover != null && (occupant == null || occupant.HasStateAuthority))
            trainMover.isDriverSeated = true;

        if (playerNetObj != null)
        {
            FirstPersonController controller = playerNetObj.GetComponent<FirstPersonController>();
            if (controller != null)
            {
                Transform mount = driverSeatPoint != null ? driverSeatPoint : transform;
                controller.ThietLapVaoGhe(this, mount);

            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_YeuCauRoiGhe(PlayerRef requestingPlayer, NetworkObject playerNetObj)
    {
        if (trainMover != null && !trainMover.IsStopped) return;

        _singlePlayerOccupied = false;
        CurrentDriver = PlayerRef.None;

        if (playerNetObj != null)
        {
            FirstPersonController controller = playerNetObj.GetComponent<FirstPersonController>();
            if (controller != null)
            {
                controller.ThietLapRoiGhe();

                // Ngắt nhận phím lái, tuyệt đối không tắt trainMover để tàu giữ tốc độ chạy tiếp
                if (trainMover != null)
                {
                    trainMover.isDriverSeated = false;
                }
            }
        }
    }
}

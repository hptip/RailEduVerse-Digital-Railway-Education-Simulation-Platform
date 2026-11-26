using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ghế khách trên tàu Yên Tử – Hạ Long (Train_THSR_700T).
/// Lấy từ model ghế có sẵn: mỗi object con của "Seat_GP" là 1 ghế cho 1 người (2 dãy hai bên lối đi, hàng cách nhau ~1 m).
/// Ưu tiên toa đầu ("dau", nơi mọi người xuất hiện, 25 ghế) rồi tới toa thân ("than"), lấy tối đa MaxSeats ghế.
/// Thứ tự ghế giống hệt nhau trên mọi máy (cùng prefab) nên chỉ cần gửi số thứ tự ghế qua mạng.
///
/// Không tốn gì mỗi khung hình: danh sách ghế dựng một lần mỗi scene; tìm ghế trống chỉ chạy khi bấm F
/// hoặc khi tàu bắt đầu chạy; danh sách người chơi làm mới tối đa 2 lần/giây.
/// </summary>
public static class UttTrainSeats
{
    public const int MaxSeats = 20;
    const string TrainName = "Train_THSR_700T";
    static readonly string[] CarOrder = { "dau", "than" };
    /// <summary>Lối đi giữa 2 dãy ghế (toạ độ x trong Seat_GP: dãy trái -1,1 m, dãy phải 0,88 m).</summary>
    const float AisleX = -0.11f;

    struct Seat
    {
        public Transform unit;    // object ghế
        public Transform seatGp;  // Seat_GP chứa ghế
        public Transform car;     // toa (hướng nhìn = hướng tàu chạy)
        public Vector3 localFeet; // vị trí chân người ngồi, trong toạ độ của object ghế
    }

    struct CarArea
    {
        public Transform car, seatGp;
        public float minZ, maxZ;
    }

    static readonly List<Seat> Seats = new List<Seat>();
    static readonly List<CarArea> Cars = new List<CarArea>();
    static int _sceneHandle = int.MinValue;
    static Transform _train;

    static readonly List<FirstPersonController> Players = new List<FirstPersonController>();
    static float _nextPlayerScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Seats.Clear();
        Cars.Clear();
        Players.Clear();
        _sceneHandle = int.MinValue;
        _train = null;
        _nextPlayerScan = 0f;
    }

    public static int Count { get { EnsureBuilt(); return Seats.Count; } }

    static void EnsureBuilt()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.handle == _sceneHandle && (_train != null || Seats.Count == 0)) return;
        _sceneHandle = scene.handle;
        Seats.Clear();
        Cars.Clear();
        GameObject trainGo = GameObject.Find(TrainName);
        _train = trainGo != null ? trainGo.transform : null;
        if (_train == null) return;

        foreach (string carName in CarOrder)
        {
            Transform car = _train.Find(carName);
            if (car == null) continue;
            Transform seatGp = FindDeep(car, "Seat_GP");
            if (seatGp == null || seatGp.childCount == 0) continue;

            // Sắp xếp cố định: từ đầu toa về cuối (z giảm dần), dãy trái trước dãy phải.
            var units = new List<Transform>();
            foreach (Transform u in seatGp) units.Add(u);
            units.Sort((a, b) =>
            {
                int z = (-Mathf.Round(a.localPosition.z * 10f)).CompareTo(-Mathf.Round(b.localPosition.z * 10f));
                return z != 0 ? z : a.localPosition.x.CompareTo(b.localPosition.x);
            });

            var area = new CarArea { car = car, seatGp = seatGp, minZ = float.MaxValue, maxZ = float.MinValue };
            foreach (var u in units)
            {
                area.minZ = Mathf.Min(area.minZ, u.localPosition.z);
                area.maxZ = Mathf.Max(area.maxZ, u.localPosition.z);
                if (Seats.Count >= MaxSeats) continue;
                Seats.Add(new Seat { unit = u, seatGp = seatGp, car = car, localFeet = FeetPoint(u) });
            }
            Cars.Add(area);
        }
        Debug.Log($"[UttTrainSeats] Tìm thấy {Seats.Count} ghế khách trên {TrainName}.");
    }

    /// <summary>Tâm ghế theo chiều ngang, chân chạm đáy ghế (≈ sàn toa).</summary>
    static Vector3 FeetPoint(Transform unit)
    {
        bool has = false;
        Bounds b = default;
        foreach (var r in unit.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has) return Vector3.zero;
        return unit.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
    }

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    /// <summary>Vị trí và hướng của người ngồi ghế index (nhìn về đầu tàu, xoay thêm yaw độ).</summary>
    public static bool TryGetPose(int index, float yaw, out Vector3 position, out Quaternion rotation)
    {
        EnsureBuilt();
        position = default;
        rotation = Quaternion.identity;
        if (index < 0 || index >= Seats.Count || Seats[index].unit == null) return false;
        var s = Seats[index];
        position = s.unit.TransformPoint(s.localFeet);
        rotation = Quaternion.LookRotation(s.car.forward, s.car.up) * Quaternion.Euler(0f, yaw, 0f);
        return true;
    }

    /// <summary>Toa chứa ghế index (để nhớ chỗ đứng trước khi ngồi, tính theo toa).</summary>
    public static Transform GetCar(int index)
    {
        EnsureBuilt();
        return index >= 0 && index < Seats.Count ? Seats[index].car : null;
    }

    /// <summary>Chỗ đứng ở lối đi ngay cạnh ghế (khi đứng dậy).</summary>
    public static bool TryGetAisle(int index, out Vector3 position)
    {
        EnsureBuilt();
        position = default;
        if (index < 0 || index >= Seats.Count || Seats[index].unit == null) return false;
        var s = Seats[index];
        Vector3 feet = s.seatGp.InverseTransformPoint(s.unit.TransformPoint(s.localFeet));
        position = s.seatGp.TransformPoint(new Vector3(AisleX, feet.y, feet.z));
        return true;
    }

    /// <summary>Điểm này có nằm trong một toa có ghế không; trả về toa đó.</summary>
    public static bool TryGetCarAt(Vector3 world, out Transform car)
    {
        EnsureBuilt();
        car = null;
        foreach (var a in Cars)
        {
            if (a.seatGp == null) continue;
            Vector3 l = a.seatGp.InverseTransformPoint(world);
            if (Mathf.Abs(l.x - AisleX) < 1.9f && l.y > -3.5f && l.y < 2.5f && l.z > a.minZ - 4f && l.z < a.maxZ + 4f)
            {
                car = a.car;
                return true;
            }
        }
        return false;
    }

    /// <summary>Ghế trống gần nhất (trong maxDistance mét), -1 nếu không có.</summary>
    public static int FindNearestFree(Vector3 world, float maxDistance, FirstPersonController self)
    {
        EnsureBuilt();
        RefreshPlayers();
        int best = -1;
        float bestSqr = maxDistance * maxDistance;
        for (int i = 0; i < Seats.Count; i++)
        {
            if (Seats[i].unit == null || IsTaken(i, self)) continue;
            float d = (Seats[i].unit.TransformPoint(Seats[i].localFeet) - world).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = i; }
        }
        return best;
    }

    /// <summary>Ghế có người khác ngồi chưa.</summary>
    public static bool IsTaken(int index, FirstPersonController self)
    {
        RefreshPlayers();
        foreach (var p in Players)
            if (p != null && p != self && p.Object != null && p.Object.IsValid && p.NetSeat == index + 1) return true;
        return false;
    }

    /// <summary>
    /// Hai người bấm cùng một ghế cùng lúc: người có số PlayerId nhỏ hơn được giữ ghế.
    /// Trả về true nếu self phải nhường ghế index.
    /// </summary>
    public static bool MustYield(int index, FirstPersonController self)
    {
        RefreshPlayers();
        int mine = self.Object != null ? self.Object.StateAuthority.PlayerId : int.MaxValue;
        foreach (var p in Players)
        {
            if (p == null || p == self || p.Object == null || !p.Object.IsValid || p.NetSeat != index + 1) continue;
            if (p.Object.StateAuthority.PlayerId < mine) return true;
        }
        return false;
    }

    static void RefreshPlayers()
    {
        if (Time.unscaledTime < _nextPlayerScan) return;
        _nextPlayerScan = Time.unscaledTime + 0.5f;
        Players.Clear();
        Players.AddRange(Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None));
    }
}

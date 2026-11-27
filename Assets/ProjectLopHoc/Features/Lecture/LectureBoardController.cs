using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using ProjectLopHoc.Core.ScriptableObjects;
namespace ProjectLopHoc.Features.Lecture
{
    [RequireComponent(typeof(NetworkObject))]
    public class LectureBoardController : NetworkBehaviour
    {
        public ClassroomConfigSO classroomConfig;
        public Renderer boardRenderer;
        public Image boardImage;
        [Networked] public int CurrentSlideIndex { get; set; }
        [Networked] public int CurrentLectureIndex { get; set; }
        // Chế độ trình chiếu (bản Web): giảng viên bật, mọi người trong phòng đều thấy.
        [Networked] public NetworkBool IsPresenting { get; set; }
        [Networked] public NetworkString<_64> PresenterName { get; set; }
        private bool _offlinePresenting; private string _offlinePresenter = "";
        private int _offlineSlideIndex, _offlineLectureIndex;
        private bool HasNetworkState => Object != null && Object.IsValid;
        public int DisplaySlideIndex => HasNetworkState ? CurrentSlideIndex : _offlineSlideIndex;
        public int DisplayLectureIndex => HasNetworkState ? CurrentLectureIndex : _offlineLectureIndex;
        public int LectureCount => classroomConfig != null && classroomConfig.availableLectures != null ? classroomConfig.availableLectures.Count : 0;
        public int SlideCount => _loadedSlides.Length;
        public Sprite DisplaySprite => SlideCount == 0 ? null : _loadedSlides[Mathf.Clamp(DisplaySlideIndex, 0, SlideCount - 1)];
        public string DisplayLectureName => DisplayLectureIndex >= 0 && DisplayLectureIndex < LectureCount ? classroomConfig.availableLectures[DisplayLectureIndex] : "";
        // Local study is allowed; shared presentation controls retain the lecturer role.
        public bool CanNavigate => !HasNetworkState || Runner == null || Runner.GameMode == GameMode.Single || UttSession.IsLecturer;
        public bool CanPreviousLecture => CanNavigate && LectureCount > 0 && DisplayLectureIndex > 0;
        public bool CanNextLecture => CanNavigate && DisplayLectureIndex + 1 < LectureCount;
        public event Action PresentationChanged;
        private Sprite[] _loadedSlides = Array.Empty<Sprite>();
        private int _cachedLectureIndex = -1, _cachedSlideIndex = -1, _cachedCount = -1;
        private ClassroomConfigSO _cachedConfig;
        private string _cachedFolder;
        private readonly Dictionary<string, Sprite[]> _lectureCache = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
        private readonly Dictionary<Texture2D, Sprite> _textureSprites = new Dictionary<Texture2D, Sprite>();
        private Material _boardMat;
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap"), MainTexId = Shader.PropertyToID("_MainTex");
        private void Start() => RefreshPresentation();
        public override void Spawned() { _cachedLectureIndex = -1; RefreshPresentation(); }
        public override void Render() => RefreshPresentation();
        private void Update() { if (!HasNetworkState) RefreshPresentation(); }
        public void RefreshPresentation()
        {
            int lecture = DisplayLectureIndex, slide = DisplaySlideIndex;
            string folder = DisplayLectureName;
            bool changed = lecture != _cachedLectureIndex || classroomConfig != _cachedConfig || folder != _cachedFolder || LectureCount != _cachedCount;
            if (!changed && slide == _cachedSlideIndex) return;
            if (changed) { _loadedSlides = LoadSlides(folder); _cachedLectureIndex = lecture; _cachedConfig = classroomConfig; _cachedFolder = folder; _cachedCount = LectureCount; }
            _cachedSlideIndex = slide;
            var sprite = DisplaySprite;
            if (boardImage != null) { boardImage.sprite = sprite; boardImage.enabled = sprite != null; boardImage.preserveAspect = true; }
            if (boardRenderer != null)
            {
                // Material riêng của màn chiếu (MaterialPropertyBlock không ổn định trên bản Web), hiển thị cả 2 mặt.
                if (_boardMat == null)
                {
                    _boardMat = boardRenderer.material;
                    if (_boardMat.HasProperty("_Cull")) _boardMat.SetFloat("_Cull", 0f);
                    if (_boardMat.HasProperty("_BaseColor")) _boardMat.SetColor("_BaseColor", Color.white);
                }
                Texture texture = sprite != null ? sprite.texture : Texture2D.whiteTexture;
                if (_boardMat.HasProperty(BaseMapId)) _boardMat.SetTexture(BaseMapId, texture);
                if (_boardMat.HasProperty(MainTexId)) _boardMat.SetTexture(MainTexId, texture);
            }
            PresentationChanged?.Invoke();
        }
        private Sprite[] LoadSlides(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return Array.Empty<Sprite>();
            if (_lectureCache.TryGetValue(folder, out var cached)) return cached;
            var slides = new List<Sprite>(Resources.LoadAll<Sprite>("Lectures/" + folder));
            var coveredTextures = new HashSet<Texture2D>();
            foreach (var sprite in slides) coveredTextures.Add(sprite.texture);
            {
                var textures = Resources.LoadAll<Texture2D>("Lectures/" + folder);
                for (int i = 0; i < textures.Length; i++)
                {
                    var texture = textures[i];
                    if (coveredTextures.Contains(texture)) continue;
                    if (!_textureSprites.TryGetValue(texture, out var sprite))
                    {
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                        sprite.name = texture.name; sprite.hideFlags = HideFlags.DontSave; _textureSprites[texture] = sprite;
                    }
                    slides.Add(sprite);
                }
            }
            var sprites = slides.ToArray();
            Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            _lectureCache[folder] = sprites; return sprites;
        }
        public void NextSlide() => ChangeSlide(1);
        public void PreviousSlide() => ChangeSlide(-1);
        public void NextLecture() => SelectLecture(DisplayLectureIndex + 1);
        public void PreviousLecture() => SelectLecture(DisplayLectureIndex - 1);
        public void SelectLecture(int index)
        {
            if (!CanNavigate || index < 0 || index >= LectureCount) return;
            if (HasNetworkState) RPC_RequestSelectLecture(index); else ApplyLecture(index);
        }
        private void ApplyLecture(int index)
        {
            if (index < 0 || index >= LectureCount) return;
            if (HasNetworkState) { CurrentLectureIndex = index; CurrentSlideIndex = 0; }
            else { _offlineLectureIndex = index; _offlineSlideIndex = 0; }
            RefreshPresentation();
        }
        private void ChangeSlide(int direction)
        {
            if (!CanNavigate) return;
            RefreshPresentation();
            if (HasNetworkState) RPC_RequestChangeSlide(direction);
            else { _offlineSlideIndex = SlideCount == 0 ? 0 : Mathf.Clamp(_offlineSlideIndex + direction, 0, SlideCount - 1); RefreshPresentation(); }
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestChangeSlide(int direction)
        {
            RefreshPresentation(); CurrentSlideIndex = SlideCount == 0 ? 0 : Mathf.Clamp(CurrentSlideIndex + direction, 0, SlideCount - 1); RefreshPresentation();
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestChangeLecture(int direction)
        {
            if (LectureCount > 0) ApplyLecture(Mathf.Clamp(CurrentLectureIndex + direction, 0, LectureCount - 1));
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestSelectLecture(int index) => ApplyLecture(index);
        // ===== Trình chiếu =====
        public bool Presenting => HasNetworkState ? (bool)IsPresenting : _offlinePresenting;
        public string PresenterDisplayName => HasNetworkState ? PresenterName.ToString() : _offlinePresenter;
        public event Action<string, bool> HandChanged;
        public event Action LookRequested;
        public Sprite GetSlide(int index) => index >= 0 && index < SlideCount ? _loadedSlides[index] : null;
        public Vector3 BoardCenter => boardRenderer != null ? boardRenderer.bounds.center : boardImage != null ? boardImage.transform.position : transform.position;
        private static string Short(string s) { s = s ?? ""; return s.Length > 60 ? s.Substring(0, 60) : s; }
        public void SetPresenting(bool on, string presenter)
        {
            if (!CanNavigate) return;
            presenter = Short(presenter);
            if (HasNetworkState) RPC_SetPresenting(on, presenter);
            else { _offlinePresenting = on; _offlinePresenter = on ? presenter : ""; }
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_SetPresenting(NetworkBool on, string presenter) { IsPresenting = on; PresenterName = on ? presenter : ""; }
        public void GoToSlide(int index)
        {
            if (!CanNavigate) return;
            RefreshPresentation();
            if (HasNetworkState) RPC_RequestGoToSlide(index);
            else { _offlineSlideIndex = SlideCount == 0 ? 0 : Mathf.Clamp(index, 0, SlideCount - 1); RefreshPresentation(); }
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestGoToSlide(int index)
        {
            RefreshPresentation(); CurrentSlideIndex = SlideCount == 0 ? 0 : Mathf.Clamp(index, 0, SlideCount - 1); RefreshPresentation();
        }
        /// <summary>Học viên giơ/hạ tay; giảng viên cũng gọi được để hạ tay hộ.</summary>
        public void RaiseHand(string who, bool up)
        {
            who = Short(who);
            if (HasNetworkState) RPC_Hand(who, up); else HandChanged?.Invoke(who, up);
        }
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_Hand(string who, NetworkBool up) => HandChanged?.Invoke(who, up);
        /// <summary>Giảng viên gọi cả lớp quay góc nhìn về màn chiếu.</summary>
        public void CallClassToBoard()
        {
            if (!CanNavigate) return;
            if (HasNetworkState) RPC_LookAtBoard(); else LookRequested?.Invoke();
        }
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_LookAtBoard() => LookRequested?.Invoke();
        private void OnDestroy()
        {
            if (_boardMat != null) Destroy(_boardMat);
            foreach (var sprite in _textureSprites.Values) if (sprite != null) Destroy(sprite);
            _textureSprites.Clear(); _lectureCache.Clear();
        }
    }
}

// ============================================================
// WindManager.cs  —  2단계 완전 버전
// 바람 방향·세기 관리 및 외력 벡터 제공
// ============================================================
using UnityEngine;
using TMPro;   // TextMeshPro 사용 시. 없으면 UnityEngine.UI의 Text로 교체

public class WindManager : MonoBehaviour
{
    // ── Inspector 설정 ────────────────────────────────────────
    [Header("바람 파라미터")]
    [Range(0f, 25f)]   public float windStrength  = 5f;    // 바람 세기 (N 또는 m/s 스케일)
    [Range(0f, 360f)]  public float windDirAngle  = 45f;   // XZ 평면 방향 각도 (Y축 기준, °)

    [Header("랜덤 바람 범위")]
    public float minStrength = 1f;
    public float maxStrength = 18f;

    [Header("UI 연결 (선택)")]
    public TMP_Text windInfoText;      // "바람: 북동 5.0m/s" 형식으로 표시
    public RectTransform windArrow;    // 화살표 UI 오브젝트 (Z 회전으로 방향 표시)

    // ── 내부 캐시 ─────────────────────────────────────────────
    private Vector3 _cachedForce;
    private bool    _dirty = true;    // 파라미터 변경 시 재계산 플래그

    // ─────────────────────────────────────────────────────────
    void Start()
    {
        RefreshCache();
        UpdateWindUI();
    }

    // ─────────────────────────────────────────────────────────
    // 핵심 메서드: 바람 외력 벡터 반환
    // BallPhysicsController.UpdateFlight()에서 매 FixedUpdate 호출
    // ─────────────────────────────────────────────────────────
    public Vector3 GetWindForce()
    {
        if (_dirty) RefreshCache();
        return _cachedForce;
    }

    // ─────────────────────────────────────────────────────────
    //   windDirAngle은 Y축 기준 회전 각도 (θ)
    //   F_wind = windStrength × (sin θ, 0, cos θ)
    //   → XZ 평면 수평 벡터: sin θ = X성분, cos θ = Z성분
    //   실제 가속도 변환은 BallPhysicsController에서 a = F/m
    // ─────────────────────────────────────────────────────────
    private void RefreshCache()
    {
        float rad    = windDirAngle * Mathf.Deg2Rad;
        Vector3 dir  = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        _cachedForce = dir * windStrength;
        _dirty       = false;
    }

    // ─────────────────────────────────────────────────────────
    // 랜덤 바람 설정 (홀 시작 시 GameManager에서 호출)
    // ─────────────────────────────────────────────────────────
    public void RandomizeWind()
    {
        windStrength = Random.Range(minStrength, maxStrength);
        windDirAngle = Random.Range(0f, 360f);
        _dirty = true;
        UpdateWindUI();
    }

    // ─────────────────────────────────────────────────────────
    // UI 업데이트: 텍스트 + 화살표 방향
    // ─────────────────────────────────────────────────────────
    public void UpdateWindUI()
    {
        if (windInfoText != null)
        {
            string dirName = GetCompassDirection(windDirAngle);
            windInfoText.text = $"Wind: {dirName}  {windStrength:F1} m/s";
        }

        // 화살표 UI: Z축 회전으로 방향 표시
        if (windArrow != null)
            windArrow.localRotation = Quaternion.Euler(0f, 0f, -windDirAngle);
    }

    // 각도 → 8방위 문자열 변환
    private string GetCompassDirection(float angle)
    {
        string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int index = Mathf.RoundToInt(angle / 45f) % 8;
        return dirs[index];
    }

    // Inspector에서 값 변경 시 캐시 갱신 (에디터 편의)
    private void OnValidate()
    {
        _dirty = true;
        UpdateWindUI();
    }

    // 현재 바람 방향 각도 반환 (TrajectoryPredictor에서 필요)
    public float GetWindAngle()    => windDirAngle;
    public float GetWindStrength() => windStrength;
}
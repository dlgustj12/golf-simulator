// ============================================================
// SurfaceManager.cs  —  5단계 완전 버전
// 싱글톤 | Raycast 지형 감지 | 표면별 마찰계수 반환
// ============================================================
using UnityEngine;

public class SurfaceManager : MonoBehaviour
{
    // ── 싱글톤 ───────────────────────────────────────────────
    public static SurfaceManager Instance { get; private set; }

    // ── Inspector 연결 ────────────────────────────────────────
    [Header("데이터 참조")]
    public PhysicsData physicsData;

    [Header("Raycast 설정")]
    [Tooltip("공 중심에서 아래로 쏘는 레이 시작 오프셋 (공이 지면에 묻힐 때 보정)")]
    public float rayStartOffset = 0.15f;
    [Tooltip("레이 최대 감지 거리")]
    public float rayLength      = 0.4f;
    [Tooltip("감지할 레이어 마스크 (Default 포함 지형 레이어 선택)")]
    public LayerMask groundLayer = ~0;   // ~0 = 모든 레이어

    // 표면 태그 상수 (Unity Editor Tag 설정과 반드시 일치)
    private const string TAG_FAIRWAY = "Fairway";
    private const string TAG_ROUGH   = "Rough";
    private const string TAG_BUNKER  = "Bunker";
    private const string TAG_GREEN   = "Green";

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────
    // 핵심 메서드: 공 위치 기반 마찰계수 반환
    // BallPhysicsController.UpdateRolling()에서 매 FixedUpdate 호출
    //
    // Code Defense 수식 포인트:
    // Physics.Raycast(origin, Vector3.down, out hit, length, layerMask)
    // 공 중심 약간 위에서 수직 하강 레이를 발사
    // hit.collider.tag로 지형 종류 판별
    // 태그별 PhysicsData의 μ 값 반환
    // ─────────────────────────────────────────────────────────
    public float GetFriction(Vector3 ballPosition)
    {
        Vector3 rayOrigin = ballPosition + Vector3.up * rayStartOffset;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
        {
            return TagToFriction(hit.collider.tag);
        }

        // 레이캐스트 실패 시 기본값(Fairway)
        return physicsData != null ? physicsData.frictionFairway : 0.25f;
    }

    // ─────────────────────────────────────────────────────────
    // 표면 이름 반환 (UIManager 디버그 표시용)
    // ─────────────────────────────────────────────────────────
    public string GetSurfaceName(Vector3 ballPosition)
    {
        Vector3 rayOrigin = ballPosition + Vector3.up * rayStartOffset;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayLength, groundLayer))
        {
            return hit.collider.tag switch
            {
                TAG_FAIRWAY => "Fairway",
                TAG_ROUGH   => "Rough",
                TAG_BUNKER  => "Bunker",
                TAG_GREEN   => "Green",
                _           => "Unknown"
            };
        }
        return "Unknown";
    }

    // ─────────────────────────────────────────────────────────
    // 태그 → 마찰계수 매핑
    // 
    // 초기값 :
    //   Fairway  μ=0.25 : 짧은 잔디, 낮은 저항 → 공이 멀리 굴러감
    //   Rough    μ=0.55 : 긴 잔디, 날 마찰 증가 → 공이 빨리 감속
    //   Bunker   μ=0.80 : 모래 입자 간 마찰 매우 큼 → 거의 즉시 정지
    //   Green    μ=0.15 : 매우 짧고 밀도 높은 잔디 → 부드럽게 굴러감
    // ─────────────────────────────────────────────────────────
    private float TagToFriction(string tag)
    {
        if (physicsData == null)
        {
            return tag switch
            {
                TAG_FAIRWAY => 0.25f,
                TAG_ROUGH   => 0.55f,
                TAG_BUNKER  => 0.80f,
                TAG_GREEN   => 0.15f,
                _           => 0.25f
            };
        }

        return tag switch
        {
            TAG_FAIRWAY => physicsData.frictionFairway,
            TAG_ROUGH   => physicsData.frictionRough,
            TAG_BUNKER  => physicsData.frictionBunker,
            TAG_GREEN   => physicsData.frictionGreen,
            _           => physicsData.frictionFairway
        };
    }

    // ─────────────────────────────────────────────────────────
    // 에디터 디버그: Scene 뷰에 레이 시각화
    // ─────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        // 씬 뷰에서 레이캐스트 범위 확인용 (빌드에 영향 없음)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            transform.position + Vector3.up * rayStartOffset,
            transform.position + Vector3.up * rayStartOffset + Vector3.down * rayLength
        );
    }
}
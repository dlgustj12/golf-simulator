// ============================================================
// TrajectoryPredictor.cs  —  3단계 완전 버전
// 바람 + 중력이 반영된 가상 시뮬레이션 루프 기반 궤적 예측
// ============================================================
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TrajectoryPredictor : MonoBehaviour
{
    // ── Inspector 연결 ────────────────────────────────────────
    [Header("참조")]
    public PhysicsData physicsData;
    public WindManager windManager;

    [Header("시뮬레이션 파라미터")]
    [Tooltip("예측 스텝 수 (많을수록 더 긴 궤적, 연산량 증가)")]
    public int   simulationSteps = 120;
    [Tooltip("각 스텝의 시간 간격 (s) — FixedUpdate dt와 동일하게 유지 권장")]
    public float simDeltaTime    = 0.02f;
    [Tooltip("궤적이 지면(y<=0)에 닿으면 예측 중단")]
    public float groundY         = 0f;

    [Header("LineRenderer 시각 설정")]
    public float lineWidth       = 0.05f;
    public Color lineColorStart  = new Color(1f, 1f, 0f, 0.9f);
    public Color lineColorEnd    = new Color(1f, 0.5f, 0f, 0.1f);

    // ── 내부 컴포넌트 ─────────────────────────────────────────
    private LineRenderer _lr;

    // 예측 좌표 재사용 버퍼 (GC 최소화)
    private Vector3[] _points;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _points = new Vector3[simulationSteps];

        // LineRenderer 기본 설정
        _lr.useWorldSpace    = true;
        _lr.positionCount    = 0;
        _lr.startWidth       = lineWidth;
        _lr.endWidth         = lineWidth * 0.3f;
        _lr.startColor       = lineColorStart;
        _lr.endColor         = lineColorEnd;

        // 점선 효과: Material 없을 시 기본 머티리얼 사용
        if (_lr.material == null)
            _lr.material = new Material(Shader.Find("Sprites/Default"));
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 예측선 계산 및 표시
    // 발사 전 매 프레임 호출 (InputController or GameManager에서)
    //
    // startPos  : 공의 현재 위치
    // angleDeg  : 현재 설정된 발사 각도
    // power     : 현재 설정된 파워
    // facingDir : 현재 카메라/플레이어 facing 방향
    // ─────────────────────────────────────────────────────────
    public void ShowTrajectory(Vector3 startPos, float angleDeg, float power, Vector3 facingDir)
    {
        // ── 초기 속도 계산 (BallPhysicsController.Launch와 동일 수식) ──
        float rad        = angleDeg * Mathf.Deg2Rad;
        Vector3 horizontal = new Vector3(facingDir.x, 0f, facingDir.z).normalized;
        Vector3 simVel   = horizontal * (power * Mathf.Cos(rad))
                         + Vector3.up  * (power * Mathf.Sin(rad));

        // ── 사전 계산: 매 스텝 공통 가속도 ──────────────────
        Vector3 gravAccel = Vector3.down * physicsData.gravity;
        Vector3 windForce = (windManager != null) ? windManager.GetWindForce() : Vector3.zero;

        //기존 코드
        //Vector3 windAccel = windForce / physicsData.ballMass;

        //수정 코드
        Vector3 windAccel = windForce * 0.1f;
        Vector3 totalAccel = gravAccel + windAccel;   // 매 스텝 동일 (바람 일정 가정)

        // ── 가상 시뮬레이션 루프 ──────────────────────────────
        //   실제 BallPhysicsController.UpdateFlight와 완전히 동일한 오일러 적분 수식을
        //   별도 변수(simPos, simVel)에 적용하여 미래 위치를 미리 계산
        //   simVel(t+dt) = simVel(t) + totalAccel × dt
        //   simPos(t+dt) = simPos(t) + simVel(t) × dt
        Vector3 simPos = startPos;
        int     validCount = 0;

        for (int i = 0; i < simulationSteps; i++)
        {
            _points[i] = simPos;
            validCount++;

            // 속도 업데이트 (가속도 적분)
            simVel += totalAccel * simDeltaTime;
            // 위치 업데이트 (속도 적분)
            simPos += simVel * simDeltaTime;

            // 지면 도달 시 예측 중단
            if (simPos.y <= groundY)
            {
                // 마지막 지면 교차 지점 보간하여 정확한 착지점 추가
                _points[validCount] = InterpolateGroundHit(
                    _points[validCount - 1], simPos);
                validCount++;
                break;
            }
        }

        // LineRenderer에 계산된 좌표 반영
        _lr.positionCount = validCount;
        _lr.SetPositions(_points);
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 예측선 숨기기 (발사 후 호출)
    // ─────────────────────────────────────────────────────────
    public void HideTrajectory()
    {
        _lr.positionCount = 0;
    }

    // ─────────────────────────────────────────────────────────
    // 지면 교차 지점 선형 보간
    // p0(지면 위)와 p1(지면 아래) 사이의 정확한 y=groundY 지점 계산
    // ─────────────────────────────────────────────────────────
    private Vector3 InterpolateGroundHit(Vector3 p0, Vector3 p1)
    {
        // t : p0→p1 구간에서 y=groundY가 되는 비율
        float t = (groundY - p0.y) / (p1.y - p0.y);
        return Vector3.Lerp(p0, p1, t);
    }

    // 파라미터 런타임 변경 시 버퍼 재할당
    public void ResizeBuffer(int newSteps)
    {
        simulationSteps = newSteps;
        _points = new Vector3[simulationSteps];
    }
}
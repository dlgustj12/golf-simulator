// ============================================================
// CameraController.cs  —  3단계 완전 버전
// 조준(Aim) ↔ 트래킹(Tracking) 카메라 전환
// Lerp(위치) + Slerp(회전) 필수 적용
// ============================================================
using UnityEngine;

public class CameraController : MonoBehaviour
{
    // ── Inspector 연결 ────────────────────────────────────────
    [Header("타겟 참조")]
    public Transform ballTransform;          // 공 Transform
    public BallPhysicsController ball;       // 상태 조회용

    [Header("조준 카메라 설정 (Aim)")]
    [Tooltip("공 중심에서 카메라까지의 거리")]
    public float aimDistance   = 8f;
    [Tooltip("공 위 기준 높이 오프셋")]
    public float aimHeight     = 3f;
    [Tooltip("좌우 회전 감도 (도/초)")]
    public float rotateSpeed   = 90f;

    [Header("트래킹 카메라 설정 (Tracking)")]
    [Tooltip("공 뒤쪽 거리 오프셋")]
    public float trackDistance = 10f;
    [Tooltip("공 위 높이 오프셋")]
    public float trackHeight   = 5f;

    [Header("보간 속도")]
    [Tooltip("위치 보간 속도 (Lerp) — 클수록 빠르게 따라감")]
    public float posLerpSpeed  = 5f;
    [Tooltip("회전 보간 속도 (Slerp) — 클수록 빠르게 회전")]
    public float rotSlerpSpeed = 4f;

    // ── 내부 상태 ─────────────────────────────────────────────
    private enum CamState { Aim, Tracking }
    private CamState _camState = CamState.Aim;

    // 조준 카메라: 수평 회전 각도 (Y축, 도)
    private float _aimYaw = 0f;

    // 전환 중 부드러운 보간을 위한 현재 카메라 위치/회전 캐시
    private Vector3    _currentPos;
    private Quaternion _currentRot;

    // 트래킹 시 공의 이동 방향을 누적하여 카메라 후방 계산
    private Vector3 _lastBallPos;
    private Vector3 _trackDir = Vector3.back;   // 초기 후방 방향

    // ─────────────────────────────────────────────────────────
    void Start()
    {
        if (ballTransform == null && ball != null)
            ballTransform = ball.transform;

        _currentPos  = transform.position;
        _currentRot  = transform.rotation;
        _lastBallPos = ballTransform != null ? ballTransform.position : Vector3.zero;

        // BallPhysicsController 이벤트 구독
        if (ball != null)
        {
            ball.OnLaunched     += OnBallLaunched;
            ball.OnBallStopped  += OnBallStopped;
        }
    }

    void OnDestroy()
    {
        if (ball != null)
        {
            ball.OnLaunched    -= OnBallLaunched;
            ball.OnBallStopped -= OnBallStopped;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 이벤트 콜백
    // ─────────────────────────────────────────────────────────
    private void OnBallLaunched()  => _camState = CamState.Tracking;
    private void OnBallStopped()   => _camState = CamState.Aim;

    // ─────────────────────────────────────────────────────────
    // LateUpdate: 물리 업데이트 이후 카메라 위치 반영
    // ─────────────────────────────────────────────────────────
    void LateUpdate()
    {
        if (ballTransform == null) return;

        float dt = Time.deltaTime;

        switch (_camState)
        {
            case CamState.Aim:
                UpdateAimCamera(dt);
                break;
            case CamState.Tracking:
                UpdateTrackingCamera(dt);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 조준 카메라: 공을 중심으로 수평 회전
    // ─────────────────────────────────────────────────────────
    private void UpdateAimCamera(float dt)
    {
        // 좌우 키 입력으로 수평 회전
        float h = Input.GetAxis("Horizontal");   // A/D 또는 ←/→
        _aimYaw += h * rotateSpeed * dt;

        // 카메라 목표 위치 계산
        //   공 위치 기준 + 거리·각도로 오프셋
        float yawRad = _aimYaw * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Sin(yawRad) * aimDistance,
            aimHeight,
            Mathf.Cos(yawRad) * aimDistance
        );
        Vector3 targetPos = ballTransform.position + offset;

        // 카메라가 바라볼 방향: 공을 향하는 벡터
        Vector3    lookDir   = (ballTransform.position - targetPos).normalized;
        Quaternion targetRot = Quaternion.LookRotation(lookDir);

        // ★ Lerp: 위치 선형 보간 (등속 이동 느낌)
        //   pos = (1-t)·current + t·target  →  매 프레임 목표 방향으로 일정 비율 이동
        _currentPos = Vector3.Lerp(_currentPos, targetPos, posLerpSpeed * dt);

        // ★ Slerp: 회전 구면 선형 보간 (일정 각속도 유지)
        //   구면 위를 등각속도로 이동 → 자연스러운 회전 궤적
        _currentRot = Quaternion.Slerp(_currentRot, targetRot, rotSlerpSpeed * dt);

        transform.position = _currentPos;
        transform.rotation = _currentRot;
    }

    // ─────────────────────────────────────────────────────────
    // 트래킹 카메라: 공 뒤를 부드럽게 추적
    // ─────────────────────────────────────────────────────────
    private void UpdateTrackingCamera(float dt)
    {
        Vector3 ballPos = ballTransform.position;

        // 공의 이동 방향 추적 (속도 방향이 있을 때만 갱신)
        Vector3 moveDelta = ballPos - _lastBallPos;
        if (moveDelta.magnitude > 0.01f)
        {
            // 수평 이동 방향만 추출하여 트래킹 방향 누적
            Vector3 horizMove = new Vector3(moveDelta.x, 0f, moveDelta.z);
            if (horizMove.magnitude > 0.005f)
                _trackDir = -horizMove.normalized;  // 공 뒤쪽 방향
        }
        _lastBallPos = ballPos;

        // 카메라 목표 위치: 공 뒤쪽 + 높이 오프셋
        Vector3 targetPos = ballPos
                          + _trackDir * trackDistance
                          + Vector3.up * trackHeight;

        // 카메라가 공을 바라보는 목표 회전
        Vector3    lookDir   = (ballPos - targetPos).normalized;
        Quaternion targetRot = Quaternion.LookRotation(lookDir);

        // ★ Lerp: 위치 보간 — 공이 빠르게 움직여도 부드럽게 따라감
        _currentPos = Vector3.Lerp(_currentPos, targetPos, posLerpSpeed * dt);

        // ★ Slerp: 회전 보간 — 공 방향으로 카메라가 자연스럽게 회전
        _currentRot = Quaternion.Slerp(_currentRot, targetRot, rotSlerpSpeed * dt);

        transform.position = _currentPos;
        transform.rotation = _currentRot;
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 현재 조준 방향 반환
    // GameManager/TrajectoryPredictor에서 facingDir로 사용
    // ─────────────────────────────────────────────────────────
    public Vector3 GetAimDirection()
    {
        // 수식이 아니라 실제 카메라가 바라보는 전방 방향을 뺏어옴
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.normalized;
    }

    // 현재 조준 Yaw 각도 반환 (UI 나침반 표시 등에 활용 가능)
    public float GetAimYaw() => _aimYaw;
}
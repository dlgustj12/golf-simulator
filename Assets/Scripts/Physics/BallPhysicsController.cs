// ============================================================
// BallPhysicsController.cs  —  5단계 Trail Renderer 추가 버전
// Custom Physics: Rigidbody는 충돌 감지 전달 용도로만 사용
// ============================================================
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BallPhysicsController : MonoBehaviour
{
    // ── Inspector 연결 ────────────────────────────────────────
    [Header("데이터 & 매니저 참조")]
    public PhysicsData physicsData;
    public WindManager windManager;

    [Header("디버그 표시")]
    public bool showDebugLog = true;

    [Header("Trail Renderer (비행 궤적 꼬리)")]
    [Tooltip("Ball 오브젝트의 TrailRenderer 컴포넌트 연결")]
    public TrailRenderer trailRenderer;

    // ── 내부 상태 ─────────────────────────────────────────────
    private Rigidbody _rb;
    private Vector3   _velocity;

    public enum BallState { Idle, Flying, Rolling }
    private BallState _state = BallState.Idle;

    private float _lastCollisionTime = -1f;
    private const float COLLISION_COOLDOWN = 0.05f;
    private const float STOP_THRESHOLD    = 0.08f;
    private const float ROLL_V_THRESHOLD  = 1.0f;

    private Vector3 _lastFacingDir = Vector3.forward;

    // ── 외부 조회용 프로퍼티 ──────────────────────────────────
    public Vector3   Velocity     => _velocity;
    public bool      IsMoving     => _state != BallState.Idle;
    public bool      IsFlying     => _state == BallState.Flying;
    public bool      IsRolling    => _state == BallState.Rolling;
    public BallState CurrentState => _state;

    // ── 이벤트 ────────────────────────────────────────────────
    public System.Action OnLaunched;
    public System.Action OnBallStopped;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic    = false;
        _rb.useGravity     = false;
        _rb.linearDamping  = 0f;
        _rb.angularDamping = 0f;
        _rb.constraints    = RigidbodyConstraints.FreezeRotation;

        // Trail Renderer 초기 비활성화
        // Inspector에서 연결 안 됐을 경우 자동 탐색
        if (trailRenderer == null)
            trailRenderer = GetComponent<TrailRenderer>();

        SetTrail(false);
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 발사
    // ─────────────────────────────────────────────────────────
    public void Launch(float angleDeg, float power, Vector3 facingDir)
    {
        float rad  = angleDeg * Mathf.Deg2Rad;
        float cosA = Mathf.Cos(rad);
        float sinA = Mathf.Sin(rad);

        Vector3 horizontal = new Vector3(facingDir.x, 0f, facingDir.z).normalized;
        _velocity          = horizontal * (power * cosA) + Vector3.up * (power * sinA);
        _lastFacingDir     = horizontal;

        _state = BallState.Flying;

        // 비행 시작 → Trail 활성화
        SetTrail(true);

        OnLaunched?.Invoke();

        if (showDebugLog)
            Debug.Log($"[Launch] angle={angleDeg}°  power={power:F2}m/s  v0={_velocity}");
    }

    // ─────────────────────────────────────────────────────────
    // FixedUpdate: 물리 연산 루프
    // ─────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        float dt = Time.fixedDeltaTime;

        switch (_state)
        {
            case BallState.Flying:
                UpdateFlight(dt);
                break;
            case BallState.Rolling:
                UpdateRolling(dt);
                break;
            case BallState.Idle:
            default:
                return;
        }

        _rb.MovePosition(_rb.position + _velocity * dt);
    }

    // ─────────────────────────────────────────────────────────
    // 비행 물리: 중력 + 바람 외력
    // ─────────────────────────────────────────────────────────
    private void UpdateFlight(float dt)
    {
        Vector3 gravityAccel = Vector3.down * physicsData.gravity;

        Vector3 windForce = (windManager != null)
            ? windManager.GetWindForce()
            : Vector3.zero;

        Vector3 windAccel = windForce * 0.1f;

        // ★ v(t+dt) = v(t) + (g_vec + a_wind) × dt
        _velocity += (gravityAccel + windAccel) * dt;
    }

    // ─────────────────────────────────────────────────────────
    // 구름 물리: 표면 마찰 감속
    // ─────────────────────────────────────────────────────────
    private void UpdateRolling(float dt)
    {
        _velocity.y = 0f;

        float mu = (SurfaceManager.Instance != null)
            ? SurfaceManager.Instance.GetFriction(transform.position)
            : physicsData.frictionFairway;

        // ★ v(t+dt) = v(t) × (1 - μ × dt)
        _velocity *= Mathf.Max(0f, 1f - mu * dt);

        if (_velocity.magnitude < STOP_THRESHOLD)
        {
            _velocity = Vector3.zero;
            _state    = BallState.Idle;

            // 정지 → Trail 비활성화
            SetTrail(false);

            OnBallStopped?.Invoke();

            if (showDebugLog)
                Debug.Log("[Rolling] 공 정지 완료");
        }
    }

    // ─────────────────────────────────────────────────────────
    // 충돌 처리: 반발계수 적용 벡터 반사
    // ─────────────────────────────────────────────────────────
    private void OnCollisionEnter(Collision collision)
    {
        if (_state != BallState.Flying) return;

        if (Time.time - _lastCollisionTime < COLLISION_COOLDOWN) return;
        _lastCollisionTime = Time.time;

        Vector3 normal = collision.contacts[0].normal;
        float   vDotN  = Vector3.Dot(_velocity, normal);

        if (vDotN >= 0f) return;

        float e = physicsData.restitution;

        // ★ v_reflect = v - (1 + e) × (v · n) × n
        _velocity = _velocity - (1f + e) * vDotN * normal;

        float verticalSpeed = Mathf.Abs(Vector3.Dot(_velocity, normal));

        if (verticalSpeed < ROLL_V_THRESHOLD)
        {
            _velocity -= Vector3.Dot(_velocity, normal) * normal;
            _velocity.y = 0f;
            _state = BallState.Rolling;

            if (showDebugLog)
                Debug.Log($"[Collision] 구름 전환 — 수직속력={verticalSpeed:F3}");
        }
        else
        {
            if (showDebugLog)
                Debug.Log($"[Collision] 바운스 — e={e}, v={_velocity}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // Trail Renderer 켜기/끄기
    // ─────────────────────────────────────────────────────────
    private void SetTrail(bool active)
    {
        if (trailRenderer == null) return;

        trailRenderer.emitting = active;

        // 비활성화 시 기존 꼬리 즉시 제거 (다음 발사 때 잔상 방지)
        if (!active)
            trailRenderer.Clear();
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC 유틸리티
    // ─────────────────────────────────────────────────────────
    public void ForceStop()
    {
        _velocity           = Vector3.zero;
        _state              = BallState.Idle;
        _rb.linearVelocity  = Vector3.zero;
        SetTrail(false);
    }

    public float   GetSpeed()           => _velocity.magnitude;
    public Vector3 GetFacingDirection() => _lastFacingDir;
}
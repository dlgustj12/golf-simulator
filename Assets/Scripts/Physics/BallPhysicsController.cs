// ============================================================
// BallPhysicsController.cs  —  5단계 Trail Renderer 추가 버전
// Custom Physics: Rigidbody는 충돌 감지 전달 용도로만 사용
// ============================================================
using UnityEngine;
using UnityEngine.SceneManagement;

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
    // 💡 삭제: _velocity.y = 0f; (경사면을 타야 하므로 Y축 속도를 죽이면 안 됨)

    // 1. 공 아래로 레이저를 쏴서 현재 밟고 있는 지면의 기울기(Normal)를 구함
    Vector3 groundNormal = Vector3.up; 
    // 공의 반경에 맞춰 레이캐스트 거리 조절 (공 크기가 1이면 0.6f 정도면 적당해)
    if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.0f))
    {
        groundNormal = hit.normal;
    }

    // 2. 경사면을 따라 내려가는 중력 계산
    Vector3 gravityForce = Vector3.down * physicsData.gravity;
    Vector3 gravityOnSlope = Vector3.ProjectOnPlane(gravityForce, groundNormal);

    // 3. 마찰력 가져오기
    float mu = (SurfaceManager.Instance != null)
        ? SurfaceManager.Instance.GetFriction(transform.position)
        : physicsData.frictionFairway;

    // 4. 속도 업데이트: 내리막 가속도 먼저 더하고 -> 마찰력으로 감속
    _velocity += gravityOnSlope * dt;
    _velocity *= Mathf.Max(0f, 1f - mu * dt);

    // 5. 공이 지면을 파고들거나 뜨지 않도록 속도의 방향을 경사면에 딱 맞춤
    _velocity = Vector3.ProjectOnPlane(_velocity, groundNormal);

    // 6. 정지 조건 수정: 속도가 느릴 뿐만 아니라 "평지(경사 5도 미만)"일 때만 멈추게 함!
    // 경사가 가파르면 마이너스 속도로 굴러내려가야 하니까 멈추면 안 돼.
    float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
    if (_velocity.magnitude < STOP_THRESHOLD && slopeAngle < 5f) 
    {
        _velocity = Vector3.zero;
        _state    = BallState.Idle;

        SetTrail(false);
        OnBallStopped?.Invoke();

        if (showDebugLog)
            Debug.Log("[Rolling] 공 정지 완료 (평지 도달)");
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
            //오류 수정 
            //_velocity.y = 0f;
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

    void Update()
    {
        if (transform.position.y < -10f)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        // 매 프레임마다 UIManager로 공의 현재 속력 쏴주기
        // rb.linearVelocity.magnitude는 벡터(방향+속도)에서 순수 '속력' 수치만 쏙 빼오는 마법의 명령어! (유니티 6 기준)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateSpeed(_velocity.magnitude);
        }
 
    }

    public float   GetSpeed()           => _velocity.magnitude;
    public Vector3 GetFacingDirection() => _lastFacingDir;
}
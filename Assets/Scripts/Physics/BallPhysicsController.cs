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
    private Vector3 _velocity;

    public enum BallState { Idle, Flying, Rolling }
    private BallState _state = BallState.Idle;

    public bool isHoleIn = false; // 홀인원 성공 여부

    //벙커 판별 변수
    public bool isInBunker = false;

    private float _lastCollisionTime = -1f;
    private const float COLLISION_COOLDOWN = 0.05f;
    private const float STOP_THRESHOLD = 0.08f;
    private const float ROLL_V_THRESHOLD = 1.0f;

    private Vector3 _lastFacingDir = Vector3.forward;

    // ── 외부 조회용 프로퍼티 ──────────────────────────────────
    public Vector3 Velocity => _velocity;
    public bool IsMoving => _state != BallState.Idle;
    public bool IsFlying => _state == BallState.Flying;
    public bool IsRolling => _state == BallState.Rolling;
    public BallState CurrentState => _state;

    // ── 이벤트 ────────────────────────────────────────────────
    public System.Action OnLaunched;
    public System.Action OnBallStopped;

    // ─────────────────────────────────────────────────────────
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = false;
        _rb.useGravity = false;
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

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
        float rad = angleDeg * Mathf.Deg2Rad;
        float cosA = Mathf.Cos(rad);
        float sinA = Mathf.Sin(rad);

        Vector3 horizontal = new Vector3(facingDir.x, 0f, facingDir.z).normalized;
        _velocity = horizontal * (power * cosA) + Vector3.up * (power * sinA);
        _lastFacingDir = horizontal;

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
        _rb.linearVelocity = Vector3.zero;
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
        Vector3 groundNormal = Vector3.up;

        // 레이저 쏘는 건 경사면 기울기(Normal) 구할 때만 씀! 태그 검사는 지웠어.
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1.0f))
        {
            groundNormal = hit.normal;
        }
        else
        {
            //새로 추가된 핵심 코드
            // 레이저가 바닥을 못 찾았다 = Paint Hole 구멍에 빠졌거나 절벽 밖으로 나갔다!
            _state = BallState.Flying; // 다시 공중 비행(추락) 상태로 변경!
            return; // 아래 구르기 연산은 취소하고 즉시 함수 빠져나가기
        }

        Vector3 gravityForce = Vector3.down * physicsData.gravity;
        Vector3 gravityOnSlope = Vector3.ProjectOnPlane(gravityForce, groundNormal);

        float mu = (SurfaceManager.Instance != null)
            ? SurfaceManager.Instance.GetFriction(transform.position)
            : physicsData.frictionFairway;

        // 🚨 [새로운 벙커 감속 로직] 투명 상자에 들어갔는지(isInBunker)만 확인!
        if (isInBunker)
        {
            mu *= 70.0f; // (필요하면 조절)

            if (_velocity.magnitude < 2.0f)
            {
                _velocity = Vector3.zero;
            }
        }

        _velocity += gravityOnSlope * dt;
        _velocity *= Mathf.Max(0f, 1f - mu * dt);
        _velocity = Vector3.ProjectOnPlane(_velocity, groundNormal);

        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);

        if (_velocity.magnitude < STOP_THRESHOLD && (slopeAngle < 5f || isInBunker))
        {
            _velocity = Vector3.zero;
            _state = BallState.Idle;

            SetTrail(false);
            OnBallStopped?.Invoke();

            if (showDebugLog)
                Debug.Log("[Rolling] 공 정지 완료 (평지 또는 벙커 안착)");
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
        float vDotN = Vector3.Dot(_velocity, normal);

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
        _velocity = Vector3.zero;
        _state = BallState.Idle;
        _rb.linearVelocity = Vector3.zero;
        SetTrail(false);
    }
    void Update()
    {
        // 홀인원이 아닐 때만 맵 밖 추락 시 씬 재시작!
        if (!isHoleIn && transform.position.y < -10f)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateSpeed(_velocity.magnitude);
        }
    }

    public float GetSpeed() => _velocity.magnitude;
    public Vector3 GetFacingDirection() => _lastFacingDir;

    // 투명 벙커 상자(BunkerZone)에 들어갈 때와 나갈 때를 감지
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bunker")) isInBunker = true;

        if (other.CompareTag("Hole"))
        {
            isHoleIn = true; // 홀인원 도장 쾅!

            _state = BallState.Idle;
            _velocity = Vector3.zero;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            Vector3 holeCenter = other.transform.position;
            transform.position = new Vector3(holeCenter.x, transform.position.y, holeCenter.z);

            GetComponent<Collider>().isTrigger = true;
            _rb.useGravity = true;

            //추가: 매니저한테 -> 공 정지했음. 점수 계산해
            OnBallStopped?.Invoke();

            Debug.Log("🎉 구멍으로 쏙! 판정 이벤트 발생!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Bunker")) isInBunker = false;
    }
}
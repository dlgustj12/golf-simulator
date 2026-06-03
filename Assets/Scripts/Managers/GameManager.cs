// ============================================================
// GameManager.cs  —  5단계 업데이트 완전 버전
// 싱글톤 | 타수 관리 | 턴 상태 머신 | 홀인 결과 | 씬 리셋
// ============================================================
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // ── 싱글톤 ───────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ── Inspector 연결 ────────────────────────────────────────
    [Header("참조")]
    public BallPhysicsController ball;
    public CameraController      cameraController;
    public TrajectoryPredictor   trajectoryPredictor;
    public WindManager           windManager;
    public UIManager             uiManager;

    [Header("발사 파라미터")]
    [Tooltip("발사 각도 고정값")]
    public float launchAngle = 45f;

    [Header("홀인 판정")]
    public Transform holeCup;
    public float     holeRadius = 4.0f;

    [Header("게임 설정")]
    [Tooltip("기준 타수 (Par)")]
    public int parCount = 3;

    [Header("공 초기 위치 (리셋용)")]
    public Vector3 ballStartPosition = new Vector3(0f, 0.5f, 0f);

    // ── 게임 상태 ─────────────────────────────────────────────
    private enum GameState { Aiming, Flying, Stopped, HoleIn }
    private GameState _gameState = GameState.Aiming;

    private int _strokeCount = 0;

    // ── 이벤트 ────────────────────────────────────────────────
    public System.Action        OnTurnStart;
    public System.Action        OnBallLaunched;
    public System.Action<int>   OnStrokeUpdated;
    public System.Action        OnHoleIn;

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
    void Start()
    {
        if (ball != null)
        {
            ball.OnLaunched    += HandleBallLaunched;
            ball.OnBallStopped += HandleBallStopped;
        }

        StartTurn();
    }

    void OnDestroy()
    {
        if (ball != null)
        {
            ball.OnLaunched    -= HandleBallLaunched;
            ball.OnBallStopped -= HandleBallStopped;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Update: 조준 중 매 프레임 궤적선 갱신
    // ─────────────────────────────────────────────────────────
    void Update()
    {
        if (_gameState != GameState.Aiming) return;
        if (ball == null || cameraController == null || trajectoryPredictor == null) return;

        Vector3 aimDir = cameraController.GetAimDirection();
        trajectoryPredictor.ShowTrajectory(
            ball.transform.position,
            launchAngle,
            GetPreviewPower(),
            aimDir
        );
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 발사 요청 (UIManager에서 호출)
    // ─────────────────────────────────────────────────────────
    public void RequestLaunch(float power)
    {
        if (_gameState != GameState.Aiming) return;
        if (ball == null || cameraController == null) return;

        Vector3 aimDir = cameraController.GetAimDirection();

        _strokeCount++;
        OnStrokeUpdated?.Invoke(_strokeCount);

        trajectoryPredictor?.HideTrajectory();
        ball.Launch(launchAngle, power, aimDir);

        Debug.Log($"[GameManager] 발사 — power={power:F1}, stroke={_strokeCount}");
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 씬 전체 재시작 (Restart 버튼 → UIManager에서 호출)
    //
    // ★ Code Defense:
    //   SceneManager.LoadScene: 씬 전체를 처음부터 다시 로드
    //   → 모든 오브젝트·변수 완전 초기화, 메모리 정리 확실
    //   → 로딩 시간 존재, 인트로 없는 단일 홀 게임에 적합
    // ─────────────────────────────────────────────────────────
    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 오브젝트 위치만 초기화 (씬 재로드 없이 빠른 리셋)
    //
    // ★ Code Defense:
    //   Object Reset: 씬을 다시 로드하지 않고 공 위치·변수만 초기화
    //   → 로딩 없이 즉각 리셋, 배경·지형 오브젝트 유지
    //   → 현재 코드에서 씬 재시작(RestartGame)을 기본으로 사용하되
    //     이 메서드는 추후 멀티홀 확장 시 홀 간 전환에 활용
    // ─────────────────────────────────────────────────────────
    public void SoftReset()
    {
        _strokeCount = 0;
        _gameState   = GameState.Aiming;

        if (ball != null)
        {
            ball.ForceStop();
            ball.transform.position = ballStartPosition;
        }

        windManager?.RandomizeWind();
        trajectoryPredictor?.HideTrajectory();
        uiManager?.HideResultPanel();

        OnStrokeUpdated?.Invoke(_strokeCount);
        StartTurn();
    }

    // ─────────────────────────────────────────────────────────
    // 턴 시작
    // ─────────────────────────────────────────────────────────
    private void StartTurn()
    {
        _gameState = GameState.Aiming;
        windManager?.RandomizeWind();
        OnTurnStart?.Invoke();

        Debug.Log($"[GameManager] 턴 시작 — 타수={_strokeCount}");
    }

    // ─────────────────────────────────────────────────────────
    // Ball 이벤트 핸들러
    // ─────────────────────────────────────────────────────────
    private void HandleBallLaunched()
    {
        _gameState = GameState.Flying;
        OnBallLaunched?.Invoke();
        uiManager?.UpdateStateUI("Airborne");
        Debug.Log("[GameManager] Aiming → Flying");
    }

    private void HandleBallStopped()
    {
        _gameState = GameState.Stopped;
        uiManager?.UpdateStateUI("Stop");

        if (CheckHoleIn())
        {
            _gameState = GameState.HoleIn;
            OnHoleIn?.Invoke();

            // 결과 계산 및 결과창 표시
            string grade = CalcGrade(_strokeCount, parCount);
            uiManager?.ShowResultPanel(_strokeCount, parCount, grade);
            Debug.Log($"[GameManager] 홀인! {_strokeCount}타 / {grade}");
            return;
        }

        Invoke(nameof(StartTurn), 1.0f);
    }

    // ─────────────────────────────────────────────────────────
    // 홀인 판정
    // ─────────────────────────────────────────────────────────
    private bool CheckHoleIn()
    {
        if (holeCup == null || ball == null) return false;

        Vector3 ballPos = ball.transform.position;
        Vector3 holePos = holeCup.position;

        float dist = Vector2.Distance(
            new Vector2(ballPos.x, ballPos.z),
            new Vector2(holePos.x, holePos.z)
        );
        return dist <= holeRadius;
    }

    // ─────────────────────────────────────────────────────────
    // 성적 계산 (Par 대비)
    // ─────────────────────────────────────────────────────────
    private string CalcGrade(int strokes, int par)
    {
        int diff = strokes - par;
        return diff switch
        {
            <= -3 => "Albatross",
            -2    => "Eagle",
            -1    => "Birdie",
             0    => "Par",
             1    => "Bogey",
             2    => "Double Bogey",
            _     => "Triple Bogey+"
        };
    }

    // ─────────────────────────────────────────────────────────
    // Preview Power (궤적선 미리보기용)
    // ─────────────────────────────────────────────────────────
    private float _previewPower = 0f;

    public void SetPreviewPower(float power) => _previewPower = power;

    private float GetPreviewPower()
        => _previewPower > 0f ? _previewPower : 7.5f;

    // 외부 조회
    public int  GetStrokeCount() => _strokeCount;
    public bool IsAiming()       => _gameState == GameState.Aiming;
}
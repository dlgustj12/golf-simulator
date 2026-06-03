// ============================================================
// UIManager.cs  —  5단계 업데이트 완전 버전
// 파워 게이지 PingPong | 바람 UI | 타수 | 결과창 | 재시작 버튼
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    // ── Inspector 연결 ────────────────────────────────────────
    [Header("참조")]
    public GameManager   gameManager;
    public WindManager   windManager;

    [Header("파워 게이지")]
    public Slider        powerSlider;
    public TMP_Text      powerValueText;

    [Header("바람 UI")]
    public TMP_Text      windInfoText;
    public RectTransform windArrow;

    [Header("타수 UI")]
    public TMP_Text      strokeText;

    [Header("상태 UI")]
    public TMP_Text      stateText;
    public TMP_Text      surfaceText;

    [Header("결과창 패널")]
    public GameObject    resultPanel;          // 결과창 루트 Panel GameObject
    public TMP_Text      resultTotalStrokeText; // "총 타수: 4"
    public TMP_Text      resultParText;         // "기준 타수: Par 3"
    public TMP_Text      resultGradeText;       // "Birdie!"
    public Button        restartButton;         // 다시 하기 버튼

    [Header("파워 게이지 파라미터")]
    [Tooltip("게이지 왕복 속도. 클수록 빠름")]
    public float pingPongSpeed = 1.2f;
    [Tooltip("최대 파워 (m/s)")]
    public float maxPower      = 15f;

    // ── 내부 상태 ─────────────────────────────────────────────
    private float _pingPongTimer = 0f;
    private bool  _isCharging   = false;
    private float _currentPower = 0f;
    private bool  _canShoot     = true;

    // ─────────────────────────────────────────────────────────
    void Start()
    {
        // 슬라이더 초기화
        if (powerSlider != null)
        {
            powerSlider.minValue = 0f;
            powerSlider.maxValue = maxPower;
            powerSlider.value    = 0f;
        }

        // 결과창 초기 비활성화
        if (resultPanel != null)
            resultPanel.SetActive(false);

        // 다시 하기 버튼 이벤트 등록
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        // GameManager 이벤트 구독
        if (gameManager != null)
        {
            gameManager.OnTurnStart     += HandleTurnStart;
            gameManager.OnBallLaunched  += HandleBallLaunched;
            gameManager.OnStrokeUpdated += HandleStrokeUpdated;
        }

        UpdateWindUI();
        UpdateStrokeUI(0);
    }

    void OnDestroy()
    {
        if (gameManager != null)
        {
            gameManager.OnTurnStart     -= HandleTurnStart;
            gameManager.OnBallLaunched  -= HandleBallLaunched;
            gameManager.OnStrokeUpdated -= HandleStrokeUpdated;
        }

        if (restartButton != null)
            restartButton.onClick.RemoveListener(OnRestartClicked);
    }

    // ─────────────────────────────────────────────────────────
    // Update: 파워 게이지 Ping-Pong
    // ─────────────────────────────────────────────────────────
    void Update()
    {
        if (!_canShoot) return;

        if (Input.GetKey(KeyCode.Space))
        {
            _isCharging = true;

            // ★ PingPong 수식:
            //   timer += speed × dt
            //   power  = PingPong(timer × maxPower, maxPower)
            _pingPongTimer += pingPongSpeed * Time.deltaTime;
            _currentPower   = Mathf.PingPong(_pingPongTimer * maxPower, maxPower);

            UpdatePowerUI(_currentPower);
            gameManager?.SetPreviewPower(_currentPower);
        }

        if (Input.GetKeyUp(KeyCode.Space) && _isCharging)
        {
            _isCharging    = false;
            _pingPongTimer = 0f;
            gameManager?.RequestLaunch(_currentPower);
        }

        if (Input.GetMouseButtonDown(1) && _canShoot)
        {
            float quickPower = maxPower * 0.7f;
            gameManager?.RequestLaunch(quickPower);
        }
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 결과창 표시
    // GameManager.HandleBallStopped → ShowResultPanel 호출
    // ─────────────────────────────────────────────────────────
    public void ShowResultPanel(int totalStrokes, int par, string grade)
    {
        if (resultPanel == null) return;

        resultPanel.SetActive(true);

        if (resultTotalStrokeText != null)
            resultTotalStrokeText.text = $"Total Strokes: {totalStrokes}";

        if (resultParText != null)
            resultParText.text = $"Par: {par}";

        if (resultGradeText != null)
        {
            resultGradeText.text  = grade;
            // 성적별 색상 강조
            resultGradeText.color = grade switch
            {
                "Albatross" => new Color(1f, 0.84f, 0f),   // 골드
                "Eagle"     => new Color(1f, 0.84f, 0f),   // 골드
                "Birdie"    => new Color(0.2f, 0.8f, 0.2f), // 초록
                "Par"       => Color.white,
                "Bogey"     => new Color(1f, 0.6f, 0.2f),  // 주황
                _           => new Color(1f, 0.3f, 0.3f)   // 빨강
            };
        }

        _canShoot = false;
    }

    // ─────────────────────────────────────────────────────────
    // PUBLIC: 결과창 숨기기 (SoftReset 시 호출)
    // ─────────────────────────────────────────────────────────
    public void HideResultPanel()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    // 다시 하기 버튼 클릭
    // ─────────────────────────────────────────────────────────
    private void OnRestartClicked()
    {
        HideResultPanel();
        gameManager?.RestartGame();   // 씬 전체 재시작
    }

    // ─────────────────────────────────────────────────────────
    // 바람 UI 갱신
    // ─────────────────────────────────────────────────────────
    public void UpdateWindUI()
    {
        if (windManager == null) return;

        float  strength = windManager.GetWindStrength();
        float  angle    = windManager.GetWindAngle();
        string dirName  = AngleToDirName(angle);

        if (windInfoText != null)
            windInfoText.text = $"Wind: {dirName}  {strength:F1} m/s";

        if (windArrow != null)
            windArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    // ─────────────────────────────────────────────────────────
    // 각종 UI 갱신 메서드
    // ─────────────────────────────────────────────────────────
    private void UpdatePowerUI(float power)
    {
        if (powerSlider != null)   powerSlider.value    = power;
        if (powerValueText != null) powerValueText.text = $"{power:F1} m/s";
    }

    public void UpdateStrokeUI(int stroke)
    {
        if (strokeText != null)
            strokeText.text = $"Count: {stroke}";
    }

    public void UpdateStateUI(string stateName)
    {
        if (stateText != null)
            stateText.text = stateName;
    }

    public void UpdateSurfaceUI(string surfaceName)
    {
        if (surfaceText != null)
            surfaceText.text = $"Surface: {surfaceName}";
    }

    // ─────────────────────────────────────────────────────────
    // GameManager 이벤트 핸들러
    // ─────────────────────────────────────────────────────────
    private void HandleTurnStart()
    {
        _canShoot      = true;
        _pingPongTimer = 0f;
        UpdatePowerUI(0f);
        UpdateStateUI("Ready");
        UpdateWindUI();
    }

    private void HandleBallLaunched()
    {
        _canShoot = false;
        UpdatePowerUI(0f);
        UpdateStateUI("Airborne");
    }

    private void HandleStrokeUpdated(int stroke)
    {
        UpdateStrokeUI(stroke);
    }

    // ─────────────────────────────────────────────────────────
    // 각도 → 8방위 문자열
    // ─────────────────────────────────────────────────────────
    private string AngleToDirName(float angle)
    {
        string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int index = Mathf.RoundToInt(angle / 45f) % 8;
        return dirs[index];
    }
}
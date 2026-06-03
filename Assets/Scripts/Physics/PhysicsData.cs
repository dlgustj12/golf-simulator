using UnityEngine;

[CreateAssetMenu(fileName = "PhysicsData", menuName = "Golf/PhysicsData")]
public class PhysicsData : ScriptableObject
{
    [Header("기본 물리 상수")]
    public float gravity    = 9.81f;      // m/s²
    public float ballMass   = 0.0459f;    // kg (골프공 표준)

    [Header("반발계수 (Coefficient of Restitution)")]
    [Range(0f, 1f)]
    public float restitution = 0.68f;     // 골프공 실측 근거값

    [Header("표면별 운동 마찰계수")]
    [Range(0f, 1f)] public float frictionFairway = 0.25f;
    [Range(0f, 1f)] public float frictionRough   = 0.55f;
    [Range(0f, 1f)] public float frictionBunker  = 0.80f;
    [Range(0f, 1f)] public float frictionGreen   = 0.15f;
}
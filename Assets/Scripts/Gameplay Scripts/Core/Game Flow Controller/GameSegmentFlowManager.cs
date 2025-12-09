using System.Collections;
using UnityEngine;

/// <summary>
/// GameSegmentFlowManager
/// Central authority for segment-driven input policy and (optionally) starter weapon equip.
/// - Movement is always allowed
/// - Fire/Special are only allowed during EnemyWave & Boss segments
/// - Starter weapon is equipped once (if a WeaponDriver is provided)
/// - Plays segment-start sounds for Enemy/Boss/Reward segments (not Space)
/// </summary>
[DefaultExecutionOrder(-1)]
public class GameSegmentFlowManager : MonoBehaviour
{
    #region Serialized References

    [Header("References")]
    [SerializeField, Tooltip("Sequencer that emits segment lifecycle events.")]
    private LevelSegmentSequencer sequencer;

    [SerializeField, Tooltip("Central input controller that gates movement/fire/special.")]
    private InputReaderController inputController;

    [SerializeField, Tooltip("Optional: If assigned, equips the starter weapon once.")]
    private WeaponDriver optionalWeaponDriver;

    #endregion

    #region Audio

    [Header("Audio")]
    [SerializeField, Tooltip("Played when an EnemyWave segment starts.")]
    private SoundData enemyWaveStartSound;

    [SerializeField, Tooltip("Played when a Boss segment starts.")]
    private SoundData bossStartSound;

    [SerializeField, Tooltip("Played when a Reward segment starts.")]
    private SoundData rewardSegmentSound;

    #endregion

    #region Starter Weapon Config

    [Header("Starter Weapon (Optional)")]
    [SerializeField, Tooltip("Starter weapon to equip once if a WeaponDriver is assigned.")]
    private WeaponType starterWeapon = WeaponType.Basic;

    [SerializeField, Tooltip("Equip starter at scene start (true) or at first combat segment (false).")]
    private bool equipStarterOnSceneStart = true;

    [SerializeField, Range(0f, 1f), Tooltip("Optional delay (seconds) before equipping starter (init order safety).")]
    private float starterEquipDelay = 0f;

    #endregion

    #region Private State

    private bool starterEquipped;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (sequencer == null) TryGetComponent(out sequencer);
        if (inputController == null) inputController = FindFirstObjectByType<InputReaderController>();

        if (sequencer == null) Debug.LogError("[GameSegmentFlowManager] Missing LevelSegmentSequencer reference.");
        if (inputController == null) Debug.LogError("[GameSegmentFlowManager] Missing InputReaderController reference.");
    }

    private void OnEnable()
    {
        if (sequencer != null)
        {
            sequencer.OnSegmentStarted += HandleSegmentStarted;
            sequencer.OnSegmentEnded += HandleSegmentEnded;
            sequencer.OnLevelEnded += HandleLevelEnded;
        }
    }

    private void Start()
    {
        // Start safe: movement-only until the first segment arrives.
        ApplyMovementOnlyProfile();

        if (equipStarterOnSceneStart)
            StartCoroutine(EquipStarterIfAvailableAfterDelay());
    }

    private void OnDisable()
    {
        if (sequencer != null)
        {
            sequencer.OnSegmentStarted -= HandleSegmentStarted;
            sequencer.OnSegmentEnded -= HandleSegmentEnded;
            sequencer.OnLevelEnded -= HandleLevelEnded;
        }
    }

    #endregion

    #region Segment Handling

    private void HandleSegmentStarted(int index, LevelSegment segment)
    {
        if (segment == null)
            return;
        Debug.Log("[Game Segment Flow] : We are at the segment " + segment.name);
        // Deferred starter equip (only if a WeaponDriver is provided)
        if (!equipStarterOnSceneStart && !starterEquipped && optionalWeaponDriver != null)
        {
            if (IsCombat(segment.SegmentType))
            {
                EquipStarterImmediate();
            }

            else
                StartCoroutine(EquipStarterIfAvailableAfterDelay()); // no-op if already equipped
        }

        // Segment start sound (EnemyWave / Boss / Reward; not Space)
        PlaySegmentStartSound(segment.SegmentType);

        // Input-driven gating by segment
        switch (segment.SegmentType)
        {
            case SegmentType.EnemyWave:
            case SegmentType.Boss:
                ApplyAllEnabledProfile();   // movement + fire + special
                break;

            case SegmentType.Space:
            case SegmentType.Reward:
            default:
                ApplyMovementOnlyProfile(); // movement only; fire/special disabled
                break;
        }
    }

    private void HandleSegmentEnded(int index, LevelSegment segment)
    {
        // No-op: we swap profiles on SegmentStarted for the next segment.
    }

    private void HandleLevelEnded()
    {
        ApplyMovementOnlyProfile(); // end-of-level: movement only
    }

    #endregion

    #region Input Profiles (no input simulation)

    /// <summary>Movement allowed; fire & special disabled. No input simulation.</summary>
    private void ApplyMovementOnlyProfile()
    {
        if (inputController == null) return;
        inputController.SetProfile_MovementOnly();
    }

    /// <summary>Movement + fire + special allowed. No input simulation.</summary>
    private void ApplyAllEnabledProfile()
    {
        if (inputController == null) return;
        inputController.SetProfile_AllEnabled();
    }

    #endregion

    #region Starter Equip

    private IEnumerator EquipStarterIfAvailableAfterDelay()
    {
        if (starterEquipped || optionalWeaponDriver == null) yield break;

        if (starterEquipDelay > 0f)
            yield return new WaitForSeconds(starterEquipDelay);

        EquipStarterImmediate();
    }

    private void EquipStarterImmediate()
    {
        if (starterEquipped || optionalWeaponDriver == null) return;

        if (!optionalWeaponDriver.IsEquipped(starterWeapon))
        {
            optionalWeaponDriver.Equip(starterWeapon);
            Debug.Log($"[GameSegmentFlowManager] Starter weapon equipped: {starterWeapon}");
        }

        starterEquipped = true;
    }

    #endregion

    #region Helpers

    private static bool IsCombat(SegmentType type)
    {
        return type == SegmentType.EnemyWave || type == SegmentType.Boss;
    }

    /// <summary>
    /// Plays the appropriate segment-start sound for the given segment type.
    /// EnemyWave, Boss, Reward -> sound. Space -> silent.
    /// </summary>
    private void PlaySegmentStartSound(SegmentType type)
    {
        switch (type)
        {
            case SegmentType.EnemyWave:
                SoundUtils.Play2D(enemyWaveStartSound);
                break;

            case SegmentType.Boss:
                SoundUtils.Play2D(bossStartSound);
                break;

            case SegmentType.Reward:
                SoundUtils.Play2D(rewardSegmentSound);
                break;

            case SegmentType.Space:
            default:
                // Intentionally no sound for Space or unknown types.
                break;
        }
    }

    #endregion
}

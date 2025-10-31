// Assets/Editor/GridGizmoDrawer.cs
// Stripe-aware gizmo drawer that visualizes mixed grid topologies inside each segment.
// - Uses SegmentStripeMap.Build(...) + GridStripeAdapter for world<->grid
// - Draws backdrop lattice per stripe (Rectangle / Hex / Octagon)
// - Draws placed entries (SpawnTable) with correct polygon per stripe
// - Draws per-segment colored bands with EXACT height by summing stripe heights (rows*step + yGap)
//
// Requires: GridConfig, LevelDefinition, LevelSegment, SpawnEntry/SpawnType,
//           SegmentStripeMap, GridStripeAdapter, HexGridMath, OctGridMath
//
#if UNITY_EDITOR
#endif
using UnityEngine;

public class LevelSequencerDebug : MonoBehaviour
{
    [SerializeField] private LevelSegmentSequencer sequencer;

    private void Reset()
    {
        if (!sequencer) sequencer = FindFirstObjectByType<LevelSegmentSequencer>();
    }

    private void OnEnable()
    {
        if (!sequencer) sequencer = FindFirstObjectByType<LevelSegmentSequencer>();
        if (!sequencer) return;

        sequencer.OnSegmentStarted += HandleSegmentStarted;
        sequencer.OnSegmentEnded += HandleSegmentEnded;
        sequencer.OnLevelEnded += HandleLevelEnded;
    }

    private void OnDisable()
    {
        if (!sequencer) return;

        sequencer.OnSegmentStarted -= HandleSegmentStarted;
        sequencer.OnSegmentEnded -= HandleSegmentEnded;
        sequencer.OnLevelEnded -= HandleLevelEnded;
    }

    private void HandleSegmentStarted(int index, LevelSegment seg)
    {
        Debug.Log($"[SEQ][START] idx={index} type={seg.SegmentType} rows={seg.LengthInRows}");
    }

    private void HandleSegmentEnded(int index, LevelSegment seg)
    {
        Debug.Log($"[SEQ][END]   idx={index} type={seg.SegmentType}");
    }

    private void HandleLevelEnded()
    {
        Debug.Log("[SEQ][LEVEL ENDED]");
    }
}

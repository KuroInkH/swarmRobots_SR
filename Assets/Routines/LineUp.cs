using System.Collections;
using UnityEngine;

// Line up: from wherever the robots are, they glide into a straight line across the middle of
// the mat and turn slowly to face the participant.
// Each robot takes the spot in the line that keeps the total travel smallest, so their straight
// paths never cross, whatever the starting layout.
public class LineUp : SwarmRoutine
{
    public override string Name { get { return "Line up: form a line"; } }
    public override int MinRobots { get { return 2; } }

    // Wider than the shared line (Swarm.LineSpacing, 35): at 48 apart the robots are outside the
    // range where the helpers push neighbours apart, so the line ends up straight and evenly spaced
    const float Spacing = 48f;
    const float LineY = Swarm.Centre;   // across the middle of the mat
    const float GlideUnitsPerSec = 70f; // calm pace: the longest trip sets how long the glide takes
    const int GlideMaxSpeed = 90;
    const int SettleMs = 1500;          // slow final settle onto the exact spots
    const double SettleTolerance = 4;   // mat units (~5 mm)
    const int TurnMs = 1500;            // turn to the participant: slow and smooth (max 2000)

    // Spots in the line, left to right, centred on the mat
    public static Vector2Int[] Spots(int n)
    {
        var spots = new Vector2Int[n];
        for (int i = 0; i < n; i++) { spots[i] = new Vector2Int(Mathf.RoundToInt(Swarm.Centre + (i - (n - 1) / 2f) * Spacing), Mathf.RoundToInt(LineY)); }
        return spots;
    }

    public override IEnumerator Run(RoutineContext ctx) { return Play(ctx); }

    // Form the line, then face the participant. Also used at the start of Messenger.
    public static IEnumerator Play(RoutineContext ctx)
    {
        yield return Form(ctx);
        yield return Swarm.TurnTo(ctx, Swarm.FacingParticipant, timeoutMs: 4000, rotateTimeMs: TurnMs);
    }

    // Glide into the line without anyone crossing
    public static IEnumerator Form(RoutineContext ctx)
    {
        int n = ctx.Count;
        var line = Spots(n);
        var from = new Vector2[n];
        var to = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            from[i] = new Vector2(ctx.Cubes[i].x, ctx.Cubes[i].y);
            to[i] = line[i];
        }
        var slot = Swarm.LeastTravel(from, to);
        var targets = new Vector2Int[n];
        for (int i = 0; i < n; i++) { targets[i] = line[slot[i]]; }
        yield return Swarm.GlideTo(ctx, targets, unitsPerSec: GlideUnitsPerSec, maxSpeed: GlideMaxSpeed);
        // The glide ends on time and the toio SDK counts 8 units as "arrived", so robots can sit a
        // little off their spots: settle slowly with a tighter tolerance so the line is straight
        yield return Settle(ctx, targets);
    }

    // End of a routine that has brought the robots (nearly) onto line spots: settle onto them
    // exactly, then face the participant, as at the end of Play
    public static IEnumerator Finish(RoutineContext ctx, Vector2Int[] targets)
    {
        yield return Settle(ctx, targets);
        yield return Swarm.TurnTo(ctx, Swarm.FacingParticipant, timeoutMs: 4000, rotateTimeMs: TurnMs);
    }

    static IEnumerator Settle(RoutineContext ctx, Vector2Int[] targets)
    {
        float end = Time.time + SettleMs / 1000f;
        while (Time.time < end)
        {
            if (ctx.CubeManager.synced)
            {
                for (int i = 0; i < ctx.Count; i++)
                {
                    ctx.Navigators[i].handle.Move2Target(targets[i].x, targets[i].y, maxSpd: 30, tolerance: SettleTolerance).Exec();
                }
            }
            yield return null;
        }
        Swarm.StopAll(ctx);
    }
}

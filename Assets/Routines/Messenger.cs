using System.Collections;
using UnityEngine;

// Messenger: the robots line up facing the participant. One robot, picked at random, leaves the
// line, stops in front of the participant and looks at them for a few seconds, turns slowly
// around, goes back to stand just in front of the group, facing it, and shakes a little, as if
// telling the others what it saw. Then everyone moves smoothly into a circle that turns.
// The other robots stay still in the line while the messenger is away.
public class Messenger : SwarmRoutine
{
    public override string Name { get { return "Messenger: visit participant -> turning circle"; } }
    public override int MinRobots { get { return 2; } }

    // ---- Visit ----
    const int VisitDistance = 70;      // how far in from the participant's edge the messenger stops
    const int LookMs = 3000;           // how long it looks at the participant
    const int TurnMs = 1800;           // slow, smooth turns (max 2000)
    const float WalkUnitsPerSec = 60f; // messenger's walking pace
    const int WalkMaxSpeed = 70;
    const float StepOut = 50f;         // first, straight out of the line this far
    const float TalkGap = 55f;         // where it stops to "talk": this far in front of the line

    // ---- Talk ----
    const int Shakes = 3;              // side-to-side wiggles
    const int ShakeSpeed = 25;
    const int ShakeMs = 150;
    const int AfterTalkMs = 800;       // short pause before the group moves

    // ---- Circle ----
    const float GatherUnitsPerSec = 45f; // calm move into the circle
    const int GatherMaxSpeed = 80;
    const float Radius = 90f;
    const float DegPerSec = 20f;       // circle turning speed
    const float SpinEaseSecs = 4f;     // turning eases in at the start and out at the end
    const float CircleSecs = 20f;      // how long the circle turns, easing included

    public override IEnumerator Run(RoutineContext ctx)
    {
        int n = ctx.Count;
        yield return Introduction(ctx);

        // 6. Everyone glides smoothly into a circle (spots chosen so nobody's path crosses)...
        var slotAngle = CircleSlots(ctx);
        var targets = new Vector2Int[n];
        for (int i = 0; i < n; i++) { targets[i] = Vector2Int.RoundToInt(Shapes.OnCircle(slotAngle[i], Radius)); }
        yield return Swarm.GlideTo(ctx, targets, unitsPerSec: GatherUnitsPerSec, maxSpeed: GatherMaxSpeed);

        // 7. ...and the circle turns, easing in and out so it starts and stops gently
        yield return Swarm.Follow(ctx, (i, t) => Shapes.OnCircle(slotAngle[i] + Turned(t), Radius), (int)(CircleSecs * 1000), maxSpeed: 90);
    }

    // Steps 1-5: line up, the messenger visits the participant, comes back and tells the group,
    // which listens from the line. Also the introduction of the Resonance and Cheering routines.
    public static IEnumerator Introduction(RoutineContext ctx)
    {
        int n = ctx.Count;

        // 1. Everyone in line, facing the participant
        yield return LineUp.Play(ctx);
        yield return Swarm.Wait(1000);

        // 2. Pick the messenger at random; it moves on its own while the rest wait
        var messenger = Only(ctx, UnityEngine.Random.Range(0, n));

        // 3. Step straight out of the line toward the participant (so it never brushes a
        //    neighbour), then go to the participant and look at them
        var here = new Vector2(messenger.Cubes[0].x, messenger.Cubes[0].y);
        var stepOut = Vector2Int.RoundToInt(here - Swarm.Inward * StepOut);
        yield return Swarm.GlideTo(messenger, new[] { stepOut }, unitsPerSec: WalkUnitsPerSec, maxSpeed: WalkMaxSpeed);
        var visit = Vector2Int.RoundToInt(Swarm.InFrontOfParticipant(VisitDistance));
        yield return Swarm.GlideTo(messenger, new[] { visit }, unitsPerSec: WalkUnitsPerSec, maxSpeed: WalkMaxSpeed);
        yield return Swarm.TurnTo(messenger, Swarm.FacingParticipant, timeoutMs: 4000, rotateTimeMs: TurnMs);
        yield return Swarm.Wait(LookMs);

        // 4. Turn slowly around and go back to just in front of the group, facing it
        double facingGroup = Swarm.FacingParticipant + 180;
        yield return Swarm.TurnTo(messenger, facingGroup, timeoutMs: 5000, rotateTimeMs: TurnMs);
        var talkSpot = Vector2Int.RoundToInt(TalkSpot);
        yield return Swarm.GlideTo(messenger, new[] { talkSpot }, unitsPerSec: WalkUnitsPerSec, maxSpeed: WalkMaxSpeed);
        yield return Swarm.TurnTo(messenger, facingGroup, timeoutMs: 3000, rotateTimeMs: 800);

        // 5. Talk: a little shake
        for (int k = 0; k < Shakes; k++)
        {
            yield return Swarm.Wheels(messenger, ShakeSpeed, -ShakeSpeed, ShakeMs);
            yield return Swarm.Wheels(messenger, -ShakeSpeed, ShakeSpeed, ShakeMs);
        }
        yield return Swarm.Wait(AfterTalkMs);
    }

    // Where the messenger stands to talk to the group
    public static Vector2 TalkSpot { get { return Shapes.Centre - Swarm.Inward * TalkGap; } }

    // A context holding just robot i, so the shared helpers move it alone
    static RoutineContext Only(RoutineContext ctx, int i)
    {
        var one = new RoutineContext();
        one.Cubes = new[] { ctx.Cubes[i] };
        one.Navigators = new[] { ctx.Navigators[i] };
        one.CubeManager = ctx.CubeManager;
        return one;
    }

    // Circle angle (deg) for each robot: evenly spaced spots, each robot taking the one that
    // keeps total travel smallest
    static float[] CircleSlots(RoutineContext ctx)
    {
        int n = ctx.Count;
        var spotAngle = new float[n];
        var from = new Vector2[n];
        var to = new Vector2[n];
        for (int j = 0; j < n; j++)
        {
            spotAngle[j] = 90f + 360f * j / n; // first spot faces the participant
            to[j] = Shapes.OnCircle(spotAngle[j], Radius);
            from[j] = new Vector2(ctx.Cubes[j].x, ctx.Cubes[j].y);
        }
        var slot = Swarm.LeastTravel(from, to);
        var angle = new float[n];
        for (int i = 0; i < n; i++) { angle[i] = spotAngle[slot[i]]; }
        return angle;
    }

    // How far the circle has turned (deg) t seconds in: speed eases up from 0 to DegPerSec, holds,
    // and eases back down to 0 at the end
    static float Turned(float t)
    {
        float slowDownAt = CircleSecs - SpinEaseSecs;
        return DegPerSec * SpinEaseSecs * (Shapes.EaseIntegral(t / SpinEaseSecs) - Shapes.EaseIntegral((t - slowDownAt) / SpinEaseSecs));
    }
}

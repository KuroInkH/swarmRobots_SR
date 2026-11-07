using System.Collections;
using System.Linq;
using UnityEngine;

// Cheering - Social dance. After the Messenger's introduction the robots pair up and each pair
// dances in its own corner of the mat: partners trace mirrored curves like butterfly wings, sweeping
// apart, arcing back toward each other and opening out again. On an inward sweep the pairs come in
// from their corners and join a compact ring, which starts to turn as it forms. Then a campfire
// dance: the ring keeps turning slowly while it widens and the robots spread out evenly, easing to
// its final size while still turning gently, and flows into the line.
public class CheeringSocialDance : SwarmRoutine
{
    public override string Name { get { return "Cheering: social dance"; } }
    public override int MinRobots { get { return 2; } }

    // ---- Timing (seconds after the introduction; the return to the line starts at 30) ----
    const float EnterSecs = 3f;   // to the corners, arriving already in the first sweep
    const float JoinFrom = 10f;   // pairs come in from their corners...
    const float JoinTo = 15f;     // ...into a compact ring, which starts turning
    const float WidenTo = 27f;    // the ring widens until here, still turning

    // ---- Butterfly pairs ----
    // Corner dance spots in the order pairs use them (2-3 robots use the first one or two).
    static readonly Vector2[] Corners = { new Vector2(-1, -1), new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1) };
    const float CornerOffset = 115f;  // dance spot from the mat centre, along x and along y
    const float WingSecs = 3.2f;      // one wing beat: apart, back together
    const float CloseGap = 44f;       // partners' distance at their closest
    const float WingOpen = 38f;       // each partner sweeps this much further out to the side
    const float WingReach = 30f;      // and this far toward and away from the mat centre

    // ---- Campfire ring ----
    const float CompactSpacing = 42f;        // neighbour distance in the compact ring
    const float CompactMinRadius = 45f;
    const float FinalDiameterShare = 0.63f;  // final ring diameter as a share of the usable mat
    const float RingSpeed = 20f;             // mat units per second along the ring, whatever its size

    public override IEnumerator Run(RoutineContext ctx) { return Show.Run(ctx, start => new Plan(start)); }

    public class Plan : Show.IPlan
    {
        readonly int n;
        readonly Vector2[] start, spot;
        readonly int[] pair, side;   // side +1/-1 for partners, 0 for a robot dancing alone
        readonly float[] place;      // radians around the ring
        readonly float compactRadius, finalRadius;
        readonly Show.Integrated turned;

        public Plan(Vector2[] start)
        {
            this.start = start;
            n = start.Length;
            compactRadius = Mathf.Max(CompactMinRadius, CompactSpacing / (2f * Mathf.Sin(Mathf.PI / n)));
            finalRadius = FinalDiameterShare * (Swarm.MatMax - Swarm.MatMin) / 2f;
            turned = new Show.Integrated(TurnRate, Show.Total + 1f);

            // Partners and corners: the dance places with the least total travel from the line
            var slotPair = new int[n]; var slotSide = new int[n];
            for (int s = 0; s < n; s++)
            {
                slotPair[s] = s / 2;
                slotSide[s] = (n % 2 == 1 && s == n - 1) ? 0 : (s % 2 == 0 ? 1 : -1);
            }
            var arrive = Enumerable.Range(0, n).Select(s => Wing(slotPair[s], slotSide[s], EnterSecs)).ToArray();
            var slot = Swarm.LeastTravel(start, arrive);
            pair = new int[n]; side = new int[n];
            for (int i = 0; i < n; i++) { pair[i] = slotPair[slot[i]]; side[i] = slotSide[slot[i]]; }

            // Ring places: each pair joins the part of the ring nearest its corner
            var joining = Enumerable.Range(0, n).Select(i => Wing(pair[i], side[i], JoinFrom)).ToArray();
            place = Show.RingPlaces(joining, compactRadius, turned.At(JoinTo));

            var leaving = Enumerable.Range(0, n).Select(i => Ring(i, Show.PerformSecs + Show.ReturnSecs / 2f)).ToArray();
            spot = Show.LineSpots(leaving);
        }

        // A partner's place in its pair's butterfly dance. Partners mirror each other across the
        // line from the corner to the mat centre; at JoinFrom they are at their closest and
        // starting a sweep toward the centre.
        static Vector2 Wing(int p, int s, float t)
        {
            Vector2 toCentre = -Corners[p].normalized;
            Vector2 across = new Vector2(-toCentre.y, toCentre.x);
            float a = 2f * Mathf.PI * (t - JoinFrom) / WingSecs;
            float apart = Mathf.Sin(a / 2f);
            return Shapes.Centre + Corners[p] * CornerOffset
                 + toCentre * WingReach * Mathf.Sin(a)
                 + across * s * (CloseGap / 2f + WingOpen * apart * apart);
        }

        float Radius(float t)
        {
            return Mathf.Lerp(compactRadius, finalRadius, Shapes.Ease((t - JoinTo) / (WidenTo - JoinTo)));
        }

        // Turning speed (radians per second): starts as the ring forms, keeps the same speed along
        // the ring as it widens, and eases away during the return
        float TurnRate(float t)
        {
            return Shapes.Ease((t - JoinFrom) / (JoinTo - JoinFrom)) * RingSpeed / Radius(t)
                 * (1f - Shapes.Ease((t - Show.PerformSecs) / Show.ReturnSecs));
        }

        Vector2 Ring(int i, float t) { return Show.OnRing(place[i] + turned.At(t), Radius(t)); }

        public Vector2 Position(int i, float t)
        {
            t = Mathf.Clamp(t, 0f, Show.Total);
            Vector2 p = Vector2.Lerp(start[i], Wing(pair[i], side[i], t), Shapes.Ease(t / EnterSecs));
            p = Vector2.Lerp(p, Ring(i, t), Shapes.Ease((t - JoinFrom) / (JoinTo - JoinFrom)));
            return Show.Return(p, spot[i], t);
        }

        public Vector2 LineSpot(int i) { return spot[i]; }
    }
}

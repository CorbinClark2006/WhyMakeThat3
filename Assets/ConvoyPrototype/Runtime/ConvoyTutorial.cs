using System;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyTutorial
    {
        [Flags]
        public enum Lesson
        {
            None = 0, Walk = 1, Look = 2, Jump = 4, BatteryPickup = 8, BatteryLoad = 16,
            GunMount = 32, GunFire = 64, DriverMount = 128, Steer = 256,
            RockPickup = 512, RockPlace = 1024
        }

        public readonly struct Hint
        {
            public readonly Lesson Lesson;
            public readonly string Title, Key, Line1, Line2, Line3;
            public readonly Transform Target;
            public readonly Vector3 TargetOffset;
            public Vector3 TargetPosition => Target.TransformPoint(TargetOffset);
            public readonly int Step, Steps;

            public Hint(Lesson lesson, string title, string key, string line1, string line2,
                string line3, Transform target = null, int step = 0, int steps = 1, Vector3 targetOffset = default)
            {
                Lesson = lesson;
                Title = title;
                Key = key;
                Line1 = line1;
                Line2 = line2;
                Line3 = line3;
                Target = target;
                TargetOffset = targetOffset;
                Step = step;
                Steps = steps;
            }
        }

        private static readonly Color Ink = ConvoyPixelUI.Ink, Plum = ConvoyPixelUI.Plum,
            Rust = ConvoyPixelUI.Rust, Copper = ConvoyPixelUI.Copper, Gold = ConvoyPixelUI.Gold, Paper = ConvoyPixelUI.Paper;
        private readonly ConvoyPrototype game;
        private float walked, looked, steered, previousSteering, refreshRemaining, hintAge, stampRemaining;
        private string stamp = "";
        private bool reloadRouteStarted;
        public Lesson Learned { get; private set; }
        public Hint Current { get; private set; }
        public bool Visible => game.player != null && !game.ChoosingRole && !game.Paused &&
            !game.IsDead && !game.player.IsRespawning && game.SimulationRunning &&
            (game.network == null || !game.network.ShowLobby);

        public ConvoyTutorial(ConvoyPrototype game) => this.game = game;

        public bool Knows(Lesson lesson) => (Learned & lesson) == lesson;

        public void Reset()
        {
            Learned = Lesson.None;
            walked = looked = steered = refreshRemaining = hintAge = stampRemaining = 0f;
            previousSteering = game.SteeringOffset;
            Current = default;
            stamp = "";
            reloadRouteStarted = false;
        }

        public void ObserveWalk(float distance)
        {
            walked += distance;
            if (walked >= 0.7f) Complete(Lesson.Walk);
        }

        public void ObserveLook(float degrees)
        {
            looked += degrees;
            if (looked >= 12f) Complete(Lesson.Look);
        }

        public void ObserveSteering(float distance)
        {
            steered += distance;
            if (steered >= 0.35f && !Knows(Lesson.Steer))
                game.RecordLesson(game.player, Lesson.Steer);
        }

        public void Complete(Lesson lesson)
        {
            if (lesson == Lesson.None || Knows(lesson))
                return;
            Learned |= lesson;
            refreshRemaining = 0f;
            if (Visible && (Current.Lesson & lesson) != 0 &&
                (Current.Lesson != (Lesson.Walk | Lesson.Look) || Knows(Current.Lesson)))
            {
                stamp = Current.Title;
                stampRemaining = 1.4f;
            }
        }

        public void Tick(float dt)
        {
            if (game.Online)
            {
                if (!game.WorldAuthority && Visible && game.player.Driving && game.network.SteeringInput != 0f)
                    ObserveSteering(Mathf.Abs(game.SteeringOffset - previousSteering));
            }
            previousSteering = game.SteeringOffset;
            if (!Visible)
            {
                Current = default;
                hintAge = 0f;
                stampRemaining = 0f;
                return;
            }
            stampRemaining = Mathf.Max(0f, stampRemaining - dt);
            hintAge += dt;
            refreshRemaining -= dt;
            if (refreshRemaining > 0f)
                return;
            refreshRemaining = 0.12f;
            Hint next = FindHint();
            if (next.Title != Current.Title || next.Key != Current.Key)
                hintAge = 0f;
            Current = next;
        }

        public Hint FindHint()
        {
            if (!Visible)
                return default;
            ConvoyPlayer actor = game.player;
            ConvoyAccelerator accelerator = game.accelerator;
            if (actor.Role != ConvoyPrototype.PlayerRole.Driver && game.ShotsAvailable == 0 &&
                !Knows(Lesson.BatteryLoad))
                reloadRouteStarted = true;
            if (actor.CarryingRock)
            {
                if (Knows(Lesson.RockPlace)) return default;
                bool reachable = accelerator.CanPlace(actor);
                return new Hint(Lesson.RockPlace, "KEEP THE ENGINE ALIVE", reachable ? "E" : "WASD",
                    reachable ? "SEAT THE ROCK ON THE PEDAL." : "BRING THE ROCK TO THE PEDAL.",
                    "THE VAN REGAINS ITS SPEED.",
                    "REPAIR HELPS BOTH CREW MEMBERS.", accelerator.pedal, 1, 2);
            }
            if (actor.CarriedAmmo != null)
            {
                if (Knows(Lesson.BatteryLoad)) return default;
                bool reachable = game.CanLoadAmmo(actor.CarriedAmmo, actor);
                return new Hint(Lesson.BatteryLoad, "FEED THE RAILGUN", reachable ? "E" : "WASD",
                    reachable ? "INSERT THE BATTERY INTO THE GUN." : "CARRY THE BATTERY TO THE REAR.",
                    "ONE BATTERY ADDS FOUR SHOTS.",
                    "E AT THE RAILGUN LOADS THE BATTERY.", game.gun, 1, 2, ConvoyPrototype.GunLoadingPoint);
            }
            if (actor.Driving)
            {
                if (Knows(Lesson.Steer)) return default;
                return new Hint(Lesson.Steer, "THREAD THE ROAD", "A D",
                    "HOLD RIGHT MOUSE + STEER LEFT/RIGHT.",
                    "DODGE ROCKS AND BUSHES AHEAD.",
                    "RELEASE RIGHT MOUSE TO STEP AWAY.", null, 1, 2);
            }
            if (reloadRouteStarted && !Knows(Lesson.BatteryLoad))
            {
                ConvoyAmmoPack nearby = actor.FindReachableAmmo();
                ConvoyAmmoPack pack = nearby ?? actor.FindAvailableAmmo();
                bool reachable = !actor.Mounted && nearby != null;
                return new Hint(Lesson.BatteryPickup, "EMPTY GUN? FOLLOW THE BATTERY",
                    actor.Mounted ? "RMB" : reachable ? "E" : "WASD",
                    actor.Mounted ? "RELEASE RIGHT MOUSE TO STEP AWAY." :
                        reachable ? "PICK UP THIS AMMO BATTERY." : "FOLLOW THE MARKER TO THE BATTERY.",
                    "THEN BRING IT BACK TO THE RAILGUN.",
                    "E AT THE RAILGUN LOADS FOUR SHOTS.", pack != null ? pack.transform : game.ammoSupply, 0, 2);
            }
            if (actor.Mounted)
            {
                if (Knows(Lesson.GunFire)) return default;
                return new Hint(Lesson.GunFire, "MAKE TWO SHOTS COUNT", game.ShotsAvailable > 0 ? "LMB" : "RMB",
                    game.ShotsAvailable > 0 ? "MOUSE TO AIM. LEFT MOUSE TO FIRE." : "EMPTY! RELEASE RIGHT MOUSE.",
                    game.ShotsAvailable > 0 ? "TWO HITS TAKE DOWN A CHASING VAN." : "FETCH A BATTERY FOR FOUR SHOTS.",
                    "RELEASE RIGHT MOUSE TO STEP AWAY.", null, 1, 2);
            }
            if (accelerator != null && !Knows(Lesson.RockPickup) && accelerator.CanPickUp(actor))
                return new Hint(Lesson.RockPickup, "THE ROCK IS YOUR THROTTLE", "E",
                    "PICK UP THE FALLEN ROCK.",
                    "BRING IT BACK TO THE ACCELERATOR.",
                    "THE ENGINE SLOWS WITHOUT IT.", accelerator.rock, 0, 2);
            if (!Knows(Lesson.BatteryPickup))
            {
                ConvoyAmmoPack pack = actor.FindReachableAmmo();
                if (pack != null)
                    return new Hint(Lesson.BatteryPickup, "POWER FOR THE REAR GUN", "E",
                        "PICK UP THIS AMMO BATTERY.",
                        "CARRY IT BACK TO THE RAILGUN.",
                        "E AT THE GUN LOADS FOUR SHOTS.", pack.transform, 0, 2);
            }
            if (actor.Role == ConvoyPrototype.PlayerRole.Driver && !Knows(Lesson.DriverMount) && actor.CanDrive)
                return new Hint(Lesson.DriverMount, "TAKE THE WHEEL", "RMB",
                    "HOLD RIGHT MOUSE AT THE WHEEL.",
                    "YOU WILL SWITCH TO THE HOOD VIEW.",
                    "KEEP HOLDING IT. A/D STEER.", game.van, 0, 2, ConvoyPrototype.DriverBoardingPoint);
            if (actor.Role != ConvoyPrototype.PlayerRole.Driver && !Knows(Lesson.GunMount) && actor.CanMount)
                return new Hint(Lesson.GunMount, "YOUR SEAT AT THE REAR", "RMB",
                    "HOLD RIGHT MOUSE BY THE GUN.",
                    "ENTER THE FIRST-PERSON GUN SIGHT.",
                    "KEEP HOLDING IT. LEFT MOUSE FIRES.", game.gun, 0, 2);
            if (!Knows(Lesson.Walk | Lesson.Look))
                return new Hint(Lesson.Walk | Lesson.Look, "FIND YOUR FEET", "WASD",
                    "WASD MOVES YOU INSIDE THE VAN.",
                    "MOVE THE MOUSE TO LOOK AROUND.",
                    "NO MENU. LEARN WHILE YOU ROLL.");
            if (!Knows(Lesson.Jump))
                return new Hint(Lesson.Jump, "A LITTLE HOP GOES A LONG WAY", "SPACE",
                    "SPACE JUMPS ONTO STOOLS / LEDGES.",
                    "WATCH THE OPENING IN THE FLOOR.",
                    "A FALL RESPAWNS ONLY YOU.");
            return default;
        }

        public void Draw()
        {
            if (!Visible)
                return;
            if (Current.Lesson != Lesson.None)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(hintAge / 0.2f));
                float y = 512f + Mathf.Round((1f - t) * 12f);
                Panel(new Rect(32, y, 570, 176), t);
                RectFill(new Rect(40, y + 8, 554, 30), Rust, t);
                Text("CREW MANUAL / LEARN BY DOING", 52, y + 16, 2, Paper, t);
                Text(Current.Title, 52, y + 52, 2, Gold, t);
                Keycap(Current.Key, 52, y + 86, t);
                Text(Current.Line1, 148, y + 88, 2, Paper, t);
                Text(Current.Line2, 148, y + 112, 2, Paper, t);
                Text(Current.Line3, 148, y + 140, 2, Gold, t);
                for (int i = 0; i < Current.Steps; i++)
                {
                    Color color = i < Current.Step ? Paper : i == Current.Step ? Gold : Plum;
                    RectFill(new Rect(554 + i * 16 - Current.Steps * 16, y + 17, 10, 10), color, t);
                    if (i == Current.Step) RectFill(new Rect(554 + i * 16 - Current.Steps * 16, y + 29, 10, 2), Paper, t);
                }
                DrawTarget(t);
            }
            if (stampRemaining > 0f)
            {
                float alpha = Mathf.Min(1f, stampRemaining / 0.3f);
                Panel(new Rect(434, 32, 402, 72), alpha);
                RectFill(new Rect(446, 48, 32, 32), Gold, alpha);
                RectFill(new Rect(452, 63, 6, 6), Ink, alpha);
                RectFill(new Rect(458, 69, 6, 6), Ink, alpha);
                RectFill(new Rect(464, 57, 6, 12), Ink, alpha);
                Text("LOGGED. YOU HAVE GOT THIS.", 494, 48, 2, Paper, alpha);
                Text(stamp, 494, 73, 1, Gold, alpha);
            }
        }

        private void DrawTarget(float alpha)
        {
            if (Current.Target == null)
                return;
            Camera camera = game.player.Mounted ? game.gunCamera : game.player.view;
            Vector3 point = camera.WorldToViewportPoint(Current.TargetPosition);
            if (point.z <= 0f)
            {
                bool right = camera.transform.InverseTransformPoint(Current.TargetPosition).x >= 0f;
                DirectionMarker(right ? 1240 : 40, 360, right ? "TURN RIGHT" : "TURN LEFT", alpha);
                return;
            }
            Vector2 screen = new Vector2(point.x * 1280f, (1f - point.y) * 720f);
            bool onScreen = screen.x >= 24 && screen.x <= 1256 && screen.y >= 120 && screen.y <= 478;
            float x = Mathf.Clamp(Mathf.Round(screen.x), 24, 1256);
            float y = Mathf.Clamp(Mathf.Round(screen.y), 120, 478);
            if (!onScreen)
            {
                DirectionMarker(x, y, screen.y > 478 ? "LOOK DOWN" : screen.y < 120 ?
                    "LOOK UP" : screen.x > 1256 ? "TURN RIGHT" : "TURN LEFT", alpha);
                return;
            }
            float size = 12 + (Mathf.FloorToInt(hintAge * 2f) % 2) * 2;
            for (int side = -1; side <= 1; side += 2)
            {
                RectFill(new Rect(x + side * size - (side > 0 ? 8 : 0), y - size, 8, 2), Gold, alpha);
                RectFill(new Rect(x + side * size - (side > 0 ? 2 : 0), y - size, 2, 8), Gold, alpha);
                RectFill(new Rect(x + side * size - (side > 0 ? 8 : 0), y + size - 2, 8, 2), Gold, alpha);
                RectFill(new Rect(x + side * size - (side > 0 ? 2 : 0), y + size - 8, 2, 8), Gold, alpha);
            }
            Text("HERE", Mathf.Clamp(x + 20, 24, 1210), y - 7, 2, Paper, alpha);
            DrawTargetLine(x, y, size, alpha);
        }

        private static void DrawTargetLine(float x, float y, float size, float alpha)
        {
            float elbow = Mathf.Min(x, 578);
            for (float dash = y + size + 10; dash < 498; dash += 12)
                RectFill(new Rect(x - 1, dash, 2, 5), Copper, alpha * 0.6f);
            RectFill(new Rect(elbow, 498, Mathf.Abs(x - elbow) + 2, 2), Copper, alpha * 0.6f);
        }

        private void DirectionMarker(float x, float y, string caption, float alpha)
        {
            DrawTargetLine(x, y, 6, alpha);
            RectFill(new Rect(x - 6, y - 6, 12, 12), Gold, alpha);
            RectFill(new Rect(x - 2, y - 2, 4, 4), Ink, alpha);
            float labelX = Mathf.Clamp(x + 18, 48, 1232 - caption.Length * 12);
            RectFill(new Rect(labelX - 4, y - 11, caption.Length * 12 + 6, 22), Ink, alpha * 0.9f);
            Text(caption, labelX, y - 7, 2, Paper, alpha);
        }

        private void Keycap(string key, float x, float y, float alpha)
        {
            RectFill(new Rect(x + 4, y + 4, 76, 64), Ink, alpha);
            RectFill(new Rect(x, y, 76, 64), Copper, alpha);
            RectFill(new Rect(x + 2, y + 2, 72, 58), Gold, alpha);
            RectFill(new Rect(x + 4, y + 4, 68, 52), Plum, alpha);
            if (key == "RMB" || key == "LMB")
            {
                RectFill(new Rect(x + 27, y + 8, 22, 28), Paper, alpha);
                RectFill(new Rect(x + 29, y + 10, 18, 24), Ink, alpha);
                RectFill(new Rect(x + (key == "RMB" ? 39 : 29), y + 10, 8, 11), Gold, alpha);
                RectFill(new Rect(x + 38, y + 10, 1, 12), Paper, alpha);
                Text(key, x + 21, y + 42, 2, Paper, alpha);
            }
            else
                Text(key, x + (76 - (key.Length * 6 - 1) * 2) / 2f, y + 25, 2, Paper, alpha);
        }

        private static void Panel(Rect rect, float alpha)
        {
            ConvoyPixelUI.Panel(rect, alpha);
        }

        private static void RectFill(Rect rect, Color color, float alpha)
        {
            ConvoyPixelUI.Fill(rect, color, alpha);
        }

        private void Text(string text, float x, float y, int scale, Color color, float alpha)
        {
            game.UI.Text(text, x, y, scale, color, alpha);
        }
    }
}

using System;
using System.Collections.Generic;
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
            public readonly int Step, Steps;

            public Hint(Lesson lesson, string title, string key, string line1, string line2,
                string line3, Transform target = null, int step = 0, int steps = 1)
            {
                Lesson = lesson;
                Title = title;
                Key = key;
                Line1 = line1;
                Line2 = line2;
                Line3 = line3;
                Target = target;
                Step = step;
                Steps = steps;
            }
        }

        private static readonly Color Ink = new Color32(46, 34, 47, 255);
        private static readonly Color Plum = new Color32(69, 41, 63, 255);
        private static readonly Color Rust = new Color32(153, 61, 65, 255);
        private static readonly Color Copper = new Color32(205, 104, 61, 255);
        private static readonly Color Gold = new Color32(251, 185, 84, 255);
        private static readonly Color Paper = new Color32(242, 236, 139, 255);
        private readonly ConvoyPrototype game;
        private readonly Dictionary<string, Texture2D> lettering = new Dictionary<string, Texture2D>();
        private float walked, looked, steered, previousSteering, refreshRemaining, hintAge, stampRemaining;
        private string stamp = "";
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
                    "LOOK FOR THE MARKED GUN BREECH.", game.ammoReceiver, 1, 2);
            }
            if (actor.Driving)
            {
                if (Knows(Lesson.Steer)) return default;
                return new Hint(Lesson.Steer, "THREAD THE ROAD", "A D",
                    "HOLD RIGHT MOUSE + STEER LEFT/RIGHT.",
                    "DODGE ROCKS AND BUSHES AHEAD.",
                    "RELEASE RIGHT MOUSE TO STEP AWAY.", null, 1, 2);
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
                    "KEEP HOLDING IT. A/D STEER.", game.van.Find("Driver steering wheel"), 0, 2);
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
                Panel(new Rect(846, 616, 402, 72), alpha);
                RectFill(new Rect(858, 632, 32, 32), Gold, alpha);
                RectFill(new Rect(864, 647, 6, 6), Ink, alpha);
                RectFill(new Rect(870, 653, 6, 6), Ink, alpha);
                RectFill(new Rect(876, 641, 6, 12), Ink, alpha);
                Text("LOGGED. YOU HAVE GOT THIS.", 906, 632, 2, Paper, alpha);
                Text(stamp, 906, 657, 1, Gold, alpha);
            }
        }

        private void DrawTarget(float alpha)
        {
            if (Current.Target == null)
                return;
            Camera camera = game.player.Mounted ? game.gunCamera : game.player.view;
            Vector3 point = camera.WorldToViewportPoint(Current.Target.position);
            if (point.z <= 0f)
            {
                bool right = camera.transform.InverseTransformPoint(Current.Target.position).x >= 0f;
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
            Text("HERE", x + 20, y - 7, 2, Paper, alpha);
            float elbow = Mathf.Min(x, 578);
            for (float dash = y + size + 10; dash < 498; dash += 12)
                RectFill(new Rect(x - 1, dash, 2, 5), Copper, alpha * 0.6f);
            RectFill(new Rect(elbow, 498, Mathf.Abs(x - elbow) + 2, 2), Copper, alpha * 0.6f);
        }

        private void DirectionMarker(float x, float y, string caption, float alpha)
        {
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
            RectFill(new Rect(rect.x + 6, rect.y + 6, rect.width, rect.height), Ink, alpha * 0.8f);
            RectFill(new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height), Copper, alpha);
            RectFill(new Rect(rect.x, rect.y + 4, rect.width, rect.height - 8), Copper, alpha);
            RectFill(new Rect(rect.x + 2, rect.y + 6, rect.width - 4, rect.height - 12), Ink, alpha);
            RectFill(new Rect(rect.x + 6, rect.y + 2, rect.width - 12, rect.height - 4), Ink, alpha);
            RectFill(new Rect(rect.x + 8, rect.y + rect.height - 6, rect.width - 16, 2), Plum, alpha);
        }

        private static void RectFill(Rect rect, Color color, float alpha)
        {
            color.a *= alpha;
            GUI.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private void Text(string text, float x, float y, int scale, Color color, float alpha)
        {
            if (!lettering.TryGetValue(text, out Texture2D texture))
            {
                texture = MakeLettering(text);
                lettering.Add(text, texture);
            }
            color.a *= alpha;
            GUI.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            GUI.DrawTexture(new Rect(Mathf.Round(x), Mathf.Round(y), texture.width * scale, texture.height * scale), texture);
        }

        // Five-column glyphs are baked once per caption, not drawn pixel by pixel every frame.
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['A'] = "7E1111117E", ['B'] = "7F49494936", ['C'] = "3E41414122",
            ['D'] = "7F4141221C", ['E'] = "7F49494941", ['F'] = "7F09090901",
            ['G'] = "3E41495132", ['H'] = "7F0808087F", ['I'] = "00417F4100",
            ['J'] = "204040403F", ['K'] = "7F08142241", ['L'] = "7F40404040",
            ['M'] = "7F020C027F", ['N'] = "7F0408107F", ['O'] = "3E4141413E",
            ['P'] = "7F09090906", ['Q'] = "3E4151215E", ['R'] = "7F09192946",
            ['S'] = "2649494932", ['T'] = "01017F0101", ['U'] = "3F4040403F",
            ['V'] = "1F2040201F", ['W'] = "3F4038403F", ['X'] = "6314081463",
            ['Y'] = "0304780403", ['Z'] = "6151494543", ['0'] = "3E4549513E",
            ['1'] = "00427F4000", ['2'] = "4261514946", ['3'] = "2141454B31",
            ['4'] = "1814127F10", ['5'] = "2745454539", ['6'] = "3C4A494930",
            ['7'] = "0171090503", ['8'] = "3649494936", ['9'] = "064949291E",
            ['.'] = "0060600000", [','] = "0040200000", ['!'] = "00005F0000",
            ['/'] = "2010080402", ['+'] = "08083E0808", ['-'] = "0808080808",
            [':'] = "0036360000", [' '] = "0000000000"
        };

        private static Texture2D MakeLettering(string text)
        {
            int width = text.Length * 6 - 1;
            Color32[] pixels = new Color32[width * 7];
            for (int i = 0; i < text.Length; i++)
            {
                if (!Glyphs.TryGetValue(text[i], out string glyph))
                    throw new InvalidOperationException("Missing crew-manual pixel glyph: " + text[i]);
                for (int x = 0; x < 5; x++)
                {
                    int column = Convert.ToInt32(glyph.Substring(x * 2, 2), 16);
                    for (int y = 0; y < 7; y++)
                        if ((column & (1 << y)) != 0)
                            pixels[(6 - y) * width + i * 6 + x] = new Color32(255, 255, 255, 255);
                }
            }
            Texture2D texture = new Texture2D(width, 7, TextureFormat.RGBA32, false)
            {
                name = "Crew manual: " + text, filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in lettering.Values)
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
            lettering.Clear();
        }
    }
}

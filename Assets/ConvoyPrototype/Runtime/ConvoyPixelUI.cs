using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Convoy
{
    public sealed class ConvoyPixelUI : IDisposable
    {
        public static readonly Color Ink = new Color32(46, 34, 47, 255);
        public static readonly Color Plum = new Color32(69, 41, 63, 255);
        public static readonly Color Rust = new Color32(153, 61, 65, 255);
        public static readonly Color Copper = new Color32(205, 104, 61, 255);
        public static readonly Color Gold = new Color32(251, 185, 84, 255);
        public static readonly Color Paper = new Color32(242, 236, 139, 255);
        private readonly Dictionary<string, Texture2D> lettering = new Dictionary<string, Texture2D>();
        private readonly Queue<string> captions = new Queue<string>();
        private GUIStyle inputStyle;
        public const int CacheLimit = 256;

        public static void Fill(Rect rect, Color color, float alpha = 1f)
        {
            color.a *= alpha;
            GUI.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        public static void Panel(Rect rect, float alpha = 1f, bool highlighted = false)
        {
            Fill(new Rect(rect.x + 6, rect.y + 6, rect.width, rect.height), Ink, alpha * 0.8f);
            Color rim = highlighted ? Gold : Copper;
            Fill(new Rect(rect.x + 4, rect.y, rect.width - 8, rect.height), rim, alpha);
            Fill(new Rect(rect.x, rect.y + 4, rect.width, rect.height - 8), rim, alpha);
            Fill(new Rect(rect.x + 2, rect.y + 6, rect.width - 4, rect.height - 12), Ink, alpha);
            Fill(new Rect(rect.x + 6, rect.y + 2, rect.width - 12, rect.height - 4), Ink, alpha);
            Fill(new Rect(rect.x + 8, rect.y + rect.height - 6, rect.width - 16, 2), Plum, alpha);
        }

        public void Text(string caption, float x, float y, int scale = 2, Color? tint = null, float alpha = 1f)
        {
            string text = Normalize(caption).Replace('\n', ' ');
            if (text.Length == 0) return;
            if (!lettering.TryGetValue(text, out Texture2D texture))
            {
                if (lettering.Count >= CacheLimit)
                {
                    string oldest = captions.Dequeue();
                    DestroyTexture(lettering[oldest]);
                    lettering.Remove(oldest);
                }
                texture = Bake(text);
                lettering.Add(text, texture);
                captions.Enqueue(text);
            }
            Color color = tint ?? Paper;
            color.a *= alpha;
            GUI.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            GUI.DrawTexture(new Rect(Mathf.Round(x), Mathf.Round(y), texture.width * scale, texture.height * scale), texture);
        }

        public void Center(string text, Rect rect, int scale = 2, Color? color = null)
        {
            int fitted = Mathf.Max(1, Mathf.Min(scale, Mathf.FloorToInt(rect.width / Mathf.Max(1, Normalize(text).Length * 6 - 1))));
            Text(text, rect.center.x - (Normalize(text).Length * 6 - 1) * fitted / 2f,
                rect.center.y - 7 * fitted / 2f, fitted, color);
        }

        public void Paragraph(string caption, Rect rect, int scale = 2, Color? color = null)
        {
            string text = Normalize(caption);
            int columns = Mathf.Max(1, Mathf.FloorToInt(rect.width / (6 * scale)));
            int row = 0;
            foreach (string paragraph in text.Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string remaining = word;
                    if (rect.y + row * 12 * scale >= rect.yMax) return;
                    if (line.Length + word.Length + 1 > columns && line.Length > 0)
                    {
                        Text(line, rect.x, rect.y + row++ * 12 * scale, scale, color);
                        line = "";
                    }
                    while (remaining.Length > columns)
                    {
                        if (rect.y + row * 12 * scale >= rect.yMax) return;
                        Text(remaining.Substring(0, columns), rect.x, rect.y + row++ * 12 * scale, scale, color);
                        remaining = remaining.Substring(columns);
                    }
                    line += (line.Length > 0 ? " " : "") + remaining;
                }
                if (rect.y + row * 12 * scale < rect.yMax)
                    Text(line, rect.x, rect.y + row++ * 12 * scale, scale, color);
            }
        }

        public bool Button(Rect rect, string title, string subtitle = "", bool selected = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition) && GUI.enabled;
            bool pressed = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            Panel(rect, GUI.enabled ? 1 : 0.4f, hover || selected);
            Fill(new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 14),
                hover ? Rust : selected ? Plum : Ink);
            if (selected) Fill(new Rect(rect.x + 12, rect.y + 12, 4, rect.height - 24), Gold);
            if (subtitle.Length == 0) Center(title, rect, 3, hover ? Paper : Gold);
            else
            {
                Center(title, new Rect(rect.x + 24, rect.y + 12, rect.width - 48, 24), 3, Gold);
                Center(subtitle, new Rect(rect.x + 16, rect.y + 42, rect.width - 32, 20), 2, Paper);
            }
            return pressed;
        }

        public string RoomField(Rect rect, string value)
        {
            Panel(rect);
            if (inputStyle == null)
            {
                inputStyle = new GUIStyle(GUIStyle.none) { fontSize = 26,
                    alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12, 12, 6, 6) };
                inputStyle.normal.textColor = Color.clear;
                inputStyle.focused.textColor = Color.clear;
            }
            GUI.color = Color.white;
            GUI.SetNextControlName("ConvoyRoomCode");
            string edited = GUI.TextField(rect, value, 16, inputStyle);
            Center(edited.Length == 0 ? "ENTER ROOM CODE" : edited, rect, 3, edited.Length == 0 ? Copper : Paper);
            if (GUI.GetNameOfFocusedControl() == "ConvoyRoomCode")
                Fill(new Rect(rect.x + 12, rect.yMax - 10, rect.width - 24, 2), Gold);
            return edited;
        }

        public void Backdrop()
        {
            Fill(new Rect(0, 0, 1280, 720), Ink, 0.96f);
            for (int y = 0; y < 720; y += 8) Fill(new Rect(0, y, 1280, 1), Plum, 0.45f);
            Fill(new Rect(32, 32, 1216, 2), Copper);
            for (int i = 0; i < 22; i++)
                Fill(new Rect(40 + i * 24, 674, 12, 4), i % 2 == 0 ? Gold : Rust);
        }

        public void VanBadge(float x, float y, bool gunner)
        {
            Fill(new Rect(x + 8, y + 16, 80, 30), Copper);
            Fill(new Rect(x + 50, y, 28, 20), Gold);
            Fill(new Rect(x + 54, y + 4, 18, 12), Plum);
            Fill(new Rect(x + 14, y + 40, 18, 14), Ink);
            Fill(new Rect(x + 62, y + 40, 18, 14), Ink);
            Fill(new Rect(x + 18, y + 44, 10, 6), Paper);
            Fill(new Rect(x + 66, y + 44, 10, 6), Paper);
            if (gunner)
            {
                Fill(new Rect(x - 8, y + 10, 46, 6), Paper);
                Fill(new Rect(x + 28, y + 8, 8, 18), Gold);
            }
            else
            {
                Fill(new Rect(x + 72, y + 10, 18, 4), Paper);
                Fill(new Rect(x + 86, y + 14, 4, 12), Paper);
            }
        }

        public static string Normalize(string caption)
        {
            StringBuilder result = new StringBuilder(caption.Length);
            foreach (char c in caption.ToUpperInvariant())
                result.Append(c == '\n' ? c : Glyphs.ContainsKey(c) ? c : c == '\u2019' ? '\'' : '?');
            return result.ToString();
        }

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
            [':'] = "0036360000", [' '] = "0000000000", ['?'] = "0201510906",
            ['\''] = "0000030000", ['('] = "00003E4100", [')'] = "00413E0000",
            ['='] = "1414141414", ['%'] = "2313086462", ['_'] = "4040404040",
            ['#'] = "147F147F14", ['['] = "007F414100", [']'] = "0041417F00"
        };

        public static Texture2D Bake(string text)
        {
            int width = text.Length * 6 - 1;
            Color32[] pixels = new Color32[width * 7];
            for (int i = 0; i < text.Length; i++)
            {
                string glyph = Glyphs[text[i]];
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
                name = "Convoy pixel caption", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }

        public void Dispose()
        {
            foreach (Texture2D texture in lettering.Values) DestroyTexture(texture);
            lettering.Clear();
            captions.Clear();
        }
    }
}

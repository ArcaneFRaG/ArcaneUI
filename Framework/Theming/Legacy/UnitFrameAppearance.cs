using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    internal static class UnitFrameAppearance
    {
        internal static readonly string[] TextureNames =
        {
            "Flat",
            "Soft",
            "Striped",
            "Smooth",
            "Gloss",
            "Bevel",
            "Diagonal",
            "Pixel",
            "Horizontal",
            "Vertical",
            "Diamond",
            "Ribbed",
            "Dither"
        };

        internal static readonly string[] FontNames =
        {
            "Default",
            "Arial",
            "Verdana",
            "Tahoma",
            "Georgia",
            "Courier New",
            "Segoe UI",
            "Trebuchet MS",
            "Calibri",
            "Cambria",
            "Garamond",
            "Consolas",
            "Times New Roman",
            "Century Gothic"
        };

        private static readonly Dictionary<string, Sprite> Sprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Font> Fonts =
            new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);

        internal static Sprite GetBarSprite(string textureName)
        {
            if (string.IsNullOrWhiteSpace(textureName))
            {
                textureName = "Flat";
            }

            if (Sprites.TryGetValue(textureName, out Sprite cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture;

            switch (textureName.Trim().ToLowerInvariant())
            {
                case "soft":
                    texture = CreateSoftTexture();
                    break;

                case "striped":
                    texture = CreateStripedTexture();
                    break;

                case "smooth":
                    texture = CreateSmoothTexture();
                    break;

                case "gloss":
                    texture = CreateGlossTexture();
                    break;

                case "bevel":
                    texture = CreateBevelTexture();
                    break;

                case "diagonal":
                    texture = CreateDiagonalTexture();
                    break;

                case "pixel":
                    texture = CreatePixelTexture();
                    break;

                case "horizontal":
                    texture = CreateHorizontalTexture();
                    break;

                case "vertical":
                    texture = CreateVerticalTexture();
                    break;

                case "diamond":
                    texture = CreateDiamondTexture();
                    break;

                case "ribbed":
                    texture = CreateRibbedTexture();
                    break;

                case "dither":
                    texture = CreateDitherTexture();
                    break;

                default:
                    textureName = "Flat";
                    texture = CreateFlatTexture();
                    break;
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode =
                string.Equals(
                    textureName,
                    "Pixel",
                    StringComparison.OrdinalIgnoreCase)
                    ? FilterMode.Point
                    : FilterMode.Bilinear;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            sprite.name = "AGF_" + textureName + "BarTexture";
            Sprites[textureName] = sprite;
            return sprite;
        }

        internal static void ApplyBarTexture(Image image, string textureName)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GetBarSprite(textureName);
            image.type = Image.Type.Tiled;
            image.preserveAspect = false;
        }

        internal static Font ResolveFont(string fontName)
        {
            if (string.IsNullOrWhiteSpace(fontName) ||
                string.Equals(fontName, "Default", StringComparison.OrdinalIgnoreCase))
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (Fonts.TryGetValue(fontName, out Font cached) && cached != null)
            {
                return cached;
            }

            Font created = null;

            try
            {
                created = Font.CreateDynamicFontFromOSFont(fontName, 16);
            }
            catch
            {
                created = null;
            }

            if (created == null)
            {
                created = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            Fonts[fontName] = created;
            return created;
        }

        internal static bool TryParseColor(string value, out Color color)
        {
            color = Color.white;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim();

            if (!normalized.StartsWith("#", StringComparison.Ordinal))
            {
                normalized = "#" + normalized;
            }

            return ColorUtility.TryParseHtmlString(normalized, out color);
        }

        internal static string NormalizeColorHex(string value, string fallback)
        {
            if (!TryParseColor(value, out Color color))
            {
                return fallback;
            }

            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        internal static Color ParseColorOrDefault(string value, Color fallback)
        {
            return TryParseColor(value, out Color color)
                ? color
                : fallback;
        }

        internal static string NextTexture(string current)
        {
            return NextChoice(TextureNames, current);
        }

        internal static string NextFont(string current)
        {
            return NextChoice(FontNames, current);
        }

        private static string NextChoice(string[] choices, string current)
        {
            int index = 0;

            for (int i = 0; i < choices.Length; i++)
            {
                if (string.Equals(choices[i], current, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }

            return choices[(index + 1) % choices.Length];
        }

        private static Texture2D CreateFlatTexture()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color white = Color.white;
            texture.SetPixels(new[] { white, white, white, white });
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateSoftTexture()
        {
            const int width = 8;
            const int height = 8;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                float brightness = Mathf.Lerp(0.72f, 1.08f, 1f - Mathf.Abs(t * 2f - 1f));

                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, new Color(brightness, brightness, brightness, 1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateStripedTexture()
        {
            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool stripe = ((x + y) % 6) < 2;
                    float brightness = stripe ? 0.78f : 1f;
                    texture.SetPixel(x, y, new Color(brightness, brightness, brightness, 1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateSmoothTexture()
        {
            const int width = 8;
            const int height = 16;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                float brightness = Mathf.Lerp(0.70f, 1.05f, t);

                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateGlossTexture()
        {
            const int width = 8;
            const int height = 16;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                float brightness;

                if (t < 0.45f)
                {
                    brightness = Mathf.Lerp(
                        1.16f,
                        0.92f,
                        t / 0.45f);
                }
                else
                {
                    brightness = Mathf.Lerp(
                        0.72f,
                        0.94f,
                        (t - 0.45f) / 0.55f);
                }

                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateBevelTexture()
        {
            const int width = 8;
            const int height = 16;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                float edgeDistance =
                    Mathf.Min(
                        y,
                        height - 1 - y);

                float edgeFactor =
                    Mathf.Clamp01(
                        edgeDistance / 4f);

                float brightness =
                    Mathf.Lerp(
                        0.62f,
                        1.02f,
                        edgeFactor);

                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateDiagonalTexture()
        {
            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int diagonal =
                        (x + y) % 8;

                    float brightness =
                        diagonal < 2
                            ? 0.72f
                            : (diagonal < 4
                                ? 0.86f
                                : 1f);

                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreatePixelTexture()
        {
            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool darkCell =
                        ((x / 2) + (y / 2)) % 2 == 0;

                    float brightness =
                        darkCell
                            ? 0.82f
                            : 1f;

                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateHorizontalTexture()
        {
            const int width = 8;
            const int height = 8;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                float brightness =
                    (y % 4) < 2
                        ? 0.80f
                        : 1f;

                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateVerticalTexture()
        {
            const int width = 8;
            const int height = 8;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float brightness =
                        (x % 4) < 2
                            ? 0.82f
                            : 1f;

                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateDiamondTexture()
        {
            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx =
                        Mathf.Abs(
                            (x % 4) - 2);

                    int dy =
                        Mathf.Abs(
                            (y % 4) - 2);

                    bool ridge =
                        dx + dy == 2;

                    float brightness =
                        ridge
                            ? 0.74f
                            : 1f;

                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateRibbedTexture()
        {
            const int width = 8;
            const int height = 16;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            {
                float t =
                    (float)(y % 4) / 3f;

                float brightness =
                    Mathf.Lerp(
                        0.72f,
                        1.02f,
                        t);

                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }


        private static Texture2D CreateDitherTexture()
        {
            const int size = 8;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool alternate =
                        ((x + y) & 1) == 0;

                    float brightness =
                        alternate
                            ? 0.86f
                            : 1f;

                    texture.SetPixel(
                        x,
                        y,
                        new Color(
                            brightness,
                            brightness,
                            brightness,
                            1f));
                }
            }

            texture.Apply(false, true);
            return texture;
        }
    }
}

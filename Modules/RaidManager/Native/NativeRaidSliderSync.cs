using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcanesGroupFrames
{
    internal static class NativeRaidSliderSync
    {
        internal static void SetRaidDps(
            float value)
        {
            SetNativeSlider(
                value,
                "raid dps",
                "dps");
        }


        internal static void SetRaidSpread(
            float value)
        {
            SetNativeSlider(
                value,
                "spread",
                "raid spread");
        }


        private static void SetNativeSlider(
            float value,
            params string[] labels)
        {
            RaidManager manager =
                GameData.RaidManager;


            if (manager == null)
            {
                return;
            }


            Slider[] sliders =
                manager.GetComponentsInChildren<Slider>(
                    true);


            if (sliders == null ||
                sliders.Length == 0)
            {
                return;
            }


            Slider best =
                null;

            int bestScore =
                int.MinValue;


            for (int i = 0;
                 i < sliders.Length;
                 i++)
            {
                Slider slider =
                    sliders[i];


                if (slider == null)
                {
                    continue;
                }


                int score =
                    ScoreSlider(
                        slider,
                        labels);


                if (score > bestScore)
                {
                    bestScore =
                        score;

                    best =
                        slider;
                }
            }


            if (best == null ||
                bestScore < 20)
            {
                return;
            }


            float nativeValue =
                Mathf.Clamp(
                    value,
                    best.minValue,
                    best.maxValue);


            // Use the normal Slider setter intentionally. Erenshor's own
            // listener must run so the native UI and its backing state stay
            // synchronized with our custom control.
            best.value =
                nativeValue;
        }


        private static int ScoreSlider(
            Slider slider,
            string[] labels)
        {
            int score =
                0;


            Transform current =
                slider.transform;


            int depth =
                0;


            while (current != null &&
                   depth < 8)
            {
                string objectName =
                    Normalize(
                        current.name);


                for (int i = 0;
                     i < labels.Length;
                     i++)
                {
                    string wanted =
                        Normalize(
                            labels[i]);


                    if (objectName.Contains(
                            wanted))
                    {
                        score +=
                            100 -
                            (depth * 8);
                    }


                    if (ContainsDirectText(
                            current,
                            wanted))
                    {
                        score +=
                            80 -
                            (depth * 6);
                    }
                }


                current =
                    current.parent;

                depth++;
            }


            return score;
        }


        private static bool ContainsDirectText(
            Transform transform,
            string wanted)
        {
            TMP_Text[] tmp =
                transform.GetComponents<TMP_Text>();


            for (int i = 0;
                 i < tmp.Length;
                 i++)
            {
                if (tmp[i] != null &&
                    Normalize(
                        tmp[i].text)
                        .Contains(
                            wanted))
                {
                    return true;
                }
            }


            Text[] legacy =
                transform.GetComponents<Text>();


            for (int i = 0;
                 i < legacy.Length;
                 i++)
            {
                if (legacy[i] != null &&
                    Normalize(
                        legacy[i].text)
                        .Contains(
                            wanted))
                {
                    return true;
                }
            }


            return false;
        }


        private static string Normalize(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return "";
            }


            return value
                .Replace(
                    " ",
                    "")
                .Replace(
                    "-",
                    "")
                .Replace(
                    "_",
                    "")
                .Replace(
                    ".",
                    "")
                .Replace(
                    ":",
                    "")
                .Trim()
                .ToLowerInvariant();
        }
    }
}

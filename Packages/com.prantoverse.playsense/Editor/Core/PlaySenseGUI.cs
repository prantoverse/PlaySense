using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PlaySense.EditorTools
{
    /// <summary>Small IMGUI helpers shared by PlaySense windows and module tabs.</summary>
    public static class PlaySenseGUI
    {
        public static readonly Color Accent = new Color(0.3f, 0.7f, 1f, 0.85f);
        public static readonly Color Good = new Color(0.35f, 0.85f, 0.45f);
        public static readonly Color Muted = new Color(0.5f, 0.5f, 0.5f, 0.35f);

        private static GUIStyle s_Title;
        private static GUIStyle s_Section;
        private static GUIStyle s_MetricValue;
        private static GUIStyle s_Wrap;
        private static GUIStyle s_Code;

        public static GUIStyle TitleStyle
        {
            get { return s_Title ?? (s_Title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 }); }
        }

        public static GUIStyle SectionStyle
        {
            get { return s_Section ?? (s_Section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 }); }
        }

        public static GUIStyle MetricValueStyle
        {
            get { return s_MetricValue ?? (s_MetricValue = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 }); }
        }

        public static GUIStyle WrapStyle
        {
            get { return s_Wrap ?? (s_Wrap = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true }); }
        }

        public static GUIStyle CodeStyle
        {
            get
            {
                if (s_Code == null)
                {
                    s_Code = new GUIStyle(EditorStyles.textArea) { wordWrap = false, richText = false };
                    s_Code.font = EditorStyles.miniFont;
                }

                return s_Code;
            }
        }

        public static void Section(string title)
        {
            GUILayout.Space(10f);
            GUILayout.Label(title, SectionStyle);
        }

        public static void Metric(string label, string value)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(110f));
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.Label(value, MetricValueStyle);
            EditorGUILayout.EndVertical();
        }

        /// <summary>A label, a proportional bar and a right-aligned value on one row.</summary>
        public static void BarRow(string label, float fraction, string value, Color color, float labelWidth = 200f)
        {
            Rect row = EditorGUILayout.GetControlRect(false, 18f);
            float valueWidth = 170f;
            float barMax = Mathf.Max(0f, row.width - labelWidth - valueWidth - 8f);

            GUI.Label(new Rect(row.x, row.y, labelWidth - 4f, row.height), label);
            EditorGUI.DrawRect(new Rect(row.x + labelWidth, row.y + 4f, barMax, row.height - 8f), Muted);
            EditorGUI.DrawRect(new Rect(row.x + labelWidth, row.y + 4f, barMax * Mathf.Clamp01(fraction), row.height - 8f), color);
            GUI.Label(new Rect(row.xMax - valueWidth, row.y, valueWidth, row.height), value, EditorStyles.miniLabel);
        }

        /// <summary>Read-only code snippet with a Copy button.</summary>
        public static void CodeBlock(string code)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.SelectableLabel(code, CodeStyle, GUILayout.Height(14f * (code.Split('\n').Length) + 8f));
            if (GUILayout.Button("Copy", GUILayout.Width(50f)))
                EditorGUIUtility.systemCopyBuffer = code;
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Simple line chart. Returns the rect used.</summary>
        public static Rect LineChart(IList<float> xs, IList<float> ys, float height, string yLabel, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.15f));

            if (xs == null || ys == null || xs.Count < 2 || xs.Count != ys.Count)
            {
                GUI.Label(rect, "Not enough data", EditorStyles.centeredGreyMiniLabel);
                return rect;
            }

            float xMin = xs[0], xMax = xs[xs.Count - 1];
            float yMax = 0f;
            for (int i = 0; i < ys.Count; i++)
                yMax = Mathf.Max(yMax, ys[i]);

            yMax = Mathf.Max(yMax * 1.1f, 0.0001f);
            float span = Mathf.Max(xMax - xMin, 0.0001f);

            Rect plot = new Rect(rect.x + 36f, rect.y + 6f, rect.width - 44f, rect.height - 22f);

            GUI.Label(new Rect(rect.x + 2f, plot.y - 4f, 34f, 16f), yMax.ToString("0"), EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 2f, plot.yMax - 10f, 34f, 16f), "0", EditorStyles.miniLabel);
            GUI.Label(new Rect(plot.x, plot.yMax + 2f, 200f, 16f), yLabel + " over " + span.ToString("0") + "s", EditorStyles.miniLabel);

            if (Event.current.type != EventType.Repaint)
                return rect;

            Vector3[] points = new Vector3[xs.Count];
            for (int i = 0; i < xs.Count; i++)
            {
                points[i] = new Vector3(
                    plot.x + (xs[i] - xMin) / span * plot.width,
                    plot.yMax - ys[i] / yMax * plot.height);
            }

            Handles.BeginGUI();
            Color previous = Handles.color;
            Handles.color = color;
            Handles.DrawAAPolyLine(2f, points);
            Handles.color = previous;
            Handles.EndGUI();

            return rect;
        }

        public static string FormatTime(float seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return span.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds)
                : string.Format("{0}:{1:00}", span.Minutes, span.Seconds);
        }

        public static string Percent(float value)
        {
            return value.ToString("0") + "%";
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using PlaySense.Analysis;
using PlaySense.Data.Models;

namespace PlaySense.EditorTools
{
    public static class PlaySenseMovementChart
    {
        private const float ChartHeight = 220f;
        private const float ChartPadding = 35f;

        // =========================================================
        // PUBLIC DRAW
        // =========================================================

        public static void Draw(SessionData session)
        {
            if (session == null)
                return;

            GUILayout.Label(
                "Movement Analysis",
                EditorStyles.boldLabel);

            GUILayout.Space(5);

            List<FrameData> frames =
                SessionQueries.GetPrimaryFrames(session);

            if (frames.Count < 2)
            {
                EditorGUILayout.HelpBox(
                    "Not enough movement data to generate a chart.",
                    MessageType.Info);

                return;
            }

            List<float> timestamps =
                new List<float>();

            List<float> speeds =
                new List<float>();

            float peakSpeed = 0f;

            for (int i = 1;
                 i < frames.Count;
                 i++)
            {
                FrameData previous =
                    frames[i - 1];

                FrameData current =
                    frames[i];

                if (previous == null ||
                    current == null)
                {
                    continue;
                }

                float deltaTime =
                    current.Timestamp -
                    previous.Timestamp;

                if (deltaTime <= 0f)
                    continue;

                float distance =
                    Vector3.Distance(
                        previous.Position,
                        current.Position);

                float speed =
                    distance / deltaTime;

                timestamps.Add(
                    current.Timestamp);

                speeds.Add(
                    speed);

                peakSpeed =
                    Mathf.Max(
                        peakSpeed,
                        speed);
            }

            if (speeds.Count < 2)
            {
                EditorGUILayout.HelpBox(
                    "Not enough valid movement samples to generate a chart.",
                    MessageType.Info);

                return;
            }

            // -----------------------------------------------------
            // SUMMARY
            // -----------------------------------------------------

            GUILayout.BeginHorizontal();

            DrawSummaryBox(
                "Samples",
                speeds.Count.ToString());

            DrawSummaryBox(
                "Peak Speed",
                $"{peakSpeed:F2} m/s");

            DrawSummaryBox(
                "Duration",
                $"{session.Duration:F2} s");

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // -----------------------------------------------------
            // CHART
            // -----------------------------------------------------

            Rect chartRect =
                GUILayoutUtility.GetRect(
                    0f,
                    ChartHeight,
                    GUILayout.ExpandWidth(true));

            DrawChartBackground(
                chartRect);

            DrawAxes(
                chartRect);

            DrawSpeedGraph(
                chartRect,
                timestamps,
                speeds,
                peakSpeed);

            DrawLabels(
                chartRect,
                timestamps,
                peakSpeed);

            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Movement speed is calculated from consecutive recorded frame positions and timestamps.",
                MessageType.None);
        }

        // =========================================================
        // SUMMARY BOX
        // =========================================================

        private static void DrawSummaryBox(
            string label,
            string value)
        {
            GUILayout.BeginVertical(
                "box",
                GUILayout.MinWidth(120f));

            GUILayout.Label(
                label,
                EditorStyles.miniLabel);

            GUIStyle valueStyle =
                new GUIStyle(
                    EditorStyles.boldLabel);

            valueStyle.fontSize = 14;

            GUILayout.Label(
                value,
                valueStyle);

            GUILayout.EndVertical();
        }

        // =========================================================
        // BACKGROUND
        // =========================================================

        private static void DrawChartBackground(
            Rect rect)
        {
            EditorGUI.DrawRect(
                rect,
                new Color(
                    0.12f,
                    0.12f,
                    0.12f,
                    1f));
        }

        // =========================================================
        // AXES
        // =========================================================

        private static void DrawAxes(
            Rect rect)
        {
            float left =
                rect.x +
                ChartPadding;

            float bottom =
                rect.yMax -
                ChartPadding;

            float right =
                rect.xMax -
                10f;

            float top =
                rect.y +
                15f;

            Handles.BeginGUI();

            Handles.color =
                new Color(
                    0.45f,
                    0.45f,
                    0.45f,
                    1f);

            Handles.DrawLine(
                new Vector3(left, bottom),
                new Vector3(right, bottom));

            Handles.DrawLine(
                new Vector3(left, bottom),
                new Vector3(left, top));

            Handles.EndGUI();
        }

        // =========================================================
        // GRAPH
        // =========================================================

        private static void DrawSpeedGraph(
            Rect rect,
            List<float> timestamps,
            List<float> speeds,
            float peakSpeed)
        {
            if (timestamps.Count < 2)
                return;

            float minTime =
                timestamps[0];

            float maxTime =
                timestamps[
                    timestamps.Count - 1];

            if (maxTime <= minTime)
                maxTime =
                    minTime + 1f;

            if (peakSpeed <= 0f)
                peakSpeed = 1f;

            float left =
                rect.x +
                ChartPadding;

            float right =
                rect.xMax -
                10f;

            float top =
                rect.y +
                15f;

            float bottom =
                rect.yMax -
                ChartPadding;

            Handles.BeginGUI();

            // -----------------------------------------------------
            // GRID
            // -----------------------------------------------------

            Handles.color =
                new Color(
                    0.25f,
                    0.25f,
                    0.25f,
                    1f);

            for (int i = 1; i <= 4; i++)
            {
                float normalized =
                    i / 5f;

                float y =
                    Mathf.Lerp(
                        bottom,
                        top,
                        normalized);

                Handles.DrawLine(
                    new Vector3(left, y),
                    new Vector3(right, y));
            }

            // -----------------------------------------------------
            // GRAPH LINE
            // -----------------------------------------------------

            Handles.color =
                new Color(
                    0.2f,
                    0.8f,
                    1f,
                    1f);

            for (int i = 1;
                 i < timestamps.Count;
                 i++)
            {
                float x1 =
                    Mathf.InverseLerp(
                        minTime,
                        maxTime,
                        timestamps[i - 1]);

                float x2 =
                    Mathf.InverseLerp(
                        minTime,
                        maxTime,
                        timestamps[i]);

                float y1 =
                    Mathf.InverseLerp(
                        0f,
                        peakSpeed,
                        speeds[i - 1]);

                float y2 =
                    Mathf.InverseLerp(
                        0f,
                        peakSpeed,
                        speeds[i]);

                Vector3 p1 =
                    new Vector3(
                        Mathf.Lerp(
                            left,
                            right,
                            x1),
                        Mathf.Lerp(
                            bottom,
                            top,
                            y1));

                Vector3 p2 =
                    new Vector3(
                        Mathf.Lerp(
                            left,
                            right,
                            x2),
                        Mathf.Lerp(
                            bottom,
                            top,
                            y2));

                Handles.DrawLine(
                    p1,
                    p2);
            }

            Handles.EndGUI();
        }

        // =========================================================
        // LABELS
        // =========================================================

        private static void DrawLabels(
            Rect rect,
            List<float> timestamps,
            float peakSpeed)
        {
            float left =
                rect.x +
                ChartPadding;

            float bottom =
                rect.yMax -
                ChartPadding;

            GUIStyle labelStyle =
                new GUIStyle(
                    EditorStyles.miniLabel);

            labelStyle.alignment =
                TextAnchor.MiddleCenter;

            // -----------------------------------------------------
            // X AXIS
            // -----------------------------------------------------

            if (timestamps.Count > 0)
            {
                float firstTime =
                    timestamps[0];

                float lastTime =
                    timestamps[
                        timestamps.Count - 1];

                GUI.Label(
                    new Rect(
                        left - 10f,
                        bottom + 5f,
                        70f,
                        20f),
                    $"{firstTime:F1}s",
                    labelStyle);

                GUI.Label(
                    new Rect(
                        rect.xMax - 70f,
                        bottom + 5f,
                        60f,
                        20f),
                    $"{lastTime:F1}s",
                    labelStyle);
            }

            // -----------------------------------------------------
            // Y AXIS
            // -----------------------------------------------------

            labelStyle.alignment =
                TextAnchor.MiddleRight;

            GUI.Label(
                new Rect(
                    rect.x + 2f,
                    rect.y + 5f,
                    ChartPadding - 7f,
                    20f),
                $"{peakSpeed:F1}",
                labelStyle);

            GUI.Label(
                new Rect(
                    rect.x + 2f,
                    bottom - 10f,
                    ChartPadding - 7f,
                    20f),
                "0",
                labelStyle);

            // -----------------------------------------------------
            // AXIS TITLES
            // -----------------------------------------------------

            labelStyle.alignment =
                TextAnchor.MiddleLeft;

            GUI.Label(
                new Rect(
                    rect.x + 5f,
                    rect.y + 2f,
                    100f,
                    20f),
                "Speed (m/s)",
                labelStyle);

            labelStyle.alignment =
                TextAnchor.MiddleRight;

            GUI.Label(
                new Rect(
                    rect.xMax - 90f,
                    rect.yMax - 18f,
                    80f,
                    20f),
                "Time",
                labelStyle);
        }
    }
}

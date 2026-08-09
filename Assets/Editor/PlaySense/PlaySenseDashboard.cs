using System.IO;
using UnityEditor;
using UnityEngine;
using PlaySense.Data.Models;
using PlaySense.Export;

public class PlaySenseDashboard : EditorWindow
{
    private Vector2 mainScroll;
    private Vector2 sessionScroll;
    private Vector2 detailsScroll;

    private SessionData selectedSession;
    private string selectedSessionPath;
    private string selectedPdfPath;

    [MenuItem("Window/PlaySense/Dashboard")]
    public static void ShowWindow()
    {
        PlaySenseDashboard window =
            GetWindow<PlaySenseDashboard>("PlaySense");

        window.minSize =
            new Vector2(700f, 500f);

        window.Show();
    }

    private void OnGUI()
    {
        mainScroll =
            EditorGUILayout.BeginScrollView(
                mainScroll,
                true,
                true);

        DrawHeader();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal(
            GUILayout.MinWidth(850f));

        DrawSessionList();

        GUILayout.Space(10);

        DrawSessionDetails();

        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // HEADER
    // =========================================================

    private void DrawHeader()
    {
        GUILayout.Space(8);

        GUIStyle titleStyle =
            new GUIStyle(GUI.skin.label);

        titleStyle.fontSize = 20;
        titleStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label(
            "PlaySense",
            titleStyle);

        GUIStyle subtitleStyle =
            new GUIStyle(GUI.skin.label);

        subtitleStyle.fontSize = 11;

        GUILayout.Label(
            "Gameplay analytics and playtest session control.",
            subtitleStyle);

        GUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "PlaySense records gameplay sessions and transforms player behaviour into designer-friendly reports.",
            MessageType.Info);

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button(
            "Refresh",
            GUILayout.Width(90),
            GUILayout.Height(26)))
        {
            Repaint();
        }

        GUILayout.EndHorizontal();
    }

    // =========================================================
    // SESSION LIST
    // =========================================================

    private void DrawSessionList()
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(390f));

        GUIStyle sectionStyle =
            new GUIStyle(GUI.skin.label);

        sectionStyle.fontSize = 14;
        sectionStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label(
            "Sessions",
            sectionStyle);

        string[] sessionFiles =
            SessionStorage.GetSessionFiles();

        GUILayout.Label(
            $"{sessionFiles.Length} recorded session(s)",
            EditorStyles.miniLabel);

        GUILayout.Space(8);

        if (sessionFiles.Length == 0)
        {
            GUILayout.FlexibleSpace();

            GUILayout.Label(
                "No sessions recorded yet.",
                EditorStyles.centeredGreyMiniLabel);

            GUILayout.FlexibleSpace();

            GUILayout.EndVertical();

            return;
        }

        sessionScroll =
            EditorGUILayout.BeginScrollView(
                sessionScroll,
                false,
                true,
                GUILayout.MinHeight(300f));

        foreach (string path in sessionFiles)
        {
            DrawSessionCard(path);
        }

        EditorGUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    private void DrawSessionCard(string path)
    {
        SessionData session =
            SessionStorage.Load(path);

        if (session == null)
            return;

        bool selected =
            selectedSessionPath == path;

        GUILayout.BeginVertical("box");

        GUIStyle titleStyle =
            new GUIStyle(GUI.skin.label);

        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.fontSize = 12;

        GUILayout.Label(
            Path.GetFileNameWithoutExtension(path),
            titleStyle);

        GUILayout.Space(4);

        DrawCompactMetric(
            "Scene",
            string.IsNullOrEmpty(session.SceneName)
                ? "Unknown"
                : session.SceneName);

        DrawCompactMetric(
            "Duration",
            $"{session.Metrics.Duration:F2} sec");

        DrawCompactMetric(
            "Distance",
            $"{session.Metrics.TotalDistance:F2} m");

        DrawCompactMetric(
            "Frames",
            session.Metrics.FrameCount.ToString());

        DrawCompactMetric(
            "Events",
            session.Events != null
                ? session.Events.Count.ToString()
                : "0");

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();

        if (selected)
        {
            GUI.enabled = false;
            GUILayout.Button("Selected");
            GUI.enabled = true;
        }
        else
        {
            if (GUILayout.Button("Select"))
            {
                SelectSession(
                    path,
                    session);
            }
        }

        if (GUILayout.Button("Delete"))
        {
            DeleteSession(path);
        }

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.Space(7);
    }

    private void DrawCompactMetric(
        string label,
        string value)
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label(
            label,
            GUILayout.Width(75));

        GUILayout.Label(value);

        GUILayout.EndHorizontal();
    }

    // =========================================================
    // DETAILS
    // =========================================================

    private void DrawSessionDetails()
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.Width(520f));

        GUIStyle sectionStyle =
            new GUIStyle(GUI.skin.label);

        sectionStyle.fontSize = 14;
        sectionStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label(
            "Session Details",
            sectionStyle);

        GUILayout.Space(8);

        detailsScroll =
            EditorGUILayout.BeginScrollView(
                detailsScroll,
                false,
                true,
                GUILayout.MinHeight(420f));

        if (selectedSession == null)
        {
            GUILayout.FlexibleSpace();

            GUILayout.Label(
                "No session selected",
                EditorStyles.centeredGreyMiniLabel);

            GUILayout.Label(
                "Select a recorded session to inspect its data.",
                EditorStyles.centeredGreyMiniLabel);

            GUILayout.FlexibleSpace();
        }
        else
        {
            DrawSelectedSession();
        }

        EditorGUILayout.EndScrollView();

        GUILayout.EndVertical();
    }

    private void DrawSelectedSession()
    {
        GUIStyle titleStyle =
            new GUIStyle(GUI.skin.label);

        titleStyle.fontSize = 12;
        titleStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label(
            Path.GetFileNameWithoutExtension(
                selectedSessionPath),
            titleStyle);

        GUILayout.Space(10);

        DrawMetricGrid();

        GUILayout.Space(15);

        GUIStyle sectionStyle =
            new GUIStyle(GUI.skin.label);

        sectionStyle.fontSize = 14;
        sectionStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label(
            "Session Information",
            sectionStyle);

        GUILayout.Space(5);

        EditorGUILayout.LabelField(
            "Scene",
            string.IsNullOrEmpty(
                selectedSession.SceneName)
                ? "Unknown"
                : selectedSession.SceneName);

        EditorGUILayout.LabelField(
            "Session ID",
            string.IsNullOrEmpty(
                selectedSession.SessionId)
                ? "Unknown"
                : selectedSession.SessionId);

        GUILayout.Space(15);

        GUILayout.Label(
            "Gameplay Events",
            sectionStyle);

        DrawEventPreview();

        GUILayout.Space(20);

        DrawActions();
    }

    // =========================================================
    // METRICS
    // =========================================================

    private void DrawMetricGrid()
    {
        GUILayout.BeginHorizontal();

        DrawMetricBox(
            "Duration",
            $"{selectedSession.Metrics.Duration:F2}s");

        DrawMetricBox(
            "Distance",
            $"{selectedSession.Metrics.TotalDistance:F2}m");

        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();

        DrawMetricBox(
            "Average Speed",
            $"{selectedSession.Metrics.AverageSpeed:F2}m/s");

        DrawMetricBox(
            "Frames",
            selectedSession.Metrics.FrameCount.ToString());

        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();

        DrawMetricBox(
            "Events",
            selectedSession.Events != null
                ? selectedSession.Events.Count.ToString()
                : "0");

        DrawMetricBox(
            "Status",
            "Recorded");

        GUILayout.EndHorizontal();
    }

    private void DrawMetricBox(
        string label,
        string value)
    {
        GUILayout.BeginVertical(
            "box",
            GUILayout.MinWidth(180f));

        GUILayout.Label(
            label,
            EditorStyles.miniLabel);

        GUIStyle valueStyle =
            new GUIStyle(GUI.skin.label);

        valueStyle.fontSize = 16;
        valueStyle.fontStyle = FontStyle.Bold;

        GUILayout.Label(
            value,
            valueStyle);

        GUILayout.EndVertical();
    }

    // =========================================================
    // EVENTS
    // =========================================================

    private void DrawEventPreview()
    {
        if (selectedSession.Events == null ||
            selectedSession.Events.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No gameplay events were recorded in this session.",
                MessageType.None);

            return;
        }

        for (int i = 0;
             i < selectedSession.Events.Count;
             i++)
        {
            GameEventData gameEvent =
                selectedSession.Events[i];

            string category =
                string.IsNullOrEmpty(gameEvent.Category)
                    ? "Event"
                    : gameEvent.Category;

            string action =
                string.IsNullOrEmpty(gameEvent.Action)
                    ? "Unknown action"
                    : gameEvent.Action;

            string target =
                string.IsNullOrEmpty(gameEvent.Target)
                    ? ""
                    : $" — {gameEvent.Target}";

            GUILayout.BeginVertical("box");

            GUILayout.Label(
                $"{gameEvent.Timestamp:F2}s",
                EditorStyles.boldLabel);

            GUILayout.Label(
                $"{category}: {action}{target}");

            GUILayout.EndVertical();

            GUILayout.Space(4);
        }
    }

    // =========================================================
    // ACTIONS
    // =========================================================

    private void DrawActions()
    {
        GUILayout.BeginHorizontal();

        GUI.enabled =
            selectedSession != null;

        if (GUILayout.Button(
            "Generate PDF Report",
            GUILayout.Height(34)))
        {
            GenerateReport();
        }

        GUI.enabled =
            !string.IsNullOrEmpty(selectedPdfPath) &&
            File.Exists(selectedPdfPath);

        if (GUILayout.Button(
            "Reveal PDF",
            GUILayout.Width(120f),
            GUILayout.Height(34)))
        {
            RevealPdf();
        }

        GUI.enabled = true;

        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        if (!string.IsNullOrEmpty(selectedPdfPath) &&
            File.Exists(selectedPdfPath))
        {
            GUILayout.Label(
                $"PDF ready: {Path.GetFileName(selectedPdfPath)}",
                EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label(
                "Generate a PDF report to create the designer-facing report.",
                EditorStyles.miniLabel);
        }
    }

    // =========================================================
    // SELECT
    // =========================================================

    private void SelectSession(
        string path,
        SessionData session)
    {
        selectedSessionPath = path;
        selectedSession = session;

        selectedPdfPath =
            Path.ChangeExtension(
                path,
                ".pdf");

        detailsScroll = Vector2.zero;

        Repaint();
    }

    // =========================================================
    // DELETE
    // =========================================================

    private void DeleteSession(string path)
    {
        bool confirmed =
            EditorUtility.DisplayDialog(
                "Delete PlaySense Session",
                "Delete this session and its generated PDF report?",
                "Delete",
                "Cancel");

        if (!confirmed)
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);

            string pdfPath =
                Path.ChangeExtension(
                    path,
                    ".pdf");

            if (File.Exists(pdfPath))
                File.Delete(pdfPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                $"PlaySense could not delete the session:\n{exception}");

            return;
        }

        if (selectedSessionPath == path)
        {
            selectedSessionPath = null;
            selectedSession = null;
            selectedPdfPath = null;
        }

        Repaint();
    }

    // =========================================================
    // PDF
    // =========================================================

    private void GenerateReport()
    {
        if (selectedSession == null)
            return;

        string outputPath =
            Path.ChangeExtension(
                selectedSessionPath,
                ".pdf");

        string generatedPath =
            PlaySensePDFExporter.Export(
                selectedSession,
                outputPath);

        if (string.IsNullOrEmpty(generatedPath))
            return;

        selectedPdfPath =
            generatedPath;

        AssetDatabase.Refresh();

        bool openNow =
            EditorUtility.DisplayDialog(
                "PlaySense Report Generated",
                $"PDF report created successfully.\n\n{generatedPath}",
                "Open PDF",
                "Close");

        if (openNow)
        {
            OpenPdf(generatedPath);
        }

        Repaint();
    }

    private void OpenPdf(string path)
    {
        if (!File.Exists(path))
            return;

        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
    }

    private void RevealPdf()
    {
        if (string.IsNullOrEmpty(selectedPdfPath))
            return;

        if (!File.Exists(selectedPdfPath))
        {
            EditorUtility.DisplayDialog(
                "PDF Not Found",
                "Generate the PDF report first.",
                "OK");

            return;
        }

        EditorUtility.RevealInFinder(
            Path.GetFullPath(selectedPdfPath));
    }
}
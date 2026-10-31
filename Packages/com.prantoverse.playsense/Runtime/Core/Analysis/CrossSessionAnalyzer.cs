using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PlaySense.Data.Models;

namespace PlaySense.Analysis
{
    /// <summary>Totals across many sessions: how many players did what.</summary>
    [Serializable]
    public class CrossSessionReport
    {
        public int SessionCount;
        public float TotalDuration;
        public float AverageDuration;
        public float AverageDistance;
        public int TotalEvents;

        /// <summary>Scene filter used ("" = all scenes).</summary>
        public string Scene = string.Empty;

        /// <summary>Scenes visited, with how many sessions reached each.</summary>
        public List<CountStat> Scenes = new List<CountStat>();

        /// <summary>Event categories, most widespread first.</summary>
        public List<CountStat> Categories = new List<CountStat>();

        /// <summary>One row per Category + Action, most widespread first.</summary>
        public List<EventStat> Events = new List<EventStat>();

        /// <summary>Spreadsheet-friendly export of the event table, one row per action and per metadata value.</summary>
        public string ToCsv()
        {
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Category,Action,Metadata Key,Metadata Value,Sessions,% of Sessions,Count,% of Action's Events,Avg per Session");

            foreach (EventStat stat in Events)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    Csv(stat.Category), Csv(stat.Action), string.Empty, string.Empty,
                    stat.SessionsWith.ToString(CultureInfo.InvariantCulture),
                    Pct(stat.PercentOfSessions),
                    stat.TotalCount.ToString(CultureInfo.InvariantCulture),
                    string.Empty,
                    stat.AveragePerSession.ToString("0.##", CultureInfo.InvariantCulture)
                }));

                foreach (MetadataStat meta in stat.Metadata)
                {
                    foreach (ValueStat value in meta.Values)
                    {
                        csv.AppendLine(string.Join(",", new[]
                        {
                            Csv(stat.Category), Csv(stat.Action), Csv(meta.Key), Csv(value.Value),
                            value.SessionsWith.ToString(CultureInfo.InvariantCulture),
                            Pct(value.PercentOfSessions),
                            value.Count.ToString(CultureInfo.InvariantCulture),
                            Pct(value.PercentOfEvents),
                            string.Empty
                        }));
                    }
                }
            }

            return csv.ToString();
        }

        private static string Csv(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\n' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }

        private static string Pct(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
    }

    [Serializable]
    public class CountStat
    {
        public string Name = string.Empty;
        public int SessionsWith;
        public float PercentOfSessions;
        public int TotalCount;
    }

    [Serializable]
    public class EventStat
    {
        public string Category = string.Empty;
        public string Action = string.Empty;

        /// <summary>Sessions with at least one of these events.</summary>
        public int SessionsWith;
        public float PercentOfSessions;

        public int TotalCount;

        /// <summary>Total divided by all sessions analysed (including ones without the event).</summary>
        public float AveragePerSession;

        /// <summary>Breakdown by metadata key, e.g. method: gun 70%, ability 30%.</summary>
        public List<MetadataStat> Metadata = new List<MetadataStat>();

        public string Label
        {
            get { return Category + " / " + Action; }
        }
    }

    [Serializable]
    public class MetadataStat
    {
        public string Key = string.Empty;
        public List<ValueStat> Values = new List<ValueStat>();
    }

    [Serializable]
    public class ValueStat
    {
        public string Value = string.Empty;
        public int Count;
        public int SessionsWith;

        /// <summary>Share of this action's events that had this value.</summary>
        public float PercentOfEvents;

        public float PercentOfSessions;
    }

    /// <summary>
    /// Answers "how many players did X" across a set of sessions, e.g.
    /// "14 of 20 players used an ability; 70% of kills were gunfire".
    /// </summary>
    public class CrossSessionAnalyzer
    {
        /// <param name="sessions">Sessions to include.</param>
        /// <param name="sceneFilter">Only count events recorded in this scene. Null or empty = all scenes.</param>
        public CrossSessionReport Analyze(IList<SessionData> sessions, string sceneFilter = null)
        {
            CrossSessionReport report = new CrossSessionReport { Scene = sceneFilter ?? string.Empty };
            if (sessions == null || sessions.Count == 0)
                return report;

            bool filter = !string.IsNullOrEmpty(sceneFilter);

            Dictionary<string, EventStat> events = new Dictionary<string, EventStat>();
            Dictionary<string, CountStat> categories = new Dictionary<string, CountStat>();
            Dictionary<string, CountStat> scenes = new Dictionary<string, CountStat>();

            // Per-session "seen" sets so each session counts once per row.
            HashSet<string> seenEvents = new HashSet<string>();
            HashSet<string> seenCategories = new HashSet<string>();
            HashSet<string> seenValues = new HashSet<string>();
            HashSet<string> seenScenes = new HashSet<string>();

            float totalDistance = 0f;

            foreach (SessionData session in sessions)
            {
                if (session == null)
                    continue;

                session.EnsureValid();
                report.SessionCount++;
                report.TotalDuration += session.Duration;
                totalDistance += session.Metrics.TotalDistance;

                seenEvents.Clear();
                seenCategories.Clear();
                seenValues.Clear();
                seenScenes.Clear();

                foreach (string sceneName in session.GetSceneNames())
                {
                    CountStat scene = Get(scenes, sceneName);
                    scene.TotalCount++;
                    if (seenScenes.Add(sceneName))
                        scene.SessionsWith++;
                }

                foreach (GameEventData gameEvent in session.Events)
                {
                    if (gameEvent == null)
                        continue;

                    if (filter && session.GetSceneName(gameEvent.Scene) != sceneFilter)
                        continue;

                    report.TotalEvents++;

                    string category = string.IsNullOrEmpty(gameEvent.Category) ? "Uncategorised" : gameEvent.Category;
                    string action = string.IsNullOrEmpty(gameEvent.Action) ? "(no action)" : gameEvent.Action;
                    string key = category + "\u001f" + action;

                    CountStat categoryStat = Get(categories, category);
                    categoryStat.TotalCount++;
                    if (seenCategories.Add(category))
                        categoryStat.SessionsWith++;

                    EventStat stat;
                    if (!events.TryGetValue(key, out stat))
                    {
                        stat = new EventStat { Category = category, Action = action };
                        events.Add(key, stat);
                    }

                    stat.TotalCount++;
                    if (seenEvents.Add(key))
                        stat.SessionsWith++;

                    if (gameEvent.Metadata == null)
                        continue;

                    foreach (EventParameter parameter in gameEvent.Metadata)
                    {
                        if (parameter == null || string.IsNullOrEmpty(parameter.Key))
                            continue;

                        MetadataStat meta = stat.Metadata.Find(m => m.Key == parameter.Key);
                        if (meta == null)
                        {
                            meta = new MetadataStat { Key = parameter.Key };
                            stat.Metadata.Add(meta);
                        }

                        string valueName = parameter.Value ?? string.Empty;
                        ValueStat value = meta.Values.Find(v => v.Value == valueName);
                        if (value == null)
                        {
                            value = new ValueStat { Value = valueName };
                            meta.Values.Add(value);
                        }

                        value.Count++;
                        if (seenValues.Add(key + "\u001f" + parameter.Key + "\u001f" + valueName))
                            value.SessionsWith++;
                    }
                }
            }

            if (report.SessionCount == 0)
                return report;

            float sessionCount = report.SessionCount;
            report.AverageDuration = report.TotalDuration / sessionCount;
            report.AverageDistance = totalDistance / sessionCount;

            foreach (EventStat stat in events.Values)
            {
                stat.PercentOfSessions = stat.SessionsWith / sessionCount * 100f;
                stat.AveragePerSession = stat.TotalCount / sessionCount;

                foreach (MetadataStat meta in stat.Metadata)
                {
                    foreach (ValueStat value in meta.Values)
                    {
                        value.PercentOfEvents = stat.TotalCount > 0 ? value.Count / (float)stat.TotalCount * 100f : 0f;
                        value.PercentOfSessions = value.SessionsWith / sessionCount * 100f;
                    }

                    meta.Values.Sort((a, b) => b.Count.CompareTo(a.Count));
                }

                report.Events.Add(stat);
            }

            report.Events.Sort((a, b) =>
            {
                int bySessions = b.SessionsWith.CompareTo(a.SessionsWith);
                return bySessions != 0 ? bySessions : b.TotalCount.CompareTo(a.TotalCount);
            });

            report.Categories = Finish(categories, sessionCount);
            report.Scenes = Finish(scenes, sessionCount);
            return report;
        }

        private static CountStat Get(Dictionary<string, CountStat> table, string name)
        {
            CountStat stat;
            if (!table.TryGetValue(name, out stat))
            {
                stat = new CountStat { Name = name };
                table.Add(name, stat);
            }

            return stat;
        }

        private static List<CountStat> Finish(Dictionary<string, CountStat> table, float sessionCount)
        {
            List<CountStat> list = new List<CountStat>(table.Values);
            foreach (CountStat stat in list)
                stat.PercentOfSessions = stat.SessionsWith / sessionCount * 100f;

            list.Sort((a, b) => b.SessionsWith != a.SessionsWith
                ? b.SessionsWith.CompareTo(a.SessionsWith)
                : b.TotalCount.CompareTo(a.TotalCount));
            return list;
        }
    }
}

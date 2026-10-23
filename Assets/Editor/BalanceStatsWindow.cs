using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Design stats over every recorded run (Tools/Dealbreak/Balance/Stats). The stats file is updated as each
/// run ends, so the window refreshes itself whenever a run finishes.
/// </summary>
public sealed class BalanceStatsWindow : EditorWindow
{
    BalanceStatsStore _store;
    BalanceStatsReport _report;
    BalanceNames _names;
    BalanceStatsFilter _filter = BalanceStatsFilter.Default;
    int _tab;
    int _sortColumn = -1;
    bool _sortDescending = true;
    Vector2 _scroll;
    string[] _versions = { "All versions" };
    int _version;
    DateTime _loadedWrite;

    [MenuItem("Tools/Dealbreak/Balance/Stats")]
    static void Open() => GetWindow<BalanceStatsWindow>("Balance Stats").Show();

    void OnEnable()
    {
        GameplayBalanceTelemetry.StatsUpdated += OnStatsUpdated;
        Reload();
    }

    void OnDisable() => GameplayBalanceTelemetry.StatsUpdated -= OnStatsUpdated;

    void OnFocus()
    {
        if (StatsFileWrite() != _loadedWrite) Reload();
    }

    void OnStatsUpdated()
    {
        Reload();
        Repaint();
    }

    static string StatsPath => Path.Combine(GameplayBalanceTelemetry.ExportDirectory, BalanceStatsStore.FileName);
    static DateTime StatsFileWrite() => File.Exists(StatsPath) ? File.GetLastWriteTimeUtc(StatsPath) : DateTime.MinValue;

    void Reload()
    {
        _loadedWrite = StatsFileWrite();
        _store = BalanceStatsStore.Load(GameplayBalanceTelemetry.ExportDirectory);
        _names = LoadNames();
        _versions = new[] { "All versions" }.Concat(_store.runs.Select(run => run.gameVersion)
            .Where(value => !string.IsNullOrEmpty(value)).Distinct().OrderBy(value => value, StringComparer.Ordinal)).ToArray();
        _version = Mathf.Clamp(_version, 0, _versions.Length - 1);
        Rebuild();
    }

    void Rebuild()
    {
        _filter.GameVersion = _version > 0 ? _versions[_version] : null;
        _report = BalanceStatsReport.Build(_store.runs, _filter, _names);
        _tab = Mathf.Clamp(_tab, 0, _report.Tables.Count - 1);
    }

    static BalanceNames LoadNames()
    {
        static IEnumerable<T> Assets<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null);
        return BalanceNames.FromCatalogs(Assets<GearData>(), Assets<ConsumableData>());
    }

    void OnGUI()
    {
        if (_report == null) Reload();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Reload();
            if (GUILayout.Button(new GUIContent("Rebuild", "Re-read every run journal in the folder, older ones included."),
                    EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _store = BalanceStatsStore.Rebuild(GameplayBalanceTelemetry.ExportDirectory);
                _store.Save(GameplayBalanceTelemetry.ExportDirectory);
                Reload();
            }
            if (GUILayout.Button("Export CSV…", EditorStyles.toolbarButton, GUILayout.Width(80))) ExportCsv();
            if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(80)))
                EditorUtility.RevealInFinder(GameplayBalanceTelemetry.ExportDirectory);
            GUILayout.Space(8);
            EditorGUI.BeginChangeCheck();
            _filter.IncludeDev = GUILayout.Toggle(_filter.IncludeDev, "Dev runs", EditorStyles.toolbarButton, GUILayout.Width(64));
            _filter.IncludeInvalid = GUILayout.Toggle(_filter.IncludeInvalid, "Invalid runs", EditorStyles.toolbarButton, GUILayout.Width(80));
            _version = EditorGUILayout.Popup(_version, _versions, EditorStyles.toolbarPopup, GUILayout.Width(110));
            if (EditorGUI.EndChangeCheck()) Rebuild();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_report.RunsConsidered} runs, {_report.RunsFinished} finished", EditorStyles.miniLabel);
            bool recording = GUILayout.Toggle(GameplayBalanceTelemetry.Enabled, "Recording", EditorStyles.toolbarButton, GUILayout.Width(72));
            if (recording != GameplayBalanceTelemetry.Enabled) EditorApplication.ExecuteMenuItem("Tools/Dealbreak/Balance/Record New Runs");
        }

        if (_report.Tables.Count == 0) return;
        EditorGUI.BeginChangeCheck();
        _tab = GUILayout.Toolbar(_tab, _report.Tables.Select(table => table.Name).ToArray());
        if (EditorGUI.EndChangeCheck()) { _sortColumn = -1; _scroll = Vector2.zero; }
        if (_report.RunsConsidered == 0)
            EditorGUILayout.HelpBox("No runs recorded yet. Every run is recorded when it ends (dev-mode runs are " +
                "hidden unless Dev runs is on). Rebuild reads older journals in the folder.", MessageType.Info);
        DrawTable(_report.Tables[_tab]);
    }

    void DrawTable(BalanceTable table)
    {
        var widths = table.Columns.Select((column, index) => Mathf.Clamp(
            Math.Max(column.Length, table.Rows.Select(row => row[index]?.Length ?? 0).DefaultIfEmpty(0).Max()) * 7 + 14,
            44, 320)).ToArray();
        var rows = SortedRows(table);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int column = 0; column < table.Columns.Length; column++)
            {
                string arrow = column == _sortColumn ? (_sortDescending ? " ▼" : " ▲") : string.Empty;
                if (GUILayout.Button(table.Columns[column] + arrow, EditorStyles.toolbarButton, GUILayout.Width(widths[column])))
                {
                    _sortDescending = _sortColumn != column || !_sortDescending;
                    _sortColumn = column;
                }
            }
        }
        for (int index = 0; index < rows.Count; index++)
        {
            var rect = EditorGUILayout.BeginHorizontal();
            if (index % 2 == 1) EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.08f));
            for (int column = 0; column < table.Columns.Length; column++)
                EditorGUILayout.SelectableLabel(rows[index][column], GUILayout.Width(widths[column]),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    List<string[]> SortedRows(BalanceTable table)
    {
        if (_sortColumn < 0 || _sortColumn >= table.Columns.Length) return table.Rows;
        int column = _sortColumn;
        var sorted = table.Rows.OrderBy(row => Number(row[column]) ?? double.NegativeInfinity)
            .ThenBy(row => row[column], StringComparer.OrdinalIgnoreCase).ToList();
        if (_sortDescending) sorted.Reverse();
        return sorted;
    }

    static double? Number(string cell)
    {
        if (string.IsNullOrEmpty(cell)) return null;
        string trimmed = cell.Replace("%", string.Empty).Replace(" pp", string.Empty).Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
    }

    void ExportCsv()
    {
        string folder = EditorUtility.SaveFolderPanel("Export balance stats as CSV",
            Path.Combine(GameplayBalanceTelemetry.ExportDirectory, "Stats"), string.Empty);
        if (string.IsNullOrEmpty(folder)) return;
        _report.WriteCsv(folder);
        EditorUtility.RevealInFinder(folder);
    }
}

using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class MyNotesPlugin : EditorWindow
{
    enum Tab { Notepad, Ideas, Tasks }
    enum Priority { Low, Medium, High }

    [Serializable]
    class IdeaEntry
    {
        public string id;
        public string title;
        public string description;
        public string timestamp;
        public bool expanded = true;
    }

    [Serializable]
    class TaskEntry
    {
        public string id;
        public string text;
        public bool done;
        public Priority priority;
        public string timestamp;
    }

    [Serializable]
    class NotesData
    {
        public string notepadText = "";
        public List<IdeaEntry> ideas = new List<IdeaEntry>();
        public List<TaskEntry> tasks = new List<TaskEntry>();
    }

    // Mirrors MyGitPlugin's protected branch list (kept local so this file
    // doesn't depend on MyGitPlugin's private fields).
    static readonly string[] ProtectedBranches = { "main", "master" };

    const string DataFolderName = "DevNotes";
    const string DataFileName = "notes_data.json";

    NotesData data = new NotesData();
    Tab currentTab = Tab.Notepad;

    Vector2 notepadScroll;
    Vector2 ideasScroll;
    Vector2 tasksScroll;

    string newIdeaTitle = "";
    string newIdeaDescription = "";
    string newTaskText = "";
    Priority newTaskPriority = Priority.Medium;
    bool showCompletedTasks = true;

    string commitMessage = "Update dev notes";

    bool isDirty = false;
    double lastEditTime;
    const double AutosaveDelay = 1.5; // seconds of inactivity before autosave

    [MenuItem("MyTools/Notes & Tasks")]
    public static void ShowWindow()
    {
        var win = GetWindow<MyNotesPlugin>("Notes & Tasks");
        win.minSize = new Vector2(360, 480);
    }

    static string DataFolderPath => Path.Combine(Application.dataPath, "..", DataFolderName);
    static string DataFilePath => Path.Combine(DataFolderPath, DataFileName);

    void OnEnable()
    {
        LoadData();
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        if (isDirty) SaveData();
    }

    void OnLostFocus()
    {
        if (isDirty) SaveData();
    }

    void OnEditorUpdate()
    {
        if (isDirty && EditorApplication.timeSinceStartup - lastEditTime > AutosaveDelay)
        {
            SaveData();
        }
    }

    void MarkDirty()
    {
        isDirty = true;
        lastEditTime = EditorApplication.timeSinceStartup;
    }

    void OnGUI()
    {
        DrawTabBar();
        GUILayout.Space(6);

        switch (currentTab)
        {
            case Tab.Notepad: DrawNotepadTab(); break;
            case Tab.Ideas: DrawIdeasTab(); break;
            case Tab.Tasks: DrawTasksTab(); break;
        }

        GUILayout.FlexibleSpace();
        DrawFooter();
    }

    void DrawTabBar()
    {
        GUILayout.BeginHorizontal();
        DrawTabButton(Tab.Notepad, "Notepad");
        DrawTabButton(Tab.Ideas, $"Ideas ({data.ideas.Count})");
        DrawTabButton(Tab.Tasks, $"Tasks ({data.tasks.Count(t => !t.done)})");
        GUILayout.EndHorizontal();
    }

    void DrawTabButton(Tab tab, string label)
    {
        bool isActive = currentTab == tab;
        GUI.backgroundColor = isActive ? new Color(0.55f, 0.75f, 1f) : Color.white;
        if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Height(24)))
        {
            currentTab = tab;
        }
        GUI.backgroundColor = Color.white;
    }

    // ---------------- Notepad ----------------

    void DrawNotepadTab()
    {
        GUILayout.Label("Notepad", EditorStyles.boldLabel);
        notepadScroll = EditorGUILayout.BeginScrollView(notepadScroll, GUILayout.ExpandHeight(true));

        EditorGUI.BeginChangeCheck();
        string newText = EditorGUILayout.TextArea(data.notepadText, GUILayout.ExpandHeight(true));
        if (EditorGUI.EndChangeCheck())
        {
            data.notepadText = newText;
            MarkDirty();
        }

        EditorGUILayout.EndScrollView();
    }

    // ---------------- Ideas ----------------

    void DrawIdeasTab()
    {
        GUILayout.Label("New Idea", EditorStyles.boldLabel);

        newIdeaTitle = EditorGUILayout.TextField("Title", newIdeaTitle);
        GUILayout.Label("Description");
        newIdeaDescription = EditorGUILayout.TextArea(newIdeaDescription, GUILayout.Height(50));

        GUI.enabled = !string.IsNullOrWhiteSpace(newIdeaTitle);
        if (GUILayout.Button("Add Idea"))
        {
            data.ideas.Insert(0, new IdeaEntry
            {
                id = Guid.NewGuid().ToString(),
                title = newIdeaTitle.Trim(),
                description = newIdeaDescription.Trim(),
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            newIdeaTitle = "";
            newIdeaDescription = "";
            GUI.FocusControl(null);
            MarkDirty();
            SaveData();
        }
        GUI.enabled = true;

        GUILayout.Space(10);
        GUILayout.Label($"Saved Ideas ({data.ideas.Count})", EditorStyles.boldLabel);

        ideasScroll = EditorGUILayout.BeginScrollView(ideasScroll, GUILayout.ExpandHeight(true));
        if (data.ideas.Count == 0)
        {
            GUILayout.Label("No ideas yet.");
        }
        else
        {
            IdeaEntry toDelete = null;
            foreach (var idea in data.ideas)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.BeginHorizontal();
                idea.expanded = EditorGUILayout.Foldout(idea.expanded, idea.title, true);
                if (GUILayout.Button("x", GUILayout.Width(20)))
                {
                    toDelete = idea;
                }
                GUILayout.EndHorizontal();

                if (idea.expanded)
                {
                    EditorGUILayout.LabelField(idea.timestamp, EditorStyles.miniLabel);
                    if (!string.IsNullOrEmpty(idea.description))
                    {
                        EditorGUILayout.LabelField(idea.description, EditorStyles.wordWrappedLabel);
                    }
                }
                GUILayout.EndVertical();
            }

            if (toDelete != null)
            {
                data.ideas.Remove(toDelete);
                MarkDirty();
                SaveData();
            }
        }
        EditorGUILayout.EndScrollView();
    }

    // ---------------- Tasks ----------------

    void DrawTasksTab()
    {
        GUILayout.Label("New Task", EditorStyles.boldLabel);
        GUILayout.BeginHorizontal();
        newTaskText = EditorGUILayout.TextField(newTaskText);
        newTaskPriority = (Priority)EditorGUILayout.EnumPopup(newTaskPriority, GUILayout.Width(80));
        GUI.enabled = !string.IsNullOrWhiteSpace(newTaskText);
        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            data.tasks.Add(new TaskEntry
            {
                id = Guid.NewGuid().ToString(),
                text = newTaskText.Trim(),
                priority = newTaskPriority,
                done = false,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            newTaskText = "";
            GUI.FocusControl(null);
            MarkDirty();
            SaveData();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        showCompletedTasks = EditorGUILayout.ToggleLeft("Show completed", showCompletedTasks);

        GUILayout.Space(4);
        tasksScroll = EditorGUILayout.BeginScrollView(tasksScroll, GUILayout.ExpandHeight(true));

        var ordered = data.tasks
            .OrderBy(t => t.done)
            .ThenByDescending(t => t.priority)
            .ToList();

        TaskEntry toDelete = null;
        foreach (var task in ordered)
        {
            if (task.done && !showCompletedTasks) continue;

            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            bool newDone = EditorGUILayout.Toggle(task.done, GUILayout.Width(20));
            if (newDone != task.done)
            {
                task.done = newDone;
                MarkDirty();
                SaveData();
            }

            GUIStyle style = new GUIStyle(EditorStyles.label);
            if (task.done) style.normal.textColor = Color.gray;
            GUILayout.Label($"[{task.priority}] {task.text}", style);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("x", GUILayout.Width(20)))
            {
                toDelete = task;
            }
            GUILayout.EndHorizontal();
        }

        if (toDelete != null)
        {
            data.tasks.Remove(toDelete);
            MarkDirty();
            SaveData();
        }

        EditorGUILayout.EndScrollView();
    }

    // ---------------- Footer / Git push ----------------

    void DrawFooter()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField(isDirty ? "Unsaved changes..." : "All changes saved", EditorStyles.miniLabel);

        if (GUILayout.Button("Save Now"))
        {
            SaveData();
        }

        GUILayout.Space(6);

        string targetBranch = EditorPrefs.GetString("MyGitPlugin_TargetBranch", "main");
        EditorGUILayout.LabelField("Will push to branch:", targetBranch);
        EditorGUILayout.HelpBox("Push stages ALL uncommitted changes in the repo (same behavior as the Git Tools window), not just this notes file.", MessageType.Info);

        commitMessage = EditorGUILayout.TextField("Commit Message:", commitMessage);

        GUI.enabled = !string.IsNullOrWhiteSpace(commitMessage);
        if (GUILayout.Button("Push Notes to GitHub"))
        {
            if (isDirty) SaveData();

            if (ProtectedBranches.Contains(targetBranch) &&
                !EditorUtility.DisplayDialog("Push to protected branch?",
                    $"You're about to push directly to '{targetBranch}'. Continue?", "Push", "Cancel"))
            {
                GUI.enabled = true;
                return;
            }

            MyGitPlugin.ExecuteGitPush(commitMessage, targetBranch);
        }
        GUI.enabled = true;
    }

    // ---------------- Persistence ----------------

    void LoadData()
    {
        try
        {
            if (File.Exists(DataFilePath))
            {
                string json = File.ReadAllText(DataFilePath);
                data = JsonUtility.FromJson<NotesData>(json) ?? new NotesData();
            }
            else
            {
                data = new NotesData();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("MyNotesPlugin: Failed to load notes data: " + ex.Message);
            data = new NotesData();
        }
        isDirty = false;
    }

    void SaveData()
    {
        try
        {
            if (!Directory.Exists(DataFolderPath))
            {
                Directory.CreateDirectory(DataFolderPath);
            }
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(DataFilePath, json);
            isDirty = false;
        }
        catch (Exception ex)
        {
            Debug.LogError("MyNotesPlugin: Failed to save notes data: " + ex.Message);
        }
    }
}
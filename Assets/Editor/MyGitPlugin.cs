using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class MyGitPlugin : EditorWindow
{
    string commitMessage = "";
    string[] branches = new string[] { "main" };
    int selectedBranchIndex = 0;
    string currentBranch = "(unknown)";
    string newBranchName = "";
    bool showNewBranchField = false;

    List<string> statusLines = new List<string>();
    Vector2 statusScroll;
    Vector2 logScroll;

    static readonly string[] ProtectedBranches = { "main", "master" };
    static List<string> log = new List<string>();
    public static event System.Action OnGitOperationComplete;

    [MenuItem("MyTools/GitHub Auto-Push")]
    public static void ShowWindow()
    {
        var win = GetWindow<MyGitPlugin>("Git Tools");
        win.minSize = new Vector2(380, 540);
    }

    void OnEnable()
    {
        OnGitOperationComplete += HandleExternalRefresh;
        RefreshAll();
    }

    void OnDisable()
    {
        OnGitOperationComplete -= HandleExternalRefresh;
    }

    void OnFocus()
    {
        RefreshAll();
    }

    void HandleExternalRefresh()
    {
        RefreshAll();
        Repaint();
    }

    void RefreshAll()
    {
        string checkRepo = RunGitCommand("rev-parse --is-inside-work-tree");
        if (!checkRepo.Trim().StartsWith("true"))
        {
            currentBranch = "(not a git repo)";
            branches = new string[] { "main" };
            statusLines.Clear();
            return;
        }

        UpdateCurrentBranch();
        UpdateBranches();
        UpdateStatus();
    }

    void OnGUI()
    {
        GUILayout.Label("GitHub Uploader", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Current branch:", currentBranch);
        GUILayout.Space(5);

        // --- Branch selection ---
        GUILayout.BeginHorizontal();
        selectedBranchIndex = EditorGUILayout.Popup("Target Branch:", Mathf.Clamp(selectedBranchIndex, 0, Mathf.Max(0, branches.Length - 1)), branches);
        if (GUILayout.Button("Refresh", GUILayout.Width(70)))
        {
            RefreshAll();
        }
        GUILayout.EndHorizontal();

        string targetBranch = branches.Length > 0 ? branches[selectedBranchIndex] : "main";
        EditorPrefs.SetString("MyGitPlugin_TargetBranch", targetBranch);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Checkout Selected Branch"))
        {
            CheckoutBranch(targetBranch);
        }
        if (GUILayout.Button(showNewBranchField ? "Cancel" : "New Branch..."))
        {
            showNewBranchField = !showNewBranchField;
        }
        GUILayout.EndHorizontal();

        if (showNewBranchField)
        {
            GUILayout.BeginHorizontal();
            newBranchName = EditorGUILayout.TextField(newBranchName);
            if (GUILayout.Button("Create & Switch", GUILayout.Width(120)))
            {
                string safeName = newBranchName.Trim().Replace(" ", "-");
                if (!string.IsNullOrEmpty(safeName))
                {
                    AppendLog(RunGitCommand($"checkout -b {safeName}").Trim());
                    newBranchName = "";
                    showNewBranchField = false;
                    RefreshAll();
                }
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // --- Auto Commit Checkbox with Warning ---
        bool isAutoCommitEnabled = EditorPrefs.GetBool("MyGitPlugin_AutoCommit", false);
        bool newAutoCommitState = EditorGUILayout.Toggle("Auto Commit on Save", isAutoCommitEnabled);
        if (isAutoCommitEnabled != newAutoCommitState)
        {
            EditorPrefs.SetBool("MyGitPlugin_AutoCommit", newAutoCommitState);
        }
        if (newAutoCommitState)
        {
            EditorGUILayout.HelpBox("Warning: Pushing to remote on every save can freeze the Editor momentarily and bloat your Git commit history. Use this feature sparingly!", MessageType.Warning);
        }

        GUILayout.Space(10);

        // --- Status panel ---
        GUILayout.Label($"Changes ({statusLines.Count})", EditorStyles.boldLabel);
        statusScroll = EditorGUILayout.BeginScrollView(statusScroll, GUILayout.Height(120));
        if (statusLines.Count == 0)
        {
            GUILayout.Label("Working tree clean.");
        }
        else
        {
            foreach (var line in statusLines)
            {
                GUILayout.Label(line);
            }
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        commitMessage = EditorGUILayout.TextField("Commit Message:", commitMessage);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Pull"))
        {
            Pull(targetBranch);
        }

        GUI.enabled = statusLines.Count > 0 && !string.IsNullOrWhiteSpace(commitMessage);
        if (GUILayout.Button("Commit & Push"))
        {
            TryPush(commitMessage, targetBranch);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        if (GUILayout.Button("Discard All Local Changes"))
        {
            if (EditorUtility.DisplayDialog("Discard all changes?",
                "This will permanently reset tracked files and delete untracked files. This cannot be undone.",
                "Discard", "Cancel"))
            {
                AppendLog(RunGitCommand("reset --hard").Trim());
                AppendLog(RunGitCommand("clean -fd").Trim());
                RefreshAll();
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Log", EditorStyles.boldLabel);
        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(100));
        for (int i = log.Count - 1; i >= 0; i--)
        {
            GUILayout.Label(log[i], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    void TryPush(string message, string targetBranch)
    {
        if (ProtectedBranches.Contains(targetBranch) &&
            !EditorUtility.DisplayDialog("Push to protected branch?",
                $"You're about to push directly to '{targetBranch}'. Continue?", "Push", "Cancel"))
        {
            return;
        }

        ExecuteGitPush(message, targetBranch);
        commitMessage = "";
        RefreshAll();
    }

    void Pull(string branch)
    {
        AppendLog($"Pulling '{branch}'...");
        RunGitCommand("fetch");
        AppendLog(RunGitCommand($"pull origin {branch}").Trim());
        RefreshAll();
    }

    void CheckoutBranch(string branch)
    {
        AppendLog(RunGitCommand($"checkout {branch}").Trim());
        RefreshAll();
    }

    // Made static so the background save processor can call it
    public static void ExecuteGitPush(string message, string targetBranch)
    {
        AppendLog($"Starting push to '{targetBranch}'...");

        RunGitCommand("add .");

        string safeMessage = string.IsNullOrWhiteSpace(message) ? "Update" : message.Replace("\"", "\\\"");
        AppendLog(RunGitCommand($"commit -m \"{safeMessage}\"").Trim());

        string pushResult = RunGitCommand($"push -u origin {targetBranch}");
        AppendLog(pushResult.Trim());
        AppendLog("Done.");

        OnGitOperationComplete?.Invoke();
    }

    void UpdateCurrentBranch()
    {
        string raw = RunGitCommand("rev-parse --abbrev-ref HEAD");
        currentBranch = string.IsNullOrEmpty(raw) ? "(unknown)" : raw.Trim();
    }

    void UpdateStatus()
    {
        string raw = RunGitCommand("status --porcelain");
        statusLines.Clear();
        if (!string.IsNullOrEmpty(raw))
        {
            statusLines.AddRange(raw.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries));
        }
    }

    void UpdateBranches()
    {
        RunGitCommand("fetch");

        List<string> branchList = new List<string>();

        string rawRemote = RunGitCommand("branch -r");
        if (!string.IsNullOrEmpty(rawRemote))
        {
            string[] remoteLines = rawRemote.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in remoteLines)
            {
                string cleanLine = line.Trim();
                if (cleanLine.Contains("->")) continue;

                if (cleanLine.StartsWith("origin/"))
                {
                    cleanLine = cleanLine.Substring(7);
                }

                if (!branchList.Contains(cleanLine))
                {
                    branchList.Add(cleanLine);
                }
            }
        }

        string rawLocal = RunGitCommand("branch --format=\"%(refname:short)\"");
        if (!string.IsNullOrEmpty(rawLocal))
        {
            string[] localLines = rawLocal.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in localLines)
            {
                string cleanLine = line.Trim();
                if (!branchList.Contains(cleanLine))
                {
                    branchList.Add(cleanLine);
                }
            }
        }

        branches = branchList.Count > 0 ? branchList.ToArray() : new string[] { "main" };

        int idx = System.Array.IndexOf(branches, EditorPrefs.GetString("MyGitPlugin_TargetBranch", "main"));
        selectedBranchIndex = idx >= 0 ? idx : 0;
    }

    static void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        log.Add($"[{System.DateTime.Now:HH:mm:ss}] {line}");
        if (log.Count > 200) log.RemoveAt(0);
    }

    // Made static so it can be used without an active window instance
    public static string RunGitCommand(string gitCommand)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        Process process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.Arguments = gitCommand;
        process.StartInfo.WorkingDirectory = Application.dataPath + "/../";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        // Use event-based async reads instead of sequential ReadToEnd() calls,
        // which can deadlock if git writes enough to fill the stderr buffer
        // while we're still blocked reading stdout.
        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        try
        {
            process.Start();
        }
        catch (System.Exception ex)
        {
            string msg = "Git not found or failed to start: " + ex.Message;
            UnityEngine.Debug.LogError(msg);
            AppendLog(msg);
            return "";
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        string errText = error.ToString();
        if (!string.IsNullOrEmpty(errText))
        {
            if (errText.Contains("fatal") || errText.ToLower().Contains("error"))
            {
                UnityEngine.Debug.LogError("Git Error: " + errText);
                AppendLog("ERROR: " + errText.Trim());
            }
            else
            {
                // git often writes normal progress info to stderr (fetch/push progress etc.)
                AppendLog(errText.Trim());
            }
        }

        return output.ToString();
    }
}

public class AutoCommitSaveProcessor : UnityEditor.AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        if (EditorPrefs.GetBool("MyGitPlugin_AutoCommit", false))
        {
            string targetBranch = EditorPrefs.GetString("MyGitPlugin_TargetBranch", "main");
            string timestampMessage = "Auto-commit on save: " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            EditorApplication.delayCall += () =>
            {
                MyGitPlugin.ExecuteGitPush(timestampMessage, targetBranch);
            };
        }

        return paths;
    }
}
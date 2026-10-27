using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.Environments;
using UnityEngine.Networking;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

namespace StormEngine.EditorTools
{
    /// <summary>
    /// Deploys the Storm Engine backend (leaderboards, the Cloud Code storm validator and the access
    /// policy) to the Unity Cloud project this Editor is linked to, through the Deployment package.
    /// </summary>
    public static class StormEngineDeploy
    {
        const string ServerRoot = "Assets/StormEngine/Server";

        /// <summary>The last result is written here as well as to the Console.</summary>
        public const string ResultPath = "Temp/storm_deploy_result.txt";

        public static List<string> BackendFiles(bool includePolicy)
        {
            var files = Directory.GetFiles(ServerRoot + "/Leaderboards", "*.lb").Select(f => f.Replace('\\', '/')).ToList();
            files.Add(ServerRoot + "/CloudCode/SubmitStorm.js");
            if (includePolicy) files.Add(ServerRoot + "/AccessControl/storm_engine.ac");
            return files;
        }

        /// <summary>Describes what would be deployed and where, without changing anything.</summary>
        public static string Describe()
        {
            var d = Deployments.Instance;
            var sb = new StringBuilder();
            sb.AppendLine($"Project: {d.ProjectIdProvider?.ProjectId ?? "(not linked)"}   Environment: {d.EnvironmentProvider?.Current ?? "(none)"}");
            if (d.DeploymentWindow == null)
            {
                sb.AppendLine("The Deployment window API is not available.");
                return sb.ToString();
            }
            d.DeploymentWindow.OpenWindow();
            var files = BackendFiles(true);
            // Files no deployment module understands come back as null entries.
            var items = d.DeploymentWindow.GetFromFiles(files).Where(i => i != null).ToList();
            sb.AppendLine($"Recognized {items.Count} of {files.Count} files:");
            foreach (var it in items) sb.AppendLine($" - {it.Name} ({it.GetType().Name})");
            foreach (var f in files)
                if (!items.Any(i => SamePath(i.Path, f))) sb.AppendLine(" ! not recognized: " + f);
            return sb.ToString();
        }

        public const string EnvironmentPath = "Temp/storm_environments.txt";

        /// <summary>Fetches the project's environments; the list goes to <see cref="EnvironmentPath"/>.</summary>
        public static async void RefreshEnvironments()
        {
            File.WriteAllText(EnvironmentPath, "running");
            var sb = new StringBuilder();
            try
            {
                var api = EnvironmentsApi.Instance;
                await api.RefreshAsync();
                foreach (var env in api.Environments) sb.AppendLine($"{env.Name} {env.Id}");
                sb.AppendLine($"Active: {api.ActiveEnvironmentName} {api.ActiveEnvironmentId}");
            }
            catch (Exception e)
            {
                sb.AppendLine("FAILED: " + e.Message);
            }
            File.WriteAllText(EnvironmentPath, sb.ToString());
        }

        /// <summary>Selects the environment the Editor deploys to (for example "production").</summary>
        public static string UseEnvironment(string name)
        {
            var api = EnvironmentsApi.Instance;
            api.SetActiveEnvironment(name);
            return $"Active: {api.ActiveEnvironmentName} {api.ActiveEnvironmentId}";
        }

        public const string DiagnosePath = "Temp/storm_cloudcode_diag.txt";

        /// <summary>
        /// Read-only check of what the Cloud Code and Leaderboards admin APIs return for this project and
        /// environment (lists only; nothing is created or changed). Results go to <see cref="DiagnosePath"/>.
        /// </summary>
        public static async void DiagnoseCloudCode()
        {
            File.WriteAllText(DiagnosePath, "running");
            var sb = new StringBuilder();
            try
            {
                var d = Deployments.Instance;
                string project = d.ProjectIdProvider?.ProjectId, env = d.EnvironmentProvider?.Current;
                sb.AppendLine($"Deployment project {project}, environment {env}; CloudProjectSettings project {CloudProjectSettings.projectId}, org {CloudProjectSettings.organizationKey}, user {CloudProjectSettings.userName}");
                string token = await new AccessTokens().GetServicesGatewayTokenAsync();
                string[] urls =
                {
                    $"https://services.unity.com/api/cloud-code/v1/projects/{project}/environments/{env}/scripts",
                    $"https://services.unity.com/api/cloud-code/v1/projects/{project}/environments/{env}/scripts/SubmitStorm",
                    $"https://services.unity.com/api/leaderboards/v1/projects/{project}/environments/{env}/leaderboards",
                };
                foreach (var url in urls)
                {
                    using var req = UnityWebRequest.Get(url);
                    req.SetRequestHeader("Authorization", "Bearer " + token);
                    var op = req.SendWebRequest();
                    while (!op.isDone) await Task.Yield();
                    string body = req.downloadHandler?.text ?? "";
                    if (body.Length > 400) body = body.Substring(0, 400) + "...";
                    sb.AppendLine($"GET {url.Replace(project, "<project>").Replace(env, "<env>")} -> {req.responseCode} {body}");
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("FAILED: " + e.Message);
            }
            File.WriteAllText(DiagnosePath, sb.ToString());
        }

        static bool SamePath(string a, string b) =>
            !string.IsNullOrEmpty(a) && Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        [MenuItem("Storm Engine/Deploy Backend (Leaderboards + Cloud Code)")]
        static void DeployMenu() => Deploy(false);

        [MenuItem("Storm Engine/Deploy Access Policy (after Cloud Code works)")]
        static void DeployPolicyMenu() => DeployFiles(new List<string> { ServerRoot + "/AccessControl/storm_engine.ac" });

        /// <summary>Deploys asynchronously; the result goes to the Console and <see cref="ResultPath"/>.</summary>
        public static void Deploy(bool includePolicy) => DeployFiles(BackendFiles(includePolicy));

        static async void DeployFiles(List<string> files)
        {
            File.WriteAllText(ResultPath, "running");
            var sb = new StringBuilder();
            try
            {
                var d = Deployments.Instance;
                if (d.DeploymentWindow == null) throw new InvalidOperationException("The Deployment window API is not available.");
                // The Deployment window must be open (and have scanned the project) for this API.
                d.DeploymentWindow.OpenWindow();
                await System.Threading.Tasks.Task.Delay(1500);
                var items = d.DeploymentWindow.GetFromFiles(files).Where(i => i != null).ToList();
                if (items.Count < files.Count) sb.AppendLine($"{files.Count - items.Count} file(s) have no deployment module in this Editor and were skipped.");
                sb.AppendLine($"Deploying {items.Count} item(s) to environment '{d.EnvironmentProvider?.Current}' of project {d.ProjectIdProvider?.ProjectId}");
                var result = await d.DeploymentWindow.Deploy(items);
                foreach (var it in items)
                {
                    var s = it.Status;
                    sb.AppendLine($" - {it.Name}: {s.Message} {s.MessageDetail} [{s.MessageSeverity}]");
                }
                sb.AppendLine($"Deployed {result?.Deployed?.Count ?? 0} of {items.Count}.");
                Debug.Log("[StormEngine] Backend deployment\n" + sb);
            }
            catch (Exception e)
            {
                sb.AppendLine("FAILED: " + e.Message);
                Debug.LogError("[StormEngine] Backend deployment failed: " + e);
            }
            File.WriteAllText(ResultPath, sb.ToString());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StormEngine.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Exceptions;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using CloudLoadOptions = Unity.Services.CloudSave.Models.Data.Player.LoadOptions;
using CloudSaveOptions = Unity.Services.CloudSave.Models.Data.Player.SaveOptions;
using PublicRead = Unity.Services.CloudSave.Models.Data.Player.PublicReadAccessClassOptions;
using PublicWrite = Unity.Services.CloudSave.Models.Data.Player.PublicWriteAccessClassOptions;

namespace StormEngine.Online
{
    /// <summary>
    /// Unity Gaming Services backend: anonymous sign-in, server-verified storm submissions through
    /// Cloud Code, global leaderboards and cloud save. Compiled only when the UGS packages exist.
    /// </summary>
    public sealed class UgsBackend : IOnlineBackend
    {
        /// <summary>Name of the deployed Cloud Code script (file name of SubmitStorm.js).</summary>
        public const string SubmitEndpoint = "SubmitStorm";

        /// <summary>
        /// Editor and development builds only: if the Cloud Code script is not deployed yet, validate
        /// the storm locally and write it straight to the boards. Release builds always require Cloud Code.
        /// </summary>
        public static bool AllowDirectWritesInDevelopment = true;

        static Content _content;
        static Content ContentData => _content ??= Content.CreateDefault();
        bool _warnedBoards;
        bool _scriptMissing;
        bool _loggedNotLinked;

        public string LastError { get; private set; }

        public string Name => "Unity Gaming Services";
        static bool Ready => UnityServices.State == ServicesInitializationState.Initialized;
        public bool IsSignedIn => Ready && AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => IsSignedIn ? AuthenticationService.Instance.PlayerId : null;
        public string PlayerName => IsSignedIn ? AuthenticationService.Instance.PlayerName : null;

        public async Task<bool> ConnectAsync()
        {
            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                LastError = L.T("online services are not set up in this build yet.");
                if (!_loggedNotLinked) Debug.Log("[StormEngine] Project is not linked to Unity Cloud yet, so leaderboards stay offline. See STORM_ENGINE_SETUP.md.");
                _loggedNotLinked = true;
                return false;
            }
            try
            {
                if (!Ready) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (string.IsNullOrEmpty(AuthenticationService.Instance.PlayerName))
                    await AuthenticationService.Instance.GetPlayerNameAsync(true);
            }
            catch (Exception)
            {
                LastError = L.T("could not reach the game servers.");
                throw;
            }
            LastError = null;
            return AuthenticationService.Instance.IsSignedIn;
        }

        public async Task<string> SetNameAsync(string name) => await AuthenticationService.Instance.UpdatePlayerNameAsync(name);

        public async Task<bool> DeleteAccountAsync()
        {
            if (!IsSignedIn) return false;
            // Unity deletes the player and their data in the other services (Cloud Save, Leaderboards).
            await AuthenticationService.Instance.DeleteAccountAsync();
            return true;
        }

        /// <summary>Response body of SubmitStorm.js.</summary>
        public sealed class SubmitResponse
        {
            public bool ok;
            public double score;
            public string cls;
            public string[] boards;
            public string[] failed;
            public string error;
        }

        public async Task<SubmitOutcome> SubmitAsync(PendingSave storm)
        {
            // Once the script is known to be missing, skip the call (and the SDK's error log) until restart.
            if (_scriptMissing) return await SubmitDirectAsync(storm, "Cloud Code script SubmitStorm is not deployed");
            try
            {
                var args = new Dictionary<string, object>
                {
                    { "submission", storm.json }, { "summary", storm.summary ?? "" }, { "title", storm.title ?? "" }
                };
                var r = await CloudCodeService.Instance.CallEndpointAsync<SubmitResponse>(SubmitEndpoint, args);
                if (r == null) return new SubmitOutcome { Retry = true, Message = L.T("Empty server response.") };
                if (r.ok) return new SubmitOutcome { Done = true, ServerScore = r.score, Message = L.T("Verified by the server.") };
                bool boardFailure = r.failed != null && r.failed.Length > 0;
                return new SubmitOutcome
                {
                    Done = !boardFailure,
                    Retry = boardFailure,
                    Message = boardFailure ? L.T("Leaderboard write failed; will retry.") : L.F("Server rejected the storm: {0}", r.error)
                };
            }
            catch (CloudCodeRateLimitedException e)
            {
                return new SubmitOutcome { Retry = true, Message = e.Message };
            }
            catch (CloudCodeException e)
            {
                switch (e.Reason)
                {
                    case CloudCodeExceptionReason.NotFound:
                        _scriptMissing = true;
                        return await SubmitDirectAsync(storm, "Cloud Code script SubmitStorm is not deployed");
                    case CloudCodeExceptionReason.NoInternetConnection:
                    case CloudCodeExceptionReason.ServiceUnavailable:
                    case CloudCodeExceptionReason.TooManyRequests:
                    case CloudCodeExceptionReason.AccessTokenMissing:
                    case CloudCodeExceptionReason.Unauthorized:
                        return new SubmitOutcome { Retry = true, Message = e.Message };
                    default:
                        Debug.LogWarning("[StormEngine] Storm submission failed: " + e.Message);
                        return new SubmitOutcome { Done = true, Message = L.F("Submission failed: {0}", e.Message) };
                }
            }
        }

        async Task<SubmitOutcome> SubmitDirectAsync(PendingSave storm, string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (AllowDirectWritesInDevelopment)
            {
                var sub = StormSubmission.FromJson(storm.json);
                if (!StormValidator.Validate(ContentData, sub, out _, out var error))
                    return new SubmitOutcome { Done = true, Message = L.F("Local validation failed: {0}", error) };
                var metadata = string.IsNullOrEmpty(storm.summary) ? null : JsonReader.Parse(storm.summary);
                if (metadata is Dictionary<string, object> meta && !string.IsNullOrEmpty(storm.title)) meta["ttl"] = storm.title;
                var boards = new List<string> { Boards.AllTime };
                if (sub.Class != "squall") boards.Add(Boards.Class(sub.Class));
                if (sub.Week == WeekClock.WeekOf(DateTimeOffset.UtcNow.ToUnixTimeSeconds())) boards.Add(Boards.Weekly);
                try
                {
                    foreach (var id in boards)
                        await LeaderboardsService.Instance.AddPlayerScoreAsync(id, sub.Score, new AddPlayerScoreOptions { Metadata = metadata });
                }
                catch (LeaderboardsException e)
                {
                    WarnBoards(e);
                    return new SubmitOutcome { Retry = true, Message = e.Message };
                }
                Debug.Log("[StormEngine] " + reason + ": storm written directly (development builds only).");
                return new SubmitOutcome { Done = true, ServerScore = sub.Score, Message = L.T("Submitted without server verification (development mode).") };
            }
#endif
            Debug.LogWarning("[StormEngine] " + reason + ". Deploy SubmitStorm.js to enable verified leaderboards.");
            await Task.Yield();
            return new SubmitOutcome { Retry = true, Message = reason };
        }

        public async Task<BoardPage> GetBoardAsync(string boardId, int limit)
        {
            var page = new BoardPage { Id = boardId };
            try
            {
                var scores = await LeaderboardsService.Instance.GetScoresAsync(boardId, new GetScoresOptions { Limit = limit, IncludeMetadata = true });
                foreach (var e in scores.Results) page.Entries.Add(Map(e));
            }
            catch (LeaderboardsException e)
            {
                WarnBoards(e);
                page.Error = e.Reason == LeaderboardsExceptionReason.LeaderboardNotFound
                    ? L.T("This leaderboard is not available yet.")
                    : L.T("Could not load the leaderboard. Check your connection.");
                return page;
            }

            try
            {
                var me = await LeaderboardsService.Instance.GetPlayerScoreAsync(boardId, new GetPlayerScoreOptions { IncludeMetadata = true });
                page.Me = Map(me);
                if (me.Rank > 0)
                {
                    var range = await LeaderboardsService.Instance.GetPlayerRangeAsync(boardId, new GetPlayerRangeOptions { RangeLimit = 1, IncludeMetadata = true });
                    foreach (var e in range.Results)
                        if (e.Rank == me.Rank - 1) page.Rival = Map(e);
                }
            }
            catch (LeaderboardsException e) when (e.Reason == LeaderboardsExceptionReason.EntryNotFound)
            {
                // The player has no score on this board yet.
            }
            return page;
        }

        BoardEntry Map(LeaderboardEntry e) => new BoardEntry
        {
            Rank = e.Rank + 1,
            PlayerId = e.PlayerId,
            Name = NameFilter.Display(e.PlayerName, L.T("Storm Chaser")),
            Score = e.Score,
            Summary = e.Metadata,
            IsMe = e.PlayerId == PlayerId
        };

        void WarnBoards(LeaderboardsException e)
        {
            if (_warnedBoards || e.Reason != LeaderboardsExceptionReason.LeaderboardNotFound) return;
            _warnedBoards = true;
            Debug.LogWarning("[StormEngine] Leaderboards are missing in your Unity Cloud project. Deploy Assets/StormEngine/Server/Leaderboards/*.lb.");
        }

        public async Task<string> LoadCloudAsync(string key)
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key }, new CloudLoadOptions());
            return data.TryGetValue(key, out var item) ? item.Value.GetAs<string>() : null;
        }

        public async Task<bool> SaveCloudAsync(string key, string json)
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { key, json } }, new CloudSaveOptions());
            return true;
        }

        const string IslandKey = "island";

        public async Task<bool> PublishIslandAsync(string json)
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { IslandKey, json } }, new CloudSaveOptions(new PublicWrite()));
            return true;
        }

        public async Task<string> LoadIslandAsync(string playerId)
        {
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { IslandKey }, new CloudLoadOptions(new PublicRead(playerId)));
            return data.TryGetValue(IslandKey, out var item) ? item.Value.GetAs<string>() : null;
        }

        public async Task<ArchivedRank> GetArchivedRankAsync(string boardId)
        {
            var versions = await LeaderboardsService.Instance.GetVersionsAsync(boardId, new GetVersionsOptions { Limit = 1 });
            if (versions?.Results == null || versions.Results.Count == 0) return new ArchivedRank { Rank = -1 };
            var v = versions.Results[0];
            var result = new ArchivedRank { VersionId = v.Id, EndUnix = new DateTimeOffset(DateTime.SpecifyKind(v.End, DateTimeKind.Utc)).ToUnixTimeSeconds() };
            try
            {
                var entry = await LeaderboardsService.Instance.GetVersionPlayerScoreAsync(boardId, v.Id);
                result.Rank = entry.Rank + 1;
            }
            catch (LeaderboardsException e) when (e.Reason == LeaderboardsExceptionReason.EntryNotFound)
            {
                result.Rank = 0;
            }
            return result;
        }
    }

    static class UgsRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => OnlineBackends.Factory = () => new UgsBackend();
    }
}

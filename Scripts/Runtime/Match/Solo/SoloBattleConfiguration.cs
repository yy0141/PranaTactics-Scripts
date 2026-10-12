using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PranaTactics.AI.Solo;

namespace PranaTactics.Match.Solo
{
    /// <summary>
    /// JSONのAI編成、ステータス、配色を検証し、人数の異なるチームを保持します。
    /// </summary>
    public sealed class SoloBattleConfiguration
    {
        public IReadOnlyList<SoloBattleTeam> Teams { get; }
        public int TotalPlayerCount { get; }
        public int MaximumTeamSize { get; }
        public SoloUnitStats PlayerStats { get; }
        public AiDifficultyOverrides EnemyAi { get; }
        private readonly IReadOnlyList<SoloBattleMember> _allySubstitutes;

        /// <summary>
        /// 検証済みの編成とプレイヤーの指定値を保存します。
        /// </summary>
        private SoloBattleConfiguration(List<SoloBattleTeam> teams, SoloUnitStats playerStats,
            List<SoloBattleMember> allySubstitutes, AiDifficultyOverrides enemyAi)
        {
            PlayerStats = playerStats;
            EnemyAi = enemyAi;
            _allySubstitutes = new ReadOnlyCollection<SoloBattleMember>(allySubstitutes);
            Teams = new ReadOnlyCollection<SoloBattleTeam>(teams);
            foreach (SoloBattleTeam team in teams)
            {
                TotalPlayerCount = checked(TotalPlayerCount + team.PlayerCount);
                MaximumTeamSize = Math.Max(MaximumTeamSize, team.PlayerCount);
            }
        }

        /// <summary>
        /// 重複のないAI編成と人数上限を検証し、チームと色の対応を確定します。
        /// </summary>
        public static SoloBattleConfiguration Parse(string json)
        {
            JsonDefinition definition = JsonConvert.DeserializeObject<JsonDefinition>(json,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
            if (definition == null || definition.EnemyTeams.Length == 0)
            {
                throw new ArgumentException("敵チームを一つ以上指定してください。", nameof(json));
            }

            if (definition.EnemyTeams.Length > 4)
            {
                throw new ArgumentException("味方を含めたチーム数は最大5チームです。");
            }

            definition.EnemyAi?.Validate();
            ValidateColor(definition.AllyColor);
            if (definition.EnemyColors.Length < definition.EnemyTeams.Length)
            {
                throw new ArgumentException("敵チーム数以上のenemyColorsが必要です。", nameof(json));
            }

            foreach (string color in definition.EnemyColors)
            {
                ValidateColor(color);
            }

            var teams = new List<SoloBattleTeam>
            {
                new SoloBattleTeam(0, definition.AllyColor, ExpandMembers(definition.AllyAi))
            };
            if (teams[0].PlayerCount > 4)
            {
                throw new ArgumentException("プレイヤーを含めた味方チームは最大4人です。");
            }

            for (int index = 0; index < definition.EnemyTeams.Length; index++)
            {
                TeamJson team = definition.EnemyTeams[index] ?? throw new ArgumentException("敵チームにnullは指定できません。");
                List<SoloBattleMember> members = ExpandMembers(team.Members);
                if (members.Count == 0)
                {
                    throw new ArgumentException($"敵チーム{index + 1}のAIが指定されていません。");
                }

                teams.Add(new SoloBattleTeam(index + 1, definition.EnemyColors[index], members));
            }

            return new SoloBattleConfiguration(teams, CreateStats(definition.PlayerStats),
                ExpandMembers(definition.AllySubstitutes), definition.EnemyAi);
        }

        /// <summary>
        /// プレイヤーを先頭にしてチームごとのAIへ連続した識別子を割り当てます。
        /// </summary>
        public IReadOnlyList<SoloBattleParticipant> CreateParticipants(string playerCharacter)
        {
            if (string.IsNullOrWhiteSpace(playerCharacter))
            {
                throw new ArgumentException("プレイヤーのキャラクターが必要です。", nameof(playerCharacter));
            }

            playerCharacter = playerCharacter.Trim();
            var players = new List<SoloBattleParticipant>
            {
                new SoloBattleParticipant(0, 0, playerCharacter, PlayerStats)
            };
            foreach (SoloBattleTeam team in Teams)
            {
                foreach (SoloBattleMember member in team.AiMembers)
                {
                    SoloBattleMember selected = member;
                    if (team.TeamId == 0 && member.CharacterName == playerCharacter)
                    {
                        selected = FindAllySubstitute(playerCharacter);
                    }

                    players.Add(new SoloBattleParticipant(players.Count, team.TeamId, selected.CharacterName, selected.Stats));
                }
            }

            return new ReadOnlyCollection<SoloBattleParticipant>(players);
        }

        /// <summary>
        /// プレイヤーと他の味方に重ならない代替キャラクターを取得します。
        /// </summary>
        private SoloBattleMember FindAllySubstitute(string playerCharacter)
        {
            var used = new HashSet<string>(StringComparer.Ordinal) { playerCharacter };
            foreach (SoloBattleMember member in Teams[0].AiMembers)
            {
                used.Add(member.CharacterName);
            }

            foreach (SoloBattleMember member in _allySubstitutes)
            {
                if (!used.Contains(member.CharacterName))
                {
                    return member;
                }
            }

            throw new InvalidOperationException("選択キャラと重ならないallySubstitutesをJSONに指定してください。");
        }

        /// <summary>
        /// 型番やキャラクター名の入力誤りをユニット生成前に検出します。
        /// </summary>
        public void ValidateCharacters(Func<string, bool> isRegistered)
        {
            if (isRegistered == null)
            {
                throw new ArgumentNullException(nameof(isRegistered));
            }

            foreach (SoloBattleMember member in _allySubstitutes)
            {
                if (!isRegistered(member.CharacterName))
                {
                    throw new InvalidOperationException($"代替キャラクターが未登録です: {member.CharacterName}");
                }
            }

            foreach (SoloBattleTeam team in Teams)
            {
                foreach (SoloBattleMember member in team.AiMembers)
                {
                    if (!isRegistered(member.CharacterName))
                    {
                        throw new InvalidOperationException($"チーム{team.TeamId}のキャラクターが未登録です: {member.CharacterName}");
                    }
                }
            }
        }

        /// <summary>
        /// キャラクターと指定値を取得し、不正な名前、人数や重複を拒否します。
        /// </summary>
        private static List<SoloBattleMember> ExpandMembers(MemberJson[] groups)
        {
            if (groups == null)
            {
                throw new ArgumentException("AI編成を配列で指定してください。");
            }

            var members = new List<SoloBattleMember>();
            foreach (MemberJson group in groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.CharacterName) || group.Count <= 0)
                {
                    throw new ArgumentException("AIのcharacterNameと1以上のcountが必要です。");
                }

                string name = group.CharacterName.Trim();
                if (group.Count != 1 || members.Exists(member => member.CharacterName == name) || members.Count >= 4)
                {
                    throw new ArgumentException("各チームは最大4人で、同じキャラクターを重複できません。countは1を指定してください。");
                }

                SoloUnitStats stats = CreateStats(group.Stats);
                members.Add(new SoloBattleMember(name, stats));
            }

            return members;
        }

        /// <summary>
        /// 省略されたステータスを未指定値として保持し、指定された値だけ検証します。
        /// </summary>
        private static SoloUnitStats CreateStats(StatsJson stats)
        {
            int? actionPoint = null;
            if (stats?.ActionPoint != null)
            {
                if (stats.ActionPoint.Type != JTokenType.Integer ||
                    !int.TryParse(stats.ActionPoint.ToString(), out int specifiedActionPoint))
                {
                    throw new ArgumentException("stats.actionPointは0以上の整数で指定してください。");
                }
                actionPoint = specifiedActionPoint;
            }

            return new SoloUnitStats(stats?.MoveSpeed, stats?.PranaCapacity, actionPoint);
        }

        /// <summary>
        /// RGBまたはRGBAの16進カラー表記を検証します。
        /// </summary>
        private static void ValidateColor(string color)
        {
            if (color == null || !Regex.IsMatch(color, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$"))
            {
                throw new ArgumentException($"色は#RRGGBBまたは#RRGGBBAAで指定してください: {color}");
            }
        }

        /// <summary>
        /// JSONの最上位設定を読み込むためのDTOです。
        /// </summary>
        private sealed class JsonDefinition
        {
            [JsonProperty(Required = Required.Always)]
            public string AllyColor { get; set; }
            [JsonProperty(Required = Required.Always)]
            public string[] EnemyColors { get; set; }
            [JsonProperty(Required = Required.Always)]
            public MemberJson[] AllyAi { get; set; }
            [JsonProperty(Required = Required.Always)]
            public TeamJson[] EnemyTeams { get; set; }
            [JsonProperty(Required = Required.DisallowNull)]
            public StatsJson PlayerStats { get; set; }
            [JsonProperty(Required = Required.DisallowNull)]
            public MemberJson[] AllySubstitutes { get; set; } = Array.Empty<MemberJson>();
            [JsonProperty(Required = Required.DisallowNull)]
            public AiDifficultyOverrides EnemyAi { get; set; }
        }

        /// <summary>
        /// 一つの敵チームのJSON編成です。
        /// </summary>
        private sealed class TeamJson
        {
            [JsonProperty(Required = Required.Always)]
            public MemberJson[] Members { get; set; }
        }

        /// <summary>
        /// キャラクターと個別のステータスを指定するJSON編成です。
        /// </summary>
        private sealed class MemberJson
        {
            [JsonProperty(Required = Required.Always)]
            public string CharacterName { get; set; }
            [JsonProperty(Required = Required.Always)]
            public int Count { get; set; }
            [JsonProperty(Required = Required.DisallowNull)]
            public StatsJson Stats { get; set; }
        }

        /// <summary>
        /// 個別に省略できるステータス設定のJSONです。
        /// </summary>
        private sealed class StatsJson
        {
            [JsonProperty(Required = Required.DisallowNull)]
            public float? MoveSpeed { get; set; }
            [JsonProperty(Required = Required.DisallowNull)]
            public float? PranaCapacity { get; set; }
            [JsonProperty(Required = Required.DisallowNull)]
            public JToken ActionPoint { get; set; }
        }
    }

    /// <summary>
    /// 一チームのAI編成とマップ表示色を保持します。
    /// </summary>
    public sealed class SoloBattleTeam
    {
        public int TeamId { get; }
        public string Color { get; }
        public IReadOnlyList<SoloBattleMember> AiMembers { get; }
        public int PlayerCount => AiMembers.Count + (TeamId == 0 ? 1 : 0);

        /// <summary>
        /// チームごとの編成とマップ表示色を保存します。
        /// </summary>
        internal SoloBattleTeam(int teamId, string color, List<SoloBattleMember> members)
        {
            TeamId = teamId;
            Color = color;
            AiMembers = new ReadOnlyCollection<SoloBattleMember>(members);
        }
    }

    /// <summary>
    /// 一体のAIのキャラクターとステージ固有のステータスを保持します。
    /// </summary>
    public sealed class SoloBattleMember
    {
        public string CharacterName { get; }
        public SoloUnitStats Stats { get; }

        /// <summary>
        /// キャラクター名とステージ固有の指定値を保存します。
        /// </summary>
        internal SoloBattleMember(string characterName, SoloUnitStats stats)
        {
            CharacterName = characterName;
            Stats = stats;
        }
    }

    /// <summary>
    /// 生成と生存人数管理で共有するソロ試合の参加者情報です。
    /// </summary>
    public sealed class SoloBattleParticipant
    {
        public int PlayerId { get; }
        public int TeamId { get; }
        public string CharacterName { get; }
        public SoloUnitStats Stats { get; }

        /// <summary>
        /// 生成識別子、キャラクター、ステージ固有の指定値を保存します。
        /// </summary>
        internal SoloBattleParticipant(int playerId, int teamId, string characterName, SoloUnitStats stats)
        {
            PlayerId = playerId;
            TeamId = teamId;
            CharacterName = characterName;
            Stats = stats;
        }
    }
}

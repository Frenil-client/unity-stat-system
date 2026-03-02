using System;
using System.Collections.Generic;

namespace StatSystem
{
    /// <summary>
    /// StatId enum과 string 이름을 양방향으로 매핑합니다.
    /// UID(uint) -> 이름(string) 조회와 이름 -> UID 조회를 모두 지원합니다.
    ///
  /// 사용 예:
    /// string name = StatRegistry.GetName(StatId.AttackPower); // "AttackPower"
    /// uint   uid  = StatRegistry.GetUid(StatId.AttackPower);  // 102
    /// StatId id   = StatRegistry.GetId("AttackPower");        // StatId.AttackPower
    /// </summary>
    public static class StatRegistry
    {
        private static readonly Dictionary<uint, string>  _uidToName = new();
        private static readonly Dictionary<string, uint>  _nameToUid = new();
        private static readonly Dictionary<uint, StatId>  _uidToId   = new();

        static StatRegistry()
        {
            foreach (StatId id in Enum.GetValues(typeof(StatId)))
            {
                uint   uid  = (uint)id;
                string name = id.ToString();

                _uidToName[uid]  = name;
                _nameToUid[name] = uid;
                _uidToId[uid]    = id;
            }
        }

        /// <summary>StatId -> 이름 문자열</summary>
        public static string GetName(StatId id) => id.ToString();

        /// <summary>StatId -> uint UID</summary>
        public static uint GetUid(StatId id) => (uint)id;

        /// <summary>uint UID -> 이름 문자열</summary>
        public static string GetName(uint uid) =>
            _uidToName.TryGetValue(uid, out var name) ? name : $"Unknown({uid})";

        /// <summary>uint UID -> StatId</summary>
        public static StatId GetId(uint uid) =>
            _uidToId.TryGetValue(uid, out var id) ? id : throw new KeyNotFoundException($"UID {uid} not found.");

        /// <summary>이름 문자열 -> uint UID</summary>
        public static uint GetUid(string name) =>
            _nameToUid.TryGetValue(name, out var uid) ? uid : throw new KeyNotFoundException($"'{name}' not found.");

        /// <summary>이름 문자열 -> StatId</summary>
        public static StatId GetId(string name) =>
            _nameToUid.TryGetValue(name, out var uid) ? _uidToId[uid] : throw new KeyNotFoundException($"'{name}' not found.");

        /// <summary>등록된 모든 StatId 목록</summary>
        public static IEnumerable<StatId> AllIds => (StatId[])Enum.GetValues(typeof(StatId));
    }
}

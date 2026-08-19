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
    /// uint   uid  = StatRegistry.GetUid(StatId.AttackPower);  // 100
    /// StatId id   = StatRegistry.GetId("AttackPower");        // StatId.AttackPower
    /// </summary>
    public static class StatRegistry
    {
        private static readonly Dictionary<uint, string>  _uidToName = new();
        private static readonly Dictionary<string, uint>  _nameToUid = new();
        private static readonly Dictionary<uint, StatId>  _uidToId   = new();
        private static readonly StatId[]                  _allIds;

        static StatRegistry()
        {
            _allIds = (StatId[])Enum.GetValues(typeof(StatId));

            foreach (StatId id in _allIds)
            {
                uint   uid  = (uint)id;
                string name = id.ToString();

                _uidToName[uid]  = name;
                _nameToUid[name] = uid;
                _uidToId[uid]    = id;
            }
        }

        /// <summary>
        /// StatId -> 이름 문자열.
        /// enum의 ToString()은 호출마다 문자열을 새로 만들고 내부적으로 리플렉션을 타므로,
        /// 정적 생성자에서 한 번 만들어 캐싱한 값을 돌려준다.
        /// </summary>
        public static string GetName(StatId id) => GetName((uint)id);

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

        /// <summary>등록된 모든 StatId 목록. 호출마다 배열을 새로 만들지 않도록 캐싱되어 있다.</summary>
        public static IReadOnlyList<StatId> AllIds => _allIds;
    }
}

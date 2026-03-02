namespace StatSystem
{
    /// <summary>
    /// 스탯 고유 ID 정의.
    /// RPG에서 범용적으로 사용되는 전투 능력치를 기반으로 구성되었습니다.
    ///
  /// 구간 규칙:
    /// 1xx - 공격력 / 마력
    /// 2xx - 데미지 / 크리티컬
    /// 3xx - 방어 / 생존
    /// 4xx - 보스 / 방어율 무시
    /// 5xx - 이동 / 기타
    /// </summary>
    public enum StatId : uint
    {
        //공격력 / 마력 (1xx) 
        /// <summary>공격력 (물리 계열 데미지에 영향)</summary>
        AttackPower         = 100,
        /// <summary>마력 (마법 계열 데미지에 영향)</summary>
        MagicAttack         = 101,
        /// <summary>공격력 % 증가 (배율 합산 - 실제 적용은 데미지 계산 레이어에서 처리)</summary>
        AttackPowerPercent  = 102,
        /// <summary>마력 % 증가 (배율 합산 - 실제 적용은 데미지 계산 레이어에서 처리)</summary>
        MagicAttackPercent  = 103,
        /// <summary>방어력</summary>
        Defense             = 110,
        /// <summary>방어력 % 증가 (배율 합산 - 실제 적용은 데미지 계산 레이어에서 처리)</summary>
        DefensePercent      = 111,

        //데미지 / 크리티컬 (2xx) 
        /// <summary>데미지 % (모든 대상 피해 증가)</summary>
        Damage              = 200,
        /// <summary>최종 데미지 % (모든 배율 적용 후 추가 증가)</summary>
        FinalDamage         = 201,
        /// <summary>크리티컬 확률 %</summary>
        CriticalRate        = 210,
        /// <summary>크리티컬 데미지 %</summary>
        CriticalDamage      = 211,

        //방어 / 생존 (3xx) 
        /// <summary>상태이상 내성 (디버프 지속시간 감소)</summary>
        StatusResistance    = 300,

        //보스 / 방어율 무시 (4xx) 
        /// <summary>보스 몬스터 공격 시 데미지 % 증가</summary>
        BossDamage          = 400,
        /// <summary>몬스터 방어율 무시 %</summary>
        IgnoreDefense       = 401,
        /// <summary>일반 몬스터 공격 시 데미지 % 증가</summary>
        NormalMonsterDamage = 402,
        /// <summary>속성 내성 무시 %</summary>
        IgnoreElemental     = 403,

        //이동 / 기타 (5xx) 
        /// <summary>이동 속도</summary>
        MoveSpeed           = 500,
        /// <summary>점프력</summary>
        JumpPower           = 501,
        /// <summary>공격 속도 단계 (높을수록 빠름)</summary>
        AttackSpeed         = 502,
    }
}

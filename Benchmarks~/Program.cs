using System;
using StatSystem;
using StatSystem.Benchmarks;

/// <summary>
/// StatId 기반 일괄 처리 경로의 힙 할당을 측정한다.
///
/// 같은 연산을 두 구현으로 돌려 비교한다 — 리팩토링 직전의 Reflection 구현
/// (LegacyReflectionStat, 커밋 b220d2e^에서 그대로 가져옴)과 현재의 배열 기반 구현.
/// 과거 수치를 문서에 적어두는 대신 여기서 매번 다시 재므로, 표의 숫자는 재현 가능하다.
///
/// 현재 구현이 1바이트라도 할당하면 종료 코드 1로 실패한다. CI가 이걸 돌리므로
/// 할당이 되살아나는 변경은 푸시 시점에 막힌다.
/// </summary>
internal static class Program
{
    private const int Iterations = 100_000;

    // 측정 전에 한 번 돌려 JIT와 티어드 컴파일 비용을 창 밖으로 뺀다.
    private const int Warmup = 1_000;

    private static int Main()
    {
        Console.WriteLine("StatSystem 할당 벤치마크");
        Console.WriteLine($"  런타임 : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"  구성   : {(IsDebug() ? "Debug" : "Release")}");
        Console.WriteLine($"  반복   : {Iterations:N0}회 × (SetValue + AddValue + GetValue + 명명 접근) = {Iterations * 4:N0} 연산");
        Console.WriteLine();

        long legacy = MeasureLegacy();
        long current = MeasureCurrent();

        Console.WriteLine("| 구현 | 할당량 | 반복당 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine($"| 이전 (FieldInfo) | {legacy:N0} B ({legacy / 1024.0 / 1024.0:F2} MB) | {legacy / (double)Iterations:F0} B |");
        Console.WriteLine($"| 이후 (배열 인덱싱) | {current:N0} B | {current / (double)Iterations:F0} B |");
        Console.WriteLine();

        if (current != 0)
        {
            Console.WriteLine($"실패: 현재 구현이 {current:N0} 바이트를 할당했습니다. 일괄 처리 경로에 할당이 되살아났습니다.");
            return 1;
        }

        Console.WriteLine("통과: 현재 구현의 일괄 처리 경로는 할당이 없습니다.");
        return 0;
    }

    private static long MeasureLegacy()
    {
        var stat = new LegacyReflectionStat();
        stat.SetValue(StatId.AttackPower, 1);
        for (int i = 0; i < Warmup; i++) { stat.AddValue(StatId.AttackPower, 1); }

        long before = Settle();
        double sink = 0;

        for (int i = 0; i < Iterations; i++)
        {
            stat.SetValue(StatId.AttackPower, i);
            stat.AddValue(StatId.AttackPower, 1);
            sink = stat.GetValue(StatId.AttackPower);
            sink = stat.AttackPower.Value.ToDouble();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(sink);
        return allocated;
    }

    private static long MeasureCurrent()
    {
        var stat = new Stat();
        stat.SetBaseValue(StatId.AttackPower, 1L);
        for (int i = 0; i < Warmup; i++) { stat.AddBaseValue(StatId.AttackPower, 1L); }

        long before = Settle();
        StatValue sink = default;

        for (int i = 0; i < Iterations; i++)
        {
            stat.SetBaseValue(StatId.AttackPower, (long)i);
            stat.AddBaseValue(StatId.AttackPower, 1L);
            sink = stat.GetValue(StatId.AttackPower);
            sink = stat.AttackPower;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(sink);
        return allocated;
    }

    // 측정 직전에 이전 단계의 잔여 할당을 정리하고 기준점을 잡는다.
    private static long Settle()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetAllocatedBytesForCurrentThread();
    }

    private static bool IsDebug()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }
}

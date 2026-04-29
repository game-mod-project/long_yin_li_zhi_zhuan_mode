using System;
using System.Collections.Generic;

namespace LongYinRoster.Core;

/// <summary>
/// PinpointPatcher.Apply 의 결과 누적. step 단위로 채워지고 상위 (DoApply) 가 토스트
/// 매핑 + 자동복원 결정에 사용.
/// </summary>
public sealed class ApplyResult
{
    public List<string>    AppliedFields { get; } = new();
    public List<string>    SkippedFields { get; } = new();
    public List<string>    WarnedFields  { get; } = new();
    public List<Exception> StepErrors    { get; } = new();
    public bool HasFatalError { get; set; }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace OTADomain.Matchings
{
    public enum EffectType : byte
    {
        Default = 0,   // 默认 双向限定
        Positive = 1,  // 增益（正向影响） 允许单独出现在右侧 在左侧必须有对应的词
        Negative = 2,  // 减益（负向影响） 允许单独出现在左侧 在右侧必须有对应的词
    }

}

﻿using LuckParser.Parser.ParsedData;

namespace LuckParser.EIData
{
    public abstract class AbstractBoonSimulationItemWasted : AbstractBoonSimulationItem
    {
        protected readonly AgentItem Src;
        private readonly long _waste;
        protected readonly long Time;

        protected AbstractBoonSimulationItemWasted(AgentItem src, long waste, long time)
        {
            Src = src;
            _waste = waste;
            Time = time;
        }

        protected long GetValue(long start, long end)
        {
            return (start <= Time && Time <= end) ? _waste : 0;
        }
    }
}

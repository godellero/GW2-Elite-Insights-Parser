﻿namespace LuckParser.Parser.ParsedData.CombatEvents
{
    public class LogStartEvent : LogDateEvent
    {
        public LogStartEvent(CombatItem evtcItem, long offset) : base(evtcItem, offset)
        {
        }

    }
}

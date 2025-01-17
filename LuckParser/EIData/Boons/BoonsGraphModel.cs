﻿using LuckParser.Parser.ParsedData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LuckParser.EIData
{
    public class BoonsGraphModel
    {

        public class Segment
        {
            public long Start { get; set; }
            public long End { get; set; }
            public int Value { get; set; }

            public Segment(long start, long end, int value)
            {
                Start = start;
                End = end;
                Value = value;
            }

            public Segment(Segment other)
            {
                Start = other.Start;
                End = other.End;
                Value = other.Value;
            }

            public bool Intersect(long start, long end)
            {
                long maxStart = Math.Max(start, Start);
                long minEnd = Math.Min(end, End);
                return minEnd - maxStart >= 0;
            }
        }

        public class SegmentWithSources : Segment
        {
            public List<AgentItem> Sources { get; set; } = new List<AgentItem>();

            public SegmentWithSources(long start, long end, int value, params AgentItem[] srcs) : base(start, end, value)
            {
                foreach (AgentItem a in srcs)
                {
                    Sources.Add(a);
                }
            }
        }

        public readonly Boon Boon;
        public List<Segment> BoonChart { get; private set; } = new List<Segment>();
        private readonly List<SegmentWithSources> _boonChartWithSource;

        // Constructor
        public BoonsGraphModel(Boon boon)
        {
            Boon = boon;
        }
        public BoonsGraphModel(Boon boon, List<SegmentWithSources> boonChartWithSource)
        {
            Boon = boon;
            _boonChartWithSource = boonChartWithSource;
            FuseFromSegmentsWithSource();
        }

        public int GetStackCount(long time)
        {
            for (int i = BoonChart.Count - 1; i >= 0; i--)
            {
                Segment seg = BoonChart[i];
                if (seg.Start <= time && time <= seg.End)
                {
                    return seg.Value;
                }
            }
            return 0;
        }


        public bool IsPresent(long time, long window)
        {
            int count = 0;
            foreach (Segment seg in BoonChart)
            {
                if (seg.Intersect(time - window, time + window))
                {
                    count += seg.Value;
                }
            }
            return count > 0;
        }

        public List<AgentItem> GetSources(long time)
        {
            if (_boonChartWithSource == null)
            {
                return new List<AgentItem>() { GeneralHelper.UnknownAgent };
            }
            for (int i = BoonChart.Count - 1; i >= 0; i--)
            {
                SegmentWithSources seg = _boonChartWithSource[i];
                if (seg.Start <= time && time <= seg.End)
                {
                    return seg.Sources;
                }
            }
            return new List<AgentItem>();
        }

        private void FuseFromSegmentsWithSource()
        {
            List<Segment> newChart = new List<Segment>();
            Segment last = null;
            foreach (Segment seg in _boonChartWithSource)
            {
                if (seg.Start == seg.End)
                {
                    continue;
                }
                if (last == null)
                {
                    newChart.Add(new Segment(seg));
                    last = newChart.Last();
                }
                else
                {
                    if (seg.Value == last.Value)
                    {
                        last.End = seg.End;
                    }
                    else
                    {
                        newChart.Add(new Segment(seg));
                        last = newChart.Last();
                    }
                }
            }
            BoonChart = newChart;
        }

        public void FuseSegments()
        {
            List<Segment> newChart = new List<Segment>();
            Segment last = null;
            foreach (Segment seg in BoonChart)
            {
                if (seg.Start == seg.End)
                {
                    continue;
                }
                if (last == null)
                {
                    newChart.Add(new Segment(seg));
                    last = newChart.Last();
                }
                else
                {
                    if (seg.Value == last.Value)
                    {
                        last.End = seg.End;
                    }
                    else
                    {
                        newChart.Add(new Segment(seg));
                        last = newChart.Last();
                    }
                }
            }
            BoonChart = newChart;
        }

    }
}

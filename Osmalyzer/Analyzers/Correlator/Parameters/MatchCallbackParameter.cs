﻿using System;

namespace Osmalyzer;

/// <summary>
/// The rule(s) by which to match data items to OSM elements.
/// If this is not speicfied, then any closest match by distance is accepted.
/// </summary>
public class MatchCallbackParameter<T> : CorrelatorParamater where T : ICorrelatorItem
{
    public Func<T, OsmElement, MatchStrength> MatchCallback { get; }
        

    public MatchCallbackParameter(Func<T, OsmElement, MatchStrength> matchCallback)
    {
        MatchCallback = matchCallback;
    }
}


public enum MatchStrength
{
    Unmatched,
    Weak,
    Mediocre,
    Strong
}
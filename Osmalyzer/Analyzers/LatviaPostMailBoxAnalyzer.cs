﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Osmalyzer;

[UsedImplicitly]
public class LatviaPostMailBoxAnalyzer : Analyzer
{
    protected string Operator { get; } = "Latvijas Pasts";

    public override string Name => Operator + " Mail boxes";

    public override string Description => "This report checks that all " + Operator + " mail boxex listed on company's website are found on the map." + Environment.NewLine +
                                          "Note that Latvija pasts' website can and do have errors: mainly incorrect position, but sometimes missing or phantom items too.";

    public override AnalyzerGroup Group => AnalyzerGroups.Misc;

    public override List<Type> GetRequiredDataTypes() => new List<Type>()
    {
        typeof(OsmAnalysisData),
        typeof(LatviaPostAnalysisData)
    };
        

    public override void Run(IReadOnlyList<AnalysisData> datas, Report report)
    {
        // Load OSM data

        OsmAnalysisData osmData = datas.OfType<OsmAnalysisData>().First();

        OsmMasterData osmMasterData = osmData.MasterData;
                
        OsmDataExtract osmPostBoxes = osmMasterData.Filter(
            new HasAnyValue("amenity", "post_box")
        );

        // Load Parcel locker data
        List<LatviaPostItem> listedItems  = datas.OfType<LatviaPostAnalysisData>().First().LatviaPostItems;
        
        List<LatviaPostItem> listedBoxes  = listedItems.Where(i => i.ItemType == LatviaPostItemType.PostBox).ToList();

        // Prepare data comparer/correlator

        Correlator<LatviaPostItem> dataComparer = new Correlator<LatviaPostItem>(
            osmPostBoxes,
            listedBoxes,
            new MatchDistanceParamater(100),
            new MatchFarDistanceParamater(200),
            new MatchExtraDistanceParamater(MatchStrength.Strong, 500),
            new DataItemLabelsParamater(Operator + " mail box", Operator + " mail boxes"),
            new OsmElementPreviewValue("name", false),
            new MatchCallbackParameter<LatviaPostItem>(GetMatchStrength)
        );
        
        [Pure]
        MatchStrength GetMatchStrength(LatviaPostItem point, OsmElement element)
        {
            if (point.Address != null)
                if (FuzzyAddressMatcher.Matches(element, point.Address))
                    return MatchStrength.Strong;
                
            return MatchStrength.Good;
        }

        // Parse and report primary matching and location correlation

        dataComparer.Parse(
            report,
            new MatchedPairBatch(),
            new MatchedLoneOsmBatch(true),
            new UnmatchedItemBatch(),
            new MatchedFarPairBatch(),
            new UnmatchedOsmBatch()
        );
    }
}
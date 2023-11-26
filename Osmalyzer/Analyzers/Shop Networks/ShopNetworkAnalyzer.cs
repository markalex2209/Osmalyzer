﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Osmalyzer;

[UsedImplicitly]
public abstract class ShopNetworkAnalyzer<T> : Analyzer where T : ShopListAnalysisData
{
    public override string Name => ShopName + " Shop Networks";

    public override string Description => "This report checks that all " + ShopName + " shops listed on brand's website are found on the map. " +
                                          "This supposes that brand shops are tagged correctly to match among multiple.";


    protected abstract string ShopName { get; }

    protected abstract List<string> ShopOsmNames { get; }


    public override List<Type> GetRequiredDataTypes() => new List<Type>()
    {
        typeof(OsmAnalysisData), 
        typeof(T) // shop list data
    };
        

    public override void Run(IReadOnlyList<AnalysisData> datas, Report report)
    {
        // Load OSM data

        OsmAnalysisData osmData = datas.OfType<OsmAnalysisData>().First();

        OsmMasterData osmMasterData = osmData.MasterData;
                
        OsmDataExtract osmShops = osmMasterData.Filter(
            new HasKey("shop")
        );
        
        OsmDataExtract brandShops = osmShops.Filter(
            new CustomMatch(ShopNameMatches)
        );

        bool ShopNameMatches(OsmElement osmElement1)
        {
            // todo: use known brand data (file)

            string? osmName =
                osmElement1.GetValue("operator") ??
                osmElement1.GetValue("brand") ??
                osmElement1.GetValue("name") ??
                null;

            return osmName != null && ShopOsmNames.Any(sn => osmName.ToLower().Contains(sn.ToLower()));
        }

        // Load Shop data

        ShopListAnalysisData shopData = datas.OfType<ShopListAnalysisData>().First();

        List<ShopData> listedShops = shopData.GetShops();

        // Prepare data comparer/correlator

        Correlator<ShopData> dataComparer = new Correlator<ShopData>(
            brandShops,
            listedShops,
            new MatchDistanceParamater(50),
            new MatchFarDistanceParamater(300), // some are really far from where the data says they ought to be
            new DataItemLabelsParamater(ShopName + " shop", ShopName + " shop"),
            new OsmElementPreviewValue("name", false),
            new LoneElementAllowanceCallbackParameter(_ => true)
        );

        // Parse and report primary matching and location correlation

        dataComparer.Parse(
            report,
            new MatchedItemBatch(),
            new UnmatchedItemBatch(),
            new MatchedFarItemBatch(),
            new UnmatchedOsmBatch()
        );
    }
}
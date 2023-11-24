﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Osmalyzer
{
    [UsedImplicitly]
    public class GlikaOaksAnalyzer : Analyzer
    {
        public override string Name => "Glika Oaks";

        public override string Description => "This report checks that all Glika Ozoli oak trees are mapped.";

        public override List<Type> GetRequiredDataTypes() => new List<Type>() { typeof(OsmAnalysisData), typeof(GlikaOzoliAnalysisData) };


        public override void Run(IReadOnlyList<AnalysisData> datas, Report report)
        {
            // Load OSM data

            OsmAnalysisData osmData = datas.OfType<OsmAnalysisData>().First();

            OsmMasterData osmMasterData = osmData.MasterData;

            OsmDataExtract osmTrees = osmMasterData.Filter(
                new IsNode(),
                new HasValue("natural", "tree")
            );
            
            // Get Oak data

            List<GlikaOak> oaks = datas.OfType<GlikaOzoliAnalysisData>().First().Oaks;
            
            // Prepare data comparer/correlator

            OsmToDataItemQuickComparer<GlikaOak> dataComparer = new OsmToDataItemQuickComparer<GlikaOak>(
                osmTrees,
                oaks,
                new MatchDistanceQuickCompareParamater(15),
                new MatchFarDistanceQuickCompareParamater(75),
                new MatchCallbackQuickCompareParameter<GlikaOak>(DoesOsmTreeMatchOak)
            );
        
            [Pure]
            static bool DoesOsmTreeMatchOak(GlikaOak oak, OsmElement osmTree)
            {
                return osmTree.GetValue("name")?.ToLower().Contains("glika ozols") ?? false;
            }
            
            // Parse and report primary matching and location correlation

            dataComparer.Parse(
                report,
                new MatchedItemQuickComparerReportEntry(),
                new UnmatchedItemQuickComparerReportEntry(),
                new MatchedItemButFarQuickComparerReportEntry()
            );
            
            // todo: denomination
            // todo: species
            // todo: start_date
        }
    }
}
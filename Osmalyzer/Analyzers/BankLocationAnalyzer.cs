﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Osmalyzer;

[UsedImplicitly]
public class BankLocationAnalyzer : Analyzer
{
    public override string Name => "Bank Locations";

    public override string Description => "This report checks that all POIs from bank lists are mapped.";

    public override List<Type> GetRequiredDataTypes() => new List<Type>() { typeof(OsmAnalysisData), typeof(SwedbankPointAnalysisData) };


    public override void Run(IReadOnlyList<AnalysisData> datas, Report report)
    {
        // Load OSM data

        OsmAnalysisData osmData = datas.OfType<OsmAnalysisData>().First();

        OsmMasterData osmMasterData = osmData.MasterData;

        OsmDataExtract osmPoints = osmMasterData.Filter(
            new HasAnyValue("amenity", "atm", "bank")
        );

        OsmDataExtract osmAtms = osmPoints.Filter(
            new HasValue("amenity", "atm")
        );

        OsmDataExtract osmBanks = osmPoints.Filter(
            new HasValue("amenity", "bank")
        );

        // Get Bank data

        List<BankPoint> points = datas.OfType<SwedbankPointAnalysisData>().First().Points;

        // Prepare data comparer/correlator

        Correlator<BankPoint> dataComparer = new Correlator<BankPoint>(
            osmPoints,
            points,
            new MatchDistanceParamater(50),
            new MatchFarDistanceParamater(300), // some are stupidly far, like at the opposite end of a shopping center from the website's point
            new DataItemLabelsParamater("Swedbank POI", "Swedbank POIs"),
            new MatchCallbackParameter<BankPoint>(DoesOsmPointMatchBankPoint)
        );
        
        [Pure]
        static bool DoesOsmPointMatchBankPoint(BankPoint point, OsmElement osmElement)
        {
            string? osmName =
                osmElement.GetValue("operator") ??
                osmElement.GetValue("brand") ??
                osmElement.GetValue("name") ??
                null; 
                
            return osmName != null && osmName.ToLower().Contains("swedbank");
        }
            
        // Parse and report primary matching and location correlation

        dataComparer.Parse(
            report,
            new MatchedItemBatch(),
            new UnmatchedItemBatch(),
            new MatchedFarItemBatch()
        );
        
        //
        // // Parse
        //
        // report.AddGroup(ReportGroup.Issues, "Issues", null, "All POIs appear to be mapped.");
        //
        // report.AddGroup(ReportGroup.Stats, "Matched POIs");
        //
        // const string bankName = "Swedbank";
        //
        // List<(OsmElement node, BankPoint point)> matchedOsmPoints = new List<(OsmElement, BankPoint)>();
        //     
        // foreach (BankPoint bankPoint in points)
        // {
        //     const double seekDistance = 200;
        //     const double acceptDistance = 50;
        //
        //         
        //     OsmDataExtract relevantElements = bankPoint.Type switch
        //     {
        //         BankPointType.Branch                  => osmBanks,
        //         BankPointType.AtmWithdrawalAndDeposit => osmAtms,
        //         BankPointType.AtmWithdrawal           => osmAtms,
        //         _                                     => throw new NotImplementedException()
        //     };
        //
        //     List<OsmElement> closestElements = relevantElements.GetClosestElementsTo(bankPoint.Coord, seekDistance);
        //
        //         
        //     if (closestElements.Count == 0)
        //     {
        //         report.AddEntry(
        //             ReportGroup.Issues,
        //             new IssueReportEntry(
        //                 "No OSM " + bankPoint.TypeString + " found in " + seekDistance + " m range of " +
        //                 bankName + " " + bankPoint.TypeString + " `" + bankPoint.Name + "` " + (bankPoint.Id != null ? "#" + bankPoint.Id + " " : "") + " (`" + bankPoint.Address + "`) at " + bankPoint.Coord.OsmUrl,
        //                 new SortEntryAsc(SortOrder.NoPoint),
        //                 bankPoint.Coord
        //             )
        //         );
        //     }
        //     else
        //     {
        //         OsmElement? matchedOsmPoint = closestElements.FirstOrDefault(t =>
        //                                                                          matchedOsmPoints.All(mp => mp.node != t) &&
        //                                                                          DoesOsmPointMatchBankPoint(t, bankPoint, bankName));
        //     
        //         if (matchedOsmPoint != null)
        //         {
        //             double matchedPointDistance = OsmGeoTools.DistanceBetween(matchedOsmPoint.GetAverageCoord(), bankPoint.Coord);
        //
        //             if (matchedPointDistance > acceptDistance)
        //             {
        //                 report.AddEntry(
        //                     ReportGroup.Issues,
        //                     new IssueReportEntry(
        //                         "Matching OSM " + bankPoint.TypeString + " " + matchedOsmPoint.OsmViewUrl + " found close to " +
        //                         bankName + " " + bankPoint.TypeString + " `" + bankPoint.Name + "` " + (bankPoint.Id != null ? "#" + bankPoint.Id + " " : "") + " (`" + bankPoint.Address + "`), " +
        //                         "but it's far away (" + matchedPointDistance.ToString("F0") + " m), expected at " + bankPoint.Coord.OsmUrl,
        //                         new SortEntryAsc(SortOrder.PointFar),
        //                         bankPoint.Coord
        //                     )
        //                 );
        //             }
        //     
        //             report.AddEntry(
        //                 ReportGroup.Stats,
        //                 new MapPointReportEntry(
        //                     matchedOsmPoint.GetAverageCoord(),
        //                     "`" + bankPoint.Name + "` " + (bankPoint.Id != null ? "#" + bankPoint.Id + " " : "") + " (`" + bankPoint.Address + "`) " +
        //                     "matched " + matchedOsmPoint.OsmViewUrl + " " +
        //                     "at " + matchedPointDistance.ToString("F0") + " m"
        //                 )
        //             );
        //                 
        //             matchedOsmPoints.Add((matchedOsmPoint, bankPoint));
        //                 
        //             // todo: check tags
        //         }
        //         else
        //         {
        //             OsmElement? alreadyMatchedOsmPoint = closestElements.FirstOrDefault(t => DoesOsmPointMatchBankPoint(t, bankPoint, bankName));
        //
        //             if (alreadyMatchedOsmPoint != null)
        //             {
        //                 double matchedPointDistance = OsmGeoTools.DistanceBetween(alreadyMatchedOsmPoint.GetAverageCoord(), bankPoint.Coord);
        //
        //                 if (matchedPointDistance <= acceptDistance)
        //                 {
        //                     BankPoint previousMatch = matchedOsmPoints.First(mp => mp.node == alreadyMatchedOsmPoint).point;
        //                         
        //                     double previousPointDistance = OsmGeoTools.DistanceBetween(alreadyMatchedOsmPoint.GetAverageCoord(), previousMatch.Coord);
        //
        //                     report.AddEntry(
        //                         ReportGroup.Issues,
        //                         new IssueReportEntry(
        //                             "Potentially-matching OSM " + bankPoint.TypeString + " " + alreadyMatchedOsmPoint.OsmViewUrl + " found close to " +
        //                             bankName + " " + bankPoint.TypeString + " `" + bankPoint.Name + "` " + (bankPoint.Id != null ? "#" + bankPoint.Id + " " : "") + " (`" + bankPoint.Address + "`) " +
        //                             "at " + matchedPointDistance.ToString("F0") + " m" +
        //                             ", but it's already matched to another point " +
        //                             " `" + previousMatch.Name + "` " + (previousMatch.Id != null ? "#" + previousMatch.Id + " " : "") + " (`" + previousMatch.Address + "`) " +
        //                             "at " + previousPointDistance.ToString("F0") + " m" +
        //                             ", expected another at " + bankPoint.Coord.OsmUrl,
        //                             new SortEntryAsc(SortOrder.PointRepeat),
        //                             bankPoint.Coord
        //                         )
        //                     );
        //                 }
        //             }
        //         }
        //     }
        //}
    }

    private enum ReportGroup
    {
        Issues,
        Stats
    }

    private enum SortOrder // values used for sorting
    {
        NoPoint = 0,
        PointFar = 1,
        PointRepeat = 2
    }
}
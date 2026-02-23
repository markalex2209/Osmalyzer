namespace Osmalyzer;

[UsedImplicitly]
public class PMLPAnalyzer : Analyzer
{
    public override string Name => "PMLP offices";

    public override string Description => "This report checks that all PMLP offices listed on goverment's website are found on the map.";

    public override AnalyzerGroup Group => AnalyzerGroup.StateServices;

    public override List<Type> GetRequiredDataTypes() =>
    [
        typeof(LatviaOsmAnalysisData),
        typeof(PMLPAnalysisData)
    ];
        

    public override void Run(IReadOnlyList<AnalysisData> datas, Report report)
    {
        // Load OSM data

        LatviaOsmAnalysisData osmData = datas.OfType<LatviaOsmAnalysisData>().First();

        OsmData OsmData = osmData.MasterData;
                
        OsmData osmCourthouses = OsmData.Filter(
            new HasValue("office", "government"),
            new OrMatch(
                new CustomMatch(e => e.GetValue("name")?.Contains("Pilsonības un migrācijas lietu pārvalde") == true),
                new CustomMatch(e => e.GetValue("name")?.Contains("PMLP") == true)
            )
        );

        // Load Courthouse data

        PMLPAnalysisData courthouseData = datas.OfType<PMLPAnalysisData>().First();

        List<CourthouseData> listedCourthouses = courthouseData.Courthouses.ToList();

        // Geolocate courthouses from their addresses
        
        List<LocatedCourthouse> locatedCourthouses = [ ];
        List<CourthouseData> unlocatedCourthouses = [ ];

        foreach (CourthouseData ch in listedCourthouses)
        {
            LocatedCourthouse? located = TryLocateCourthouse(ch, OsmData);
            if (located != null)
                locatedCourthouses.Add(located);
            else
                unlocatedCourthouses.Add(ch);
        }

        // Prepare data comparer/correlator

        Correlator<LocatedCourthouse> correlator = new Correlator<LocatedCourthouse>(
            osmCourthouses,
            locatedCourthouses,
            new MatchDistanceParamater(100), // most data is like 50 meters away
            new MatchFarDistanceParamater(300),
            new MatchExtraDistanceParamater(MatchStrength.Strong, 700), // allow really far for exact matches
            new DataItemLabelsParamater("courthouse", "courthouses"),
            new OsmElementPreviewValue("name", false)
        );
        
        // Parse and report primary matching and location correlation

        CorrelatorReport correlation = correlator.Parse(
            report,
            new MatchedPairBatch(),
            new UnmatchedItemBatch(),
            new MatchedFarPairBatch(),
            new MatchedLoneOsmBatch(true)
        );

        // Offer updates to matched courthouse values (name, phones, email)
        
        List<MatchedCorrelation<LocatedCourthouse>> matchedPairs = correlation.Correlations
            .OfType<MatchedCorrelation<LocatedCourthouse>>()
            .ToList();

        if (matchedPairs.Count > 0)
        {
            List<TagComparison<LocatedCourthouse>> comparisons = [
                new TagComparison<LocatedCourthouse>(
                    "name",
                    lc => lc.Courthouse.Name
                ),
                new TagComparison<LocatedCourthouse>(
                    "email",
                    lc => lc.Courthouse.Email
                ),
                new TagComparison<LocatedCourthouse>(
                    "phone",
                    lc => string.Join(";", lc.Courthouse.Phones),
                    TagUtils.ValuesMatch
                ),
                new TagComparison<LocatedCourthouse>(
                    "opening_hours",
                    lc => lc.Courthouse.OpeningHours,
                    TagUtils.ValuesMatchOrderSensitive // prefer "sorted" days
                )
            ];

            TagSuggester<LocatedCourthouse> suggester = new TagSuggester<LocatedCourthouse>(
                matchedPairs,
                lc => lc.Courthouse.Name,
                "courthouse"
            );

            suggester.Suggest(
                report,
                comparisons
            );
        }

        // Report any courthouses we couldn't geolocate by address
        
        if (unlocatedCourthouses.Count > 0)
        {
            report.AddGroup(
                ExtraReportGroup.UnlocatedCourthouses,
                "Non-geolocated Courthouses",
                "These listed courthouses could not be geolocated to an OSM address. " +
                "Possibly, the data values are incorrect, differently-formatted or otherwise fail to match automatically."
            );

            foreach (CourthouseData unlocated in unlocatedCourthouses)
            {
                report.AddEntry(
                    ExtraReportGroup.UnlocatedCourthouses,
                    new IssueReportEntry(
                        "Courthouse `" + unlocated.Name + "` could not be geolocated for `" + unlocated.Address + "`"
                    )
                );
            }
        }
        
        // List all
        
        report.AddGroup(
            ExtraReportGroup.AllCourthouses,
            "All Courthouses"
        );

        foreach (CourthouseData courthouse in courthouseData.Courthouses)
        {
            report.AddEntry(
                ExtraReportGroup.AllCourthouses,
                new IssueReportEntry(
                    courthouse.ReportString()
                )
            );
        }
    }


    [Pure]
    private static LocatedCourthouse? TryLocateCourthouse(CourthouseData ch, OsmData osmData)
    {
        OsmCoord? coord = FuzzyAddressFinder.Find(
            osmData, 
            ch.Address,
            // all are of form "Aiviekstes iela 6, Rīga, LV-1019"
            new FuzzyAddressStreetLineHint(0), 
            new FuzzyAddressCityHint(1), 
            new FuzzyAddressPostcodeHint(2)
        );

        if (coord == null)
            return null;

        return new LocatedCourthouse(ch, coord.Value);
    }

    private record LocatedCourthouse(CourthouseData Courthouse, OsmCoord Coord) : IDataItem
    {
        public string Name => Courthouse.Name;
        
        public string ReportString() => Courthouse.ReportString();
    }
    
    private enum ExtraReportGroup
    {
        UnlocatedCourthouses,
        AllCourthouses
    }
}
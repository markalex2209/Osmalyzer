using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Osmalyzer;

[UsedImplicitly]
public class OsmAnalysisData : AnalysisData, IDatedAnalysisData
{
    public override string Name => "OSM";

    public override string ReportWebLink => @"https://download.geofabrik.de/europe/latvia.html";

    public override bool NeedsPreparation => true;


    public bool DataDateHasDayGranularity => true;

    protected override string DataFileIdentifier => "osm-latvia";


    public OsmMasterData MasterData { get; private set; } = null!; // only null during initialization


    public DateTime RetrieveDataDate()
    {
        string result = WebsiteDownloadHelper.Read("https://download.geofabrik.de/europe/latvia.html", true);
                
        Match match = Regex.Match(result, @"contains all OSM data up to ([^\.]+)\.");
        string newestDateString = match.Groups[1].ToString(); // will be something like "2023-06-12T20:21:53Z"
            
        return DateTime.Parse(newestDateString);
    }

    protected override void Download()
    {
        WebsiteDownloadHelper.Download(
            "https://download.geofabrik.de/europe/latvia-latest.osm.pbf", 
            Path.Combine(CacheBasePath, DataFileIdentifier + @".osm.pbf")
        );
    }

    protected override void DoPrepare()
    {
        MasterData = new OsmMasterData(Path.Combine(CacheBasePath, DataFileIdentifier + @".osm.pbf"));
    }
}
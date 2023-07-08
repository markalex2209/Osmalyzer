using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Osmalyzer
{
    [UsedImplicitly]
    public class CommonBrandsAnalyzer : Analyzer
    {
        public override string Name => "Common Brands";

        public override string Description => "This report lists the most common brand names and name variations for common amenity/shop/misc POIs.";


        public override List<Type> GetRequiredDataTypes() => new List<Type>() { typeof(OsmAnalysisData) };
        

        public override void Run(IReadOnlyList<AnalysisData> datas, Report report)
        {
            const int titleCountThreshold = 10;

            List<string> titleTags = new List<string>() { "brand", "name", "operator" };
            // Note that the first found is picked, so if there's no brand but is a "name", then "operator" will be ignored and "name" picked

            // Load OSM data

            OsmAnalysisData osmData = datas.OfType<OsmAnalysisData>().First();

            OsmMasterData osmMasterData = osmData.MasterData;
                
            OsmDataExtract titledElements = osmMasterData.Filter(
                new HasAnyKey(titleTags)
            );
            
            // Load NSI tags

            string nsiTagsFileName = @"data/NSI tags.tsv"; // from https://nsi.guide/?t=brands

            if (!File.Exists(nsiTagsFileName))
                nsiTagsFileName = @"../../../../" + nsiTagsFileName; // "exit" Osmalyzer\bin\Debug\net6.0\ folder and grab it from root data\
            
            string[] nsiRawTags = File.ReadAllLines(nsiTagsFileName);

            List<(string, string[])> nsiTags = nsiRawTags.Select(t =>
            {
                int i = t.IndexOf('\t'); 
                return (t.Substring(0, i), t.Substring(i + 1).Split(';').ToArray());
            }).ToList();
            // todo: retrieve automatically from NSI repo or wherever they keep these
            // todo: would need to manually specify exceptions/grouping if parsing
            // todo: this can only group different values for the same key, not different keys

            // Prepare groups

            report.AddGroup(ReportGroup.Main, "Frequent brand names");

            report.AddEntry(
                ReportGroup.Main,
                new DescriptionReportEntry(
                    "These are the most common POI titles with at least " + titleCountThreshold + " occurences grouped by type (recognized by NSI)." + Environment.NewLine +
                    "POI \"title\" here means the first found value from tags " + string.Join(", ", titleTags.Select(t => "`" + t + "`")) + "." + Environment.NewLine +
                    "Title values are case-insensitive, leading/trailing whitespace ignored, Latvian diacritics ignored, character '!' ignored." + Environment.NewLine +
                    "Title counts will repeat if the same element is tagged with multiple NSI POI types."
                )
            );
            
            // todo: report tables?

            // Parse
            
            foreach ((string nsiTag, string[] nsiValues) in nsiTags)
            {
                OsmDataExtract matchingElements = titledElements.Filter(
                    new HasAnyValue(nsiTag, nsiValues)
                );

                OsmGroups titleGroupsSeparate = matchingElements.GroupByValues(titleTags, false);

                OsmMultiValueGroups titleGroupsSimilar = titleGroupsSeparate.CombineBySimilarValues(
                    (s1, s2) => string.Equals(
                        CleanName(s1), 
                        CleanName(s2), 
                        StringComparison.InvariantCulture),
                    true
                );

                string CleanName(string s)
                {
                    return s
                           .Trim()
                           .ToLower()
                           .Replace("!", "") // e.g. Top! -> Top
                           .Replace("ā", "a")
                           .Replace("č", "c")
                           .Replace("ē", "e")
                           .Replace("ģ", "g")
                           .Replace("ī", "i")
                           .Replace("ķ", "k")
                           .Replace("ļ", "l")
                           .Replace("ņ", "n")
                           .Replace("ō", "o")
                           .Replace("š", "s")
                           .Replace("ū", "u")
                           .Replace("ž", "z");
                }

                foreach (OsmMultiValueGroup group in titleGroupsSimilar.groups)
                {
                    if (group.Elements.Count >= titleCountThreshold)
                    {
                        string reportLine;

                        // Title

                        if (group.Values.Count == 1)
                            reportLine = "`" + group.Values[0].value + "`";
                        else
                            reportLine = string.Join(", ", group.Values.Select(v => "`" + v.value + "` (" + v.count + ")"));
                                
                        // Occurrences
                        
                        reportLine += " found " + group.Elements.Count + " times ";

                        // Tags

                        List<(string v, int c)> uniqueNsiValues = group.GetUniqueValuesForKey(nsiTag, true); // just because we grouped NSI POI types, doesn't mean data has instances for each

                        if (uniqueNsiValues.Count == 1)
                            reportLine += "for `" + nsiTag + "=" + uniqueNsiValues[0].v + "`";
                        else
                            reportLine += "for `" + nsiTag + "=` values " + string.Join(", ", uniqueNsiValues.Select(uv => "`" + uv.v + "` (" + uv.c + ")"));

                        // Mismatched names

                        if (group.Values.Count > 1)
                        {
                            int max = group.Values.Max(v => v.count);

                            List<(string value, int count)> reportable = group.Values.Where(v => v.count < 10 && v.count <= max / 2).ToList();

                            if (reportable.Count > 0)
                            {
                                reportLine += " -- " + string.Join("; ", reportable.Select(r => "`" + r.value + "`: " + string.Join(", ", group.GetElementsWithValue(r.value).Select(e => e.OsmViewUrl))));
                            }
                        }


                        if (group.Values.Count > 1)
                        {
                            report.AddEntry(
                                ReportGroup.Main,
                                new IssueReportEntry(
                                    reportLine,
                                    new SortEntryDesc(group.Elements.Count)
                                )
                            );
                        }
                        else
                        {
                            report.AddEntry(
                                ReportGroup.Main,
                                new GenericReportEntry(
                                    reportLine,
                                    new SortEntryDesc(group.Elements.Count)
                                )
                            );
                        }
                    }
                }
            }
        }
        
        private enum ReportGroup
        {
            Main
        }
    }
}

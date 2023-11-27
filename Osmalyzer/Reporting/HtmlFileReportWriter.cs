using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;
using JetBrains.Annotations;

namespace Osmalyzer;

public class HtmlFileReportWriter : ReportWriter
{
    public string ReportFileName { get; private set; } = null!;


    public override void Save(Report report)
    {
        string output = GetOutputTemplate();

        if (!report.NeedMap)
            output = StripSection(output, "MAP");

        string title = HttpUtility.HtmlEncode(report.Name);
        
        output = ReplaceMarker(output, "TITLE", title);
        output = ReplaceMarker(output, "DESCR_TITLE", title);

        string bodyContent = BuildContent(report, title);
        
        output = ReplaceMarker(output, "BODY", bodyContent);

        // Write
        
        ReportFileName = report.Name + " report.html";
            
        string fullReportFileName = Path.Combine(OutputPath, ReportFileName);
            
        File.WriteAllText(fullReportFileName, output);
    }

    
    [Pure]
    private static string BuildContent(Report report, string title)
    {
        // TODO: StringBuilder
        
        string bodyContent = "<h2>Report for " + title + "</h2>" + Environment.NewLine;

        bodyContent += "<p>" + PolishLine(report.Description) + "</p>" + Environment.NewLine;

        List<ReportGroup> groups = report.CollectGroups();

        bool needTOC = groups.Count > 1 && groups.Sum(g => g.TotalEntryCount) > 30;

        if (needTOC)
        {
            bodyContent += "<h3>Sections</h3>" + Environment.NewLine;
            bodyContent += "<ul class=\"custom-list toc-list\">" + Environment.NewLine;
            for (int g = 0; g < groups.Count; g++)
            {
                int importantEntryCount = groups[g].ImportantEntryCount;
                
                bodyContent += @"<li><a href=""#g" + (g + 1) + @""">" + groups[g].Title + "</a>";

                if (importantEntryCount > 0)
                    if (groups[g].ShowImportantEntryCount)
                        bodyContent += @" (" + importantEntryCount + ")";

                bodyContent += @"</li>" + Environment.NewLine;
            }

            bodyContent += "</ul>" + Environment.NewLine;
        }
        
        if (report.NeedMap)
        {
            // Define (shared) leaflet icons
            
            bodyContent += @"<script>" + Environment.NewLine;

            foreach (LeafletIcon leafletIcon in EmbeddedIcons.Icons.OfType<LeafletIcon>())
                AddIcon(leafletIcon.Name, leafletIcon.Size);

            void AddIcon(string iconName, int size)
            {
                bodyContent += @"var " + iconName + " = L.icon({" + Environment.NewLine;
                bodyContent += @"iconUrl: 'icons/" + iconName + ".png'," + Environment.NewLine;
                bodyContent += @"iconSize: ["+size+", "+size+"]," + Environment.NewLine; // size of the icon
                bodyContent += @"iconAnchor: ["+(size/2)+", "+(size/2)+"]," + Environment.NewLine; // point of the icon which will correspond to marker's location
                bodyContent += @"popupAnchor: [2, -"+(size+2)+"]" + Environment.NewLine; // point from which the popup should open relative to the iconAnchor
                //bodyContent += @"shadowUrl: 'icons/leaf-shadow.png'," + Environment.NewLine;
                //bodyContent += @"shadowSize: [50, 64]," + Environment.NewLine; // size of the shadow
                //bodyContent += @"shadowAnchor: [4, 62]," + Environment.NewLine
                bodyContent += @"});" + Environment.NewLine;
            }

            bodyContent += @"</script>" + Environment.NewLine;
        }
            
        for (int g = 0; g < groups.Count; g++)
        {
            ReportGroup group = groups[g];

            if (needTOC)
                bodyContent += @"<h3 id=""g" + (g + 1) + @""">" + group.Title + "</h3>" + Environment.NewLine;
            else
                bodyContent += "<h3>" + group.Title + "</h3>" + Environment.NewLine;

            if (group.DescriptionEntry != null)
                bodyContent += "<p>" + PolishLine(group.DescriptionEntry.Text) + "</p>" + Environment.NewLine;

            if (!group.HaveAnyContentEntries)
                if (group.PlaceholderEntry != null)
                    bodyContent += "<p>" + PolishLine(group.PlaceholderEntry.Text) + "</p>" + Environment.NewLine;

            if (group.MapPointEntries.Count > 0)
            {
                bodyContent += $@"<div class=""map"" id=""map{g}"" style=""width: 1200px; height: 600px;""></div>" + Environment.NewLine;

                bodyContent += @"<script>" + Environment.NewLine;

                bodyContent += @$"var map"+g+$@" = L.map('map{g}').setView([56.906, 24.505], 7);" + Environment.NewLine;
                bodyContent += @"L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {" + Environment.NewLine;
                bodyContent += @"    maxZoom: 21," + Environment.NewLine;
                bodyContent += @"    attribution: '&copy; <a href=""https://www.openstreetmap.org/copyright"">OSM</a>'" + Environment.NewLine;
                bodyContent += @"}).addTo(map"+g+@");" + Environment.NewLine;

                bool clustered = group.MapPointEntries.Count > 100;
                if (clustered)
                {
                    bodyContent += @"var mainMarkerGroup" + g + @" = L.markerClusterGroup(
{
    showCoverageOnHover: false,
    spiderfyOnMaxZoom: false,
    animate: false,
    disableClusteringAtZoom: 14,
    maxClusterRadius: 30,
    iconCreateFunction: function (cluster) {        
        var icon = 'grayCircle';
        var red = false;
        var green = false;
        cluster.getAllChildMarkers().forEach(function (marker) {
            if (marker.group == 'green') green = true;
            if (marker.group == 'red') red = true;
        });
        if (red && green) icon = 'redGreenCircle';
        else if (red) icon = 'redCircle';
        else if (green) icon = 'greenCircle';
        return L.divIcon({ html: '<img src=\'icons/'+icon+'.png\' class=\'clusterIcon\'><span class=\'clusterText\'>' + cluster.getChildCount() + '</span>', className: 'cluster', iconSize: L.point(20, 20) });
    }
}).addTo(map" + g + @");" + Environment.NewLine; // TODO: de-hardcode icon to group relation, have cluster icon define this
                }
                else
                {
                    bodyContent += @"var mainMarkerGroup" + g + @" = L.featureGroup().addTo(map" + g + @");" + Environment.NewLine;
                }

                bodyContent += @"var subMarkerGroup"+g+@" = L.featureGroup().addTo(map"+g+@");" + Environment.NewLine;
                bodyContent += @"map"+g+@".removeLayer(subMarkerGroup"+g+@");" + Environment.NewLine;
                
                foreach (MapPointReportEntry mapPointEntry in group.MapPointEntries)
                {
                    string lat = mapPointEntry.Coord.lat.ToString("F6");
                    string lon = mapPointEntry.Coord.lon.ToString("F6");
                    string text = PolishLine(mapPointEntry.Text).Replace("\"", "\\\"");
                    string mapUrl = @"<a href=\""" + mapPointEntry.Coord.OsmUrl + @"\"" target=\""_blank\"" title=\""Open map at this location\"">🔗</a>";
                    LeafletIcon icon = EmbeddedIcons.Icons.OfType<LeafletIcon>().First(i => i.Styles.Contains(mapPointEntry.Style));
                    bodyContent += $@"L.marker([{lat}, {lon}], {{icon: " + icon.Name + $@"}}).addTo(" + StyleMarkerGroup(icon.Group) + g + $@").bindPopup(""{text} {mapUrl}"").group=""" + IconVisualColorGroup(icon.ColorGroup) + @""";" + Environment.NewLine;

                    [Pure]
                    static string StyleMarkerGroup(LeafletIcon.IconGroup group)
                    {
                        return group switch
                        {
                            LeafletIcon.IconGroup.Main => "mainMarkerGroup",
                            LeafletIcon.IconGroup.Sub  => "subMarkerGroup",

                            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
                        };
                    }
                    
                    [Pure]
                    static string IconVisualColorGroup(ColorGroup group)
                    {
                        return group switch
                        {
                            ColorGroup.Green => "green",
                            ColorGroup.Red   => "red",
                            ColorGroup.Other => "none",

                            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null)
                        };
                    }
                }

                // Hide sub icons/group when zoomed out, only show when close
                bodyContent += @"map"+g+@".on('zoomend', function() {" + Environment.NewLine;
                bodyContent += @"    if (map"+g+@".getZoom() < 14) map"+g+@".removeLayer(subMarkerGroup"+g+@");" + Environment.NewLine;
                bodyContent += @"    else map"+g+@".addLayer(subMarkerGroup"+g+@");" + Environment.NewLine;
                bodyContent += @"});" + Environment.NewLine;
                
                bodyContent += @"map"+g+@".fitBounds(mainMarkerGroup"+g+@".getBounds(), { maxZoom: 12, animate: false });" + Environment.NewLine;

                bodyContent += @"</script>" + Environment.NewLine;
            }

            if (group.IssueEntryCount > 0)
            {
                bodyContent += "<ul class=\"custom-list issues-list\">" + Environment.NewLine;
                foreach (IssueReportEntry entry in group.CollectIssueEntries())
                    bodyContent += "<li>" + PolishLine(entry.Text) + "</li>" + Environment.NewLine;
                bodyContent += "</ul>" + Environment.NewLine;
            }

            if (group.GenericEntryCount > 0)
            {
                bodyContent += "<ul class=\"custom-list notes-list\">" + Environment.NewLine;
                foreach (GenericReportEntry entry in group.CollectGenericEntries())
                    bodyContent += "<li>" + PolishLine(entry.Text) + "</li>" + Environment.NewLine;
                bodyContent += "</ul>" + Environment.NewLine;
            }
        }

        bodyContent += "<h3>Source data</h3>" + Environment.NewLine;
        
        bodyContent += "<ul>" + Environment.NewLine;
        foreach (AnalysisData data in report.Datas)
            bodyContent += "<li>" + DataInfoLine(data) + "</li>" + Environment.NewLine;
        bodyContent += "</ul>" + Environment.NewLine;
        
        bodyContent += "Provided as is; mistakes possible." + Environment.NewLine;

        return bodyContent;
    }

    [Pure]
    private static string PolishLine(string line)
    {
        line = HttpUtility.HtmlEncode(line);
            
        line = Regex.Replace(line, @"(https://www.openstreetmap.org/node/(\d+))", @"<a href=""$1"" class=""osm-link"">Node $2</a>");
        line = Regex.Replace(line, @"(https://www.openstreetmap.org/way/(\d+))", @"<a href=""$1"" class=""osm-link"">Way $2</a>");
        line = Regex.Replace(line, @"(https://www.openstreetmap.org/relation/(\d+))", @"<a href=""$1"" class=""osm-link"">Rel $2</a>");
        line = Regex.Replace(line, @"(https://www.openstreetmap.org/changeset/(\d+))", @"<a href=""$1"" class=""osm-link"">Changeset $2</a>");
        line = Regex.Replace(line, @"(https://www.openstreetmap.org/#map=\d{1,2}/(-?\d{1,3}\.\d+)/(-?\d{1,3}\.\d+))", @"<a href=""$1"" class=""osm-link"">Location $2, $3</a>");
            
        line = Regex.Replace(line, @"(https://overpass-turbo.eu/\?Q=[a-zA-Z0-9%\-_\.!*()+]+)", @"<a href=""$1"">Query</a>");

        line = Regex.Replace(line, @"`([^`]+)`", @"<code class=""osm-tag"">$1</code>");

        line = line.Replace(Environment.NewLine, "<br>");
            
        return line;
    }

    [Pure]
    private static string DataInfoLine(AnalysisData data)
    {
        string s = HttpUtility.HtmlEncode(data.Name);

        if (data is IDatedAnalysisData datedData)
            s += " as of " +
                 (datedData.DataDateHasDayGranularity ?
                     data.DataDate!.Value.ToString("yyyy-MM-dd HH:mm:ss") :
                     data.DataDate!.Value.ToString("yyyy-MM-dd"));
        else
            s += " (undated)";

        if (data.ReportWebLink != null)
            s += @" <a href=""" + data.ReportWebLink + @""" target=\""_blank\"">Link</a>";

        return s;
    }

    [Pure]
    private static string GetOutputTemplate()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        
        const string resourcePath = @"Osmalyzer.Reporting.report template.html";

        using Stream stream = assembly.GetManifestResourceStream(resourcePath)!;
        
        using StreamReader reader = new StreamReader(stream);
        
        return reader.ReadToEnd();
    }

    [Pure]
    private static string StripSection(string output, string marker)
    {
        string fromString = "<!--" + marker + "-->";
        string toString = "<!--END " + marker + "-->";

        int startIndex = output.IndexOf(fromString, StringComparison.Ordinal);
        int endIndex = output.IndexOf(toString, startIndex, StringComparison.Ordinal);
        
        return output[.. startIndex] + output[(endIndex + toString.Length) ..];
    }

    [Pure]
    private static string ReplaceMarker(string output, string marker, string value)
    {
        string markerString = "<!--" + marker + "-->";

        int index = output.IndexOf(markerString, StringComparison.Ordinal);
        
        return output[.. index] + value + output[(index + markerString.Length) ..];
    }
}
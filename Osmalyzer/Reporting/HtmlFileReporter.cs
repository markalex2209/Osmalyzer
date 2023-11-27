using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Web;

namespace Osmalyzer;

public class HtmlFileReporter : Reporter
{
    public override void Save()
    {
        if (Directory.Exists(ReportWriter.OutputPath))
            Directory.Delete(ReportWriter.OutputPath, true);  
                
        Directory.CreateDirectory(ReportWriter.OutputPath);

        using StreamWriter reportFile = File.CreateText(Path.Combine(ReportWriter.OutputPath, "index.html"));
            
        reportFile.WriteLine(@"<!doctype html>");
        reportFile.WriteLine(@"<html>");
        reportFile.WriteLine(@"<head>");
        reportFile.WriteLine(@"<title>Osmalyzer reports</title>");
        reportFile.WriteLine(@"<meta name=""description"" content=""A list of Osmalyzer reports"" />");
        reportFile.WriteLine(@"<meta http-equiv=""Cache-Control"" content=""no-cache, no-store, must-revalidate"" />");
        reportFile.WriteLine(@"<meta http-equiv=""Pragma"" content=""no-cache"" />");
        reportFile.WriteLine(@"<meta http-equiv=""Expires"" content=""0"" />");
            
        reportFile.WriteLine(@"<style>"); // todo: stylesheets? never heard of it
        reportFile.WriteLine(@"body {");
        reportFile.WriteLine(@"  font-family: Arial, sans-serif;");
        reportFile.WriteLine(@"  margin: 0;");
        reportFile.WriteLine(@"  padding: 20px;");
        reportFile.WriteLine(@"  background-color: #f2f2f2;");
        reportFile.WriteLine(@"  color: #333;");
        reportFile.WriteLine(@"}");
        reportFile.WriteLine(@"h1, h2, h3 {");
        reportFile.WriteLine(@"  color: #555;");
        reportFile.WriteLine(@"}");
        reportFile.WriteLine(@"a {");
        reportFile.WriteLine(@"  color: #007bff;");
        reportFile.WriteLine(@"  text-decoration: none;");
        reportFile.WriteLine(@"}");
        reportFile.WriteLine(@"a:hover {");
        reportFile.WriteLine(@"  text-decoration: underline;");
        reportFile.WriteLine(@"}");
        reportFile.WriteLine(@"a:visited {");
        reportFile.WriteLine(@"  color: #1b4b99;");
        reportFile.WriteLine(@"}");
        reportFile.WriteLine(@"</style>");

        reportFile.WriteLine(@"</head>");
        reportFile.WriteLine(@"<body>");
            
        reportFile.WriteLine("<h3>Reports</h3>");

        HtmlFileReportWriter reportWriter = new HtmlFileReportWriter();

        if (reports.Count > 0)
        {
            reportFile.WriteLine("<ul>");

            foreach (Report report in reports)
            {
                Console.Write("Writing report for " + report.Name + "...");

                Stopwatch saveStopwatch = Stopwatch.StartNew();
                
                reportWriter.Save(report);

                Console.WriteLine(" (" + saveStopwatch.ElapsedMilliseconds + " ms)");

                reportFile.WriteLine("<li><a href=\"" + reportWriter.ReportFileName + "\">" + HttpUtility.HtmlEncode(report.Name) + "</a></li>");
            }

            reportFile.WriteLine("</ul>");
        }
        else
        {
            reportFile.WriteLine("<p>No reports were generated.</p>");
        }

        if (skippedReports.Count > 0)
        {
            reportFile.WriteLine("<h4>Skipped</h4>");

            foreach ((string report, string reason) in skippedReports)
                reportFile.WriteLine("<li>" + HttpUtility.HtmlEncode(report) + " - " + HttpUtility.HtmlEncode(reason) + "</li>");
        }

        reportFile.WriteLine("<p>Reports generated " + DateTime.UtcNow.ToString("R") + ".</p>");

            
        reportFile.WriteLine("<h3>Disclaimer</h3>");

        reportFile.WriteLine("<p>Reports look for specific problems, but the very nature of OSM free tagging and public editing means there are endless possibilities. " +
                             "Thus no report is complete or exhaustive - there are false positives and false negatives.</p>");
        reportFile.WriteLine("<p>If you are fixing anything based on this, it's your responsibility to understand the actual issue and why and how it should be fixed. " +
                             "Not everything identified as an issue is an issue.</p>");
        reportFile.WriteLine("<p>This supposes that you are familiar with OSM and tagging. Reports can and do omit a lot of explanations.</p>");
        reportFile.WriteLine("<p>The sources used are almost always out of date. " +
                             "OSM may lag a day or more behind, while various other providers may be months or even years out of date.</p>");
        reportFile.WriteLine("<p>Make sure you understand licensing, copyright and the exact terms of use for each source. " +
                             "OSM does not allow data from incompatable licenses. " +
                             "These reports may use publicly-available data, but this does not mean it's freely-usable. " +
                             "These reports are merely an indication of problems and not an invitation to change anything en masse. " +
                             "You are still responsible for your edits and by using any information from these reports, " +
                             "you may also be indirectly using data from such sources that you may not have permission to use in this way.</p>");

        reportFile.WriteLine(@"</body>");
        reportFile.WriteLine(@"</html>");

        reportFile.Close();


        CopyIconsForLeaflet();
    }


    private void CopyIconsForLeaflet()
    {
        foreach (EmbeddedIcon embeddedIcon in EmbeddedIcons.Icons)
            CopyIcon(embeddedIcon);

        return;
        
        
        static void CopyIcon(EmbeddedIcon embeddedIcon)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            const string resourcePrefix = @"Osmalyzer.Reporting.HTML_report_resources.";
            string resourcePath = resourcePrefix + embeddedIcon.Name + ".png";

            using Stream stream = assembly.GetManifestResourceStream(resourcePath)!;

            string outputPath = Path.Combine(ReportWriter.OutputPath, @"icons");
        
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);  
        
            FileStream fileStream = new FileStream(Path.Combine(outputPath, embeddedIcon.Name + ".png"), FileMode.Create);
            StreamWriter streamWriter = new StreamWriter(fileStream);
            stream.CopyTo(streamWriter.BaseStream);
            streamWriter.Close();
            fileStream.Close();
        }
    }
}
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Osmalyzer
{
    public class Report
    {
        public string AnalyzerName { get; }
        
        public string? AnalyzerDescription { get; }

        public string? AnalyzedDataDates { get; }

        
        public ReadOnlyCollection<string> RawLines => _rawLines.AsReadOnly();
        // TODO: remove this


        private readonly List<string> _rawLines = new List<string>();
        
        private readonly List<ReportGroup> _groups = new List<ReportGroup>();


        public Report(Analyzer analyzer, IEnumerable<AnalysisData> datas)
        {
            AnalyzerName = analyzer.Name;
            
            AnalyzerDescription = analyzer.Description;

            List<AnalysisData> datasWithDate = datas.Where(d => d.DataDate != null).ToList();

            if (datasWithDate.Count > 0)
                AnalyzedDataDates = string.Join(", ", datasWithDate.Select(d => (d.DataDateHasDayGranularity!.Value ? d.DataDate!.Value.ToString("yyyy-MM-dd HH:mm:ss") : d.DataDate!.Value.ToString("yyyy-MM-dd")) + (datasWithDate.Count > 1 ? " (" + d.Name + ")" : "")));
        }


        public void WriteRawLine(string line)
        {
            _rawLines.Add(line);
        }

        public void AddGroup(string name, string description)
        {
            _groups.Add(new ReportGroup(name, description));
        }

        public void WriteEntry(string groupName, string text)
        {
            if (_groups.All(g => g.Name != groupName)) throw new InvalidOperationException("Group \"" + groupName + "\" has not been created!");
            
            
            ReportGroup group = _groups.First(g => g.Name == groupName);

            group.AddEntry(new ReportEntry(text));
        }

        public List<ReportGroup> CollectEntries()
        {
            // TODO: organize
            
            return _groups.ToList();
        }


        public class ReportGroup
        {
            public string Name { get; }
            
            public string Description { get; }


            public ReadOnlyCollection<ReportEntry> Entries => _entries.AsReadOnly();


            private readonly List<ReportEntry> _entries = new List<ReportEntry>();


            public ReportGroup(string name, string description)
            {
                Name = name;
                Description = description;
            }

            public void AddEntry(ReportEntry newEntry)
            {
                _entries.Add(newEntry);
            }
        }

        public class ReportEntry
        {
            public string Text { get; }
            

            public ReportEntry(string text)
            {
                Text = text;
            }
        }
    }
}
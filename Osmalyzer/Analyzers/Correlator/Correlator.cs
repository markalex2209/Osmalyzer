﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Osmalyzer
{
    /// <summary>
    /// Match OSM elements to custom data items, such as coming from some source.
    /// Reusable generic logic for locating and matching items on the map and finding common problems. 
    /// </summary>
    public class Correlator<T> where T : ICorrelatorItem
    {
        private readonly OsmDataExtract _osmElements;
        
        private readonly List<T> _dataItems;
        
        private readonly CorrelatorParamater[] _paramaters;


        public Correlator(
            OsmDataExtract osmElements, 
            List<T> dataItems, 
            params CorrelatorParamater[] paramaters)
        {
            if (osmElements == null) throw new ArgumentNullException(nameof(osmElements));
            if (dataItems == null) throw new ArgumentNullException(nameof(dataItems));
            
            _osmElements = osmElements;
            _dataItems = dataItems;
            _paramaters = paramaters;
        }


        public CorrelatorReport<T> Parse(Report report, params CorrelatorBatch[] entries)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            
            // See what sort of filters we have and which matching logic we will need to do (and report)
            
            bool shouldReportMatchedItem = entries.OfType<MatchedItemCBatch>().Any();
            bool shouldReportMatchedItemFar = entries.OfType<MatchedFarItemBatch>().Any();
            bool shouldReportUnmatchedItem = entries.OfType<UnmatchedItemBatch>().Any();
            bool shouldReportUnmatchedOsm = entries.OfType<UnmatchedOsmBatch>().Any();

            // Gather (optional) parameters (or set defaults)
            
            double matchDistance = _paramaters.OfType<MatchDistanceParamater>().FirstOrDefault()?.Distance ?? 15;
            double unmatchDistance = _paramaters.OfType<MatchFarDistanceParamater>().FirstOrDefault()?.FarDistance ?? 75;
            Func<T, OsmElement, bool>? matchCallback = _paramaters.OfType<MatchCallbackParameter<T>>().FirstOrDefault()?.MatchCallback ?? null;
            Func<OsmElement, bool>? loneElementAllowanceCallback = _paramaters.OfType<LoneElementAllowanceCallbackParameter>().FirstOrDefault()?.AllowanceCallback ?? null;
            
            // Prepare report groups

            if (shouldReportUnmatchedItem || shouldReportUnmatchedOsm || shouldReportMatchedItemFar)
            {
                report.AddGroup(
                    ReportGroup.Unmatched,
                    "Unmatched items",
                    "This lists the items and elements that could not be matched to each other.",
                    "All elements appear to be mapped."
                );
            }

            if (shouldReportMatchedItem)
            {
                report.AddGroup(
                    ReportGroup.MatchedOsm, 
                    "Matched items",
                    "This displays a map of all the items that were matched to each other."
                );
            }

            // Go

            Dictionary<OsmElement, T> matchedElements = new Dictionary<OsmElement, T>();
            
            foreach (T dataItem in _dataItems)
            {
                List<OsmNode> closestOsmElements = _osmElements.GetClosestNodesTo(dataItem.Coord, unmatchDistance);

                if (closestOsmElements.Count == 0)
                {
                    if (shouldReportUnmatchedItem)
                    {
                        report.AddEntry(
                            ReportGroup.Unmatched,
                            new IssueReportEntry(
                                "No OSM element found in " + unmatchDistance + " m range of " +
                                dataItem.ReportString() + " at " + dataItem.Coord.OsmUrl,
                                new SortEntryAsc(SortOrder.NoItem),
                                dataItem.Coord
                            )
                        );
                    }
                }
                else
                {
                    OsmNode? matchedOsmElement = closestOsmElements.FirstOrDefault(t => matchCallback == null || matchCallback(dataItem, t));
                    
                    if (matchedOsmElement != null)
                    {
                        matchedElements.Add(matchedOsmElement, dataItem);

                        double matchedOsmElementDistance = OsmGeoTools.DistanceBetween(matchedOsmElement.coord, dataItem.Coord);

                        if (matchedOsmElementDistance > matchDistance)
                        {
                            if (shouldReportMatchedItemFar)
                            {
                                report.AddEntry(
                                    ReportGroup.Unmatched,
                                    new IssueReportEntry(
                                        "Matching OSM element " +
                                        OsmElementReportText(matchedOsmElement) + " found close to " +
                                        dataItem.ReportString() + ", " +
                                        "but it's far away (" + matchedOsmElementDistance.ToString("F0") + " m), expected at " + dataItem.Coord.OsmUrl,
                                        new SortEntryAsc(SortOrder.ElementFar),
                                        dataItem.Coord
                                    )
                                );
                            }
                        }

                        if (shouldReportMatchedItem)
                        {
                            report.AddEntry(
                                ReportGroup.MatchedOsm,
                                new MapPointReportEntry(
                                    matchedOsmElement.coord,
                                    dataItem.ReportString() + " matched " +
                                    OsmElementReportText(matchedOsmElement) +
                                    " at " + matchedOsmElementDistance.ToString("F0") + " m"
                                )
                            );
                        }
                    }
                }
            }
            
            foreach (OsmElement osmElement in _osmElements.Elements)
            {
                if (matchedElements.ContainsKey(osmElement))
                    continue;

                bool allowedByItself =
                    loneElementAllowanceCallback != null &&
                    loneElementAllowanceCallback(osmElement);
                
                if (!allowedByItself)
                {
                    if (shouldReportUnmatchedOsm)
                    {
                        report.AddEntry(
                            ReportGroup.Unmatched,
                            new IssueReportEntry(
                                "No item found in " + unmatchDistance + " m range of OSM element " +
                                OsmElementReportText(osmElement),
                                new SortEntryAsc(SortOrder.NoOsmElement),
                                osmElement.GetAverageCoord()
                            )
                        );
                        
                        // TODO: report closest (unmatched) data item (these could be really far, so limit distance)
                    }
                }
                else
                {
                    if (shouldReportMatchedItem)
                    {
                        report.AddEntry(
                            ReportGroup.MatchedOsm,
                            new MapPointReportEntry(
                                osmElement.GetAverageCoord(),
                                "Matched OSM element by itself " +
                                OsmElementReportText(osmElement)
                            )
                        );
                    }
                }
            }
            
            // Return a report about what we parsed and found

            return new CorrelatorReport<T>(matchedElements);
        }

        
        [Pure]
        private static string OsmElementReportText(OsmElement element)
        {
            return 
                (element.HasKey("name") ? "`" + element.GetValue("name") + "` " : "") + 
                element.OsmViewUrl;
        }


        private enum ReportGroup
        {
            Unmatched = -10, // probably before analyzer extra issues
            MatchedOsm = 100 // probably after analyzer issues
        }        
        
        private enum SortOrder // values used for sorting
        {
            NoItem = 0,
            NoOsmElement = 0,
            ElementFar = 1
        }
    }
}
﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using OsmSharp;

namespace Osmalyzer
{
    public class OsmRelation : OsmElement
    {
        [PublicAPI]
        public IReadOnlyList<OsmRelationMember> Members => members.AsReadOnly();

        /// <summary>
        ///
        /// This will not contain null/missing elements, even if some are not loaded.
        /// </summary>
        [PublicAPI]
        public IEnumerable<OsmElement> Elements => members.Where(m => m.Element != null).Select(m => m.Element)!;
        
        
        internal readonly List<OsmRelationMember> members;


        internal OsmRelation(OsmGeo RawElement)
            : base(RawElement)
        {
            members = ((Relation)RawElement).Members.Select(m => new OsmRelationMember(this, m.Type, m.Id, m.Role)).ToList();
        }

        
        public OsmPolygon GetOuterWayPolygon()
        {
            List<OsmWay> outerWays = GetOuterWays();

            outerWays = OsmAlgorithms.SortWays(outerWays);

            List<OsmNode> nodes = OsmAlgorithms.CollectNodes(outerWays);

            return new OsmPolygon(nodes.Select(n => (n.Lat, n.Lon)).ToList());
        }

        public List<OsmWay> GetOuterWays()
        {
            List<OsmWay> outerWays = new List<OsmWay>();

            foreach (OsmRelationMember member in Members)
            {
                if (member.Element is OsmWay wayElement && member.Role == "outer")
                {
                    outerWays.Add(wayElement);
                }
            }

            return outerWays;
        }
    }
}
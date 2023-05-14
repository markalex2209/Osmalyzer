﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using OsmSharp;
using OsmSharp.Streams;

namespace Osmalyzer
{
    public class OsmBlob
    {
        public IReadOnlyList<OsmElement> Elements => _elements.AsReadOnly();


        private readonly List<OsmElement> _elements;


        public OsmBlob(string dataFileName, params OsmFilter[] filters)
        {
            _elements = new List<OsmElement>();

            using FileStream fileStream = new FileInfo(dataFileName).OpenRead();

            using PBFOsmStreamSource source = new PBFOsmStreamSource(fileStream);

            foreach (OsmGeo element in source)
                if (OsmElementMatchesFilters(element, filters))
                    _elements.Add(new OsmElement(element));
        }


        private OsmBlob(List<OsmElement> elements)
        {
            _elements = elements;
        }


        [Pure]
        public OsmBlob Filter(params OsmFilter[] filters)
        {
            List<OsmElement> filteredElements = new List<OsmElement>();

            foreach (OsmElement element in _elements)
                if (OsmElementMatchesFilters(element.Element, filters))
                    filteredElements.Add(element);

            return new OsmBlob(filteredElements);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="split">Split semicolon-delimited OSM values, e.g. "gravel;asphalt". This is only useful for tags that are actually allowed top have multiple values.</param>
        /// <returns></returns>
        public OsmGroups GroupByValues(string tag, bool split)
        {
            List<OsmGroup> groups = new List<OsmGroup>();

            Dictionary<string, int> indices = new Dictionary<string, int>(); // for fast lookup

            List<string> values = new List<string>();

            foreach (OsmElement element in _elements)
            {
                if (element.Element.Tags != null &&
                    element.Element.Tags.ContainsKey(tag))
                {
                    values.Clear();

                    string rawValue = element.Element.Tags.GetValue(tag);

                    if (split)
                        values.AddRange(TagUtils.SplitValue(rawValue));
                    else
                        values.Add(rawValue);

                    foreach (string value in values)
                    {
                        if (indices.TryGetValue(value, out int index))
                        {
                            groups[index].Elements.Add(element);
                        }
                        else
                        {
                            OsmGroup newGroup = new OsmGroup(value);

                            newGroup.Elements.Add(element);

                            groups.Add(newGroup);

                            indices.Add(value, groups.Count - 1);
                        }
                    }
                }
            }

            return new OsmGroups(groups);
        }

        public List<string> GetUniqueValues(string tag)
        {
            List<string> values = new List<string>();

            foreach (OsmElement element in _elements)
            {
                if (element.Element.Tags != null &&
                    element.Element.Tags.ContainsKey(tag))
                {
                    string value = element.Element.Tags.GetValue(tag);

                    if (!values.Contains(value))
                        values.Add(value);
                }
            }

            return values;
        }

        /// <summary>
        ///
        /// 1 2 3 4 - 2 3 5 = 1 4
        /// </summary>
        public OsmBlob Subtract(OsmBlob other)
        {
            List<OsmElement> elements = new List<OsmElement>();

            foreach (OsmElement element in Elements)
                if (!other.Elements.Contains(element))
                    elements.Add(element);

            return new OsmBlob(elements);
        }

        
        [Pure]
        private static bool OsmElementMatchesFilters(OsmGeo element, params OsmFilter[] filters)
        {
            bool matched = true;

            foreach (OsmFilter filter in filters)
            {
                if (!filter.Matches(element))
                {
                    matched = false;
                    break;
                }
            }

            return matched;
        }
    }

    public class OsmGroups
    {
        public readonly List<OsmGroup> groups;


        public OsmGroups(List<OsmGroup> groups)
        {
            this.groups = groups;
        }


        public void SortGroupsByElementCountAsc()
        {
            groups.Sort((g1, g2) => g1.Elements.Count.CompareTo(g2.Elements.Count));
        }

        public void SortGroupsByElementCountDesc()
        {
            groups.Sort((g1, g2) => g2.Elements.Count.CompareTo(g1.Elements.Count));
        }
    }

    public class OsmGroup
    {
        public string Value { get; }

        public List<OsmElement> Elements { get; } = new List<OsmElement>();


        public OsmGroup(string value)
        {
            Value = value;
        }
    }

    public class OsmElement
    {
        public OsmGeo Element { get; }
        // todo: as "raw"
        // todo: encapsulate


        public OsmElement(OsmGeo element)
        {
            Element = element;
        }
    }

    public abstract class OsmFilter
    {
        internal abstract bool Matches(OsmGeo element);
    }

    public class IsNode : OsmFilter
    {
        internal override bool Matches(OsmGeo element)
        {
            return element.Type == OsmGeoType.Node;
        }
    }

    public class IsWay : OsmFilter
    {
        internal override bool Matches(OsmGeo element)
        {
            return element.Type == OsmGeoType.Way;
        }
    }

    public class IsRelation : OsmFilter
    {
        internal override bool Matches(OsmGeo element)
        {
            return element.Type == OsmGeoType.Relation;
        }
    }

    public class IsNodeOrWay : OsmFilter
    {
        internal override bool Matches(OsmGeo element)
        {
            return 
                element.Type == OsmGeoType.Node ||
                element.Type == OsmGeoType.Way;
        }
    }

    public class HasTag : OsmFilter
    {
        private readonly string _tag;


        public HasTag(string tag)
        {
            _tag = tag;
        }


        internal override bool Matches(OsmGeo element)
        {
            return
                element.Tags != null &&
                element.Tags.ContainsKey(_tag);
        }
    }

    public class DoesntHaveTag : OsmFilter
    {
        private readonly string _tag;


        public DoesntHaveTag(string tag)
        {
            _tag = tag;
        }


        internal override bool Matches(OsmGeo element)
        {
            return
                element.Tags == null ||
                !element.Tags.ContainsKey(_tag);
        }
    }

    public class HasValue : OsmFilter
    {
        private readonly string _tag;
        private readonly string _value;


        public HasValue(string tag, string value)
        {
            _tag = tag;
            _value = value;
        }


        internal override bool Matches(OsmGeo element)
        {
            return
                element.Tags != null &&
                element.Tags.Contains(_tag, _value);
        }
    }

    public class SplitValuesMatchRegex : OsmFilter
    {
        private readonly string _tag;
        private readonly string _pattern;


        public SplitValuesMatchRegex(string tag, string pattern)
        {
            _tag = tag;
            _pattern = pattern;
        }


        internal override bool Matches(OsmGeo element)
        {
            if (element.Tags == null)
                return false;

            string rawValue = element.Tags.GetValue(_tag);

            List<string> splitValues = TagUtils.SplitValue(rawValue);

            if (splitValues.Count == 0)
                return false;

            foreach (string splitValue in splitValues)
                if (!Regex.IsMatch(splitValue, _pattern))
                    return false;

            return true;
        }
    }

    public class SplitValuesCheck : OsmFilter
    {
        private readonly string _tag;
        private readonly Func<string, bool> _check;


        public SplitValuesCheck(string tag, Func<string, bool> check)
        {
            _tag = tag;
            _check = check;
        }


        internal override bool Matches(OsmGeo element)
        {
            if (element.Tags == null)
                return false;

            string rawValue = element.Tags.GetValue(_tag);

            List<string> splitValues = TagUtils.SplitValue(rawValue);

            if (splitValues.Count == 0)
                return false;

            foreach (string splitValue in splitValues)
                if (!_check(splitValue))
                    return false;

            return true;
        }
    }
}
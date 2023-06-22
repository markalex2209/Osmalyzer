﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Osmalyzer
{
    public abstract class Analyzer
    {
        public abstract string Name { get; }
        
        public abstract string? Description { get; }


        public abstract List<Type> GetRequiredDataTypes();

        public abstract void Run(IEnumerable<AnalysisData> datas, Report report);
    }
}
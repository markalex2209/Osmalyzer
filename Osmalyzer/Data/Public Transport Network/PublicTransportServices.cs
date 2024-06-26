﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Osmalyzer;

public class PublicTransportServices
{
    public IEnumerable<PublicTransportService> Services => _services.Values.AsEnumerable();

        
    private readonly Dictionary<string, PublicTransportService> _services;

        
    public PublicTransportServices(string dataFileName)
    {
        _services = new Dictionary<string, PublicTransportService>();

        string[] lines = File.ReadAllLines(dataFileName);

        for (int i = 0; i < lines.Length; i++)
        {
            if (i == 0) // header row
                continue;
            
            string line = lines[i];
            // service_id,monday,tuesday,wednesday,thursday,friday,saturday,sunday,start_date,end_date
            // 24837,0,0,0,0,0,1,1,20230415,20240401

            List<string> segments = line.Split(',').Select(s => s.Trim()).ToList();

            // service_id - 24837
            // monday - 0
            // tuesday - 0
            // wednesday - 0
            // thursday - 0
            // friday - 0
            // saturday - 1
            // sunday - 1
            // start_date - 20230415
            // end_date - 20240401

            string serviceId = segments[0];

            PublicTransportService service = new PublicTransportService(serviceId);
            _services.TryAdd(service.Id, service);
        }
    }

        
    [Pure]
    public PublicTransportService? GetService(string id)
    {
        if (_services.TryGetValue(id, out PublicTransportService? s))
            return s;
            
        return null;
    }
}
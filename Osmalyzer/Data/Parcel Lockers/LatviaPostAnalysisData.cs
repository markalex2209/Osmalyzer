﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

namespace Osmalyzer;

[UsedImplicitly]
public class LatviaPostAnalysisData : AnalysisData, IParcelLockerListProvider
{
    public override string Name => "Latvijas Pasts";

    public override bool NeedsPreparation => true;

    public override string ReportWebLink => @"https://pasts.lv/lv/kategorija/pasta_nodalas/";

    protected override string DataFileIdentifier => "latvia-post";

    public string DataFileName => Path.Combine(CacheBasePath, DataFileIdentifier + @".json");

    public List<LatviaPostItem> LatviaPostItems{ get; private set; } = null!; // only null until prepared

    public IEnumerable<ParcelLocker> ParcelLockers
    { 
        get { return LatviaPostItems.Where(i => i.ItemType == LatviaPostItemType.ParcelLocker); } 
    }

    
    protected override void Download()
    {
        // list at https://pasts.lv/lv/kategorija/pasta_nodalas/
        // query to get json data at https://pasts.lv/ajax/module:post_office/

        WebsiteBrowsingHelper.DownloadPage( // direct download can fail both locally and on remote
            "https://pasts.lv/ajax/module:post_office/", 
            DataFileName
        );
    }

    protected override void DoPrepare()
    {
        LatviaPostItems = new List<LatviaPostItem>();

        string source = File.ReadAllText(DataFileName);

        // Due to headless browsing, it could/will be wrapped in boilerplate HTML that we need to strip
        source = WebsiteBrowsingHelper.TryUnwrapJsonFromBoilerplateHtml(source);

        dynamic content;
        
        try
        {
            content = JsonConvert.DeserializeObject<dynamic>(source)!;
        }
        catch (JsonException)
        {
            Console.WriteLine("JSON exception!");
            Console.WriteLine("We were trying to parse: " + (source.Length <= 200 ? source : source[..200] + " [" + (source.Length - 200) + "]..."));
            throw;
        }
        
        
        string serialisedArray = content.all;

        // Format of data: {"all":"<serialised json array of POIs>","count":1287}

        dynamic[] jsonItems = JsonConvert.DeserializeObject<dynamic[]>(serialisedArray)!;

        foreach (dynamic item in jsonItems)
        {
            // Expecting item to be:
            // {
            //     "tmpLat":57.077432765182,
            //     "tmpLong":24.323845390354,
            //     "tmpName":"\\u0100da\\u017eu pasta noda\\u013ca",
            //     "tmpNameFull":"\\u0100da\\u017eu pasta noda\\u013ca LV-2164",
            //     "tmpAddress":"Gaujas iela 11, \\u0100da\\u017ei, \\u0100da\\u017eu nov., LV-2164",
            //     "tmpCategory":1,
            //     "tmpDistrict":"\\u0100da\\u017eu nov.",
            //     "tmpPhone":"67008001",
            //     "tmpService":"LV-2164",
            //     "tmpImage":null,
            //     "tmpWork":"P 8:00-18:00<br \\/>O 8:00-18:00<br \\/>T 8:00-19:00<br \\/>C 8:00-18:00<br \\/>P 8:00-18:00<br \\/>S -<br \\/>Sv -<br \\/>",
            //     "tmpPayment":"1",
            //     "tmpFilterOut":false,
            //     "tmpMarker":null,
            //     "tmpInfo":null
            // },
            
            string name = item.tmpName;
            string address = item.tmpAddress;
            string code = item.tmpService;
            double lat = item.tmpLat;
            double lon = item.tmpLong;
            int type = item.tmpCategory;

            LatviaPostItems.Add(
                new LatviaPostItem(
                    name,
                    address,
                    code,
                    parseTypeOfItem(type),
                    new OsmCoord(lat, lon)
                )
            );
        }
    }

    private LatviaPostItemType parseTypeOfItem(int itemType)
    {
        switch(itemType)
        {
            case 1: 
                return LatviaPostItemType.Office;
            case 2:
                return LatviaPostItemType.CircleK;
            case 4:
                return LatviaPostItemType.PostBox;
            case 5:
                return LatviaPostItemType.ParcelLocker;
            case 6:
                return LatviaPostItemType.ServiceOnRequest;
            default:
                throw new ArgumentException(String.Format("Value {0} is unexpected", itemType));
        }
    }
}
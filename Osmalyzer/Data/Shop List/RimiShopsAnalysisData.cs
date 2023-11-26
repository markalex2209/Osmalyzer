﻿using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Osmalyzer;

[UsedImplicitly]
public class RimiShopsAnalysisData : ShopListAnalysisData
{
    public override string Name => "Rimi Shops";

    protected override string DataFileIdentifier => "shops-rimi";


    public override string DataFileName => cacheBasePath + DataFileIdentifier + @".html";

    public override string ShopListUrl => "https://www.rimi.lv/veikali";


    public override List<ShopData> GetShops()
    {
        // APP.shops.list = [{"id":159426,"url":"https:\/\/www.rimi.lv\/veikali\/rimi-galerija-centrs","full_name":"Rimi Galerija centrs","business_name":"rimi galerija centrs","address_line_1":"audeju iela 16","keywords":null,"locality":"riga","icon":"https:\/\/rimibaltic-web-res.cloudinary.com\/image\/upload\/c_fit,f_auto,h_48,q_auto,w_48\/v1\/web-cms\/fd86ab20bf713e2cd42f2fbbe6a01a9459c20b4b.png","longitude":"24.11271384","latitude":"56.94801025","most_visited":1,"display":"\u201eRimi Galerija centrs\u201c, Aud\u0113ju iela 16, R\u012bga"},   
                
        string source = File.ReadAllText(DataFileName);

        Match listMatch = Regex.Match(
            source,
            @"APP\.shops\.list = \[([^\]]+?)\]"
        );
        
        MatchCollection matches = Regex.Matches(
            listMatch.Groups[1].ToString(), 
            @",?\{([^\}]+?)\}"
        );
        
        List<ShopData> listedShops = new List<ShopData>();
                
        foreach (Match match in matches)
        {
            string raw = match.Groups[1].ToString();

            // "id":159427,"url":"https:\/\/www.rimi.lv\/veikali\/rimi-dole","full_name":"Rimi Dole","business_name":"rimi dole","address_line_1":"maskavas iela 357","keywords":null,"locality":"riga","icon":"https:\/\/rimibaltic-web-res.cloudinary.com\/image\/upload\/c_fit,f_auto,h_48,q_auto,w_48\/v1\/web-cms\/fd86ab20bf713e2cd42f2fbbe6a01a9459c20b4b.png","longitude":"24.1909338","latitude":"56.90560051","most_visited":1,"display":"\u201eRimi Dole\u201c, Maskavas iela 357, R\u012bga"

            raw = Regex.Unescape(raw); // the string is JSON, but it's basically the same as Regex escaping

            // "id":159427,"url":"https://www.rimi.lv/veikali/rimi-dole","full_name":"Rimi Dole","business_name":"rimi dole","address_line_1":"maskavas iela 357","keywords":null,"locality":"riga","icon":"https://rimibaltic-web-res.cloudinary.com/image/upload/c_fit,f_auto,h_48,q_auto,w_48/v1/web-cms/fd86ab20bf713e2cd42f2fbbe6a01a9459c20b4b.png","longitude":"24.1909338","latitude":"56.90560051","most_visited":1,"display":"„Rimi Dole“, Maskavas iela 357, Rīga"
            
            string display = Regex.Match(raw, @"""display"":""([^""]+)""").Groups[1].ToString();
            //string name = Regex.Match(raw, @"""full_name"":""([^""]+)""").Groups[1].ToString();
            double lat = double.Parse(Regex.Match(raw, @"""latitude"":""([^""]+)""").Groups[1].ToString());
            double lon = double.Parse(Regex.Match(raw, @"""longitude"":""([^""]+)""").Groups[1].ToString());

            listedShops.Add(new ShopData(display, new OsmCoord(lat, lon)));
        }

        return listedShops;
    }
}
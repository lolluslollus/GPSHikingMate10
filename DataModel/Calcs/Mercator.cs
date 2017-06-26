using LolloGPS.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LolloGPS.Calcs
{
    public static class PseudoMercator
    {
        /*
         GlobalMercator (based on EPSG:900913 = EPSG:3857) (deprecated EPSG:3785) is for Google Maps, Yahoo Maps, Microsoft Maps compatible tiles
         GlobalGeodetic (based on EPSG:4326) is for OpenLayers Base Map and Google Earth compatible tiles 
        */
        // LOLLO NOTE check the mercator formulas at http://wiki.openstreetmap.org/wiki/Mercator
        // and http://wiki.openstreetmap.org/wiki/EPSG:3857
        // and http://www.maptiler.org/google-maps-coordinates-tile-bounds-projection/

        public static readonly double TO_RAD = ConstantData.DEG_TO_RAD;
        public static readonly double TO_DEG = ConstantData.RAD_TO_DEG;
        public static int Lon2TileX(double lonDeg, int zoom)
        {
            //                   N * (lon + 180) / 360
            return Math.Max((int)(Math.Floor((lonDeg + 180.0) / 360.0 * Math.Pow(2.0, zoom))), 0);
        }
        public static int Lat2TileY(double latDeg, int zoom)
        {
            //                   N *  { 1 - log[ tan ( lat ) + sec ( lat ) ] / Pi } / 2
            //      sec(x) = 1 / cos(x)
            //return (int)(Math.Floor((1.0 - Math.Log(Math.Tan(latDeg * Math.PI / 180.0) + 1.0 / Math.Cos(latDeg * Math.PI / 180.0)) / Math.PI) / 2.0 * Math.Pow(2.0, z)));
            return Math.Max((int)(Math.Floor((1.0 - Math.Log(Math.Tan(latDeg * TO_RAD) + 1.0 / Math.Cos(latDeg * TO_RAD)) / Math.PI) / 2.0 * Math.Pow(2.0, zoom))), 0);
        }
        public static int MaxTilexX4Zoom(int zoom)
        {
            return Lon2TileX(179.9999999, zoom);
        }
        public static int Zoom2TileN(int zoom)
        {
            return (int)Math.Pow(2.0, Convert.ToDouble(zoom));
        }
        public static double TileX2Lon(int x, int zoom)
        {
            return x / Math.Pow(2.0, zoom) * 360.0 - 180;
        }
        public static double TileY2Lat(int y, int zoom)
        {
            double n = Math.PI - ConstantData.PI_DOUBLE * y / Math.Pow(2.0, zoom);
            //return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
            return TO_DEG * Math.Atan(Math.Sinh(n));
        }

        /*
	 * Reproject the coordinates to the Mercator projection (from EPSG:4326 to EPSG:3857): 
		x = lon
		y = arsinh(tan(lat)) = log[tan(lat) + sec(lat)]
		(lat and lon are in radians)
	 * Transform range of x and y to 0 – 1 and shift origin to top left corner: 
		x = [1 + (x / π)] / 2
		y = [1 − (y / π)] / 2

	 * Calculate the number of tiles across the map, n, using 2zoom
	 * Multiply x and y by n. Round results down to give tilex and tiley.
*/
        // some more notes

        // http://msdn.microsoft.com/en-us/library/windowsphone/develop/windows.ui.xaml.controls.maps.aspx
        // http://pietschsoft.com/post/2009/11/13/Prototype_OpenStreetMap_Silverlight_Control_using_Bing_Maps_SDK_and_DeepEarth

        // openstreetmap http://tile.openstreetmap.org/{2}/{0}/{1}.png that is, zoom, x, y, that is, zoom, long, lat
        // or http://a.tile.openstreetmap.org/{zoomlevel}/{x}/{y}.png
        // with zoom = 2,
        // x and y go between 0 and 3. y = 3 is antarctica, y = 0 is the arctic. No decimals are allowed.z goes between 2 and 19.
        // with zoom = 1, x and y go between 0 and 1
        // with zoom 3, x and y go between 0 and 7
        // basically, the max value is Pow(2,zoom) -1, for both x and y
        // the zero x is at meridian 180°
        // the zero y is at 85.0511 N (they use a Mercator projection) 85.0511 is the result of arctan(sinh(π)). 
        // the min zoom is 0 (zoomed out)
        // the max zoom is 19 (zoomed in)
        // MapControl goes between 1 and 20, instead
        // similar is openaerialmap http://tile.openaerialmap.org/tiles/1.0.0/openaerialmap-900913/{2}/{0}/{1}.jpg
        // otherwise, we can use http://nominatim.openstreetmap.org/search.php?q=8%2C+44%2C+9%2C+49&viewbox=-168.76%2C66.24%2C168.76%2C-66.24&polygon=1 not really, that one looks for names
        // http://wiki.openstreetmap.org/wiki/Nominatim
        //
        // http://pietschsoft.com/post/2009/03/20/Virtual-Earth-Silverlight-Overlay-OpenStreetMap2c-OpenAerialMap-and-Yahoo-Map-Imagery-using-Custom-Tile-Layers!
        //yahoo http://us.maps2.yimg.com/us.png.maps.yimg.com/png?v=3.52&t=m&x={0}&y={1}&z={2}
        //         public override Uri GetUri(int x, int y, int zoomLevel) 
        //{
        //    // The math used here was copied from the DeepEarth Project (http://deepearth.codeplex.com) 
        //    double posY;
        //    double zoom = 18 - zoomLevel;
        //    double num4 = Math.Pow(2.0, zoomLevel) / 2.0;

        //    if (y < num4)
        //    {
        //        posY = (num4 - Convert.ToDouble(y)) - 1.0;
        //    }
        //    else
        //    {
        //        posY = ((Convert.ToDouble(y) + 1) - num4) * -1.0;
        //    }
        // more here: http://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
        // hence:
        //            int long2tilex(double lon, int z) 
        //{ 
        //    return (int)(floor((lon + 180.0) / 360.0 * pow(2.0, z))); 
        //}

        //int lat2tiley(double lat, int z)
        //{ 
        //    return (int)(floor((1.0 - log( tan(lat * M_PI/180.0) + 1.0 / cos(lat * M_PI/180.0)) / M_PI) / 2.0 * pow(2.0, z))); 
        //}

        //double tilex2long(int x, int z) 
        //{
        //    return x / pow(2.0, z) * 360.0 - 180;
        //}

        //double tiley2lat(int y, int z) 
        //{
        //    double n = M_PI - 2.0 * M_PI * y / pow(2.0, z);
        //    return 180.0 / M_PI * atan(0.5 * (exp(n) - exp(-n)));
        //}
        //    return new Uri(string.Format(this.UriFormat, x, posY, zoom));
        //}

        //            public PointF WorldToTilePos(double lon, double lat, int zoom)
        //{
        //    PointF p = new Point();
        //    p.X = (float)((lon + 180.0) / 360.0 * (1 << zoom));
        //    p.Y = (float)((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) + 
        //        1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * (1 << zoom));

        //    return p;
        //}

        //public PointF TileToWorldPos(double tile_x, double tile_y, int zoom) 
        //{
        //    PointF p = new Point();
        //    double n = Math.PI - ((2.0 * Math.PI * tile_y) / Math.Pow(2.0, zoom));

        //    p.X = (float)((tile_x / Math.Pow(2.0, zoom) * 360.0) - 180.0);
        //    p.Y = (float)(180.0 / Math.PI * Math.Atan(Math.Sinh(n)));

        //    return p;
        //}
        //HttpMapTileDataSource dataSource = new HttpMapTileDataSource() { UriFormatString = "{UriScheme}://ecn.t0.tiles.virtualearth.net/tiles/r{quadkey}.jpeg?g=129&mkt=en-us&shading=hill&stl=H" };
        //HttpMapTileDataSource dataSource = new HttpMapTileDataSource() { UriFormatString = "http://tile.openstreetmap.org/2/0/1.png" }; //  "{UriScheme}://ecn.t0.tiles.virtualearth.net/tiles/r{quadkey}.jpeg?g=129&mkt=en-us&shading=hill&stl=H" };

    }
}

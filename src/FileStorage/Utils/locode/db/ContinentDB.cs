using System.IO;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;

namespace Neo.FileStorage.Utils.locode.db
{
    public class ContinentDB
    {
        public string path;
        private Once once = new();
        private IFeature[] features;

        public Continent PointContinent(Point point)
        {
            once.Do(() => { Init(); });
            NetTopologySuite.Geometries.Point planarPoint = new NetTopologySuite.Geometries.Point(point.longitude, point.latitude);
            string continent = null;
            foreach (var feature in features)
            {
                if (feature.Geometry is MultiPolygon)
                {
                    if (((MultiPolygon)feature.Geometry).Contains(planarPoint))
                    {
                        continent = feature.Attributes["Continent"].ToString();
                        break;
                    }
                }
                else if (feature.Geometry is Polygon)
                {
                    if (((Polygon)feature.Geometry).Contains(planarPoint))
                    {
                        continent = feature.Attributes["Continent"].ToString();
                        break;
                    }
                }
            }
            return continent.ContinentFromString();
        }

        public void Init()
        {
            string data = File.ReadAllText(path);
            FeatureCollection featureCollection = JsonConvert.DeserializeObject<FeatureCollection>(data);
            features = featureCollection.ToArray();
        }
    }
}

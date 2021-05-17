using System;
using System.IO;
using Neo.IO;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils.locode.db
{
    public class Point : ISerializable
    {
        public double Latitude;
        public double Longitude;

        public int Size => 2 * sizeof(double);

        public void Deserialize(BinaryReader reader)
        {
            Latitude = reader.ReadDouble();
            Longitude = reader.ReadDouble();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Latitude);
            writer.Write(Longitude);
            writer.Flush();
        }

        public static Point PointFromCoordinates(Coordinates crd)
        {
            if (crd is null) return null;
            var cLat = crd.lat;
            var cLatDeg = cLat.Degrees();
            var cLatMnt = cLat.Minutes();
            var lat = Double.Parse(string.Join(string.Join(BitConverter.ToString(cLatDeg), "."), BitConverter.ToString(cLatMnt)));
            if (!cLat.North()) lat = -lat;
            var cLng = crd.lng;
            var cLngDeg = cLng.Degrees();
            var cLngMnt = cLng.Minutes();
            var lng = Double.Parse(string.Join(string.Join(BitConverter.ToString(cLngDeg), "."), BitConverter.ToString(cLngMnt)));
            if (!cLng.East()) lng = -lng;
            return new Point() { Latitude = lat, Longitude = lng };
        }
    }
}

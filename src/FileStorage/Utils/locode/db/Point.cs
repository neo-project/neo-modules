using Neo.IO;
using System;
using System.IO;
using static Neo.FileStorage.Utils.locode.Column;

namespace Neo.FileStorage.Utils.locode.db
{
    public class Point : ISerializable
    {
        public double latitude;
        public double longitude;

        public int Size => 2 * sizeof(double);

        public void Deserialize(BinaryReader reader)
        {
            latitude = reader.ReadDouble();
            longitude = reader.ReadDouble();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(latitude);
            writer.Write(longitude);
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
            return new Point() { latitude = lat, longitude = lng };
        }
    }
}

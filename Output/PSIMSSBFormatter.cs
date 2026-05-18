// AZMLib/Output/Formatters/PSIMSSBFormatter.cs
using System.Globalization;
using UCNLNav;

namespace AZMLib.Output
{
    public class PSIMSSBFormatter : IOutputFormatter
    {
        public string Id => "PSIMSSB";

        public IEnumerable<string> FormatBeacon(ResponderBeacon beacon, AZMTranscieverState state, DateTime timestamp)
        {
            if (!state.Lat_deg.IsInitializedAndNotObsolete ||
                !state.Lon_deg.IsInitializedAndNotObsolete ||
                !state.Heading_deg.IsInitializedAndNotObsolete ||
                !beacon.Lat_deg.IsInitialized ||
                !beacon.Lon_deg.IsInitialized)
                yield break;

            string timeStr = timestamp.ToString("HHmmss.ff", CultureInfo.InvariantCulture);
            string btpId = "B" + AZM.Addr2Str(beacon.Address).PadLeft(2, '0');

            double spLatRad = Algorithms.Deg2Rad(state.Lat_deg.Value);
            double spLonRad = Algorithms.Deg2Rad(state.Lon_deg.Value);
            double epLatRad = Algorithms.Deg2Rad(beacon.Lat_deg.Value);
            double epLonRad = Algorithms.Deg2Rad(beacon.Lon_deg.Value);

            Algorithms.GetDeltasByGeopoints_WGS84(spLatRad, spLonRad, epLatRad, epLonRad,
                out double deltaLat_m, out double deltaLon_m);

            double headingRad = Algorithms.Deg2Rad(state.Heading_deg.Value);
            double cosH = Math.Cos(headingRad);
            double sinH = Math.Sin(headingRad);

            double x_m = deltaLat_m * cosH + deltaLon_m * sinH;
            double y_m = -deltaLat_m * sinH + deltaLon_m * cosH;
            double z_m = beacon.Depth_m.IsInitialized ? beacon.Depth_m.Value : 0.0;

            string sentence = string.Format(CultureInfo.InvariantCulture,
                "PSIMSSB,{0},{1},A,,C,H,M,{2:F02},{3:F02},{4:F02},0.0,N,,",
                timeStr, btpId, x_m, y_m, z_m);

            string nmeaLine = "$" + sentence;
            byte checksum = 0;
            for (int i = 0; i < nmeaLine.Length; i++)
                checksum ^= (byte)nmeaLine[i];

            yield return nmeaLine + "*" + checksum.ToString("X2");
        }

        public string FormatStation(AZMTranscieverState state, DateTime timestamp)
        {
            return state.StationParametersToString();
        }
    }
}
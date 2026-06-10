namespace MedicalApp.API.Helpers
{
    /// <summary>
    /// Geospatial helpers. Pure utility — no DB, no DI, no allocations.
    /// </summary>
    public static class GeoUtils
    {
        // Mean Earth radius (kilometers). Source: IUGG.
        private const double EarthRadiusKm = 6371.0088;

        /// <summary>
        /// Great-circle distance between two lat/lng points using the Haversine formula.
        /// Accurate enough for the "Nearby" map at city-scale distances (&lt; 100 km).
        /// </summary>
        public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        private static double ToRad(double deg) => deg * (Math.PI / 180.0);
    }
}

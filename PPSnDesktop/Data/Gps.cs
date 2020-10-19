using System;
using System.Device.Location;
using TecWare.PPSn.UI;

namespace TecWare.PPSn.Data
{
	[PpsService(typeof(IPpsGpsService))]
	internal sealed class GpsService : IPpsGpsService
	{
		private static GeoCoordinateWatcher geoCoordinateWatcher;

		private static bool TryGetGeoCoordinateWatcher(out GeoCoordinateWatcher watcher)
		{
			if (geoCoordinateWatcher != null)
			{
				watcher = geoCoordinateWatcher;
				return true;
			}

			var tmp = new GeoCoordinateWatcher(GeoPositionAccuracy.High);
			if (tmp.TryStart(true, TimeSpan.FromMilliseconds(2000)))
			{
				watcher = geoCoordinateWatcher = tmp;
				return true;
			}
			else
			{
				tmp.Dispose();
				watcher = null;
				return false;
			}
		} // func TryGetGeoCoordinateWatcher

		bool IPpsGpsService.TryGetGeoCoordinate(out double longitude, out double latitude, out long timestamp)
		{
			if (TryGetGeoCoordinateWatcher(out var watcher) && watcher.Status == GeoPositionStatus.Ready)
			{
				longitude = watcher.Position.Location.Longitude;
				latitude = watcher.Position.Location.Latitude;
				var ts = watcher.Position.Timestamp;
				timestamp = (long)(ts - new DateTimeOffset(1970, 1, 1, 0, 0, 0, ts.Offset)).TotalMilliseconds;
				return true;
			}
			else
			{
				longitude = Double.NaN;
				latitude = Double.NaN;
				timestamp = 0;
				return false;
			}
		} // func TryGetGeoCoordinate
	} // class GpsService
}

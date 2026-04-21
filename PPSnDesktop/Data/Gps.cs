#nullable enable
using System;
using Windows.Devices.Geolocation;
using TecWare.PPSn.UI;
using System.Threading.Tasks;
using System.Windows.Documents.DocumentStructures;
using TecWare.DE.Stuff;

namespace TecWare.PPSn.Data
{
	[PpsService(typeof(IPpsGpsService))]
	internal sealed class GpsService : IPpsGpsService
	{
		private static object lockLastPositon = new object();
		private static Geolocator? geoCoordinateWatcher = null;
		private static PpsGpsLocation? lastLocation = null;

		private static async Task GetGeoPositionAsync()
		{
			// get request
			if (geoCoordinateWatcher is null)
			{
				var accessStatus = await Geolocator.RequestAccessAsync();
				switch (accessStatus)
				{
					case GeolocationAccessStatus.Allowed:
						geoCoordinateWatcher = new Geolocator
						{
							DesiredAccuracy = PositionAccuracy.High
						};
						break;
					case GeolocationAccessStatus.Denied:
						throw new Exception("Zugriff auf Standort verweigert. Aktivieren sie es in den Einstellungen unter Datenschutz & Sicherheit > Standort!");
					case GeolocationAccessStatus.Unspecified:
						throw new Exception("Zugriff auf Standort verweigert. Ein unbekannter Fehler ist aufgetreten!");
					default:
						throw new ArgumentException();
				}
			}

			// get point
			var current = await geoCoordinateWatcher.GetGeopositionAsync();
			var ts = current.Coordinate.Timestamp;
			lock (lockLastPositon)
				lastLocation = new PpsGpsLocation(current.Coordinate.Latitude, current.Coordinate.Longitude, (long)(ts - new DateTimeOffset(1970, 1, 1, 0, 0, 0, ts.Offset)).TotalMilliseconds);
		} // GetGeoPositionAsync

		private static void BeginGetGeoPosition()
			=> GetGeoPositionAsync().ContinueWith(EndGetGeoPosition);

		private static void EndGetGeoPosition(Task task)
		{
			try
			{
				task.Wait();
			}
			catch (Exception e)
			{
				var log = PpsShell.Current?.LogProxy("Gps");
				log?.Except(e);
			}
		} // func EndGetGeoPosition

		async Task<PpsGpsLocation> IPpsGpsService.RequestGeoCoordinateAsync()
		{
			await GetGeoPositionAsync();
			if (!lastLocation.HasValue)
				throw new Exception("Letzte Position konnte nicht ermittelt werden (ist NULL)!");

			return lastLocation.Value;
		} // func RequestGeoCoordinateAsync

		bool IPpsGpsService.TryGetGeoCoordinate(out PpsGpsLocation location)
		{
			BeginGetGeoPosition();

			lock (lockLastPositon)
			{
				if (lastLocation.HasValue)
				{
					location = lastLocation.Value;
					return true;
				}
				else
				{
					location = PpsGpsLocation.Empty;
					return false;
				}
			}
		} // func TryGetGeoCoordinate
	} // class GpsService
}

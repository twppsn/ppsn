#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.PPSn.UI
{
	#region -- struct PpsGpsLocation --------------------------------------------------

	/// <summary>Represents a position with a timestamp.</summary>
	public readonly struct PpsGpsLocation : IEquatable<PpsGpsLocation>
	{
		/// <summary>Public Constructor.</summary>
		/// <param name="latitude"></param>
		/// <param name="longitude"></param>
		/// <param name="unixTimeStamp"></param>
		public PpsGpsLocation(double latitude, double longitude, long unixTimeStamp)
		{
			Latitude = latitude;
			Longitude = longitude;
			UnixTimeStamp = unixTimeStamp;
		} // ctor

		/// <summary></summary>
		public override bool Equals(object obj)
			=> obj is PpsGpsLocation && Equals((PpsGpsLocation)obj);

		/// <summary></summary>
		public override int GetHashCode()
			=> Latitude.GetHashCode() ^ Longitude.GetHashCode();

		/// <summary></summary>
		public bool Equals(PpsGpsLocation other)
			=> EqualsWithEpsilon(other.Latitude, Latitude) && EqualsWithEpsilon(other.Longitude, Longitude);

		private static bool EqualsWithEpsilon(double a, double b)
		{
			if(Math.Abs(a - b) < double.Epsilon)
			{ 
				return true;
			}

			return false; 
		} // func EqualsWithEpsilon

		/// <summary>Latitude.</summary>
		public double Latitude { get; }
		/// <summary>Longitude.</summary>
		public double Longitude{ get; }
		/// <summary>Timestamp of position measurement.</summary>
		public long UnixTimeStamp { get; }

		/// <summary>Empty state.</summary>
		public static PpsGpsLocation Empty { get; } = new PpsGpsLocation(Double.NaN, Double.NaN, 0);
	} // struct PpsGpsLocation

	#endregion

	#region -- interface IPpsGpsService -----------------------------------------------

	/// <summary>Get the location of the current device.</summary>
	public interface IPpsGpsService
	{
		/// <summary>Last known location of the device.</summary>
		/// <param name="location"></param>
		/// <returns></returns>
		bool TryGetGeoCoordinate(out PpsGpsLocation location);
		
		/// <summary>Last known location of the device.</summary>
		/// <returns></returns>
		Task<PpsGpsLocation> RequestGeoCoordinateAsync();
	} // interface IPpsGpsService

	#endregion
}

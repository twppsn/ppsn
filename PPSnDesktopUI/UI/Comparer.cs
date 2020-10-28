﻿#region -- copyright --
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//
#endregion
using System.Collections;
using System.Collections.Generic;
using System.Windows.Media;

namespace TecWare.PPSn.UI
{
	#region -- class PpsComparer ------------------------------------------------------

	/// <summary></summary>
	public static class PpsComparer
	{
		#region -- class ColorComparer ------------------------------------------------

		private class ColorComparer : IComparer, IComparer<Color>
		{
			public int Compare(object x, object y)
				=> Compare((Color)x, (Color)y);

			public int Compare(Color x, Color y)
				=> x.ToHsvColor().CompareTo(y.ToHsvColor());
		} // class ColorComparer

		#endregion

		/// <summary></summary>
		public static IComparer Colors { get; } = new ColorComparer();
	} // class PpsComparer

	#endregion
}
namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Localization;

public partial class ClusterSearch
{
	#region Nested types

	//Merged between bars
	private class MergedClusterDictionary(int priceRowsMerge, decimal tickSize) : Dictionary<decimal, CustomVolumeInfo>
	{
		#region Fields

		private decimal _maxVol = decimal.MinValue;

		#endregion

		#region Properties

		public decimal TotalVolume { get; private set; }

		/// <summary>
		/// Calculated with N - 1 levels price merged values
		/// </summary>
		
		/* N = 2
		 * 30
		 * 14 |
		 * 23 | <= POC is 23 + 14 = 37
		 * 1
		 */
		public decimal PocPrice { get; private set; }

		public new CustomVolumeInfo this[decimal price]
		{
			get => base[price];
			set
			{
				if (TryGetValue(price, out var level))
					TotalVolume -= level.Volume;

				base[price] = value;
				TotalVolume += value.Volume;

                var sum = 0m;

				for (var iPrice = price; iPrice <= price + (priceRowsMerge - 1) * tickSize; iPrice += tickSize)
				{
					if (!TryGetValue(iPrice, out var iLevel))
						continue;

					sum += iLevel.Volume;
				}

				if (sum <= _maxVol)
					return;

				_maxVol = sum;
				PocPrice = price;
			}
		}

        #endregion

        #region Public methods

        public void RemoveVolume(CustomVolumeInfo level)
        {
            TotalVolume -= level.Volume;
        }

        public void AddVolume(CustomVolumeInfo level)
        {
            TotalVolume += level.Volume;
        }

        public new void Clear()
        {
			_maxVol = decimal.MinValue;
			TotalVolume = 0;
			base.Clear();
		}

		#endregion
	}

	//PriceVolumeInfo with additional properties
	private class CustomVolumeInfo : PriceVolumeInfo
	{
		#region Properties

		public decimal AvgTrade => Ticks is 0 ? 0 : Volume / Ticks;

		public decimal Delta => Ask - Bid;
		
		public CrossColor? PriceSelectionColor { get; set; }

        #endregion

        #region ctor

        public CustomVolumeInfo(decimal price)
		{
			Price = price;
		}

		public CustomVolumeInfo(PriceVolumeInfo cluster)
		{
			Price = cluster.Price;

			Ask = cluster.Ask;
			Between = cluster.Between;
			Bid = cluster.Bid;
			Ticks = cluster.Ticks;
			Volume = cluster.Volume;
        }

		#endregion

		#region Public methods
		
		public static CustomVolumeInfo operator+(CustomVolumeInfo a, CustomVolumeInfo b)
		{
			a.Ask += b.Ask;
			a.Between += b.Between;
			a.Bid += b.Bid;
			a.Ticks += b.Ticks;
			a.Volume += b.Volume;
			return a;
		}
		
		#endregion
	}

	public enum CalcMode
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
		Bid = 0,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
		Ask = 1,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Delta))]
		Delta = 2,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
		Volume = 3,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
		Tick = 4,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PocLevel))]
		MaxVolume = 5,

        [Display(Name = "Diagonal Imbalance")]
        DiagonalImbalance = 6,

        [Browsable(false)]
		[Obsolete]
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Time))]
		Time = 7
	}

	public enum CandleDirection
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bearlish))]
		Bearish,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bullish))]
		Bullish,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
		Any,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Neutral))]
		Neutral
	}

	public enum PriceLocation
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AtHigh))]
		AtHigh,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AtLow))]
		AtLow,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
		Any,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Body))]
		Body,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UpperWick))]
		UpperWick,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LowerWick))]
		LowerWick,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AtHighOrLow))]
		AtHighOrLow,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AnyWick))]
		AtUpperLowerWick
	}

	#endregion
}
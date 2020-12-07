namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[Category("Bid x Ask,Delta,Volume")]
	[DisplayName("Bar's volume filter")]
	public class BarVolumeFilter : Indicator
	{
		#region Nested types

		public enum VolumeType
		{
			[Display(ResourceType = typeof(Resources), Name = "Volume")]
			Volume,

			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Ticks,

			[Display(ResourceType = typeof(Resources), Name = "Delta")]
			Delta,

			[Display(ResourceType = typeof(Resources), Name = "Bid")]
			Bid,

			[Display(ResourceType = typeof(Resources), Name = "Ask")]
			Ask
		}

		#endregion

		#region Fields

		private readonly PaintbarsDataSeries _paintBars = new PaintbarsDataSeries("Paint bars");
		private Color _color = Colors.Orange;
		private VolumeType _volumeType;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Type", Order = 5)]
		public VolumeType Type
		{
			get => _volumeType;
			set
			{
				_volumeType = value;
				RecalculateValues();
			}
		}

		[Browsable(false)]
		[Display(ResourceType = typeof(Resources), Name = "Minimum", Order = 10)]
		public decimal MinFilter
		{
			get => MinimumFilter.Value;
			set
			{
				MinimumFilter.Value = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Minimum", Order = 10)]
		public Filter MinimumFilter { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "Maximum", Order = 20)]
		public Filter MaximumFilter { get; set; } = new Filter { Value = 100 };

		[Browsable(false)]
		[Display(ResourceType = typeof(Resources), Name = "Maximum", Order = 20)]
		public decimal MaxFilter
		{
			get => MaximumFilter.Value;
			set
			{
				MaximumFilter.Value = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Color", Order = 30)]
		public Color FilterColor
		{
			get => _color;
			set
			{
				_color = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BarVolumeFilter()
			: base(true)
		{
			DataSeries[0] = _paintBars;
			_paintBars.IsHidden = true;
			DenyToChangePanel = true;
		}

		#endregion

		#region Overrides of BaseIndicator

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			decimal volume;

			switch (Type)
			{
				case VolumeType.Volume:
				{
					volume = candle.Volume;
					break;
				}
				case VolumeType.Ticks:
				{
					volume = candle.Ticks;
					break;
				}
				case VolumeType.Delta:
				{
					volume = candle.Delta;
					break;
				}
				case VolumeType.Bid:
				{
					volume = candle.Bid;
					break;
				}
				case VolumeType.Ask:
				{
					volume = candle.Ask;
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			var isUnderFilter = !(MinimumFilter.Enabled && volume <= MinimumFilter.Value);

			if (isUnderFilter && MaximumFilter.Enabled && volume > MaximumFilter.Value)
				isUnderFilter = false;

			_paintBars[bar] = isUnderFilter ? (Color?)_color : null;
		}

		protected override void OnInitialize()
		{
			MaximumFilter.PropertyChanged += (a, b) =>
			{
				RecalculateValues();
				RedrawChart();
			};

			MinimumFilter.PropertyChanged += (a, b) =>
			{
				RecalculateValues();
				RedrawChart();
			};
		}

		#endregion
	}
}
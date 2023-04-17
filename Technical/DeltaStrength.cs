namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Delta Strength")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45992-delta-strength")]
	public class DeltaStrength : Indicator
	{
		#region Nested types

		public enum FilterType
		{
			[Display(ResourceType = typeof(Resources), Name = "Bullish")]
			Bull,

			[Display(ResourceType = typeof(Resources), Name = "Bearlish")]
			Bear,

			[Display(ResourceType = typeof(Resources), Name = "Any")]
			All
		}

		#endregion

		#region Fields

		private FilterType _negFilter;

		private ValueDataSeries _negSeries = new(Resources.Negative);
		private FilterType _posFilter;
		private ValueDataSeries _posSeries = new(Resources.Positive);
		private ValueDataSeries _neutralSeries = new(Resources.Neutral);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "MaxValue", GroupName = "Settings", Order = 100)]
		[Range(0, 100)]
		public Filter MaxFilter { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "MinValue", GroupName = "Settings", Order = 110)]
		[Range(0, 100)]
		public Filter MinFilter { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "PositiveDelta", GroupName = "Filter", Order = 200)]
		public FilterType PosFilter
		{
			get => _posFilter;
			set
			{
				_posFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "NegativeDelta", GroupName = "Filter", Order = 210)]
		public FilterType NegFilter
		{
			get => _negFilter;
			set
			{
				_negFilter = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeltaStrength()
			: base(true)
		{
			DenyToChangePanel = true;
			_posFilter = _negFilter = FilterType.All;
			_posSeries.Color = Colors.Green;
			_negSeries.Color = Colors.Red;
			_neutralSeries.Color = Colors.Gray;
			_posSeries.VisualType = _negSeries.VisualType = _neutralSeries.VisualType = VisualMode.Dots;
			_posSeries.Width = _negSeries.Width = _neutralSeries.Width = 4;

			_posSeries.ShowCurrentValue = _negSeries.ShowCurrentValue = false;

			MaxFilter = new Filter
			{
				Enabled = true,
				Value = 98
			};

			MinFilter = new Filter
			{
				Enabled = true,
				Value = 90
			};

			MaxFilter.PropertyChanged += FilterChanged;
			MinFilter.PropertyChanged += FilterChanged;
			DataSeries[0] = _posSeries;
			DataSeries.Add(_negSeries);
			DataSeries.Add(_neutralSeries);

			_posSeries.ShowTooltip = _negSeries.ShowTooltip = _neutralSeries.ShowTooltip = false;
		}

        #endregion

        #region Protected methods

        protected override void OnApplyDefaultColors()
        {
	        if (ChartInfo is null)
		        return;

	        _posSeries.Color = ChartInfo.ColorsStore.UpCandleColor.Convert();
	        _negSeries.Color = ChartInfo.ColorsStore.DownCandleColor.Convert();
	        _neutralSeries.Color = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        }

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			if (!MaxFilter.Enabled && !MinFilter.Enabled)
				return;

			var candle = GetCandle(bar);

			if (candle.Delta < 0 && candle.MinDelta < 0
				&& candle.Delta <= candle.MinDelta * 0.01m * MinFilter.Value
				&& candle.Delta >= candle.MinDelta * 0.01m * MaxFilter.Value)
			{
				if (_negFilter == FilterType.All
					|| _negFilter == FilterType.Bull && candle.Close > candle.Open
					|| _negFilter == FilterType.Bear && candle.Close < candle.Open)
					_negSeries[bar] = candle.High + 2 * InstrumentInfo.TickSize;
				else
					_negSeries[bar] = _neutralSeries[bar] = 0;
				
			}
			else
				_negSeries[bar] = _neutralSeries[bar] = 0;

			if (candle.Delta > 0 && candle.MaxDelta > 0
				&& (candle.Delta >= candle.MaxDelta * 0.01m * MinFilter.Value || !MinFilter.Enabled)
				&& (candle.Delta <= candle.MaxDelta * 0.01m * MaxFilter.Value || !MaxFilter.Enabled))
			{
				if (_posFilter == FilterType.All
					|| _posFilter == FilterType.Bull && candle.Close > candle.Open
					|| _posFilter == FilterType.Bear && candle.Close < candle.Open)
					_posSeries[bar] = candle.Low - 2 * InstrumentInfo.TickSize;
				else
					_posSeries[bar] = _neutralSeries[bar] = 0;
			}
			else
				_posSeries[bar] = _neutralSeries[bar] = 0;

			if(candle.Delta == 0 && MinFilter.Value <= 0)
				_neutralSeries[bar] = candle.Low - 2 * InstrumentInfo.TickSize;
		}

		
		protected override void OnFinishRecalculate()
		{
			RedrawChart();
		}
		
		#endregion

		#region Private methods

		private void FilterChanged(object sender, PropertyChangedEventArgs e)
		{
			RecalculateValues();
		}

		#endregion
	}
}
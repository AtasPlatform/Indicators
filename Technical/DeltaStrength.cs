namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Delta Strength")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DeltaStrengthDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602363")]
	public class DeltaStrength : Indicator
	{
		#region Nested types

		public enum FilterType
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bullish))]
			Bull,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bearlish))]
			Bear,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
			All
		}

		#endregion

		#region Fields

		private FilterType _negFilter;
        private FilterType _posFilter;

		private ValueDataSeries _negSeries = new("NegSeries", Strings.Negative)
		{
			DescriptionKey = nameof(Strings.PositiveDeltaSettingsDescription),
		};

		private ValueDataSeries _posSeries = new("PosSeries", Strings.Positive)
		{
			DescriptionKey = nameof(Strings.NegativeDeltaSettingsDescription),
		};

		private ValueDataSeries _neutralSeries = new("NeutralSeries", Strings.Neutral)
		{
            DescriptionKey = nameof(Strings.NeutralDeltaSettingsDescription),
		};

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaxValue), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MaxDeltaFilterPercentDescription), Order = 100)]
		[Range(0, 100)]
		public Filter MaxFilter { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinValue), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinDeltaFilterPercentDescription), Order = 110)]
		[Range(0, 100)]
		public Filter MinFilter { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PositiveDelta), GroupName = nameof(Strings.Filter), Description = nameof(Strings.BarDirectionDescription), Order = 200)]
		public FilterType PosFilter
		{
			get => _posFilter;
			set
			{
				_posFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.NegativeDelta), GroupName = nameof(Strings.Filter), Description = nameof(Strings.BarDirectionDescription), Order = 210)]
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
			_posSeries.Color = System.Drawing.Color.Green.Convert();
			_negSeries.Color = System.Drawing.Color.Red.Convert();
			_neutralSeries.Color = System.Drawing.Color.Gray.Convert();
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
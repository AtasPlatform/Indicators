namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;


	[DisplayName("Wavetrend")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38044-wavetrend")]
	public class Wavetrend : Indicator
	{
		#region Static and constants

		private const int _avgSmaPeriod = 4;
        private const int _sellDefault = 60;
        private const int _buyDefault = -60;

        #endregion

        #region Fields

        private readonly LineSeries _sellUp = new("SellUpId", "SellUp")
		{
			Value = _sellDefault + 10,
			LineDashStyle = LineDashStyle.Dash,
			Color = Colors.Gray
		};

        private readonly LineSeries _sellDown = new("SellDownId", "SellDown")
        {
            Value = _sellDefault,
            LineDashStyle = LineDashStyle.Dash,
            Color = DefaultColors.Red.Convert()
        };

        private readonly LineSeries _buyUp = new("BuyUpId", "BuyUp")
        {
            Value = _buyDefault,
            LineDashStyle = LineDashStyle.Dash,
            Color = DefaultColors.Green.Convert()
        };

        private readonly LineSeries _buyDown = new("BuyDownId", "BuyDown")
        {
            Value = _buyDefault - 10,
            LineDashStyle = LineDashStyle.Dash,
            Color = Colors.Gray
        };

		private readonly ValueDataSeries _bullLine = new("BullLineId", "BullLine") { Color = DefaultColors.Green.Convert() };
		private readonly ValueDataSeries _bearLine = new("BearLineId", "BearLine") { Color = DefaultColors.Red.Convert() };

        private readonly ValueDataSeries _buyDots = new("BuyDotsId", "BuyDots")
		{
			ShowZeroValue = false,
			Color = DefaultColors.Aqua.Convert(),
			LineDashStyle = LineDashStyle.Solid,
			VisualType = VisualMode.Dots,
			Width = 5
		};
        private readonly ValueDataSeries _sellDots = new("SellDotsId", "SellDots")
        {
	        ShowZeroValue = false,
	        Color = DefaultColors.Yellow.Convert(),
	        LineDashStyle = LineDashStyle.Solid,
	        VisualType = VisualMode.Dots,
	        Width = 5
        };

        private readonly EMA _bullEma = new() { Period = 21 };
        private readonly EMA _waveEmaPrice = new() { Period = 10 };
        private readonly EMA _waveEmaVolatility = new() { Period = 10 };
        private SMA _avgSma = new() { Period = 4 };

		private int _overbought = _sellDefault;
        private int _oversold = -_buyDefault;
		private int _sellUpCache;
        private int _buyDownCache;

        #endregion

        #region Properties

        [Browsable(false)]
        [Parameter]
		[Display(ResourceType = typeof(Strings), GroupName = "Settings", Name = "Overbought", Order = 1)]
		public int Overbought
		{
			get => _overbought;
			set
			{
				if (Math.Abs(value) > 100 || value < Oversold)
					return;

				_overbought = value;
			}
		}

        [Browsable(false)]
        [Parameter]
		[Display(ResourceType = typeof(Strings), GroupName = "Settings", Name = "Oversold", Order = 2)]
		public int Oversold
		{
			get => _oversold;
			set
			{
				if (Math.Abs(value) > 100 || value > Overbought)
					return;

				_oversold = value;
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Strings), GroupName = "Settings", Name = "AveragePeriod", Order = 3)]
		[Range(1, 10000)]
		public int AvgPeriod
		{
			get => _bullEma.Period;
			set
			{
				_bullEma.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Strings), GroupName = "Settings", Name = "WavePeriod", Order = 4)]
		[Range(1, 10000)]
        public int WavePeriod
		{
			get => _waveEmaPrice.Period;
			set
			{
				_waveEmaPrice.Period = value;
				_waveEmaVolatility.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Wavetrend()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;

			_avgSma.Period = _avgSmaPeriod;
			
			DataSeries[0] = _buyDots;
			DataSeries.Add(_sellDots);
			DataSeries.Add(_bullLine);
			DataSeries.Add(_bearLine);

			LineSeries.Add(_sellUp);
            LineSeries.Add(_sellDown);
            LineSeries.Add(_buyUp);
            LineSeries.Add(_buyDown);

            _sellUp.PropertyChanged += LineSeriesPropertyChanged;
            _sellDown.PropertyChanged += LineSeriesPropertyChanged;
            _buyUp.PropertyChanged += LineSeriesPropertyChanged;
            _buyDown.PropertyChanged += LineSeriesPropertyChanged;

            _sellUpCache = (int)_sellUp.Value; 
            _buyDownCache = (int)_buyDown.Value;
        }

        #endregion

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_buyDots.Clear();
				_sellDots.Clear();
				_bullLine.Clear();
				_bearLine.Clear();

				_avgSma = new SMA
					{ Period = _avgSmaPeriod };
			}
			else
			{
				var candle = GetCandle(bar);

				var avgPrice = Math.Abs(candle.High + candle.Close + candle.Low) / 3.0m;

				var waveMa = _waveEmaPrice.Calculate(bar, avgPrice);

				var waveVolatilityEma = _waveEmaVolatility.Calculate(bar, Math.Abs(avgPrice - waveMa));

				var ci = (avgPrice - waveMa) / (0.015m * waveVolatilityEma);

				var tci = _bullEma.Calculate(bar, ci);

				var wt = _avgSma.Calculate(bar, tci);

				_bullLine[bar] = tci;
				_bearLine[bar] = wt;

				if (_bullLine[bar] <= _bearLine[bar]
					&& _bullLine[bar - 1] >= _bearLine[bar - 1]
					&& _bullLine[bar] >= _overbought)
					_sellDots[bar] = _bullLine[bar];

				if (_bullLine[bar] >= _bearLine[bar]
					&& _bullLine[bar - 1] <= _bearLine[bar - 1]
					&& _bearLine[bar] <= _oversold)
					_buyDots[bar] = _bullLine[bar];
			}
		}

        #endregion

        #region Private methods

        private void LineSeriesPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
			if (e.PropertyName != "Value")
				return;

			if (sender.Equals(_sellUp))
                CheckAndSetValue(_sellUp, ref _sellUpCache, _sellUp.Value < _sellDown.Value);

			if (sender.Equals(_sellDown))
				CheckAndSetValue(_sellDown, ref _overbought, _sellDown.Value < _oversold);

            if (sender.Equals(_buyDown))
                CheckAndSetValue(_buyDown, ref _buyDownCache, _buyDown.Value > _buyUp.Value);

            if (sender.Equals(_buyUp))
                CheckAndSetValue(_buyUp, ref _oversold, _buyUp.Value > _overbought);
        }

        private void CheckAndSetValue(LineSeries line, ref int val, bool falseCondition)
        {
			if (line.Value < -100 || line.Value > 100 || falseCondition)
				line.Value = val;
			else
				val = (int)line.Value;
        }

        #endregion
    }
}
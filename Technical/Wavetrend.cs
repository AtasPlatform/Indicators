namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	using Utils.Common.Collections;

	[DisplayName("Wavetrend")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38044-wavetrend")]
	public class Wavetrend : Indicator
	{
		#region Static and constants

		private const int _avgSmaPeriod = 4;

		#endregion

		#region Fields

		private readonly EMA _bullEma = new() { Period = 21 };

		private readonly ValueDataSeries _bullLine = new("BullLine") { Color = DefaultColors.Green.Convert() };
		private readonly ValueDataSeries _bearLine = new("BearLine") { Color = DefaultColors.Red.Convert() };

        private readonly ValueDataSeries _buyDots = new("BuyDots")
		{
			ShowZeroValue = false,
			Color = DefaultColors.Aqua.Convert(),
			LineDashStyle = LineDashStyle.Solid,
			VisualType = VisualMode.Dots,
			Width = 5
		};
        private readonly ValueDataSeries _sellDots = new("SellDots")
        {
	        ShowZeroValue = false,
	        Color = DefaultColors.Yellow.Convert(),
	        LineDashStyle = LineDashStyle.Solid,
	        VisualType = VisualMode.Dots,
	        Width = 5
        };

        private readonly EMA _waveEmaPrice = new() { Period = 10 };
        private readonly EMA _waveEmaVolatility = new() { Period = 10 };

        private SMA _avgSma = new() { Period = 4 };

		private int _overbought = 53;
        private int _oversold = -50;

        #endregion

        #region Properties

        [Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Overbought", Order = 1)]
		public int Overbought
		{
			get => _overbought;
			set
			{
				if (Math.Abs(value) > 100 || value < Oversold)
					return;

				_overbought = value;
				ReRenderLines();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Oversold", Order = 2)]
		public int Oversold
		{
			get => _oversold;
			set
			{
				if (Math.Abs(value) > 100 || value > Overbought)
					return;

				_oversold = value;
				ReRenderLines();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "AveragePeriod", Order = 3)]
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
		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "WavePeriod", Order = 4)]
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
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				ReRenderLines();
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
					&&
					_bullLine[bar - 1] >= _bearLine[bar - 1]
					&&
					_bullLine[bar] >= Overbought)
					_sellDots[bar] = _bullLine[bar];

				if (_bullLine[bar] >= _bearLine[bar]
					&&
					_bullLine[bar - 1] <= _bearLine[bar - 1]
					&&
					_bearLine[bar] <= Oversold)
					_buyDots[bar] = _bullLine[bar];
			}
		}

		#endregion

		#region Private methods

		private void ReRenderLines()
		{
			if (LineSeries.IsNullOrEmpty())
			{
				LineSeries.Add(new LineSeries("SellUp")
				{
					Value = Overbought + 10,
					LineDashStyle = LineDashStyle.Dash,
					Color = Colors.Gray
				});

				LineSeries.Add(new LineSeries("SellDown")
				{
					Value = Overbought,
					LineDashStyle = LineDashStyle.Dash,
					Color = Colors.Gray
				});

				LineSeries.Add(new LineSeries("BuyUp")
				{
					Value = Oversold,
					LineDashStyle = LineDashStyle.Dash,
					Color = Colors.Gray
				});

				LineSeries.Add(new LineSeries("BuyDown")
				{
					Value = Oversold - 10,
					LineDashStyle = LineDashStyle.Dash,
					Color = Colors.Gray
				});
			}
			else
			{
				LineSeries[0].Value = Overbought + 10;
				LineSeries[1].Value = Overbought;
				LineSeries[2].Value = Oversold;
				LineSeries[3].Value = Oversold - 10;
			}
		}

		#endregion
	}
}
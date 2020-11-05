namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Settings;

	[DisplayName("Wavetrend")]
	public class Wavetrend : Indicator
	{
		#region Static and constants

		private const int _avgSmaPeriod = 4;

		#endregion

		#region Fields

		private readonly SMA _avgSma = new SMA();
		private readonly ValueDataSeries _bearLine;
		private readonly EMA _bullEma = new EMA();
		private readonly ValueDataSeries _bullLine;

		private readonly ValueDataSeries _buyDots;
		private readonly ValueDataSeries _sellDots;

		private readonly EMA _waveEmaPrice = new EMA();
		private readonly EMA _waveEmaVolatility = new EMA();

		private int _overbought;
		private int _oversold;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "Overbought", Order = 2)]
		public int Overbought
		{
			get => _overbought;
			set
			{
				if (Math.Abs(value) > 100 || value < Oversold)
					return;

				_overbought = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "Oversold", Order = 2)]
		public int Oversold
		{
			get => _oversold;
			set
			{
				if (Math.Abs(value) > 100 || value > Overbought)
					return;

				_oversold = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "AveragePeriod", Order = 2)]
		public int AvgPeriod
		{
			get => _bullEma.Period;
			set
			{
				if (value <= 0)
					return;

				_bullEma.Period = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "WavePeriod", Order = 2)]
		public int WavePeriod
		{
			get => _waveEmaPrice.Period;
			set
			{
				if (value <= 0)
					return;

				_waveEmaPrice.Period = _waveEmaVolatility.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Wavetrend()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DenyToChangePanel = true;

			_avgSma.Period = _avgSmaPeriod;
			_bullEma.Period = 21;
			_waveEmaVolatility.Period = _waveEmaPrice.Period = 10;

			_oversold = -50;
			_overbought = 53;

			_buyDots = new ValueDataSeries("BuyDots")
			{
				ShowZeroValue = false,
				Color = Colors.Aqua,
				LineDashStyle = LineDashStyle.Solid,
				VisualType = VisualMode.Dots,
				Width = 5
			};

			_sellDots = new ValueDataSeries("SellDots")
			{
				ShowZeroValue = false,
				Color = Colors.Yellow,
				LineDashStyle = LineDashStyle.Solid,
				VisualType = VisualMode.Dots,
				Width = 5
			};

			_bullLine = new ValueDataSeries("BullLine")
			{
				Color = Colors.Green
			};

			_bearLine = new ValueDataSeries("BearLine")
			{
				Color = Colors.Red
			};
			DataSeries.Clear();
			DataSeries.Add(_buyDots);
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
			}

			if (bar > AvgPeriod)
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
			LineSeries.Clear();

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

		#endregion
	}
}